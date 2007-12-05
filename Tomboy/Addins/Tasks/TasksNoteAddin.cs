
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Tasks
{
	public class TasksNoteAddin : NoteAddin
	{
#region Members
		TaskTag last_removed_tag;
		
		// Each time the mouse is clicked in the TextView, update the click_mark
		// so that when we need to popup the Task Options dialog, we'll know
		// where to place it.
		Gtk.TextMark click_mark;
		
		Dictionary<Task, TaskOptionsDialog> options_dialogs;
		
		const string TASK_REGEX = 
			@"(^\s*{0}:.*$)";

		static Regex regex;
#endregion // Members

#region Constructors
		static TasksNoteAddin ()
		{
			regex = new Regex (
					string.Format (TASK_REGEX,
					Catalog.GetString ("todo")),
					RegexOptions.IgnoreCase | RegexOptions.Compiled
					   | RegexOptions.Singleline);
		}
#endregion // Constructors
		
#region NoteAddin Implementation
		public override void Initialize ()
		{
			if (!Note.TagTable.IsDynamicTagRegistered ("task")) {
				Note.TagTable.RegisterDynamicTag ("task", typeof (TaskTag));
			}
			
			TaskManager.TaskDeleted += OnTaskDeleted;
			TaskManager.TaskRenamed += OnTaskRenamed;
			TaskManager.TaskStatusChanged += OnTaskStatusChanged;
			
			options_dialogs = new Dictionary<Task, TaskOptionsDialog> ();
		}
		
		public override void Shutdown ()
		{
			TaskManager.TaskDeleted -= OnTaskDeleted;
			TaskManager.TaskRenamed -= OnTaskRenamed;
			TaskManager.TaskStatusChanged -= OnTaskStatusChanged;
			
			if (Note.HasBuffer) {
				Buffer.InsertText -= OnInsertText;
				Buffer.DeleteRange -= OnDeleteRange;
				Buffer.DeleteRange -= OnDeleteRangeConnectBefore;
				Buffer.TagRemoved -= OnTagRemoved;
			}
			
			if (Note.HasWindow) {
				Window.Editor.ButtonPressEvent -= OnButtonPress;
				Window.Editor.PopulatePopup -= OnPopulatePopup;
				Window.Editor.PopupMenu -= OnPopupMenu;
			}
		}
		
		public override void OnNoteOpened ()
		{
			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;
			Buffer.DeleteRange += OnDeleteRangeConnectBefore;
			Buffer.TagRemoved += OnTagRemoved;

			Window.Editor.ButtonPressEvent += OnButtonPress;
			Window.Editor.PopulatePopup += OnPopulatePopup;
			Window.Editor.PopupMenu += OnPopupMenu;
			
			click_mark = Buffer.CreateMark (null, Buffer.StartIter, true);
			
			UpdateTaskTagStatuses ();
		}
#endregion // NoteAddin Implementation

#region Private Methods
		void UpdateTaskTagStatuses ()
		{
			// FIXME: Should really just create an enumerator class for
			// enumerating a Buffer's TaskTags instead of doing it this
			// way in almost every method.
			Gtk.TextIter iter = Buffer.StartIter;
			iter.ForwardLine (); // Move past the note's title
			
			do {
				TaskTag task_tag = (TaskTag)
						Buffer.GetDynamicTag ("task", iter);
				if (task_tag == null)
					continue;
				
				task_tag.UpdateStatus ();
			} while (iter.ForwardLine());
		}
		
		void ApplyTaskTagToBlock (ref Gtk.TextIter start, Gtk.TextIter end)
		{
			Gtk.TextIter line_end = start;
			while (line_end.EndsLine () == false) {
				line_end.ForwardChar ();
			}
			// For some reason, the above code behaves like it should (i.e.,
			// without advancing to the next line).  The line below that's
			// commented out doesn't work.  It ends up advancing the iter to
			// the end of the next line.  Very strange!
//			line_end.ForwardToLineEnd ();

			
			TaskTag task_tag = GetTaskTagFromLineIter (ref start);
			
			if (task_tag != null) {
				Buffer.RemoveTag (task_tag, start, line_end);
			} else {
				task_tag = last_removed_tag;
			}
			
			string text = start.GetText (line_end);
//			Logger.Debug ("Evaluating with regex: {0}", text);
			
			TaskManager task_mgr = TasksApplicationAddin.DefaultTaskManager;
			Task task;

			Match match = regex.Match (text);
			if (match.Success) {
				string summary = GetTaskSummaryFromLine (text);
				if (task_tag == null) {
					task = task_mgr.Create (summary);
					task.QueueSave (true);
					task.OriginNoteUri = Note.Uri;
					task_tag = (TaskTag)
						Note.TagTable.CreateDynamicTag ("task");
					task_tag.Uri = task.Uri;
				} else {
					task = task_mgr.FindByUri (task_tag.Uri);
					if (task != null) {
						task.Summary = summary;
					} else {
						Logger.Debug ("FIXME: Add code to remove the task tag if this case is hit");
					}
				}

				Buffer.ApplyTag (task_tag, start, line_end);
				last_removed_tag = null;
			} else if (task_tag != null) {
				// This task should be deleted
				task = task_mgr.FindByUri (task_tag.Uri);
				if (task != null) {
					task_mgr.Delete (task);
				}

				last_removed_tag = null;
			}
		}
		
		/// <summary>
		/// Parse the task summary from the line omitting the "todo:" prefix
		/// </summary>
		string GetTaskSummaryFromLine (string line)
		{
			string todo_str = Catalog.GetString ("todo") + ":";
			int todo_pos = line.ToLower ().IndexOf (todo_str);
			if (todo_pos < 0)
				return line.Trim ();
			
			// Check to see if there's any content
			if (line.Length <= todo_str.Length)
				return string.Empty;
			
			string summary = line.Substring (todo_str.Length);
			return summary.Trim ();
		}
		
		/// <summary>
		/// Returns true if the current Note contains the specified text.
		/// <param name="text">The text to search for in the note.</param>
		/// </summary>
		bool ContainsText (string text)
		{
			string body = Note.TextContent.ToLower ();
			string match = text.ToLower ();

			return body.IndexOf (match) > -1;
		}
		
		TaskTag GetTaskTagFromLineIter (ref Gtk.TextIter line_iter)
		{
			TaskTag task_tag = null;
			
			while (line_iter.StartsLine () == false) {
				line_iter.BackwardChar ();
			}
			
			task_tag = (TaskTag) Buffer.GetDynamicTag ("task", line_iter);
			
			return task_tag;
		}
		
		/// <summary>
		/// Each time the user enters a newline (presses enter),
		/// evaluate the previous line to see if a new task should
		/// be created or the previous one removed...
		/// </summary>
		bool ProcessNewline ()
		{
			Gtk.TextIter iter = Buffer.GetIterAtMark (Buffer.InsertMark);
			Gtk.TextIter prev_line = iter;
			if (prev_line.BackwardLine () == false)
				return false;
			
			TaskTag task_tag = GetTaskTagFromLineIter (ref prev_line);
			if (task_tag == null)
				return false; // nothing to do with tasks here!
			Task task =
				TasksApplicationAddin.DefaultTaskManager.FindByUri (task_tag.Uri);
			
			if (task == null) {
				// This shouldn't happen, but just in case we have a left-over
				// TaskTag without a real task, go ahead and remove the TaskTag
				
				// FIXME: Remove TaskTag from the line
				
				return false;
			}
			
			if (task.Summary == string.Empty) {
				// If the previous line's task summary is empty, delete the task
				Logger.Debug ("Previous line's task summary is empty, deleting it...");
				TasksApplicationAddin.DefaultTaskManager.Delete (task);
			} else {
				// If the previous line's task summary is not empty, create a new
				// task on the current line.
				
				// I'm disabling the following code for now.  It automatically
				// starts up a new task on the newline.  But since this modifies
				// the buffer, it sometimes causes problems.
				// TODO: Make the auto-newline work
//				Buffer.InsertAtCursor (
//						string.Format ("{0}: ",
//								Catalog.GetString ("todo")));
			}
			
			return true; // The buffer was modified
		}
		
		/// <summary>
		/// Remove the task from the line specified by the TextIter.  This
		/// will remove the TextTag and also the "todo:" portion of the line
		/// so it will no longer be a task.  The task summary text will be
		/// left on the line.
		/// <param name="iter">The TextIter specifying the line where the
		/// task should be removed.</param>
		/// <returns>True if a task was removed, otherwise False.</returns>
		/// </summary>
		bool RemoveTaskFromLine (ref Gtk.TextIter iter)
		{
			if (RemoveTaskTagFromLine (iter) == false)
				return false;
			
			while (iter.StartsLine () == false) {
				iter.BackwardChar ();
			}
			
			Gtk.TextIter line_end = iter;
			while (line_end.EndsLine () == false) {
				line_end.ForwardChar ();
			}
//			line_end.ForwardToLineEnd ();
			
			string text = iter.GetText (line_end);
			
			Buffer.Delete (ref iter, ref line_end);
			
			text = GetTaskSummaryFromLine (text);
			if (text.Length > 0)
				Buffer.Insert (ref iter, text);
			return true;
		}
		
		/// <summary>
		/// Remove the task tag on the line specified by the TextIter.  This
		/// will not remove the "todo:" text (i.e., it will not modify the
		/// actual characters of the TextBuffer.
		/// <param name="iter">The TextIter specifying the line where the
		/// TaskTag should be removed.</param>
		/// <returns>True if a TaskTag was removed, otherwise False.</returns>
		/// </summary>
		bool RemoveTaskTagFromLine (Gtk.TextIter iter)
		{
			Gtk.TextIter start = iter;
			Gtk.TextIter end = iter;
			TaskTag task_tag = GetTaskTagFromLineIter (ref start);
			if (task_tag == null)
				return false;

			while (start.StartsLine () == false) {
				start.BackwardChar ();
			}
			
			while (end.EndsLine () == false) {
				end.ForwardChar ();
			}
//			end.ForwardToLineEnd ();
			last_removed_tag = null;
			Buffer.RemoveTag (task_tag, start, end);
			return true;
		}
		
		/// <summary>
		/// This method should be called during a large deletion of a
		/// range of text so it can properly remove all the task tags.
		/// </summary>
		void RemoveTaskTagsFromRange (Gtk.TextIter start, Gtk.TextIter end)
		{
			TaskTag task_tag;
			Gtk.TextIter line;
			Gtk.TextIter line_start;
			Gtk.TextIter line_end;
			if (start.Line == end.Line) {
				// The iters are on the same line.
				
				// If there's only one character being deleted, don't do
				// anything here.  This condition will be taken care of
				// in ApplyTaskTagToBlock ().
				if (end.LineOffset - start.LineOffset == 1) {
					return;
				}
				
				// Determine whether this line contains a TaskTag.  If it
				// does, determine whether deleting the range will delete
				// the todo.
				line = start;
				task_tag = GetTaskTagFromLineIter (ref line);
				if (task_tag != null) {
					if (start.LineIndex == 0) {
						// Start iter is at beginning of line
						if (end.LineOffset >= Catalog.GetString ("todo:").Length) {
							RemoveTaskTagFromLine (start);
						}
					} else if (start.LineIndex < Catalog.GetString ("todo:").Length) {
						// The start of the range is inside the "todo:" area,
						// so the TaskTag needs to be removed.
						RemoveTaskTagFromLine (start);
					} else {
						// Do nothing.  The deletion is just inside the
						// summary of the task.
					}
				}
			} else {
				// The iters are on different lines
				
				line = start;
				do {
					task_tag = GetTaskTagFromLineIter (ref line);
					if (task_tag != null) {
						// Handle the first and last lines special since their
						// range may not span the entire line.
						if (line.Line == start.Line) {
							// This is the first line
							line_end = line;
							while (line_end.EndsLine () == false) {
								line_end.ForwardChar ();
							}
//							line_end.ForwardToLineEnd ();
							RemoveTaskTagsFromRange (start, line_end);
						} else if (line.Line == end.Line) {
							// This is the last line
							line_start = line;
							while (line_start.StartsLine () == false) {
								line_start.BackwardChar ();
							}
							RemoveTaskTagsFromRange (line_start, end);
						} else {
							// This line is in the middle of the range
							// so it's completely safe to remove the TaskTag
							RemoveTaskTagFromLine (line);
						}

						// Delete the task
						TaskManager task_mgr = TasksApplicationAddin.DefaultTaskManager;
						Task task = task_mgr.FindByUri (task_tag.Uri);
						if (task != null) {
							task_mgr.Delete (task);
						}
					}
				} while (line.ForwardLine () && line.Line <= end.Line);
			}
		}
#endregion // Private Methods

#region Private Event Handlers
		/// <summary>
		/// If the deleted task is included inside this note, this
		/// handler removes the TextTag surrounding the task.
		/// </summary>
		private void OnTaskDeleted (TaskManager manager, Task task)
		{
			if (task.OriginNoteUri == null || task.OriginNoteUri != Note.Uri)
				return;

			// Search through the note looking for the TaskTag so that it can
			// be renamed

			// Iterate through the lines looking for tasks
			Gtk.TextIter iter = Buffer.StartIter;
			iter.ForwardLine (); // Move past the note's title
			
			do {
				TaskTag task_tag = (TaskTag)
						Buffer.GetDynamicTag ("task", iter);
				if (task_tag != null) {
					if (task_tag.Uri != task.Uri)
						continue;
					
					RemoveTaskFromLine (ref iter);
					break;
				}
			} while (iter.ForwardLine());
		}
		
		/// <summary>
		/// If the renamed task is included inside this note, this
		/// handler will update the task summary in the note buffer.
		/// </summary>
		private void OnTaskRenamed (Task task, string old_title)
		{
			if (task.OriginNoteUri == null || task.OriginNoteUri != Note.Uri)
				return;

			// Search through the note looking for the TaskTag so that it can
			// be renamed

			if (!ContainsText (old_title))
				return;

			// Iterate through the lines looking for tasks
			Gtk.TextIter iter = Buffer.StartIter;
			iter.ForwardLine (); // Move past the note's title
			
			do {
				TaskTag task_tag = (TaskTag)
						Buffer.GetDynamicTag ("task", iter);
				if (task_tag != null) {
					if (task_tag.Uri != task.Uri)
						continue;
					
					Gtk.TextIter line_start = iter;
					while (line_start.StartsLine () == false)
						line_start.BackwardChar ();
					Gtk.TextIter line_end = iter;
					while (line_end.EndsLine () == false) {
						line_end.ForwardChar ();
					}
//					line_end.ForwardToLineEnd ();
					
					Buffer.Delete (ref line_start, ref line_end);
					last_removed_tag = task_tag;
					Buffer.Insert (ref line_start,
							string.Format ("{0}: {1}",
									Catalog.GetString ("todo"),
									task.Summary));
					break;
				}
			} while (iter.ForwardLine());
		}
		
		/// <summary>
		/// If the specified task is included inside this note, this
		/// handler will update the task's status representation.
		/// </summary>
		private void OnTaskStatusChanged (Task task)
		{
			if (task.OriginNoteUri == null || task.OriginNoteUri != Note.Uri)
				return;

			// Search through the note looking for the TaskTag so that it can
			// be updated

			// Iterate through the lines looking for tasks
			Gtk.TextIter iter = Buffer.StartIter;
			iter.ForwardLine (); // Move past the note's title
			
			do {
				TaskTag task_tag = (TaskTag)
						Buffer.GetDynamicTag ("task", iter);
				if (task_tag != null) {
					if (task_tag.Uri != task.Uri)
						continue;
					
					task_tag.Completed = task.IsComplete;
					break;
				}
			} while (iter.ForwardLine());
		}
		
		[GLib.ConnectBeforeAttribute]
		void OnDeleteRangeConnectBefore (object sender, Gtk.DeleteRangeArgs args)
		{
			RemoveTaskTagsFromRange (args.Start, args.End);
		}
		
		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Gtk.TextIter start = args.Start;
			ApplyTaskTagToBlock (ref start, args.End);
		}
		
		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
//Logger.Debug ("TaskNoteAddin.OnInsertText:\n" +
//				"\tLength: {0}\n" +
//				"\tText: {1}\n" +
//				"\tLine: {2}",
//				args.Length,
//				args.Text,
//				args.Pos.Line);

			if (args.Length == 1 && args.Text == "\n") {
				Gtk.TextIter curr_line = args.Pos;
				TaskTag task_tag = GetTaskTagFromLineIter (ref curr_line);

				Gtk.TextIter prev_line = args.Pos;
				prev_line.BackwardLine ();
				/*TaskTag*/ task_tag = GetTaskTagFromLineIter (ref prev_line);
				if (task_tag != null) {
					// If the user just entered a newline and the previous
					// line was a task, do some special processing...but
					// we have to do it on idle since there are other
					// Buffer.InsertText handlers that we'll screw up if
					// we modify anything here.
					args.RetVal = ProcessNewline ();
				} else {
					// Check to see if the previous line is a todo: line
					while (prev_line.StartsLine () == false) {
						prev_line.BackwardChar ();
					}
					
					Gtk.TextIter prev_line_end = prev_line;
					while (prev_line_end.EndsLine () == false) {
						prev_line_end.ForwardChar ();
					}
					
					string prev_line_text = prev_line.GetText (prev_line_end);
					
					Match match = regex.Match (prev_line_text);
					if (match.Success && last_removed_tag != null) {
						TaskManager task_mgr = TasksApplicationAddin.DefaultTaskManager;
						Task task;

						task = task_mgr.FindByUri (last_removed_tag.Uri);
						if (task != null) {
							// Update the task's summary and make sure that
							// the previous line is appropriately tagged.
							string summary = GetTaskSummaryFromLine (prev_line_text);
							task.Summary = summary;
							Buffer.ApplyTag (last_removed_tag, prev_line, prev_line_end);
						} else {
							Logger.Debug ("Shouldn't ever hit this code (hopefully)");
						}
					}
					
					last_removed_tag = null;
				}
			} else {
				ApplyTaskTagToBlock (ref start, args.Pos);
			}
		}

		[GLib.ConnectBefore]
		void OnButtonPress (object sender, Gtk.ButtonPressEventArgs args)
		{
			int x, y;

			Window.Editor.WindowToBufferCoords (Gtk.TextWindowType.Text,
							    (int) args.Event.X,
							    (int) args.Event.Y,
							    out x,
							    out y);
			Gtk.TextIter click_iter = Window.Editor.GetIterAtLocation (x, y);

			// Move click_mark to click location
			Buffer.MoveMark (click_mark, click_iter);
		}
		
		void OnPopulatePopup (object sender, Gtk.PopulatePopupArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (click_mark);
			TaskTag task_tag = (TaskTag)
					Buffer.GetDynamicTag ("task", click_iter);
			if (task_tag == null)
				return;
			
			Gtk.MenuItem item;
			
			item = new Gtk.SeparatorMenuItem ();
			item.Show ();
			args.Menu.Prepend (item);
			
			item = new Gtk.MenuItem (Catalog.GetString ("Open To Do List"));
			item.Activated += OnOpenTaskListWindow;
			item.Show ();
			args.Menu.Prepend (item);
			
			item = new TaskMenuItem (task_tag.Uri, Catalog.GetString ("To Do Options"));
			item.Activated += OnOpenTaskOptions;
			item.ShowAll ();
			args.Menu.Prepend (item);
			
			item = new TaskMenuItem (
						task_tag.Uri,
						task_tag.Completed ?
								Catalog.GetString ("Mark Undone") :
								Catalog.GetString ("Mark Complete"));
			item.Activated += OnToggleCompletionStatus;
			item.ShowAll ();
			args.Menu.Prepend (item);
		}

		// Called via Alt-F10.  Reset click_mark to cursor location.
		[GLib.ConnectBefore]
		void OnPopupMenu (object sender, Gtk.PopupMenuArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (Buffer.InsertMark);
			Buffer.MoveMark (click_mark, click_iter);
			args.RetVal = false; // Continue event processing
		}

		void OnTagRemoved (object sender, Gtk.TagRemovedArgs args)
		{
			TaskTag task_tag = args.Tag as TaskTag;
			if (task_tag == null)
				return;
			
			last_removed_tag = task_tag;
		}
		
		void OnOpenTaskListWindow (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["OpenToDoListAction"].Activate ();
		}
		
		void OnToggleCompletionStatus (object sender, EventArgs args)
		{
			TaskMenuItem item = sender as TaskMenuItem;
			if (item == null)
				return;
			
			Task task =
					TasksApplicationAddin.DefaultTaskManager.FindByUri (
						item.TaskUri);
			if (task == null)
				return;
			
			if (task.IsComplete)
				task.ReOpen ();
			else
				task.Complete ();
		}
		
		void OnOpenTaskOptions (object sender, EventArgs args)
		{
			TaskMenuItem item = sender as TaskMenuItem;
			if (item == null)
				return;
			
			Task task =
					TasksApplicationAddin.DefaultTaskManager.FindByUri (
						item.TaskUri);
			if (task == null)
				return;
			
			TaskOptionsDialog dialog;
			if (options_dialogs.ContainsKey (task)) {
				dialog = options_dialogs [task];
			} else {
				dialog =
					new TaskOptionsDialog (
						Note.Window,
						Gtk.DialogFlags.DestroyWithParent,
						task);
				dialog.WindowPosition = Gtk.WindowPosition.CenterOnParent;
				dialog.DeleteEvent += OnOptionsDialogDeleted;
				options_dialogs [task] = dialog;
			}
			
			dialog.Show ();
			dialog.GrabFocus ();
		}

		void OnOptionsDialogDeleted (object sender, Gtk.DeleteEventArgs args)
		{
			TaskOptionsDialog dialog = sender as TaskOptionsDialog;
			if (dialog == null)
				return;
			
			if (options_dialogs.ContainsKey (dialog.Task))
				options_dialogs.Remove (dialog.Task);
		}
#endregion // Private Event Handlers
	}
	
	public class TaskMenuItem : Gtk.MenuItem
	{
		public string TaskUri;
		
		public TaskMenuItem (string uri, string label)
			: base (label)
		{
			this.TaskUri = uri;
		}
	}
}
