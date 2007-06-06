
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
//		TaskManager task_mgr;
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
//					   | RegexOptions.Multiline);
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
		
		void ApplyTaskTagToBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			while (start.StartsLine () == false) {
				start.BackwardChar ();
			}
			
			Gtk.TextIter line_end = start;
			line_end.ForwardToLineEnd ();
			
			TaskTag task_tag = (TaskTag)
					Buffer.GetDynamicTag ("task", start);
			
			if (task_tag != null) {
				Buffer.RemoveTag (task_tag, start, line_end);
			} else {
				task_tag = last_removed_tag;
			}
			
			string text = start.GetText (line_end);
			//Logger.Debug ("Evaluating with regex: {0}", text);
			
			Match match = regex.Match (text);
			if (match.Success) {
				string summary = GetTaskSummaryFromLine (text);
				TaskManager task_mgr = TasksApplicationAddin.DefaultTaskManager;
				Task task;
				if (task_tag == null) {
					Logger.Debug ("Creating a new task for: {0}", summary);
					task = task_mgr.Create (summary);
					task.OriginNoteUri = Note.Uri;
					task_tag = (TaskTag)
						Note.TagTable.CreateDynamicTag ("task");
					task_tag.Uri = task.Uri;
				} else {
					task = task_mgr.FindByUri (task_tag.Uri);
					task.Summary = summary;
				}

				Buffer.ApplyTag (task_tag, start, line_end);
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

		bool ContainsText (string text)
		{
			string body = Note.TextContent.ToLower ();
			string match = text.ToLower ();

			return body.IndexOf (match) > -1;
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
					
					Gtk.TextIter line_start = iter;
					while (line_start.StartsLine () == false)
						line_start.BackwardChar ();
					Gtk.TextIter line_end = iter;
					line_end.ForwardToLineEnd ();
					
					Buffer.Delete (ref line_start, ref line_end);
					last_removed_tag = null;
					Buffer.RemoveTag (task_tag, line_start, line_end);
					Buffer.Insert (ref line_start, task.Summary);
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
					line_end.ForwardToLineEnd ();
					
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

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			ApplyTaskTagToBlock (args.Start, args.End);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			ApplyTaskTagToBlock (start, args.Pos);
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
				dialog.DeleteEvent += OnOptionsDialogDeleted;
				options_dialogs [task] = dialog;
			}
			
			Logger.Debug ("FIXME: Attempt to move the Task Options popup right at the tag location");
			Gdk.Rectangle allocation = Note.Window.Editor.Allocation;
			int origin_x, origin_y;
			Note.Window.Editor.GdkWindow.GetOrigin (out origin_x, out origin_y);
			dialog.Move (origin_x + allocation.X, origin_y + allocation.Y);
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
