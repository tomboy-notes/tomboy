
using System;
using System.Collections;

using NDesk.DBus;
using org.freedesktop.DBus;

namespace Tomboy
{
	[Interface ("com.beatniksoftware.Tomboy.RemoteControl")]
	public class RemoteControl : MarshalByRefObject
	{
		private NoteManager note_manager;

		public RemoteControl (NoteManager mgr)
		{
			note_manager = mgr;
		}

		public bool DisplayNote (string uri)
		{
			Note note;

			note = note_manager.FindByUri (uri);
			if (note == null)
				return false;

			note.Window.Present ();
			return true;
		}

		public bool DisplayNoteWithSearch (string uri, string search)
		{
			Note note;

			note = note_manager.FindByUri (uri);
			if (note == null)
				return false;

			note.Window.Present ();

			// Pop open the find-bar
			NoteFindBar find = note.Window.Find;
			find.ShowAll ();
			find.Visible = true;
			find.SearchText = search;

			return true;
		}

		public string FindNote (string linked_title)
		{
			Note note = note_manager.Find (linked_title);
			return (note == null) ? "" : note.Uri;
		}

		public string CreateNote ()
		{
			try {
				Note note = note_manager.Create ();
				return note.Uri;
			} catch (Exception e) {
				return  "";
			}
		}

		public string CreateNamedNote (string linked_title)
		{
			Note note;
			
			note = note_manager.Find (linked_title);
			if (note != null)
				return "";

			try {
				note = note_manager.Create (linked_title);
				return note.Uri;
			} catch (Exception e) {
				return "";
			}
		}

		public bool DeleteNote (string uri)
		{
			Note note;

			note = note_manager.FindByUri (uri);
			if (note == null)
				return false;

			note_manager.Delete (note);
			return true;
		}
		
		public void DisplaySearch ()
		{
			NoteRecentChanges.GetInstance (note_manager).Present ();
		}
		
		public void DisplaySearchWithText (string search_text)
		{
			NoteRecentChanges recent_changes =
				NoteRecentChanges.GetInstance (note_manager);
			if (recent_changes == null)
				return;
			
			recent_changes.SearchText = search_text;
			recent_changes.Present ();
		}
	}
}
