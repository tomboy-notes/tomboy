
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Mono.Unix;

namespace Tomboy
{
	public delegate void NoteRenameHandler (Note sender, string old_title);
	public delegate void NoteSavedHandler (Note note);

	// Contains all pure note data, like the note title and note text.
	public class NoteData
	{
		readonly string uri;
		string title;
		string text;
		DateTime create_date;
		DateTime change_date;

		int cursor_pos;
		int width, height;
		int x, y;

		const int noPosition = -1;

		public NoteData (string uri)
		{
			this.uri = uri;
			this.text = "";
			x = noPosition;
			y = noPosition;
		}

		public string Uri
		{
			get { return uri; }
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

	// This class wraps a NoteData instance. Most method calls are
	// forwarded to the wrapped instance, but there is special behaviour
	// for the Text attribute. This class takes care that this attribute
	// is synchronized with the contents of a NoteBuffer instance.
	public class NoteDataBufferSynchronizer
	{
		readonly NoteData data;
		NoteBuffer buffer;

		public NoteDataBufferSynchronizer (NoteData data)
		{
			this.data = data;
		}

		public NoteData GetDataSynchronized ()
		{
			// Assert that Data.Text returns the current
			// text from the text buffer.
			SynchronizeText ();
			return data;
		}

		public NoteData Data
		{
			get { return data; }
		}

		public NoteBuffer Buffer
		{
			get { return buffer; }
			set {
				buffer = value;
				buffer.Changed += BufferChanged;
				buffer.TagApplied += BufferTagApplied;
				buffer.TagRemoved += BufferTagRemoved;

				SynchronizeBuffer ();

				InvalidateText ();
			}
		}

		//Text is actually an Xml formatted string
		public string Text
		{
			get {
				SynchronizeText ();
				return data.Text;
			}
			set {
				data.Text = value;
				SynchronizeBuffer ();
			}
		}

		// Custom Methods

		void InvalidateText ()
		{
			data.Text = "";
		}

		bool TextInvalid ()
		{
			return data.Text == "";
		}

		void SynchronizeText ()
		{
			if (TextInvalid () && buffer != null) {
				data.Text = NoteBufferArchiver.Serialize (buffer); 
			}
		}

		void SynchronizeBuffer ()
		{
			if (!TextInvalid () && buffer != null) {
				// Don't create Undo actions during load
				buffer.Undoer.FreezeUndo ();

				buffer.Clear ();

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
			}
		}

		// Callbacks

		void BufferChanged (object sender, EventArgs args)
		{
			InvalidateText ();
		}

		void BufferTagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (NoteTagTable.TagIsSerializable (args.Tag)) {
				InvalidateText ();
			}
		}

		void BufferTagRemoved (object sender, Gtk.TagRemovedArgs args)
		{
			if (NoteTagTable.TagIsSerializable (args.Tag)) {
				InvalidateText ();
			}
		}
	}

	public class Note 
	{
		readonly NoteDataBufferSynchronizer data;

		string filepath;

		bool save_needed;

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
			this.data = new NoteDataBufferSynchronizer (data);
			this.filepath = filepath;
			this.manager = manager;
			save_timeout = new InterruptableTimeout ();
			save_timeout.Timeout += SaveTimeout;
		}

		static string UrlFromPath (string filepath)
		{
			return "note://tomboy/" +
				Path.GetFileNameWithoutExtension (filepath);
		}

		public static Note CreateNewNote (string title,
						  string filepath,
						  NoteManager manager)
		{
			NoteData data = new NoteData (UrlFromPath (filepath));
			data.Title = title;
			data.CreateDate = DateTime.Now;
			data.ChangeDate = data.CreateDate;
			return new Note (data, filepath, manager);
		}

		public static Note CreateExistingNote (NoteData data,
						       string filepath,
						       NoteManager manager)
		{
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
			NoteData data = NoteArchiver.Read (read_file, UrlFromPath (read_file));
			Note note = CreateExistingNote (data, read_file, manager);

			return note;
		}

		public void Save () 
		{
			// Do nothing if we don't need to save.  Avoids unneccessary saves
			// e.g on forced quit when we call save for every note.
			if (!save_needed)
				return;

			Logger.Log ("Saving '{0}'...", data.Data.Title);

			NoteArchiver.Write (filepath, data.GetDataSynchronized ());
			
			if (Saved != null)
				Saved (this);
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

			data.Data.CursorPosition = args.Location.Offset;

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

			if (data.Data.X == cur_x && 
			    data.Data.Y == cur_y &&
			    data.Data.Width == cur_width && 
			    data.Data.Height == cur_height)
				return;

			data.Data.SetPositionExtent (cur_x, cur_y, cur_width, cur_height);
			
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
		public void QueueSave (bool content_changed)
		{
			DebugSave ("Got QueueSave");

			// Replace the existing save timeout.  Wait 4 seconds
			// before saving...
			save_timeout.Reset (4000);
			save_needed = true;

			if (content_changed) {
				data.Data.ChangeDate = DateTime.Now;
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
			get { return data.Data.Uri; }
		}

		public string FilePath 
		{
			get { return filepath; }
			set { filepath = value; }
		}

		public string Title 
		{
			get { return data.Data.Title; }
			set {
				if (data.Data.Title != value) {
					if (window != null)
						window.Title = value;

					string old_title = data.Data.Title;
					data.Data.Title = value;

					if (Renamed != null)
						Renamed (this, old_title);
				}
			}
		}

		public string XmlContent 
		{
			get { return data.Text; }
			set { 
				if (buffer != null)
					buffer.SetText (XmlDecoder.Decode (value));
				else
					data.Text = value; 
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
			set {
				if (buffer != null)
					buffer.SetText (value);
				else
					Logger.Log ("Setting text content for closed notes not supported");
			}
				
		}

		public NoteData Data
		{
			get { return data.GetDataSynchronized (); }
		}

		public DateTime CreateDate 
		{
			get { return data.Data.CreateDate; }
		}

		public DateTime ChangeDate 
		{
			get { return data.Data.ChangeDate; }
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

		public bool HasBuffer
		{
			get { return null != buffer; }
		}

		public NoteBuffer Buffer
		{
			get {
				if (buffer == null) {
					Logger.Log ("Creating Buffer for '{0}'...", 
						    data.Data.Title);

					buffer = new NoteBuffer (TagTable);
					data.Buffer = buffer;

					// Listen for further changed signals
					buffer.Changed += BufferChanged;
					buffer.TagApplied += BufferTagApplied;
					buffer.TagRemoved += BufferTagRemoved;
					buffer.MarkSet += BufferInsertMarkSet;
				}
				return buffer;
			}
		}

		public bool HasWindow
		{
			get { return null != window; }
		}

		public NoteWindow Window 
		{
			get {
				if (window == null) {
					window = new NoteWindow (this);
					window.Destroyed += WindowDestroyed;
					window.ConfigureEvent += WindowConfigureEvent;
					
					if (data.Data.HasExtent ())
						window.SetDefaultSize (data.Data.Width, 
								       data.Data.Height);

					if (data.Data.HasPosition ())
						window.Move (data.Data.X, data.Data.Y);

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
			get { return NoteManager.StartNoteUri == data.Data.Uri; }
		}

		public bool IsNew 
		{
			get { 
				// Note is new if created in the last 24 hours.
				return data.Data.CreateDate > DateTime.Now.AddHours (-24);
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

		public bool IsPinned
		{
			get {
				string pinned_uris = (string)
					Preferences.Get (Preferences.MENU_PINNED_NOTES);
				return pinned_uris.IndexOf (Uri) > -1;
			}
			set {
				string new_pinned = "";
				string old_pinned = (string)
					Preferences.Get (Preferences.MENU_PINNED_NOTES);
				bool pinned = old_pinned.IndexOf (Uri) > -1;

				if (value == pinned) 
					return;

				if (value) {
					new_pinned = Uri + " " + old_pinned;
				} else {
					string [] pinned_split = old_pinned.Split (' ', '\t', '\n');
					foreach (string pin in pinned_split) {
						if (pin != "" && pin != Uri) {
							new_pinned += pin + " ";
						}
					}
				}

				Preferences.Set (Preferences.MENU_PINNED_NOTES, new_pinned);
			}
		}

		public event EventHandler Opened;
		public event NoteRenameHandler Renamed;
		public event NoteSavedHandler Saved;
	}

	// Singleton - allow overriding the instance for easy sensing in
	// test classes - we're not bothering with double-check locking,
	// since this class is only seldomly used
	public class NoteArchiver
	{
		public const string CURRENT_VERSION = "0.2";
		
		public const string DATE_TIME_FORMAT = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";

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

		public static NoteData Read (string read_file, string uri)
		{
			return Instance.ReadFile (read_file, uri);
		}

		public virtual NoteData ReadFile (string read_file, string uri) 
		{
			NoteData note = new NoteData (uri);
			string version = "";

			StreamReader reader = new StreamReader (read_file, 
								System.Text.Encoding.UTF8);
			XmlTextReader xml = new XmlTextReader (reader);
			xml.Namespaces = false;

			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					switch (xml.Name) {
					case "note":
						version = xml.GetAttribute ("version");
						break;
					case "title":
						note.Title = xml.ReadString ();
						break;
					case "text":
						// <text> is just a wrapper around <note-content>
						// NOTE: Use .text here to avoid triggering a save.
						note.Text = xml.ReadInnerXml ();
						break;
					case "last-change-date":
						note.ChangeDate = 
							XmlConvert.ToDateTime (xml.ReadString (), DATE_TIME_FORMAT);
						break;
					case "create-date":
						note.CreateDate = 
							XmlConvert.ToDateTime (xml.ReadString (), DATE_TIME_FORMAT);
						break;
					case "cursor-position":
						note.CursorPosition = int.Parse (xml.ReadString ());
						break;
					case "width":
						note.Width = int.Parse (xml.ReadString ());
						break;
					case "height":
						note.Height = int.Parse (xml.ReadString ());
						break;
					case "x":
						note.X = int.Parse (xml.ReadString ());
						break;
					case "y":
						note.Y = int.Parse (xml.ReadString ());
						break;
					}
					break;
				}
			}

			xml.Close ();

			if (version != NoteArchiver.CURRENT_VERSION) {
				// Note has old format, so rewrite it.  No need
				// to reread, since we are not adding anything.
				Logger.Log ("Updating note XML to newest format...");
				NoteArchiver.Write (read_file, note);
			}

			return note;
		}

		public static void Write (string write_file, NoteData note)
		{
			Instance.WriteFile (write_file, note);
		}

		public virtual void WriteFile (string write_file, NoteData note) 
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
		}

		public static void Write (TextWriter writer, NoteData note)
		{
			Instance.WriteFile (writer, note);
		}

		public void WriteFile (TextWriter writer, NoteData note)
		{
			XmlTextWriter xml = new XmlTextWriter (writer);
			Write (xml, note);
			xml.Close ();
		}

		void Write (XmlTextWriter xml, NoteData note)
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
			xml.WriteString (note.Title);
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "text", null);
			xml.WriteAttributeString ("xml", "space", null, "preserve");
			// Insert <note-content> blob...
			xml.WriteRaw (note.Text);
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "last-change-date", null);
			xml.WriteString (
						XmlConvert.ToString (note.ChangeDate, DATE_TIME_FORMAT));
			xml.WriteEndElement ();

			if (note.CreateDate != DateTime.MinValue) {
				xml.WriteStartElement (null, "create-date", null);
				xml.WriteString (
						XmlConvert.ToString (note.CreateDate, DATE_TIME_FORMAT));
				xml.WriteEndElement ();
			}

			xml.WriteStartElement (null, "cursor-position", null);
			xml.WriteString (note.CursorPosition.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "width", null);
			xml.WriteString (note.Width.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "height", null);
			xml.WriteString (note.Height.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "x", null);
			xml.WriteString (note.X.ToString ());
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "y", null);
			xml.WriteString (note.Y.ToString ());
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
