
using System;
using System.IO;
using Mono.Unix;
using Tomboy;
using Gtk;



[PluginInfo("Tasks Plugin", Defines.VERSION,
            PluginInfoAttribute.OFFICIAL_AUTHOR,
            "Provides task management in Tomboy",
            WebSite = Defines.TOMBOY_WEBSITE
            )]

public class TasksPlugin : NotePlugin
{
	static TaskManager manager;
	static object locker = new object ();

	static TasksPlugin ()
	{
	}
	
	public static event EventHandler TasksPluginShutdownEvent;
	
	public static TaskManager DefaultTaskManager
	{
		get { return manager; }
	}

	protected override void Initialize ()
	{
		if (!Note.TagTable.IsDynamicTagRegistered ("task")) {
			Note.TagTable.RegisterDynamicTag ("task", typeof (TaskTag));
		}
		
		if (manager == null) {
			lock (locker) {
				if (manager == null) {
					manager = new TaskManager (
						Path.Combine (Note.Manager.NoteDirectoryPath, "Tasks"));
				}
			}
		}
	}

	protected override void Shutdown ()
	{
		if (TasksPluginShutdownEvent != null) {
			TasksPluginShutdownEvent (this, EventArgs.Empty);
			manager = null;
		}
		
//		Buffer.InsertText -= OnInsertText;
//		Buffer.DeleteRange -= OnDeleteRange;
	}

	protected override void OnNoteOpened ()
	{
//		Buffer.InsertText += OnInsertText;
//		Buffer.DeleteRange += OnDeleteRange;
	}

/*
	void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
	{
		Gtk.TextIter line_start = args.Start;
		line_start.BackwardChars (line_start.LineOffset);
		Gtk.TextIter line_end = line_start;
		line_end.ForwardToLineEnd ();
		
		TaskTag task_tag;
		if (GetTaskTagFromIter (line_start, out task_tag)) {
			// Remove the old tag
			Gtk.TextIter start = line_start;
			Gtk.TextIter end = start;
			NoteBuffer.GetBlockExtents (ref start,
						ref end,
						Manager.TitleTrie.MaxLength,
						task_tag);
			Buffer.RemoveTag (task_tag, start, end);
			string line_text = line_start.GetText (line_end);
			task_tag.Summary = line_text;
			Buffer.ApplyTag (task_tag, line_start, line_end);
		}
	}

	void OnInsertText (object sender, Gtk.InsertTextArgs args)
	{
		Gtk.TextIter line_start = args.Pos;
		line_start.BackwardChars (line_start.LineOffset);
		Gtk.TextIter line_end = args.Pos;
		line_end.ForwardToLineEnd ();
		
		string line_text = line_start.GetText (line_end);
		
		Logger.Debug ("Parsing possible task: {0}", line_text);
		
		// Check to see whether this text is already inside of
		// a task tag.  If so, extend the tag to include the new
		// text.
		
		// If not, check to see if it starts with "TODO:" and
		// apply the task tag.
		TaskTag task_tag;
		if (GetTaskTagFromIter (line_start, out task_tag)) {
			// Remove the old tag
			Gtk.TextIter start = line_start;
			Gtk.TextIter end = start;
			NoteBuffer.GetBlockExtents (ref start,
						ref end,
						Manager.TitleTrie.MaxLength,
						task_tag);
			Buffer.RemoveTag (task_tag, start, end);
			task_tag.Summary = line_text;
			Buffer.ApplyTag (task_tag, line_start, line_end);
		} else {
			if (line_text.ToLower ().StartsWith (Catalog.GetString ("todo:"))) {
				task_tag = Note.TagTable.CreateDynamicTag ("task") as TaskTag;
				task_tag.Summary = line_text;

				// Tag the entire line as a task
				Logger.Debug ("Applying the task tag to: {0}", line_text);
				Buffer.ApplyTag (task_tag, line_start, line_end);
			} else {
				Logger.Debug ("Not a task");
			}
		}
	}
*/

	bool OnTaskTagActivated (NoteTag      sender,
				 NoteEditor   editor,
				 Gtk.TextIter start, 
				 Gtk.TextIter end)
	{
		return PopupTaskDetails (start, end);
	}
	
	bool PopupTaskDetails (Gtk.TextIter start, Gtk.TextIter end)
	{
		Logger.Debug ("PopupTaskDetails");
		TaskTag task_tag;
		if (GetTaskTagFromIter (start, out task_tag)) {
			Task task = manager.FindByUri (task_tag.Uri);
			if (task != null) {
				Logger.Debug ("FIXME: Pop open the task details: {0}", task.Summary);
				return true;
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Returns true and passes back the task_tag if one is found.
	/// </summary>
	bool GetTaskTagFromIter (Gtk.TextIter iter, out TaskTag task_tag)
	{
		task_tag = null;
		
		foreach (TextTag tag in iter.Tags) {
			if (string.Compare (tag.Name, "task") == 0) {
				// Found a TaskTag!
				task_tag = tag as TaskTag;
				return true;
			}
		}
		
		return false;
	}
}
