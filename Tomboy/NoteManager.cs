
using System;
using System.IO;
using System.Collections;
using Mono.Posix;

namespace Tomboy
{
	public delegate void NotesChangedHandler (object sender, Note changed);

	public class NoteManager 
	{
		string notes_dir;
		string backup_dir;
		ArrayList notes;
		PluginManager plugin_mgr;

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

			bool first_run = !Directory.Exists (notes_dir);
			if (first_run) {
				// First run. Create storage directory.
				Directory.CreateDirectory (notes_dir);
			}

			// Create & populate the Plugins dir if it doesn't exist
			string plugins_dir = Path.Combine (notes_dir, "Plugins");
			if (!Directory.Exists (plugins_dir))
				PluginManager.CreatePluginsDir (plugins_dir);

			plugin_mgr = new PluginManager (plugins_dir);

			if (first_run) {
				// First run. Create "Start Here" note
				CreateStartNote ();
			} else {
				string [] files = Directory.GetFiles (notes_dir, "*.note");

				foreach (string file_path in files) {
					Note note = Note.Load (file_path, this);
					if (note != null) {
						note.Renamed += OnNoteRename;
						notes.Add (note);
					}
				}

				// Load all the plugins for our notes
				foreach (Note note in notes) {
					plugin_mgr.LoadPluginsForNote (note);
				}
			}

			Tomboy.ExitingEvent += OnExitingEvent;
		}

		void OnNoteRename (Note note, string old_title)
		{
			if (NoteRenamed != null)
				NoteRenamed (note, old_title);
		}

		void CreateStartNote () 
		{
			string content = 
				string.Format ("<note-content>" +
					       "{0}\n\n" +
					       "<bold>{1}</bold>\n\n" +
					       "{2}" +
					       "</note-content>",
					       Catalog.GetString ("Start Here"),
					       Catalog.GetString ("Welcome to Tomboy!"),
					       Catalog.GetString ("Use this page as a Start Page for organizing your " +
								  "notes and keeping unorganized ideas around."));

			Note start_note = Create (Catalog.GetString ("Start Here"), content);

			if (start_note != null) {
				start_note.Save ();
				start_note.Window.Show ();
			}
		}

		void OnExitingEvent (object sender, EventArgs args)
		{
			Console.WriteLine ("Saving unsaved notes...");

			foreach (Note note in notes) {
				note.Save ();
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

		// Create a new note with a generated title
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

		// Create a new note with the specified title, and a simple
		// "Describe..." body which will be selected for easy overwrite.
		public Note Create (string linked_title) 
		{
			string header = linked_title + "\n\n";
			string content = 
				String.Format ("<note-content>{0}{1}</note-content>",
					       XmlEncoder.Encode (header),
					       Catalog.GetString ("Describe your new note here."));

			Note new_note = Create (linked_title, content);

			// Select the inital "Describe..." text so typing will
			// immediately overwrite...
			NoteBuffer buffer = new_note.Buffer;
			Gtk.TextIter iter = buffer.GetIterAtOffset (header.Length);
			buffer.MoveMark (buffer.SelectionBound, iter);
			buffer.MoveMark (buffer.InsertMark, buffer.EndIter);

			return new_note;
		}

		// Create a new note with the specified Xml content
		public Note Create (string title, string xml_content)
		{
			string filename = MakeNewFileName ();

			Note new_note = new Note (title, filename, this);
			new_note.XmlContent = xml_content;
			new_note.Renamed += OnNoteRename;

			notes.Add (new_note);

			// Load all the plugins for the new note
			plugin_mgr.LoadPluginsForNote (new_note);

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

		public PluginManager PluginManager
		{
			get {
				return plugin_mgr;
			}
		}

		public event NotesChangedHandler NoteDeleted;
		public event NotesChangedHandler NoteAdded;
		public event NoteRenameHandler NoteRenamed;
	}
}
