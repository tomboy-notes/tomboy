
using System;
using System.IO;
using System.Collections;
using System.Web;
using Mono.Posix;

namespace Tomboy
{
	public delegate void NotesChangedHandler (object sender, Note changed);

	public class NoteManager 
	{
		string notes_dir;
		string backup_dir;
		ArrayList notes;

		public NoteManager () : 
			this (Path.Combine (Environment.GetEnvironmentVariable ("HOME"), 
					    ".tomboy")) 
		{
		}

		public NoteManager (string directory) : 
			this (directory, Path.Combine (directory, "Backup")) 
		{
		}

		public NoteManager (string directory, string backup_directory) 
		{
			notes_dir = directory;
			backup_dir = backup_directory;
			notes = new ArrayList ();

			if (Directory.Exists (notes_dir)) {
				string [] files = Directory.GetFiles (notes_dir, "*.note");

				foreach (string file_path in files) {
					Note note = Note.Load (file_path, this);
					if (note != null) {
						note.Renamed += OnNoteRename;
						notes.Add (note);
					}
				}
			} else {
				// First run. Create storage directory and "Start Here" note
				Directory.CreateDirectory (notes_dir);
				CreateStartNote ();
			}
		}

		void OnNoteRename (Note note, string old_title)
		{
			if (NoteRenamed != null)
				NoteRenamed (note, old_title);
		}

		void CreateStartNote () 
		{
			Note start_note = Create (Catalog.GetString ("Start Here"));
			if (start_note != null) {
				start_note.Text = 
					"<note-content>" +
					Catalog.GetString ("Start Here") + "\n\n" +
					"<bold>" +
					Catalog.GetString ("Welcome to Tomboy!") +
					"</bold>\n\n" +
					Catalog.GetString ("Use this page as a Start Page for organizing your " +
							   "notes and keeping unorganized ideas around.") +
					"</note-content>";
				start_note.Save ();
				start_note.Window.Show ();
			}
		}

		public void Delete (Note note) 
		{
			if (File.Exists (note.FilePath)) {
				if (backup_dir != null) {
					if (!Directory.Exists (backup_dir))
						Directory.CreateDirectory (backup_dir);

					string backup_path = 
						Path.Combine (backup_dir, 
							      Path.GetFileName (note.FilePath));
					if (File.Exists (backup_path))
						File.Delete (backup_path);

					File.Move (note.FilePath, backup_path);
				} else 
					File.Delete (note.FilePath);
			}

			notes.Remove (note);
			note.Delete ();

			Console.WriteLine ("Deleting note '{0}'.", note.Title);

			if (NoteDeleted != null)
				NoteDeleted (this, note);
		}

		string MakeNewFileName ()
		{
			Guid guid = Guid.NewGuid ();
			return Path.Combine (notes_dir, guid.ToString () + ".note");
		}

		public Note Create ()
		{
			int new_num = notes.Count;
			string temp_title;

			while (true) {
				temp_title = String.Format (Catalog.GetString ("New Note {0}"), 
							    ++new_num);
				if (Find (temp_title) == null)
					break;
			}

			return Create (temp_title);
		}

		public Note Create (string linked_title) 
		{
			string filename = MakeNewFileName ();
			string header = linked_title + "\n\n";

			Note new_note = new Note (linked_title, filename, this);
			new_note.Text = 
				String.Format ("<note-content>{0}{1}</note-content>",
					       HttpUtility.HtmlEncode (header),
					       Catalog.GetString ("Describe your new note here."));
			new_note.Renamed += OnNoteRename;

			// Select the inital "Describe..." text so typing will
			// immediately overwrite...
			NoteBuffer buffer = new_note.Buffer;
			Gtk.TextIter iter = buffer.GetIterAtOffset (header.Length);
			buffer.MoveMark (buffer.SelectionBound, iter);
			buffer.MoveMark (buffer.InsertMark, buffer.EndIter);

			notes.Add (new_note);

			if (NoteAdded != null)
				NoteAdded (this, new_note);

			return new_note;
		}

		public Note Find (string linked_title) 
		{
			foreach (Note note in notes) {
				if (note.Title.ToLower () == linked_title.ToLower ())
					return note;
			}
			return null;
		}

		public Note FindByUri (string uri)
		{
			foreach (Note note in notes) {
				if (note.Uri == uri)
					return note;
			}
			return null;
		}

		class CompareDates : IComparer
		{
			public int Compare (object a, object b)
			{
				Note note_a = a as Note;
				Note note_b = b as Note;

				// Sort in reverse chrono order...
				if (note_a == null || note_b == null)
					return -1;
				else
					return DateTime.Compare (note_b.ChangeDate, 
								 note_a.ChangeDate);
			}
		}

		public ArrayList Notes 
		{
			get {
				// FIXME: Only sort on change by listening to
				//        Note.Saved or Note.Buffer.Changed
				notes.Sort (new CompareDates ());
				return notes; 
			}
		}

		public event NotesChangedHandler NoteDeleted;
		public event NotesChangedHandler NoteAdded;
		public event NoteRenameHandler NoteRenamed;
	}
}
