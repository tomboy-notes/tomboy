
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Mono.Unix;

namespace Tomboy
{
	public delegate void NoteRenameHandler (Note sender, string old_title);

	// Contains all pure note data, like the note title and note text.
	public class NoteData
	{
		string uri;
		string title;
		string text;
		DateTime create_date;
		DateTime change_date;

		int cursor_pos;
		int width, height;
		int x, y;

		const int noPosition = -1;

		public NoteData ()
		{
			x = noPosition;
			y = noPosition;
		}

		public string Uri
		{
			get { return uri; }
			set { uri = value; }
		}

		public string Title
		{
			get { return title; }
			set { title = value; }
		}

		public string Text
		{
			get { return text; }
			set { text = value; }
		}

		public DateTime CreateDate
		{
			get { return create_date; }
			set { create_date = value; }
		}

		public DateTime ChangeDate
		{
			get { return change_date; }
			set { change_date = value; }
		}

		// FIXME: the next five attributes don't belong here (the data
		// model), but belong into the view; for now they are kept here
		// for backwards compatibility
		public int CursorPosition
		{
			get { return cursor_pos; }
			set { cursor_pos = value; }
		}

		public int Width
		{
			get { return width; }
			set { width = value; }
		}

		public int Height
		{
			get { return height; }
			set { height = value; }
		}

		public int X
		{
			get { return x; }
			set { x = value; }
		}

		public int Y
		{
			get { return y; }
			set { y = value; }
		}

		public void SetPositionExtent (int x, int y, int width, int height)
		{
			Debug.Assert (x >= 0 && y >= 0);
			Debug.Assert (width > 0 && height > 0);

			this.x = x;
			this.y = y;
			this.width = width;
			this.height = height;
		}

		public bool HasPosition ()
		{
			return x != noPosition && y != noPosition;
		}

		public bool HasExtent ()
		{
			return width != 0 && height != 0;
		}
	}

	public class Note 
	{
		internal NoteData data;

		string filepath;

		bool save_needed;

		// Accessed by NoteArchiver...
		internal string version;

		NoteManager manager;
		NoteWindow window;
		NoteBuffer buffer;
		NoteTagTable tag_table;

		InterruptableTimeout save_timeout;

		[System.Diagnostics.Conditional ("DEBUG_SAVE")]
		static void DebugSave (string format, params object[] args)
		{
			Console.WriteLine (format, args);
		}

		Note (NoteData data, string filepath, NoteManager manager)
		{
			this.data = data;
			this.filepath = filepath;
			this.manager = manager;
			save_timeout = new InterruptableTimeout ();
			save_timeout.Timeout += SaveTimeout;
		}

		public static Note CreateNewNote (string title, 
						  string filepath, 
						  NoteManager manager)
		{
			NoteData data = new NoteData ();
			data.Title = title;
			data.CreateDate = DateTime.Now;
			data.ChangeDate = data.CreateDate;
			return new Note (data, filepath, manager);
		}

		public static Note CreateExistingNote (string filepath,
						       NoteManager manager)
		{
			NoteData data = new NoteData ();
			data.CreateDate = DateTime.MinValue;
			data.ChangeDate = File.GetLastWriteTime (filepath);
			return new Note (data, filepath, manager);
		}

		public void Delete ()
		{
			save_timeout.Cancel ();

			if (window != null) {
				window.Hide ();
				window.Destroy ();
			}
		}

		// Load from an existing Note...
		public static Note Load (string read_file, NoteManager manager)
		{
			Note note = CreateExistingNote (read_file, manager);
			NoteArchiver.Read (read_file, note);

			if (note.version != NoteArchiver.CURRENT_VERSION) {
				// Note has old format, so rewrite it.  No need
				// to reread, since we are not adding anything.
				Logger.Log ("Updating note XML to newest format...");
				NoteArchiver.Write (read_file, note);
			}

			return note;
		}

		public void Save () 
		{
			// Do nothing if we don't need to save.  Avoids unneccessary saves
			// e.g on forced quit when we call save for every note.
			if (!save_needed)
				return;

			Logger.Log ("Saving '{0}'...", data.Title);

			NoteArchiver.Write (filepath, this);
		}

		//
		// Buffer change signals.  These queue saves and invalidate the serialized text
		// depending on the change...
		//

		void BufferChanged (object sender, EventArgs args)
		{
			DebugSave ("BufferChanged queueing save");
			QueueSave (true);
		}

		void BufferTagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (NoteTagTable.TagIsSerializable (args.Tag)) {
				DebugSave ("BufferTagApplied queueing save: {0}", args.Tag.Name);
				QueueSave (true);
			}
		}

		void BufferTagRemoved (object sender, Gtk.TagRemovedArgs args)
		{
			if (NoteTagTable.TagIsSerializable (args.Tag)) {
				DebugSave ("BufferTagRemoved queueing save: {0}", args.Tag.Name);
				QueueSave (true);
			}
		}

		void BufferInsertMarkSet (object sender, Gtk.MarkSetArgs args)
		{
			if (args.Mark != buffer.InsertMark)
				return;

			data.CursorPosition = args.Location.Offset;

			DebugSave ("BufferInsertSetMark queueing save");
			QueueSave (false);
		}

		//
		// Window events.  Queue a save when the window location/size has changed, and set
		// our window to null on delete, and fire the Opened event on window realize...
		//

		[GLib.ConnectBefore]
		void WindowConfigureEvent (object sender, Gtk.ConfigureEventArgs args)
		{
			int cur_x, cur_y, cur_width, cur_height;

			// Ignore events when maximized.  We don't want notes
			// popping up maximized the next run.
			if ((window.GdkWindow.State & Gdk.WindowState.Maximized) > 0)
				return;

			window.GetPosition (out cur_x, out cur_y);
			window.GetSize (out cur_width, out cur_height);

			if (data.X == cur_x && 
			    data.Y == cur_y &&
			    data.Width == cur_width && 
			    data.Height == cur_height)
				return;

			data.SetPositionExtent (cur_x, cur_y, cur_width, cur_height);
			
			DebugSave ("WindowConfigureEvent queueing save");
			QueueSave (false);
		}

		[GLib.ConnectBefore]
		void WindowDestroyed (object sender, EventArgs args) 
		{
			window = null;
		}

		// Set a 4 second timeout to execute the save.  Possibly
		// invalidate the text, which causes a re-serialize when the
		// timeout is called...
		public void QueueSave (bool invalidate_text)
		{
			DebugSave ("Got QueueSave with invalidate = {0}", invalidate_text);

			// Replace the existing save timeout.  Wait 4 seconds
			// before saving...
			save_timeout.Reset (4000);
			save_needed = true;

			// Force a re-get of text on save
			if (invalidate_text) {
				data.ChangeDate = DateTime.Now;
				data.Text = null;
			}
		}

		// Save timeout to avoid constanly resaving.  Called every 4 seconds.
		void SaveTimeout (object sender, EventArgs args)
		{
			try {
				Save ();
				save_needed = false;
			} catch (Exception e) {
				// FIXME: Present a nice dialog here that interprets the
				// error message correctly.
				Logger.Log ("Error while saving: {0}", e);
			}
		}

		public string Uri
		{
			get { 
				return "note://tomboy/" + 
					Path.GetFileNameWithoutExtension (filepath); 
			}
		}

		public string FilePath 
		{
			get { return filepath; }
			set { filepath = value; }
		}

		public string Title 
		{
			get { return data.Title; }
			set {
				if (data.Title != value) {
					if (window != null)
						window.Title = value;

					string old_title = data.Title;
					data.Title = value;

					if (Renamed != null)
						Renamed (this, old_title);
				}
			}
		}

		public string XmlContent 
		{
			get {
				if (data.Text == null && buffer != null) {
					DebugSave ("Re-serializing to XML...");
					data.Text = NoteBufferArchiver.Serialize (buffer); 
				}
				return data.Text;
			}
			set {
				if (buffer != null) {
					buffer.Clear ();
					buffer.Undoer.FreezeUndo ();
					NoteBufferArchiver.Deserialize (buffer, 
									buffer.StartIter, 
									value);
					buffer.Undoer.ThawUndo ();
				}

				data.Text = value;

				DebugSave ("Set of XmlContent queueing save");
				QueueSave (false);
			}
		}

		public string TextContent
		{
			get {
				if (buffer != null)
					return buffer.GetSlice (buffer.StartIter, 
								buffer.EndIter, 
								false /* hidden_chars */);
				else 
					return XmlDecoder.Decode (XmlContent);
			}
		}

		public DateTime CreateDate 
		{
			get { return data.CreateDate; }
		}

		public DateTime ChangeDate 
		{
			get { return data.ChangeDate; }
		}

		public NoteManager Manager
		{
			get { return manager; }
			set { manager = value; }
		}

		public NoteTagTable TagTable
		{
			get {
				if (tag_table == null) {
#if FIXED_GTKSPELL
					// NOTE: Sharing the same TagTable means
					// that formatting is duplicated between
					// buffers.
					tag_table = NoteTagTable.Instance;
#else
					// NOTE: GtkSpell chokes on shared
					// TagTables because it blindly tries to
					// create a new "gtkspell-misspelling"
					// tag, which fails if one already
					// exists in the table.
					tag_table = new NoteTagTable ();
#endif
				}
				return tag_table;
			}
		}

		public NoteBuffer Buffer
		{
			get {
				if (buffer == null) {
					Logger.Log ("Creating Buffer for '{0}'...", data.Title);

					buffer = new NoteBuffer (TagTable);

					// Don't create Undo actions during load
					buffer.Undoer.FreezeUndo ();

					// Load the stored xml text
					NoteBufferArchiver.Deserialize (buffer, 
									buffer.StartIter, 
									data.Text);
					buffer.Modified = false;

					
					Gtk.TextIter cursor;
					if (data.CursorPosition != 0) {
						// Move cursor to last-saved position
						cursor = buffer.GetIterAtOffset (data.CursorPosition);
					} else {
						// Avoid title line
						cursor = buffer.GetIterAtLine (2);
					}
					buffer.PlaceCursor (cursor);

					// New events should create Undo actions
					buffer.Undoer.ThawUndo ();

					// Listen for further changed signals
					buffer.Changed += BufferChanged;
					buffer.TagApplied += BufferTagApplied;
					buffer.TagRemoved += BufferTagRemoved;
					buffer.MarkSet += BufferInsertMarkSet;
				}
				return buffer;
			}
		}

		public NoteWindow Window 
		{
			get {
				if (window == null) {
					window = new NoteWindow (this);
					window.Destroyed += WindowDestroyed;
					window.ConfigureEvent += WindowConfigureEvent;
					
					if (data.HasExtent ())
						window.SetDefaultSize (data.Width, data.Height);

					if (data.HasPosition ())
						window.Move (data.X, data.Y);

					// This is here because emiting inside
					// OnRealized causes segfaults.
					if (Opened != null)
						Opened (this, new EventArgs ());
				}
				return window; 
			}
		}

		public bool IsSpecial 
		{
			get { return data.Title == Catalog.GetString ("Start Here"); }
		}

		public bool IsNew 
		{
			get { 
				// Note is new if created in the last 24 hours.
				return data.CreateDate > DateTime.Now.AddHours (-24);
			}
		}

		public bool IsLoaded 
		{
			get { return buffer != null; }
		}

		public bool IsOpened 
		{
			get { return window != null; }
		}

		public event EventHandler Opened;
		public event NoteRenameHandler Renamed;
	}

	// Singleton - allow overriding the instance for easy sensing in
	// test classes - we're not bothering with double-check locking,
	// since this class is only seldomly used
	public class NoteArchiver
	{
		public const string CURRENT_VERSION = "0.2";

		static NoteArchiver instance = null;
		static readonly object lock_ = new object();

		protected NoteArchiver ()
		{
		}

		public static NoteArchiver Instance
		{
			get
			{
				lock (lock_)
				{
					if (instance == null)
						instance = new NoteArchiver ();
					return instance;
				}
			}
			set {
				lock (lock_)
				{
					instance = value;
				}
			}
		}

		public static void Read (string read_file, Note note)
		{
			Instance.ReadFile (read_file, note);
		}

		public virtual void ReadFile (string read_file, Note note) 
		{
			StreamReader reader = new StreamReader (read_file, 
								System.Text.Encoding.UTF8);
			XmlTextReader xml = new XmlTextReader (reader);
			xml.Namespaces = false;

			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					switch (xml.Name) {
					case "note":
						note.version = xml.GetAttribute ("version");
						break;
					case "title":
						note.data.Title = xml.ReadString ();
						break;
					case "text":
						// <text> is just a wrapper around <note-content>
						// NOTE: Use .text here to avoid triggering a save.
						note.data.Text = xml.ReadInnerXml ();
						break;
					case "last-change-date":
						note.data.ChangeDate = 
							XmlConvert.ToDateTime (xml.ReadString ());
						break;
					case "create-date":
						note.data.CreateDate = 
							XmlConvert.ToDateTime (xml.ReadString ());
						break;
					case "cursor-position":
						note.data.CursorPosition = int.Parse (xml.ReadString ());
						break;
					case "width":
						note.data.Width = int.Parse (xml.ReadString ());
						break;
					case "height":
						note.data.Height = int.Parse (xml.ReadString ());
						break;
					case "x":
						note.data.X = int.Parse (xml.ReadString ());
						break;
					case "y":
						note.data.Y = int.Parse (xml.ReadString ());
						break;
					}
					break;
				}
			}

			xml.Close ();
		}

		public static void Write (string write_file, Note note)
		{
			Instance.WriteFile (write_file, note);
		}

		public virtual void WriteFile (string write_file, Note note) 
		{
			string tmp_file = write_file + ".tmp";

			XmlTextWriter xml = new XmlTextWriter (tmp_file, System.Text.Encoding.UTF8);
			Write (xml, note);
			xml.Close ();

			if (File.Exists (write_file)) {
				string backup_path = write_file + "~";
				if (File.Exists (backup_path))
					File.Delete (backup_path);

				// Backup the to a ~ file, just in case
				File.Move (write_file, backup_path);

				// Move the temp file to write_file
				File.Move (tmp_file, write_file);

				// Delete the ~ file
				File.Delete (backup_path);
			} else {
				// Move the temp file to write_file
				File.Move (tmp_file, write_file);
			}

			// This is always the latest after a write
			note.version = CURRENT_VERSION;
		}

		public static void Write (TextWriter writer, Note note)
		{
			Instance.WriteFile (writer, note);
		}

		public void WriteFile (TextWriter writer, Note note)
		{
			XmlTextWriter xml = new XmlTextWriter (writer);
			Write (xml, note);
			xml.Close ();

			note.version = CURRENT_VERSION;
		}

		void Write (XmlTextWriter xml, Note note)
		{
			xml.Formatting = Formatting.Indented;

			xml.WriteStartDocument ();
			xml.WriteStartElement (null, "note", "http://beatniksoftware.com/tomboy");
			xml.WriteAttributeString(null, 
						 "version", 
						 null, 
						 CURRENT_VERSION);
			xml.WriteAttributeString("xmlns", 
						 "link", 
						 null, 
						 "http://beatniksoftware.com/tomboy/link");
			xml.WriteAttributeString("xmlns", 
						 "size", 
						 null, 
						 "http://beatniksoftware.com/tomboy/size");

			xml.WriteStartElement (null, "title", null);
			xml.WriteString (note.data.Title);
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "text", null);
			xml.WriteAttributeString ("xml", "space", null, "preserve");
			// Insert <note-content> blob...
			// NOTE: Use .XmlContent here to force a reget of text
			//       from the buffer, if needed.
			xml.WriteRaw (note.XmlContent);
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "last-change-date", null);
			xml.WriteString (XmlConvert.ToString (note.data.ChangeDate));
			xml.WriteEndElement ();

			if (note.data.CreateDate != DateTime.MinValue) {
				xml.WriteStartElement (null, "create-date", null);
				xml.WriteString (XmlConvert.ToString (note.data.CreateDate));
				xml.WriteEndElement ();
			}

			xml.WriteStartElement (null, "cursor-position", null);
			xml.WriteString (note.data.CursorPosition.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "width", null);
			xml.WriteString (note.data.Width.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "height", null);
			xml.WriteString (note.data.Height.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "x", null);
			xml.WriteString (note.data.X.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "y", null);
			xml.WriteString (note.data.Y.ToString ());
			xml.WriteEndElement ();

			xml.WriteEndElement (); // Note
			xml.WriteEndDocument ();
		}
	}

	public class NoteUtils
	{
		public static void ShowDeletionDialog (Note note, Gtk.Window parent) 
		{
			HIGMessageDialog dialog = 
				new HIGMessageDialog (
					parent,
					Gtk.DialogFlags.DestroyWithParent,
					Gtk.MessageType.Question,
					Gtk.ButtonsType.None,
					Catalog.GetString ("Really delete this note?"),
					Catalog.GetString ("If you delete a note it is " +
							   "permanently lost."));

			Gtk.Button button;

			button = new Gtk.Button (Gtk.Stock.Cancel);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, Gtk.ResponseType.Cancel);
			dialog.DefaultResponse = Gtk.ResponseType.Cancel;

			button = new Gtk.Button (Gtk.Stock.Delete);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, 666);

			int result = dialog.Run ();
			if (result == 666) {
				note.Manager.Delete (note);
			}

			dialog.Destroy();
		}
	}
}
