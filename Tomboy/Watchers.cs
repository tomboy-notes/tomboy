
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mono.Posix;

namespace Tomboy
{
	public class NoteRenameWatcher : NotePlugin
	{
		bool editing_title;

		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void Shutdown ()
		{
			// Do nothing.
		}

		Gtk.TextTag TitleTag
		{
			get { return Buffer.TagTable.Lookup ("note-title"); }
		}

		Gtk.TextIter TitleEnd 
		{
			get {
				Gtk.TextIter line_end = Buffer.StartIter;
				line_end.ForwardToLineEnd ();
				return line_end;
			}
		}

		Gtk.TextIter TitleStart 
		{
			get { return Buffer.StartIter; }
		}

		protected override void OnNoteOpened ()
		{
			Buffer.MarkSet += OnMarkSet;
			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;

			// FIXME: Needed because we hide on delete event, and
			// just hide on accelerator key, so we can't use delete
			// event.  This means the window will flash if closed
			// with a name clash.
			Window.UnmapEvent += OnWindowClosed;

			// Clean up title line
			Buffer.RemoveAllTags (TitleStart, TitleEnd);
			Buffer.ApplyTag (TitleTag, TitleStart, TitleEnd);
		}

		// This only gets called on an explicit move, not when typing
		void OnMarkSet (object sender, Gtk.MarkSetArgs args)
		{
			Update ();
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Changed ();
			Update ();

			Gtk.TextIter end = args.Pos;
			end.ForwardToLineEnd ();

			// Avoid lingering note-title after a multi-line insert...
			Buffer.RemoveTag (TitleTag, TitleEnd, end);
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Changed ();
			Update ();
		}

		void Update ()
		{
			Gtk.TextIter insert = Buffer.GetIterAtMark (Buffer.InsertMark);
			Gtk.TextIter selection = Buffer.GetIterAtMark (Buffer.SelectionBound);

			if (insert.Line == 0 || selection.Line == 0) {
				if (!editing_title)
					editing_title = true;
			} else {
				if (editing_title) {
					UpdateNoteTitle ();
					editing_title = false;
				}
			}
		}

		void Changed ()
		{
			if (!editing_title)
				return;

			// Make sure the title line is big and red...
			Buffer.RemoveAllTags (TitleStart, TitleEnd);
			Buffer.ApplyTag (TitleTag, TitleStart, TitleEnd);

			// NOTE: Use "(Untitled #)" for empty first lines...
			string title = TitleStart.GetText (TitleEnd).Trim ();
			if (title == string.Empty)
				title = GetUniqueUntitled ();

			// Only set window title here, to give feedback that we
			// are indeed changing the title.
			Window.Title = title;
		}

		[GLib.ConnectBefore]
		void OnWindowClosed (object sender, Gtk.UnmapEventArgs args)
		{
			if (!editing_title)
				return;

			if (!UpdateNoteTitle ()) {
				args.RetVal = true;
				return;
			}
		}

		string GetUniqueUntitled ()
		{
			int new_num = Manager.Notes.Count;
			string temp_title;

			while (true) {
				temp_title = String.Format (Catalog.GetString ("(Untitled {0})"), 
							    ++new_num);
				if (Manager.Find (temp_title) == null)
					return temp_title;
			}
		}

		bool UpdateNoteTitle ()
		{
			string title = Window.Title;

			Note existing = Manager.Find (title);
			if (existing != null && existing != this.Note) {
				// Present the window in case it got unmapped...
				// FIXME: Causes flicker.
				Note.Window.Present ();

				ShowNameClashError (title);
				return false;
			}

			Note.Title = title;
			return true;
		}

		void ShowNameClashError (string title)
		{
			string message = 
				String.Format (Catalog.GetString ("A note with the title " +
								  "<b>{0}</b> already exists. " +
								  "Please choose another name " +
								  "for this note before " +
								  "continuing."),
					       title);

			HIGMessageDialog dialog = 
				new HIGMessageDialog (Window,
						      Gtk.DialogFlags.DestroyWithParent,
						      Gtk.MessageType.Warning,
						      Gtk.ButtonsType.Ok,
						      Catalog.GetString ("Note title taken"),
						      message);

			dialog.Run ();
			dialog.Destroy ();
		}
	}

	public class NoteSpellChecker : NotePlugin
	{
		IntPtr obj_ptr = IntPtr.Zero;

		[DllImport ("libgtkspell")]
		static extern IntPtr gtkspell_new_attach (IntPtr text_view, 
							  string locale, 
							  IntPtr error);

		[DllImport ("libgtkspell")]
		static extern void gtkspell_detach (IntPtr obj);

		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void Shutdown ()
		{
			// Do nothing.
		}

		protected override void OnNoteOpened ()
		{
			Buffer.TagApplied += TagApplied;
			Preferences.SettingChanged += OnEnableSpellcheckChanged;

			if ((bool) Preferences.Get (Preferences.ENABLE_SPELLCHECKING)) {
				Attach ();
			}
		}

		// To unset GtkSpell's tag Foreground correctly we need to call
		// SetProperty, which is a protected member.
		class OldGtkSpellFixup : Gtk.TextTag
		{
			public OldGtkSpellFixup (IntPtr raw)
				: base (raw)
			{
			}

			public void Fixup ()
			{
				SetProperty ("foreground_gdk", new GLib.Value (Gdk.Color.Zero));

				// Force the value to 4 since error underlining
				// isn't mapped in Gtk# yet.
				Underline = (Pango.Underline) 4;
			}
		}

		// NOTE: Older versions of GtkSpell before 2.0.6 use red
		// foreground color and a single underline.  This conflicts with
		// internal note links.  So fix it up to unset foreground and
		// use the "error" underline.
		void FixupOldGtkSpell ()
		{
#if OLD_GTKSPELL
			Gtk.TextTag misspell = Buffer.TagTable.Lookup ("gtkspell-misspelled");
			if (misspell != null) {
				OldGtkSpellFixup hack = new OldGtkSpellFixup (misspell.Handle);
				hack.Fixup ();
			}
#endif
		}

		void Attach ()
		{
			if (obj_ptr == IntPtr.Zero) {
				obj_ptr = gtkspell_new_attach (Window.Editor.Handle, 
							       null, 
							       IntPtr.Zero);
				FixupOldGtkSpell ();
			}
		}

		void Detach ()
		{
			if (obj_ptr != IntPtr.Zero) {
				gtkspell_detach (obj_ptr);
				obj_ptr = IntPtr.Zero;
			}
		}

		void OnEnableSpellcheckChanged (object sender, GConf.NotifyEventArgs args)
		{
			if (args.Key != Preferences.ENABLE_SPELLCHECKING)
				return;

			if ((bool) args.Value) {
				Attach ();
			} else {
				Detach ();
			}
		}

		void TagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (args.Tag.Name == "gtkspell-misspelled") {
				// Remove misspelled tag for links & title
				foreach (Gtk.TextTag tag in args.StartChar.Tags) {
					if (tag.Name != null && 
					    (tag.Name.StartsWith ("link:") || 
					     tag.Name == "note-title")) {
						Buffer.RemoveTag ("gtkspell-misspelled", 
								  args.StartChar, 
								  args.EndChar);
						break;
					}
				}
			} else if (args.Tag.Name != null && 
				   (args.Tag.Name.StartsWith ("link:") ||
				    args.Tag.Name == "note-title")) {
				Gtk.TextTag misspell = 
					Buffer.TagTable.Lookup ("gtkspell-misspelled");
				if (misspell != null) {
					Buffer.RemoveTag ("gtkspell-misspelled", 
							  args.StartChar, 
							  args.EndChar);
				}
			}
		}
	}

	public class NoteUrlWatcher : NotePlugin
	{
		Gtk.TextTag url_tag;
		Gtk.TextMark click_mark;

		const string URL_REGEX = 
			@"((\b((news|http|https|ftp|file|irc)://|mailto:|(www|ftp)\.|\S*@\S*\.)|(^|\s)/\S+/|(^|\s)~/\S+)\S*\b/?)";

		static Regex regex;
		static bool text_event_connected;

		static NoteUrlWatcher ()
		{
			regex = new Regex (URL_REGEX, 
					   RegexOptions.IgnoreCase | RegexOptions.Compiled);
			text_event_connected = false;
		}

		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void Shutdown ()
		{
			// Do nothing.
		}

		protected override void OnNoteOpened ()
		{
			url_tag = Buffer.TagTable.Lookup ("link:url");
			if (url_tag == null) {
				Console.WriteLine ("Tag 'link:url' not registered for buffer");
				return;
			}

#if FIXED_GTKSPELL
			// NOTE: This hack helps avoid multiple URL opens for
			// cases where the GtkSpell version is fixed to allow
			// TagTable sharing.  This is because if the TagTable is
			// shared, we will connect to the same Tag's event
			// source each time a note is opened, and get called
			// multiple times for each button press.  Fixes bug
			// #305813.
			if (!text_event_connected) {
				url_tag.TextEvent += OnTextEvent;
				text_event_connected = true;
			}
#else
			url_tag.TextEvent += OnTextEvent;
#endif

			click_mark = Buffer.CreateMark (null, Buffer.StartIter, true);

			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;

			Window.Editor.ButtonPressEvent += OnButtonPress;
			Window.Editor.PopulatePopup += OnPopulatePopup;
			Window.Editor.PopupMenu += OnPopupMenu;
		}

		string GetUrlAtIter (Gtk.TextIter iter)
		{
			Gtk.TextIter start = iter;
			start.BackwardToTagToggle (url_tag);

			Gtk.TextIter end = iter;
			end.ForwardToTagToggle (url_tag);

			string url = start.GetText (end);

			// FIXME: Needed because the file match is greedy and
			// eats a leading space.
			url = url.Trim ();

			// Simple url massaging.  Add to 'http://' to the front
			// of www.foo.com, 'mailto:' to alex@foo.com, 'file://'
			// to /home/alex/foo.
			if (url.StartsWith ("www."))
				url = "http://" + url;
			else if (url.StartsWith ("http://") ||
				 url.StartsWith ("https://") ||
				 url.StartsWith ("ftp://") ||
				 url.StartsWith ("file://"))
				url = url;
			else if (url.StartsWith ("/") && 
				 url.LastIndexOf ("/") > 1)
				url = "file://" + url;
			else if (url.StartsWith ("~/"))
				url = "file://" + 
					Path.Combine (Environment.GetEnvironmentVariable ("HOME"),
						      url.Substring (2));
			else if (url.IndexOf ("@") > 1 &&
				 url.IndexOf (".") > 3 &&
				 !url.StartsWith ("mailto:"))
				url = "mailto:" + url;

			return url;
		}

		void OpenUrl (string url) 
		{
			if (url != string.Empty) {
				Console.WriteLine ("Opening url '{0}'...", url);
				Gnome.Url.Show (url);
			}
		}

		void ShowOpeningLocationError (string url, string error)
		{
			string message = String.Format ("{0}: {1}", url, error);

			HIGMessageDialog dialog = 
				new HIGMessageDialog (Window,
						      Gtk.DialogFlags.DestroyWithParent,
						      Gtk.MessageType.Info,
						      Gtk.ButtonsType.Ok,
						      Catalog.GetString ("Cannot open location"),
						      message);
			dialog.Run ();
			dialog.Destroy ();
		}

		void OnTextEvent (object sender, Gtk.TextEventArgs args)
		{
			Gtk.TextTag tag = (Gtk.TextTag) sender;

			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Gdk.EventButton button_ev = new Gdk.EventButton (args.Event.Handle);
			if (button_ev.Button != 1 && button_ev.Button != 2)
				return;

			/* Don't open link if Shift or Control is pressed */
			if ((int) (button_ev.State & (Gdk.ModifierType.ShiftMask |
						      Gdk.ModifierType.ControlMask)) != 0)
				return;

			string url = GetUrlAtIter (args.Iter);
			try {
				OpenUrl (url);

				// Close note on middle-click
				if (button_ev.Button == 2) {
					Window.Hide ();
				}
			} catch (GLib.GException e) {
				ShowOpeningLocationError (url, e.Message);
			}

			// Kill the middle button paste...
			args.RetVal = true;
		}

		void ApplyUrlToBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			start.LineOffset = 0;
			end.ForwardToLineEnd ();

			Buffer.RemoveTag (url_tag, start, end);

			for (Match match = regex.Match (start.GetText (end)); 
			     match.Success; 
			     match = match.NextMatch ()) {
				Group group = match.Groups [1];

				/*
				Console.WriteLine("Highlighting url: '{0}' at offset {1}",
						  group,
						  group.Index);
				*/

				Gtk.TextIter start_cpy = start;
				start_cpy.ForwardChars (group.Index);

				end = start_cpy;
				end.ForwardChars (group.Length);

				Buffer.ApplyTag (url_tag, start_cpy, end);
			}
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			ApplyUrlToBlock (args.Start, args.End);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			ApplyUrlToBlock (start, args.Pos);
		}

		[GLib.ConnectBefore]
		void OnButtonPress (object sender, Gtk.ButtonPressEventArgs args)
		{
			int x, y;

			Window.Editor.WindowToBufferCoords (Gtk.TextWindowType.Text,
							    (int) args.Event.X,
							    (int) args.Event.Y,
							    out x,
							    out y);
			Gtk.TextIter click_iter = Window.Editor.GetIterAtLocation (x, y);

			// Move click_mark to click location
			Buffer.MoveMark (click_mark, click_iter);

			// Continue event processing
			args.RetVal = false;
		}

		void OnPopulatePopup (object sender, Gtk.PopulatePopupArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (click_mark);
			if (click_iter.HasTag (url_tag) || click_iter.EndsTag (url_tag)) {
				Gtk.MenuItem item;

				item = new Gtk.SeparatorMenuItem ();
				item.Show ();
				args.Menu.Prepend (item);

				item = new Gtk.MenuItem (Catalog.GetString ("_Copy Link Address"));
				item.Activated += CopyLinkActivate;
				item.Show ();
				args.Menu.Prepend (item);

				item = new Gtk.MenuItem (Catalog.GetString ("_Open Link"));
				item.Activated += OpenLinkActivate;
				item.Show ();
				args.Menu.Prepend (item);
			}
		}

		// Called via Alt-F10.  Reset click_mark to cursor location.
		[GLib.ConnectBefore]
		void OnPopupMenu (object sender, Gtk.PopupMenuArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (Buffer.InsertMark);
			Buffer.MoveMark (click_mark, click_iter);
			args.RetVal = false; // Continue event processing
		}

		void OpenLinkActivate (object sender, EventArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (click_mark);
			string url = GetUrlAtIter (click_iter);
			try {
				OpenUrl (url);
			} catch (GLib.GException e) {
				ShowOpeningLocationError (url, e.Message);
			}
		}

		void CopyLinkActivate (object sender, EventArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (click_mark);
			string url = GetUrlAtIter (click_iter);

			Gtk.Clipboard clip = Window.Editor.GetClipboard (Gdk.Selection.Clipboard);
			clip.SetText (url);
		}
	}

	public class NoteLinkWatcher : NotePlugin
	{
		protected override void Initialize () 
		{
			Manager.NoteDeleted += OnNoteDeleted;
			Manager.NoteAdded += OnNoteAdded;
			Manager.NoteRenamed += OnNoteRenamed;
		}

		protected override void Shutdown ()
		{
			Manager.NoteDeleted -= OnNoteDeleted;
			Manager.NoteAdded -= OnNoteAdded;
			Manager.NoteRenamed -= OnNoteRenamed;
		}

		protected override void OnNoteOpened ()
		{
			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;
		}

		bool ContainsText (string text)
		{
			string body = Note.TextContent.ToLower ();
			string match = text.ToLower ();

			return body.IndexOf (match) > -1;
		}

		void OnNoteAdded (object sender, Note added)
		{
			if (added == this.Note)
				return;

			if (!ContainsText (added.Title))
				return;

			// Highlight previously unlinked text
			Highlighter.HighlightNote (Note, Buffer.StartIter, Buffer.EndIter, added);
		}

		void OnNoteDeleted (object sender, Note deleted)
		{
			if (deleted == this.Note)
				return;

			if (!ContainsText (deleted.Title))
				return;

			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			Gtk.TextTag broken_link_tag = Buffer.TagTable.Lookup ("link:broken");

			string old_title_lower = deleted.Title.ToLower ();

			// Turn all link:internal to link:broken for the deleted note.
			TextTagEnumerator enumerator = new TextTagEnumerator (Buffer, link_tag);
			foreach (TextRange range in enumerator) {
				if (range.Text.ToLower () != old_title_lower)
					continue;

				Buffer.RemoveTag (link_tag, range.Start, range.End);
				Buffer.ApplyTag (broken_link_tag, range.Start, range.End);
			}
		}

		void OnNoteRenamed (Note renamed, string old_title)
		{
			if (renamed == this.Note)
				return;

			// Highlight previously unlinked text
			if (ContainsText (renamed.Title)) {
				Highlighter.HighlightNote (Note, 
							   Buffer.StartIter, 
							   Buffer.EndIter, 
							   renamed);
			}

			if (!ContainsText (old_title))
				return;

			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			string old_title_lower = old_title.ToLower ();

			// Replace existing links with the new title.
			TextTagEnumerator enumerator = new TextTagEnumerator (Buffer, link_tag);
			foreach (TextRange range in enumerator) {
				if (range.Text.ToLower () != old_title_lower)
					continue;

				Console.WriteLine ("Replacing '{0}' with '{0}'", 
						   range.Text, 
						   renamed.Title);

				Buffer.Delete (range.Start, range.End);
				Buffer.InsertWithTags (range.Start, renamed.Title, link_tag);
			}
		}

		class Highlighter
		{
			Note note;
			Gtk.TextIter start;
			Gtk.TextIter end;

			Gtk.TextTag url_tag;
			Gtk.TextTag link_tag;
			Gtk.TextTag broken_link_tag;

			Highlighter (Note         note, 
				     Gtk.TextIter start, 
				     Gtk.TextIter end)
			{
				this.note = note;
				this.start = start;
				this.end = end;

				this.url_tag = note.Buffer.TagTable.Lookup ("link:url");
				this.link_tag = note.Buffer.TagTable.Lookup ("link:internal");
				this.broken_link_tag = note.Buffer.TagTable.Lookup ("link:broken");
			}

			public static void HighlightTrie (Note         note, 
							  Gtk.TextIter start, 
							  Gtk.TextIter end,
							  TrieTree     trie)
			{
				Highlighter high = new Highlighter (note, start, end);
				trie.FindMatches (start.GetText (end), 
						  new MatchHandler (high.TitleFound));
			}

			public static void HighlightNote (Note         note, 
							  Gtk.TextIter start, 
							  Gtk.TextIter end,
							  Note         find_note)
			{
				Highlighter high = new Highlighter (note, start, end);

				string buffer_text = start.GetText (end).ToLower();
				string find_title_lower = find_note.Title.ToLower ();
				int idx = 0;

				while (true) {
					idx = buffer_text.IndexOf (find_title_lower, idx);
					if (idx < 0)
						break;

					high.TitleFound (buffer_text, idx, find_note);
					idx += find_title_lower.Length;
				}
			}

			void TitleFound (string haystack, int start_idx, object value)
			{
				Note match_note = (Note) value;

				if (match_note == note)
					return;

				Gtk.TextIter title_start = start;
				title_start.ForwardChars (start_idx);

				// Don't create links inside URLs
				if (title_start.HasTag (url_tag))
					return;

				Gtk.TextIter title_end = title_start;
				title_end.ForwardChars (match_note.Title.Length);

				Console.WriteLine ("Matching Note title '{0}'...", 
						   match_note.Title);

				note.Buffer.RemoveTag (broken_link_tag, title_start, title_end);
				note.Buffer.ApplyTag (link_tag, title_start, title_end);
			}
		}

		void HighlightInBlock (Gtk.TextIter start, Gtk.TextIter end) 
		{
			GetBlockExtents (ref start, ref end);
			Highlighter.HighlightTrie (Note, start, end, Manager.TitleTrie);
		}

		void UnhighlightInBlock (Gtk.TextIter start, Gtk.TextIter end) 
		{
			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			Buffer.RemoveTag (link_tag, start, end);
		}

		void GetBlockExtents (ref Gtk.TextIter start, ref Gtk.TextIter end) 
		{
			// FIXME: Should only be processing the largest title
			// size, so we don't slow down for large paragraphs 

			start.LineOffset = 0;
			end.ForwardToLineEnd ();
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Gtk.TextIter start = args.Start;
			Gtk.TextIter end = args.End;

			UnhighlightInBlock (start, end);
			HighlightInBlock (start, end);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			Gtk.TextIter end = args.Pos;

			UnhighlightInBlock (start, end);
			HighlightInBlock (start, end);
		}
	}

	public class NoteWikiWatcher : NotePlugin
	{
		Gtk.TextTag broken_link_tag;

		// NOTE: \p{Lu} is Unicode uppercase and \p{Ll} is lowercase.
		const string WIKIWORD_REGEX = @"\b((\p{Lu}+[\p{Ll}0-9]+){2}([\p{Lu}\p{Ll}0-9])*)\b";

		static Regex regex; 

		static NoteWikiWatcher ()
		{
			regex = new Regex (WIKIWORD_REGEX, RegexOptions.Compiled);
		}

		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void Shutdown ()
		{
			// Do nothing.
		}

		protected override void OnNoteOpened ()
		{
			broken_link_tag = Buffer.TagTable.Lookup ("link:broken");
			if (broken_link_tag == null) {
				Console.WriteLine ("ERROR: Broken link tags not registered.");
				return;
			}

			if ((bool) Preferences.Get (Preferences.ENABLE_WIKIWORDS)) {
				Buffer.InsertText += OnInsertText;
				Buffer.DeleteRange += OnDeleteRange;
			}
			Preferences.SettingChanged += OnEnableWikiwordsChanged;
		}

		void OnEnableWikiwordsChanged (object sender, GConf.NotifyEventArgs args)
		{
			if (args.Key != Preferences.ENABLE_WIKIWORDS)
				return;

			if ((bool) args.Value) {
				Buffer.InsertText += OnInsertText;
				Buffer.DeleteRange += OnDeleteRange;
			} else {
				Buffer.InsertText -= OnInsertText;
				Buffer.DeleteRange -= OnDeleteRange;
			}
		}

		static string [] PatronymicPrefixes = 
			new string [] { "Mc", "Mac", "Le", "La", "De", "Van" };

		bool IsPatronymicName (string word)
		{
			foreach (string prefix in PatronymicPrefixes) {
				if (word.StartsWith (prefix) &&
				    char.IsUpper (word [prefix.Length]))
					return true;
			}

			return false;
		}

		void ApplyWikiwordToBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			start.LineOffset = 0;
			end.ForwardToLineEnd ();

			Buffer.RemoveTag (broken_link_tag, start, end);

			for (Match match = regex.Match (start.GetText (end)); 
			     match.Success; 
			     match = match.NextMatch ()) {
				Group group = match.Groups [1];

				if (IsPatronymicName (group.ToString ()))
					continue;

				Console.WriteLine("Highlighting wikiword: '{0}' at offset {1}",
						  group,
						  group.Index);

				Gtk.TextIter start_cpy = start;
				start_cpy.ForwardChars (group.Index);

				end = start_cpy;
				end.ForwardChars (group.Length);

				if (Manager.Find (group.ToString ()) == null) {
					Buffer.ApplyTag (broken_link_tag, start_cpy, end);
				}
			}
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			ApplyWikiwordToBlock (args.Start, args.End);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			ApplyWikiwordToBlock (start, args.Pos);
		}
	}

	public class MouseHandWatcher : NotePlugin
	{
		Gtk.TextTag link_tag;
		Gtk.TextTag broken_link_tag;

		bool hovering_on_link;

		static Gdk.Cursor normal_cursor;
		static Gdk.Cursor hand_cursor;
		static bool text_event_connected;

		static MouseHandWatcher ()
		{
			normal_cursor = new Gdk.Cursor (Gdk.CursorType.Xterm);
			hand_cursor = new Gdk.Cursor (Gdk.CursorType.Hand2);
			text_event_connected = false;
		}

		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void Shutdown ()
		{
			// Do nothing.
		}

		protected override void OnNoteOpened ()
		{
			link_tag = Buffer.TagTable.Lookup ("link:internal");
			broken_link_tag = Buffer.TagTable.Lookup ("link:broken");

#if FIXED_GTKSPELL
			// NOTE: This avoid multiple link opens for cases where
			// the GtkSpell version is fixed to allow TagTable
			// sharing.  This is because if the TagTable is shared,
			// we will connect to the same Tag's event source each
			// time a note is opened, and get called multiple times
			// for each button press.  Fixes bug #305813.
			if (!text_event_connected) {
				link_tag.TextEvent += OnLinkTextEvent;
				broken_link_tag.TextEvent += OnLinkTextEvent;
				text_event_connected = true;
			}
#else
			link_tag.TextEvent += OnLinkTextEvent;
			broken_link_tag.TextEvent += OnLinkTextEvent;
#endif

			Gtk.TextView editor = Window.Editor;
			editor.MotionNotifyEvent += OnEditorMotion;
			editor.KeyPressEvent += OnEditorKeyPress;
			editor.KeyReleaseEvent += OnEditorKeyRelease;
		}

		bool OpenOrCreateLink (Gtk.TextIter start, Gtk.TextIter end)
		{
			string link_name = start.GetText (end);
			Note link = Manager.Find (link_name);

			if (link == null) {
				Console.WriteLine ("Creating note '{0}'...", link_name);
				link = Manager.Create (link_name);
			}

			if (link != null && link != this.Note) {
				Console.WriteLine ("Opening note '{0}' on click...", link_name);
				link.Window.Present ();
				return true;
			}

			return false;
		}

		void OnLinkTextEvent (object sender, Gtk.TextEventArgs args)
		{
			Gtk.TextTag tag = (Gtk.TextTag) sender;

			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Gdk.EventButton button_ev = new Gdk.EventButton (args.Event.Handle);
			if (button_ev.Button != 1 && button_ev.Button != 2)
				return;

			/* Don't open link if Shift or Control is pressed */
			if ((int) (button_ev.State & (Gdk.ModifierType.ShiftMask |
						      Gdk.ModifierType.ControlMask)) != 0)
				return;

			Gtk.TextIter start = args.Iter, end = args.Iter;

			if (!start.BeginsTag (tag))
				start.BackwardToTagToggle (tag);
			end.ForwardToTagToggle (tag);

			if (!OpenOrCreateLink (start, end))
				return;

			if (button_ev.Button == 2) {
				Window.Hide ();

				// Kill the middle button paste...
				args.RetVal = true;
			}
		}

		[GLib.ConnectBefore]
		void OnEditorKeyPress (object sender, Gtk.KeyPressEventArgs args) 
		{
			switch (args.Event.Key) {
			case Gdk.Key.Shift_L:
			case Gdk.Key.Shift_R:
			case Gdk.Key.Control_L:
			case Gdk.Key.Control_R:
				// Control or Shift when hovering over a link
				// swiches to a bar cursor...

				if (!hovering_on_link)
					break;

				Gdk.Window win = Window.Editor.GetWindow (Gtk.TextWindowType.Text);
				win.Cursor = normal_cursor;
				break;

			case Gdk.Key.Return:
			case Gdk.Key.KP_Enter:
				// Control-Enter opens the link at point...

				// FIXME: just fire a Widget.Event for this
				// args.Event, and let the handlers deal

				if ((int) (args.Event.State & Gdk.ModifierType.ControlMask) == 0)
					break;
				
				Gtk.TextIter iter = Buffer.GetIterAtMark (Buffer.InsertMark);

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (tag == link_tag || tag == broken_link_tag) {
						Gtk.TextIter start = iter, end = iter;

						if (!start.BeginsTag (tag))
							start.BackwardToTagToggle (tag);
						end.ForwardToTagToggle (tag);

						OpenOrCreateLink (start, end);
						args.RetVal = true;
						break;
					}
				}
				break;
			}
		}

		[GLib.ConnectBefore]
		void OnEditorKeyRelease (object sender, Gtk.KeyReleaseEventArgs args) 
		{
			switch (args.Event.Key) {
			case Gdk.Key.Shift_L:
			case Gdk.Key.Shift_R:
			case Gdk.Key.Control_L:
			case Gdk.Key.Control_R:
				if (!hovering_on_link)
					break;

				Gdk.Window win = Window.Editor.GetWindow (Gtk.TextWindowType.Text);
				win.Cursor = hand_cursor;
				break;
			}
		}

		[GLib.ConnectBefore]
		void OnEditorMotion (object sender, Gtk.MotionNotifyEventArgs args)
		{
			int pointer_x, pointer_y;
			Gdk.ModifierType pointer_mask;

			Window.Editor.GdkWindow.GetPointer (out pointer_x, 
							    out pointer_y, 
							    out pointer_mask);

			bool hovering = false;
			
			// Figure out if we're on a link by getting the text
			// iter at the mouse point, and checking for tags that
			// start with "link:"...

			int buffer_x, buffer_y;
			Window.Editor.WindowToBufferCoords (Gtk.TextWindowType.Widget,
							    pointer_x, 
							    pointer_y,
							    out buffer_x, 
							    out buffer_y);
			
			Gtk.TextIter iter = Window.Editor.GetIterAtLocation (buffer_x, buffer_y);

			foreach (Gtk.TextTag tag in iter.Tags) {
				if (tag.Name != null && 
				    tag.Name.StartsWith ("link:")) {
					hovering = true;
					break;
				}
			}

			// Don't show hand if Shift or Control is pressed 
			bool avoid_hand = (pointer_mask & (Gdk.ModifierType.ShiftMask |
							   Gdk.ModifierType.ControlMask)) != 0;

			if (hovering != hovering_on_link) {
				hovering_on_link = hovering;

				Gdk.Window win = Window.Editor.GetWindow (Gtk.TextWindowType.Text);
				if (hovering && !avoid_hand)
					win.Cursor = hand_cursor;
				else 
					win.Cursor = normal_cursor;
			}
		}
	}
}
