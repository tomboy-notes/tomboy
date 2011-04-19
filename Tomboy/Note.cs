using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using Mono.Unix;

namespace Tomboy
{
	public delegate void NoteRenameHandler (Note sender, string old_title);
	public delegate void NoteSavedHandler (Note note);
	public delegate void TagAddedHandler (Note note, Tag tag);
	public delegate void TagRemovingHandler (Note note, Tag tag);
	public delegate void TagRemovedHandler (Note note, string tag_name);

	public enum ChangeType
	{
		NoChange,
		ContentChanged,
		OtherDataChanged
	}

	// Contains all pure note data, like the note title and note text.
	public class NoteData
	{
		readonly string uri;
		string title;
		string text;
		DateTime create_date;
		DateTime change_date;
		DateTime metadata_change_date;

		int cursor_pos;
		int width, height;
		int x, y;
		bool open_on_startup;

		Dictionary<string, Tag> tags;

		const int noPosition = -1;

		public NoteData (string uri)
		{
			this.uri = uri;
			this.text = "";
			x = noPosition;
			y = noPosition;

			tags = new Dictionary<string, Tag> ();

			create_date = DateTime.MinValue;
			change_date = DateTime.MinValue;
			metadata_change_date = DateTime.MinValue;
		}

		public string Uri
		{
			get {
				return uri;
			}
		}

		public string Title
		{
			get {
				return title;
			}
			set {
				title = value;
			}
		}

		public string Text
		{
			get {
				return text;
			}
			set {
				text = value;
			}
		}

		public DateTime CreateDate
		{
			get {
				return create_date;
			}
			set {
				create_date = value;
			}
		}

		/// <summary>
		/// Indicates the last time note content data changed.
		/// Does not include tag/notebook changes (see MetadataChangeDate).
		/// </summary>
		public DateTime ChangeDate
		{
			get {
				return change_date;
			}
			set {
				change_date = value;
				metadata_change_date = value;
			}
		}

		/// <summary>
		/// Indicates the last time non-content note data changed.
		/// This currently only applies to tags/notebooks.
		/// </summary>
		public DateTime MetadataChangeDate
		{
			get {
				return metadata_change_date;
			}
			set {
				metadata_change_date = value;
			}
		}
		

		// FIXME: the next five attributes don't belong here (the data
		// model), but belong into the view; for now they are kept here
		// for backwards compatibility

		public int CursorPosition
		{
			get {
				return cursor_pos;
			}
			set {
				cursor_pos = value;
			}
		}

		public int Width
		{
			get {
				return width;
			}
			set {
				width = value;
			}
		}

		public int Height
		{
			get {
				return height;
			}
			set {
				height = value;
			}
		}

		public int X
		{
			get {
				return x;
			}
			set {
				x = value;
			}
		}

		public int Y
		{
			get {
				return y;
			}
			set {
				y = value;
			}
		}

		public Dictionary<string, Tag> Tags
		{
			get {
				return tags;
			}
		}

		public bool IsOpenOnStartup
		{
			get {
				return open_on_startup;
			}
			set {
				open_on_startup = value;
			}
		}

		public void SetPositionExtent (int x, int y, int width, int height)
		{
			if (x < 0 || y < 0)
				return;
			if (width <= 0 || height <= 0)
				return;

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
			get {
				return data;
			}
		}

		public NoteBuffer Buffer
		{
			get {
				return buffer;
			}
			set {
				buffer = value;
				buffer.Changed += OnBufferChanged;
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

		void OnBufferChanged (object sender, EventArgs args)
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
		bool is_deleting;
		bool enabled = true;

		NoteManager manager;
		NoteWindow window;
		NoteBuffer buffer;
		NoteTagTable tag_table;

		InterruptableTimeout save_timeout;

		struct ChildWidgetData
		{
			public Gtk.TextChildAnchor anchor;
			public Gtk.Widget widget;
		};

		Queue <ChildWidgetData> childWidgetQueue;

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

			// Make sure each of the tags that NoteData found point to the
			// instance of this note.
			foreach (Tag tag in data.Tags.Values) {
				AddTag (tag);
			}

			save_timeout = new InterruptableTimeout ();
			save_timeout.Timeout += SaveTimeout;

			childWidgetQueue = new Queue <ChildWidgetData> ();
			
			is_deleting = false;
		}
		/// <summary>
		/// Returns a Tomboy URL from the given path.
		/// </summary>
		/// <param name="filepath">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.String"/>
		/// </returns>
		static string UrlFromPath (string filepath)
		{
			return "note://tomboy/" +
			       Path.GetFileNameWithoutExtension (filepath);
		}

		public override int GetHashCode ()
		{
			return this.Title.GetHashCode();
		}
		/// <summary>
		/// Creates a New Note with the given values.
		/// </summary>
		/// <param name="title">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="filepath">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="manager">
		/// A <see cref="NoteManager"/>
		/// </param>
		/// <returns>
		/// A <see cref="Note"/>
		/// </returns>
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
			if (data.CreateDate == DateTime.MinValue)
				data.CreateDate = File.GetCreationTime (filepath);
			if (data.ChangeDate == DateTime.MinValue)
				data.ChangeDate = File.GetLastWriteTime (filepath);
			return new Note (data, filepath, manager);
		}


		public void Delete ()
		{
			is_deleting = true;
			save_timeout.Cancel ();

			// Remove the note from all the tags
			foreach (Tag tag in Tags) {
				RemoveTag (tag);
			}

			if (window != null) {
				window.Hide ();
				window.Destroy ();
			}
			
			

			// Remove note URI from GConf entry menu_pinned_notes
			IsPinned = false;
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
			// Prevent any other condition forcing a save on the note
			// if Delete has been called.
			if (is_deleting)
				return;
			
			// Do nothing if we don't need to save.  Avoids unneccessary saves
			// e.g on forced quit when we call save for every note.
			if (!save_needed)
				return;

			string new_note_pattern = String.Format (Catalog.GetString ("New Note {0}"), @"\d+");
			Note template_note = manager.GetOrCreateTemplateNote ();
			string template_content = template_note.TextContent.Replace (template_note.Title, Title);

			// Do nothing if this note contains the unchanged template content
			// and if the title matches the new note title template to prevent
			// lots of unwanted "New Note NNN" notes: Bug #545252
			if (Regex.IsMatch (Title, new_note_pattern) && TextContent.Equals (template_content))
				return;

			Logger.Debug ("Saving '{0}'...", data.Data.Title);

			try {
				NoteArchiver.Write (filepath, data.GetDataSynchronized ());
			} catch (Exception e) {
				// Probably IOException or UnauthorizedAccessException?
				Logger.Error ("Exception while saving note: " + e.ToString ());
				NoteUtils.ShowIOErrorDialog (window);
			}

			if (Saved != null)
				Saved (this);
		}

		//
		// Buffer change signals.  These queue saves and invalidate the serialized text
		// depending on the change...
		//

		void OnBufferChanged (object sender, EventArgs args)
		{
			DebugSave ("OnBufferChanged queueing save");
			QueueSave (ChangeType.ContentChanged);
			if (BufferChanged != null)
				BufferChanged (this);
		}

		void BufferTagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (NoteTagTable.TagIsSerializable (args.Tag)) {
				DebugSave ("BufferTagApplied queueing save: {0}", args.Tag.Name);
				QueueSave (ChangeType.ContentChanged);
			}
		}

		void BufferTagRemoved (object sender, Gtk.TagRemovedArgs args)
		{
			if (NoteTagTable.TagIsSerializable (args.Tag)) {
				DebugSave ("BufferTagRemoved queueing save: {0}", args.Tag.Name);
				QueueSave (ChangeType.ContentChanged);
			}
		}

		void BufferInsertMarkSet (object sender, Gtk.MarkSetArgs args)
		{
			if (args.Mark != buffer.InsertMark)
				return;

			data.Data.CursorPosition = args.Location.Offset;

			DebugSave ("BufferInsertSetMark queueing save");
			QueueSave (ChangeType.NoChange);
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
			QueueSave (ChangeType.NoChange);
		}

		[GLib.ConnectBefore]
		void WindowDestroyed (object sender, EventArgs args)
		{
			window = null;
		}
		
		/// <summary>
		/// Set a 4 second timeout to execute the save.  Possibly
		/// invalidate the text, which causes a re-serialize when the
		/// timeout is called...
		/// </summary>
		/// <param name="content_changed">Indicates whether or not
		/// to update the note's last change date</param>
		public void QueueSave (ChangeType changeType)
		{
			DebugSave ("Got QueueSave");

			// Replace the existing save timeout.  Wait 4 seconds
			// before saving...
			save_timeout.Reset (4000);
			if (!is_deleting)
				save_needed = true;
			
			switch (changeType)
			{
			case ChangeType.ContentChanged:
				// NOTE: Updating ChangeDate automatically updates MetdataChangeDate to match.
				data.Data.ChangeDate = DateTime.Now;
				break;
			case ChangeType.OtherDataChanged:
				// Only update MetadataChangeDate.  Used by sync/etc
				// to know when non-content note data has changed,
				// but order of notes in menu and search UI is
				// unaffected.
				data.Data.MetadataChangeDate = DateTime.Now;
				break;
			default:
				break;
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
				Logger.Error ("Error while saving: {0}", e);
			}
		}

		public void AddTag (Tag tag)
		{
			if (tag == null)
				throw new ArgumentNullException ("Note.AddTag () called with a null tag.");

			tag.AddNote (this);

			if (!data.Data.Tags.ContainsKey (tag.NormalizedName)) {
				data.Data.Tags [tag.NormalizedName] = tag;

				if (TagAdded != null)
					TagAdded (this, tag);

				DebugSave ("Tag added, queueing save");
				QueueSave (ChangeType.OtherDataChanged);
			}
		}

		public void RemoveTag (Tag tag)
		{
			if (tag == null)
				throw new ArgumentException ("Note.RemoveTag () called with a null tag.");

			if (!data.Data.Tags.ContainsKey (tag.NormalizedName))
				return;

			if (TagRemoving != null)
				TagRemoving (this, tag);

			data.Data.Tags.Remove (tag.NormalizedName);
			tag.RemoveNote (this);

			if (TagRemoved != null)
				TagRemoved (this, tag.NormalizedName);

			DebugSave ("Tag removed, queueing save");
			QueueSave (ChangeType.OtherDataChanged);
		}
		
		public bool ContainsTag (Tag tag)
		{
			if (data.Data.Tags.ContainsKey (tag.NormalizedName) == true)
				return true;
			
			return false;
		}

		public void AddChildWidget (Gtk.TextChildAnchor childAnchor, Gtk.Widget widget)
		{
			ChildWidgetData data = new ChildWidgetData ();
			data.anchor = childAnchor;
			data.widget = widget;

			childWidgetQueue.Enqueue (data);

			if (HasWindow)
				ProcessChildWidgetQueue ();
		}

		private void ProcessChildWidgetQueue ()
		{
			// Insert widgets in the childWidgetQueue into the NoteEditor
			if (!HasWindow)
				return; // can't do anything without a window

			foreach (ChildWidgetData data in childWidgetQueue) {
				data.widget.Show();
				Window.Editor.AddChildAtAnchor (data.widget, data.anchor);
			}

			childWidgetQueue.Clear ();
		}

		public string Uri
		{
			get {
				return data.Data.Uri;
			}
		}

		public string Id
		{
			get {
				return data.Data.Uri.Replace ("note://tomboy/","");        // TODO: Store on Note instantiation
			}
		}

		public string FilePath
		{
			get {
				return filepath;
			}
			set {
				filepath = value;
			}
		}

		public string Title
		{
			get {
				return data.Data.Title;
			}
			set {
				SetTitle (value, false);
			}
		}

		public void SetTitle (string new_title, bool from_user_action)
		{
			if (data.Data.Title != new_title) {
				if (window != null)
					window.Title = new_title;

				string old_title = data.Data.Title;
				data.Data.Title = new_title;

				if (from_user_action)
					ProcessRenameLinkUpdate (old_title);

				if (Renamed != null)
					Renamed (this, old_title);

				QueueSave (ChangeType.ContentChanged); // TODO: Right place for this?
			}
		}

		private void ProcessRenameLinkUpdate (string old_title)
		{
			List<Note> linkingNotes = new List<Note> ();
			foreach (Note note in manager.Notes) {
				// Technically, containing text does not imply linking,
				// but this is less work
				if (note != this && note.ContainsText (old_title))
					linkingNotes.Add (note);
			}

			if (linkingNotes.Count > 0) {
				NoteRenameBehavior behavior = (NoteRenameBehavior)
					Preferences.Get (Preferences.NOTE_RENAME_BEHAVIOR);
				if (behavior == NoteRenameBehavior.AlwaysShowDialog) {
					var dlg = new NoteRenameDialog (linkingNotes, old_title, this);
					Gtk.ResponseType response = (Gtk.ResponseType) dlg.Run ();
					if (response != Gtk.ResponseType.Cancel &&
					    dlg.SelectedBehavior != NoteRenameBehavior.AlwaysShowDialog)
						Preferences.Set (Preferences.NOTE_RENAME_BEHAVIOR, (int) dlg.SelectedBehavior);
					foreach (var pair in dlg.Notes) {
						if (pair.Value && response == Gtk.ResponseType.Yes) // Rename
							pair.Key.RenameLinks (old_title, this);
						else
							pair.Key.RemoveLinks (old_title, this);
					}
					dlg.Destroy ();
				} else if (behavior == NoteRenameBehavior.AlwaysRemoveLinks)
					foreach (var note in linkingNotes)
						note.RemoveLinks (old_title, this);
				else if (behavior == NoteRenameBehavior.AlwaysRenameLinks)
					foreach (var note in linkingNotes)
						note.RenameLinks (old_title, this);
			}
		}

		private bool ContainsText (string text)
		{
			return TextContent.IndexOf (text, StringComparison.InvariantCultureIgnoreCase) > -1;
		}

		private void RenameLinks (string old_title, Note renamed)
		{
			HandleLinkRename (old_title, renamed, true);
		}

		private void RemoveLinks (string old_title, Note renamed)
		{
			HandleLinkRename (old_title, renamed, false);
		}

		private void HandleLinkRename (string old_title, Note renamed, bool rename_links)
		{
			// Check again, things may have changed
			if (!ContainsText (old_title))
				return;

			string old_title_lower = old_title.ToLower ();

			NoteTag link_tag = TagTable.LinkTag;

			// Replace existing links with the new title.
			TextTagEnumerator enumerator = new TextTagEnumerator (Buffer, link_tag);
			foreach (TextRange range in enumerator) {
				if (range.Text.ToLower () != old_title_lower)
					continue;

				if (!rename_links) {
					Logger.Debug ("Removing link tag from text '{0}'",
					              range.Text);
					Buffer.RemoveTag (link_tag, range.Start, range.End);
				} else {
					Logger.Debug ("Replacing '{0}' with '{1}'",
					              range.Text,
					              renamed.Title);
					Gtk.TextIter start_iter = range.Start;
					Gtk.TextIter end_iter = range.End;
					Buffer.Delete (ref start_iter, ref end_iter);
					start_iter = range.Start;
					Buffer.InsertWithTags (ref start_iter, renamed.Title, link_tag);
				}
			}
		}

		public void RenameWithoutLinkUpdate (string newTitle)
		{
			if (data.Data.Title != newTitle) {
				if (window != null)
					window.Title = newTitle;

				data.Data.Title = newTitle;

				// HACK:
				if (Renamed != null)
					Renamed (this, newTitle);

				QueueSave (ChangeType.ContentChanged); // TODO: Right place for this?
			}
		}

		public string XmlContent
		{
			get {
				return data.Text;
			}
			set {
				if (buffer != null) {
					buffer.Text = string.Empty;
					NoteBufferArchiver.Deserialize (buffer, value);
				} else
					data.Text = value;
			}
		}

		/// <summary>
		/// Return the complete contents of this note's .note XML file
		/// In case of any error, null is returned.
		/// </summary>
		public string GetCompleteNoteXml ()
		{
			return NoteArchiver.WriteString (data.GetDataSynchronized ());
		}

		// Reload note data from a complete note XML string
		// Should referesh note window, too
		public void LoadForeignNoteXml (string foreignNoteXml, ChangeType changeType)
		{
			if (foreignNoteXml == null)
				throw new ArgumentNullException ("foreignNoteXml");

			// Arguments to this method cannot be trusted.  If this method
			// were to throw an XmlException in the middle of processing,
			// a note could be damaged.  Therefore, we check for parseability
			// ahead of time, and throw early.
			XmlDocument xmlDoc = new XmlDocument ();
			// This will throw an XmlException if foreignNoteXml is not parseable
			xmlDoc.LoadXml (foreignNoteXml);
			xmlDoc = null;

			// Remove tags now, since a note with no tags has
			// no "tags" element in the XML
			List<Tag> newTags = new List<Tag> ();

			using (var xml = new XmlTextReader (new StringReader (foreignNoteXml)) {Namespaces = false}) {
				DateTime date;
	
				while (xml.Read ()) {
					switch (xml.NodeType) {
					case XmlNodeType.Element:
						switch (xml.Name) {
						case "title":
							Title = xml.ReadString ();
							break;
						case "text":
							XmlContent = xml.ReadInnerXml ();
							break;
						case "last-change-date":
							if (DateTime.TryParse (xml.ReadString (), out date))
								data.Data.ChangeDate = date;
							else
								data.Data.ChangeDate = DateTime.Now;
							break;
						case "last-metadata-change-date":
							if (DateTime.TryParse (xml.ReadString (), out date))
								data.Data.MetadataChangeDate = date;
							else
								data.Data.MetadataChangeDate = DateTime.Now;
							break;
						case "create-date":
							if (DateTime.TryParse (xml.ReadString (), out date))
								data.Data.CreateDate = date;
							else
								data.Data.CreateDate = DateTime.Now;
							break;
						case "tags":
							XmlDocument doc = new XmlDocument ();
							List<string> tag_strings = ParseTags (doc.ReadNode (xml.ReadSubtree ()));
							foreach (string tag_str in tag_strings) {
								Tag tag = TagManager.GetOrCreateTag (tag_str);
								newTags.Add (tag);
							}
							break;
						case "open-on-startup":
							bool isStartup;
							if (bool.TryParse (xml.ReadString (), out isStartup))
								IsOpenOnStartup = isStartup;
							break;
						}
						break;
					}
				}
			}

			foreach (Tag oldTag in Tags)
				if (!newTags.Contains (oldTag))
				    RemoveTag (oldTag);
			foreach (Tag newTag in newTags)
				AddTag (newTag);

			// Allow method caller to specify ChangeType (mostly needed by sync)
			QueueSave (changeType);
		}

		// TODO: CODE DUPLICATION SUCKS
		List<string> ParseTags (XmlNode tagNodes)
		{
			List<string> tags = new List<string> ();

			foreach (XmlNode node in tagNodes.SelectNodes ("//tag")) {
				string tag = node.InnerText;
				tags.Add (tag);
			}

			return tags;
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
					buffer.Text = value;
				else
					Logger.Error ("Setting text content for closed notes not supported");
			}

		}

		public NoteData Data
		{
			get {
				return data.GetDataSynchronized ();
			}
		}

		public DateTime CreateDate
		{
			get {
				return data.Data.CreateDate;
			}
		}

		/// <summary>
		/// Indicates the last time note content data changed.
		/// Does not include tag/notebook changes (see MetadataChangeDate).
		/// </summary>
		public DateTime ChangeDate
		{
			get {
				return data.Data.ChangeDate;
			}
		}
		
		/// <summary>
		/// Indicates the last time non-content note data changed.
		/// This currently only applies to tags/notebooks.
		/// </summary>
		public DateTime MetadataChangeDate
		{
			get {
				return data.Data.MetadataChangeDate;
			}
		}

		public NoteManager Manager
		{
			get {
				return manager;
			}
			set {
				manager = value;
			}
		}

		public NoteTagTable TagTable
		{
			get {
				if (tag_table == null) {
					// NOTE: Sharing the same TagTable means
					// that formatting is duplicated between
					// buffers.
					tag_table = NoteTagTable.Instance;
				}
				return tag_table;
			}
		}

		public bool HasBuffer
		{
			get {
				return null != buffer;
			}
		}

		public NoteBuffer Buffer
		{
			get {
				if (buffer == null) {
					Logger.Debug ("Creating Buffer for '{0}'...",
					data.Data.Title);

					buffer = new NoteBuffer (TagTable, this);
					data.Buffer = buffer;

					// Listen for further changed signals
					buffer.Changed += OnBufferChanged;
					buffer.TagApplied += BufferTagApplied;
					buffer.TagRemoved += BufferTagRemoved;
					buffer.MarkSet += BufferInsertMarkSet;
				}
				return buffer;
			}
		}

		public bool HasWindow
		{
			get {
				return null != window;
			}
		}

		private Gtk.Widget focusWidget;
		public bool Enabled
		{
			get { return enabled; }
			set {
				enabled = value;
				if (window != null) {
					if (!enabled)
						focusWidget = window.Focus;
					window.Sensitive = enabled;
					if (enabled)
						window.Focus = focusWidget;
				}
			}
		}

		public NoteWindow Window
		{
			get {
				if (window == null) {
					window = new NoteWindow (this);
					window.Destroyed += WindowDestroyed;
					window.ConfigureEvent += WindowConfigureEvent;
					// TODO: What about a disabled set where you can still copy text?
					window.Editor.Sensitive = Enabled;

					if (data.Data.HasExtent ())
						window.SetDefaultSize (data.Data.Width,
						                       data.Data.Height);

					if (data.Data.HasPosition ())
						window.Move (data.Data.X, data.Data.Y);

					// This is here because emiting inside
					// OnRealized causes segfaults.
					if (Opened != null)
						Opened (this, new EventArgs ());

					// Add any child widgets if any exist now that
					// the window is showing.
					ProcessChildWidgetQueue ();
				}
				return window;
			}
		}

		public bool IsSpecial
		{
			get {
				return NoteManager.StartNoteUri == data.Data.Uri;
			}
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
			get {
				return buffer != null;
			}
		}

		public bool IsOpened
		{
			get {
				return window != null;
			}
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

		public bool IsOpenOnStartup
		{
			get {
				return Data.IsOpenOnStartup;
			}
			set {
				if (Data.IsOpenOnStartup != value) {
					Data.IsOpenOnStartup = value;
					save_needed = true;
				}
			}
		}

		public List<Tag> Tags
		{
			get {
				return new List<Tag> (data.Data.Tags.Values);
			}
		}

		public event EventHandler Opened;
		public event NoteRenameHandler Renamed;
		public event NoteSavedHandler Saved;
		public event TagAddedHandler TagAdded;
		public event TagRemovingHandler TagRemoving;
		public event TagRemovedHandler TagRemoved;
		public event Action<Note> BufferChanged;
	}

	// Singleton - allow overriding the instance for easy sensing in
	// test classes - we're not bothering with double-check locking,
	// since this class is only seldomly used
	public class NoteArchiver
	{
		public const string CURRENT_VERSION = "0.3";

		// NOTE: If this changes from a standard format, make sure to update
		//       XML parsing to have a DateTime.TryParseExact
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
			NoteData data;
			string version;
			using (var xml = new XmlTextReader (new StreamReader (read_file, System.Text.Encoding.UTF8)) {Namespaces = false})
				data = Read (xml, uri, out version);

			if (version != NoteArchiver.CURRENT_VERSION) {
				// Note has old format, so rewrite it.  No need
				// to reread, since we are not adding anything.
				Logger.Info ("Updating note XML to newest format...");
				NoteArchiver.Write (read_file, data);
			}

			return data;
		}

		public virtual NoteData Read (XmlTextReader xml, string uri)
		{
			string version; // discarded
			NoteData data = Read (xml, uri, out version);
			return data;
		}

		private NoteData Read (XmlTextReader xml, string uri, out string version)
		{
			NoteData note = new NoteData (uri);
			DateTime date;
			int num;
			version = String.Empty;

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
						if (DateTime.TryParse (xml.ReadString (), out date))
							note.ChangeDate = date;
						else
							note.ChangeDate = DateTime.Now;
						break;
					case "last-metadata-change-date":
						if (DateTime.TryParse (xml.ReadString (), out date))
							note.MetadataChangeDate = date;
						else
							note.MetadataChangeDate = DateTime.Now;
						break;
					case "create-date":
						if (DateTime.TryParse (xml.ReadString (), out date))
							note.CreateDate = date;
						else
							note.CreateDate = DateTime.Now;
						break;
					case "cursor-position":
						if (int.TryParse (xml.ReadString (), out num))
							note.CursorPosition = num;
						break;
					case "width":
						if (int.TryParse (xml.ReadString (), out num))
							note.Width = num;
						break;
					case "height":
						if (int.TryParse (xml.ReadString (), out num))
							note.Height = num;
						break;
					case "x":
						if (int.TryParse (xml.ReadString (), out num))
							note.X = num;
						break;
					case "y":
						if (int.TryParse (xml.ReadString (), out num))
							note.Y = num;
						break;
					case "tags":
						XmlDocument doc = new XmlDocument ();
						List<string> tag_strings = ParseTags (doc.ReadNode (xml.ReadSubtree ()));
						foreach (string tag_str in tag_strings) {
							Tag tag = TagManager.GetOrCreateTag (tag_str);
							note.Tags [tag.NormalizedName] = tag;
						}
						break;
					case "open-on-startup":
						bool isStartup;
						if (bool.TryParse (xml.ReadString (), out isStartup))
							note.IsOpenOnStartup = isStartup;
						break;
					}
					break;
				}
			}

			return note;
		}

		public static string WriteString(NoteData note)
		{
			StringWriter str = new StringWriter ();
			using (var xml = XmlWriter.Create (str, XmlEncoder.DocumentSettings))
				Instance.Write (xml, note);
			str.Flush();
			return str.ToString ();
		}

		public static void Write (string write_file, NoteData note)
		{
			Instance.WriteFile (write_file, note);
		}

		public virtual void WriteFile (string write_file, NoteData note)
		{
			string tmp_file = write_file + ".tmp";

			using (var xml = XmlWriter.Create (tmp_file, XmlEncoder.DocumentSettings))
				Write (xml, note);

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
			using (var xml = XmlWriter.Create (writer, XmlEncoder.DocumentSettings))
				Write (xml, note);
		}

		void Write (XmlWriter xml, NoteData note)
		{
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

			xml.WriteStartElement (null, "last-metadata-change-date", null);
			xml.WriteString (
			        XmlConvert.ToString (note.MetadataChangeDate, DATE_TIME_FORMAT));
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

			if (note.Tags.Count > 0) {
				xml.WriteStartElement (null, "tags", null);
				foreach (Tag tag in note.Tags.Values) {
					xml.WriteStartElement (null, "tag", null);
					xml.WriteString (tag.Name);
					xml.WriteEndElement ();
				}
				xml.WriteEndElement ();
			}

			xml.WriteStartElement (null, "open-on-startup", null);
			xml.WriteString (note.IsOpenOnStartup.ToString ());
			xml.WriteEndElement ();

			xml.WriteEndElement (); // Note
			xml.WriteEndDocument ();
		}

		// <summary>
		// Parse the tags from the <tags> element
		// </summary>
		List<string> ParseTags (XmlNode tagNodes)
		{
			List<string> tags = new List<string> ();

			foreach (XmlNode node in tagNodes.SelectNodes ("//tag")) {
				tags.Add (node.InnerText);
			}

			return tags;
		}

		public virtual string GetRenamedNoteXml (string noteXml, string oldTitle, string newTitle)
		{
			string updatedXml;

			// Replace occurences of oldTitle with newTitle in noteXml
			string titleTagPattern =
			        string.Format ("<title>{0}</title>", oldTitle);
			string titleTagReplacement =
			        string.Format ("<title>{0}</title>", newTitle);
			updatedXml = Regex.Replace (noteXml, titleTagPattern, titleTagReplacement);

			string titleContentPattern =
			        string.Format ("<note-content([^>]*)>\\s*{0}", oldTitle);
			string titleContentReplacement =
			        string.Format ("<note-content$1>{0}", newTitle);
			updatedXml = Regex.Replace (updatedXml, titleContentPattern, titleContentReplacement);

			return updatedXml;
		}

		public virtual string GetTitleFromNoteXml (string noteXml)
		{
			if (noteXml != null && noteXml.Length > 0) {
				XmlTextReader xml = new XmlTextReader (new StringReader (noteXml));
				xml.Namespaces = false;

				while (xml.Read ()) {
					switch (xml.NodeType) {
					case XmlNodeType.Element:
						switch (xml.Name) {
						case "title":
							return xml.ReadString ();
						}
						break;
					}
				}
			}

			return null;
		}
	}

	public class NoteUtils
	{
		public static void ShowDeletionDialog (List<Note> notes, Gtk.Window parent)
		{
			string message;

			if ((bool) Preferences.Get (Preferences.ENABLE_DELETE_CONFIRM)) {
				// show confirmation dialog
				if (notes.Count == 1)
					message = Catalog.GetString ("Really delete this note?");
				else
					message = string.Format (Catalog.GetPluralString (
						"Really delete this {0} note?",
						"Really delete these {0} notes?",
						notes.Count), notes.Count);
			
				HIGMessageDialog dialog =
				        new HIGMessageDialog (
				        parent,
				        Gtk.DialogFlags.DestroyWithParent,
				        Gtk.MessageType.Question,
				        Gtk.ButtonsType.None,
				        message,
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
					foreach (Note note in notes) {
						note.Manager.Delete (note);
					}
				}

				dialog.Destroy();
			} else {
				// no confirmation dialog, just delete
				foreach (Note note in notes) {
					note.Manager.Delete (note);
				}
			}
		}
		
		public static void ShowIOErrorDialog (Gtk.Window parent)
		{
			string errorMsg = Catalog.GetString ("An error occurred while saving your notes. " +
			                                     "Please check that you have sufficient disk " +
			                                     "space, and that you have appropriate rights " +
			                                     "on {0}. Error details can be found in " +
			                                     "{1}.");
			string logPath = System.IO.Path.Combine (Services.NativeApplication.LogDirectory,
			                                         "tomboy.log");
			errorMsg = String.Format (errorMsg,
			                          Services.NativeApplication.DataDirectory,
			                          logPath);
			HIGMessageDialog dialog =
				new HIGMessageDialog (
				                      parent,
				                      Gtk.DialogFlags.DestroyWithParent,
				                      Gtk.MessageType.Error,
				                      Gtk.ButtonsType.Ok,
				                      Catalog.GetString ("Error saving note data."),
				                      errorMsg);
			dialog.Run ();
			dialog.Destroy ();
		}
	}
}
