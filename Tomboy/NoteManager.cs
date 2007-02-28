
using System;
using System.IO;
using System.Collections;
using Mono.Unix;

namespace Tomboy
{
	public delegate void NotesChangedHandler (object sender, Note changed);

	public class NoteManager 
	{
		string notes_dir;
		string backup_dir;
		ArrayList notes;
		PluginManager plugin_mgr;
		TrieController trie_controller;
		
		static string start_note_uri = String.Empty;

		static NoteManager ()
		{
			// Watch the START_NOTE_URI setting and update it so that the
			// StartNoteUri property doesn't generate a call to
			// Preferences.Get () each time it's accessed.
			start_note_uri =
				Preferences.Get (Preferences.START_NOTE_URI) as string;
			Preferences.SettingChanged += OnSettingChanged;
		}
		
		static void OnSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			switch (args.Key) {
			case Preferences.START_NOTE_URI:
				start_note_uri = args.Value as string;
				break;
			}
		}

		public NoteManager (string directory) : 
			this (directory, Path.Combine (directory, "Backup")) 
		{
		}

		public NoteManager (string directory, string backup_directory) 
		{
			Logger.Log ("NoteManager created with note path \"{0}\".", directory);

			notes_dir = directory;
			backup_dir = backup_directory;
			notes = new ArrayList ();

			bool first_run = FirstRun ();
			CreateNotesDir ();

			plugin_mgr = CreatePluginManager ();
			trie_controller = CreateTrieController ();

			if (first_run) {
				// First run. Create "Start Here" notes.
				CreateStartNotes ();
			} else {
				LoadNotes ();
			}

			Tomboy.ExitingEvent += OnExitingEvent;
		}

		// Create & populate the Plugins dir. For overriding in test
		// methods.
		protected virtual PluginManager CreatePluginManager ()
		{
			// Allow developers to override the plugins path with an
			// environment variable.
			string plugins_dir =
				Environment.GetEnvironmentVariable ("TOMBOY_PLUGINS_PATH");

			if (plugins_dir == null) {
				string tomboy_dir = 
					Path.Combine (Environment.GetEnvironmentVariable ("HOME"), 
						      ".tomboy");
				plugins_dir = Path.Combine (tomboy_dir, "Plugins");
			}

			PluginManager.CreatePluginsDir (plugins_dir);

			return new PluginManager (plugins_dir);
		}

		// Create the TrieController. For overriding in test methods.
		protected virtual TrieController CreateTrieController ()
		{
			return new TrieController (this);
		}

		// For overriding in test methods.
		protected virtual bool DirectoryExists (string directory)
		{
			return Directory.Exists (directory);
		}

		// For overriding in test methods.
		protected virtual DirectoryInfo CreateDirectory (string directory)
		{
			return Directory.CreateDirectory (directory);
		}

		protected virtual bool FirstRun ()
		{
			return !DirectoryExists (notes_dir);
		}

		// Create the notes directory if it doesn't exist yet.
		void CreateNotesDir ()
		{
			if (!DirectoryExists (notes_dir)) {
				// First run. Create storage directory.
				CreateDirectory (notes_dir);
			}
		}

		void OnNoteRename (Note note, string old_title)
		{
			if (NoteRenamed != null)
				NoteRenamed (note, old_title);
		}
		
		void OnNoteSave (Note note)
		{
			if (NoteSaved != null)
				NoteSaved (note);
		}

		protected virtual void CreateStartNotes () 
		{
			// FIXME: Delay the creation of the start notes so the panel/tray
			// icon has enough time to appear so that Tomboy.TrayIconShowing
			// is valid.  Then, we'll be able to instruct the user where to
			// find the Tomboy icon.
			//string icon_str = Tomboy.TrayIconShowing ?
			//					Catalog.GetString ("System Tray Icon area") :
			//					Catalog.GetString ("GNOME Panel");
			string start_note_content = 
				Catalog.GetString ("<note-content>" +
						"Start Here\n\n" +
						"<bold>Welcome to Tomboy!</bold>\n\n" +
						"Use this \"Start Here\" note to begin organizing " +
						"your ideas and thoughts.\n\n" +
						"You can create new notes to hold your ideas by " +
						"selecting the \"Create New Note\" item from the " +
						"Tomboy Notes menu in your GNOME Panel. " +
						"Your note will be saved automatically.\n\n" +
						"Then organize the notes you create by linking " +
						"related notes and ideas together!\n\n" +
						"We've created a note called " +
						"<link:internal>Using Links in Tomboy</link:internal>.  " +
						"Notice how each time we type <link:internal>Using " +
						"Links in Tomboy</link:internal> it automatically " +
						"gets underlined?  Click on the link to open the note." +
						"</note-content>");

			string links_note_content =
				Catalog.GetString ("<note-content>" +
						"Using Links in Tomboy\n\n" +
						"Notes in Tomboy can be linked together by " +
						"highlighting text in the current note and clicking" +
						" the <bold>Link</bold> button above in the toolbar.  " +
						"Doing so will create a new note and also underline " +
						"the note's title in the current note.\n\n" +
						"Changing the title of a note will update links " +
						"present in other notes.  This prevents broken links " +
						"from occurring when a note is renamed.\n\n" +
						"Also, if you type the name of another note in your " +
						"current note, it will automatically be linked for you." +
						"</note-content>");

			try {
				Note start_note = Create (Catalog.GetString ("Start Here"),
								start_note_content);
				start_note.QueueSave (true);
				Preferences.Set (Preferences.START_NOTE_URI, start_note.Uri);

				Note links_note = Create (Catalog.GetString ("Using Links in Tomboy"),
								links_note_content);
				links_note.QueueSave (true);
				
				start_note.Window.Show ();
			} catch (Exception e) {
				Logger.Warn ("Error creating start notes: {0}\n{1}",
						e.Message, e.StackTrace);
			}
		}

		protected virtual void LoadNotes ()
		{
			string [] files = Directory.GetFiles (notes_dir, "*.note");

			foreach (string file_path in files) {
				try {
					Note note = Note.Load (file_path, this);
					if (note != null) {
						note.Renamed += OnNoteRename;
						note.Saved += OnNoteSave;
						notes.Add (note);
					}
				} catch (System.Xml.XmlException e) {
					Logger.Log ("Error parsing note XML, skipping \"{0}\": {1}",
						    file_path,
						    e.Message);
				}
			}

			// Update the trie so plugins can access it, if they want.
			trie_controller.Update ();

			// Load all the plugins for our notes.
			// Iterating through copy of notes list, because list may be
			// changed when loading plugins.
			ArrayList notesCopy = new ArrayList (notes);
			foreach (Note note in notesCopy) {
				plugin_mgr.LoadPluginsForNote (note);
			}
			
			// Make sure that a Start Note Uri is set in the preferences.  This
			// has to be done here for long-time Tomboy users who won't go
			// through the CreateStartNotes () process.
			if (StartNoteUri == String.Empty) {
				// Attempt to find an existing Start Here note
				Note start_note = Find (Catalog.GetString ("Start Here"));
				if (start_note != null)
					Preferences.Set (Preferences.START_NOTE_URI, start_note.Uri);
			}
		}

		void OnExitingEvent (object sender, EventArgs args)
		{
			Logger.Log ("Saving unsaved notes...");

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

			Logger.Log ("Deleting note '{0}'.", note.Title);

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

		public static string SplitTitleFromContent (string title, out string body)
		{
			body = null;

			if (title == null)
				return null;

			title = title.Trim();
			if (title == string.Empty)
				return null;

			string[] lines = title.Split (new char[] { '\n', '\r' }, 2);
			if (lines.Length > 0) {
				title = lines [0];
				title = title.Trim ();
				title = title.TrimEnd ('.', ',', ';');
				if (title == string.Empty)
					return null;
			}

			if (lines.Length > 1)
				body = lines [1];

			return title;
		}

		// Create a new note with the specified title, and a simple
		// "Describe..." body which will be selected for easy overwrite.
		public Note Create (string title) 
		{
			string body = null;

			title = SplitTitleFromContent (title, out body);
			if (title == null)
				return null;

			if (body == null)
				body = Catalog.GetString ("Describe your new note here.");

			string header = title + "\n\n";
			string content = 
				String.Format ("<note-content>{0}{1}</note-content>",
					       XmlEncoder.Encode (header),
					       XmlEncoder.Encode (body));

			Note new_note = Create (title, content);

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
			if (title == null || title == string.Empty)
				throw new Exception ("Invalid title");

			if (Find (title) != null)
				throw new Exception ("A note with this title already exists");

			string filename = MakeNewFileName ();

			Note new_note = Note.CreateNewNote (title, filename, this);
			new_note.XmlContent = xml_content;
			new_note.Renamed += OnNoteRename;
			new_note.Saved += OnNoteSave;

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
		
		public static string StartNoteUri
		{
			get { return start_note_uri; }
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

		public TrieTree TitleTrie
		{
			get {
				return trie_controller.TitleTrie;
			}
		}

		public event NotesChangedHandler NoteDeleted;
		public event NotesChangedHandler NoteAdded;
		public event NoteRenameHandler NoteRenamed;
		public event NoteSavedHandler NoteSaved;
	}

	public class TrieController
	{
		TrieTree title_trie;
		NoteManager manager;

		public TrieController (NoteManager manager)
		{
			this.manager = manager;
			manager.NoteDeleted += OnNoteDeleted;
			manager.NoteAdded += OnNoteAdded;
			manager.NoteRenamed += OnNoteRenamed;

			Update ();
		}

		void OnNoteAdded (object sender, Note added)
		{
			Update ();
		}

		void OnNoteDeleted (object sender, Note deleted)
		{
			Update ();
		}

		void OnNoteRenamed (Note renamed, string old_title)
		{
			Update ();
		}

		public void Update ()
		{
			title_trie = new TrieTree (false /* !case_sensitive */);

			foreach (Note note in manager.Notes) {
				title_trie.AddKeyword (note.Title, note);
			}
		}

		public TrieTree TitleTrie 
		{
			get { return title_trie; }
		}
	}
}
