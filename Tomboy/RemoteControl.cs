
using DBus;
using System;
using System.Collections;

namespace Tomboy
{
	public class RemoteControl : RemoteControlProxy
	{
		private NoteManager note_manager;

		public RemoteControl (NoteManager mgr)
		{
			note_manager = mgr;
		}

		public override bool DisplayNote (string uri)
		{
			Note note;

			note = note_manager.FindByUri (uri);
			if (note == null)
				return false;

			note.Window.Present ();
			return true;
		}


		public override bool DisplayNoteWithSearch (string uri, string [] searches)
		{
			Note note;

			note = note_manager.FindByUri (uri);
			if (note == null)
				return false;

			ArrayList matches = NoteFindDialog.FindMatches (note.Buffer, searches, false);

			NoteFindDialog.HighlightMatches (matches, true);
			
			note.Window.Present ();
			return true;
		}


		public override string FindNote (string linked_title)
		{
			Note note;
			note = note_manager.Find (linked_title);
			return (note == null) ? "" : note.Uri;
		}

		public override string CreateNote ()
		{
			Note note;
			note = note_manager.Create ();
			return note.Uri;
		}

		public override string CreateNamedNote (string linked_title)
		{
			Note note;
			
			note = note_manager.Find (linked_title);
			if (note != null)
				return "";

			note = note_manager.Create (linked_title);
			return note.Uri;
		}

		public override bool DeleteNote (string uri)
		{
			Note note;

			note = note_manager.FindByUri (uri);
			if (note == null)
				return false;

			note_manager.Delete (note);
			return true;
		}
	}
}
