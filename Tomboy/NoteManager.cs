
using System;
using System.IO;
using System.Collections;

namespace Tomboy
{
	public delegate void NotesChangedHandler (object sender, Note added, Note deleted);

	public class NoteManager 
	{
		string notes_dir;
		string backup_dir;
		ArrayList notes;

		public NoteManager () : 
			this (Path.Combine (Environment.GetEnvironmentVariable ("HOME"), 
					    "tomboy")) 
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
						note.Renamed += new NoteRenameHandler (OnNoteRename);
						notes.Add (note);
					}
				}
			} else {
				// First run. Create storage directory and "Start Here" note
				Directory.CreateDirectory (notes_dir);
				CreateStartNote ();
			}
		}

		void ShowNameClashError (Note note, string title)
		{
			string label_text = 
				String.Format ("<span size=\"large\"><b>" +
					       "Note title already taken" +
					       "</b></span>\n\n" +
					       "A note with the title <b>{0}</b> already exists. " +
					       "Please choose another name for this note before " +
					       "continuing.",
					       title);

			Gtk.Label label = new Gtk.Label (label_text);
			label.UseMarkup = true;
			label.Wrap = true;
			label.Show ();

			Gtk.Button button = new Gtk.Button (Gtk.Stock.Ok);
			button.Show ();

			Gtk.Dialog dialog = new Gtk.Dialog ("Note title already taken",
							    note.Window,
							    Gtk.DialogFlags.DestroyWithParent);
			dialog.AddActionWidget (button, Gtk.ResponseType.Ok);
			dialog.VBox.PackStart (label, false, false, 0);

			dialog.Run ();
			dialog.Destroy ();
		}

		void OnNoteRename (Note note, string old_title)
		{
			string new_file = ConvertTitleToFileName (note.Title);
			if (File.Exists (new_file)) {
				ShowNameClashError (note, note.Title);
				return;
			}

			if (File.Exists (note.FilePath)) {
				// Move the existing file
				File.Move (note.FilePath, new_file);
				note.FilePath = new_file;
			}

			if (NoteRenamed != null)
				NoteRenamed (note, old_title);
		}

		void CreateStartNote () 
		{
			Note start_note = Create ("Start Here");
			if (start_note != null) {
				start_note.Text = 
					"<note-content>" +
					"Start Here\n\n<bold>Welcome to Tomboy!</bold>\n\n" +
					"Use this page as a Start Page for organizanizing your " +
					"notes and keeping unorganized ideas around." +
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

			if (NotesChanged != null)
				NotesChanged (this, null, note);
		}

		string ConvertTitleToFileName (string title)
		{
			//
			// FIXME: handle encoding conversions here.
			//
			string filename = title.Replace (" ", "_") + ".note";
			return Path.Combine (notes_dir, filename);
		}

		public Note Create (string linked_title) 
		{
			string filename = ConvertTitleToFileName (linked_title);

			Note new_note = new Note (linked_title, filename, this);
			new_note.Text = 
				"<note-content>" + 
				linked_title + "\n\n" + "Describe your new note here." +
				"</note-content>";
			new_note.Renamed += new NoteRenameHandler (OnNoteRename);

			notes.Add (new_note);

			if (NotesChanged != null)
				NotesChanged (this, new_note, null);

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
					return DateTime.Compare (note_b.SaveDate, note_a.SaveDate);
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

		public event NotesChangedHandler NotesChanged;
		public event NoteRenameHandler NoteRenamed;
	}
}
