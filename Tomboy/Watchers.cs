
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
			Buffer.ApplyTag ("note-title", TitleStart, TitleEnd);
		}

		// This only gets called on an explicit move, not by
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
			Buffer.RemoveTag ("note-title", TitleEnd, end);
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
			Buffer.ApplyTag ("note-title", TitleStart, TitleEnd);

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

	public class NoteRelatedToWatcher : NotePlugin
	{
		ArrayList linked_from;
		ArrayList linked_to;

		protected override void Initialize ()
		{
			linked_from = new ArrayList ();
			linked_to = new ArrayList ();

			LinkCreated += OnLinkCreated;
		}

		protected override void OnNoteOpened ()
		{
			Buffer.TagApplied += OnTagApplied;
		}

		void OnTagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (args.Tag.Name != "link:internal") 
				return;

			string link_text = args.StartChar.GetText (args.EndChar);

			// Ignore self-references
			if (link_text.ToLower () == Note.Title.ToLower ())
				return;

			linked_to.Add (link_text);

			Console.WriteLine ("Link Tag applied to '{0}', for '{1}'",
					   Note.Title,
					   link_text);

			LinkCreated (Note, link_text);
		}

		void OnTagRemoved (object sender, Gtk.TagAppliedArgs args)
		{
			if (args.Tag.Name != "link:internal")
				return;

			Console.WriteLine ("Tag 'link:internal' removed!!!");
		}

		void OnLinkCreated (Note note, string link_text)
		{
			if (link_text.ToLower () != Note.Title.ToLower ())
				return;

			Console.WriteLine ("Received Link text from '{0}' = '{1}'",
					   note.Title,
					   link_text);

			// Don't show links we already link to in the body
			if (linked_to.Contains (link_text))
				return;

			if (linked_from.Contains (note.Title))
				return;

			linked_from.Add (note.Title);

			Gtk.TextTag tag = Buffer.TagTable.Lookup ("related-to");
			Gtk.TextIter iter = Buffer.StartIter;

			if (iter.ForwardToTagToggle (tag)) 
				RemoveRelatedLine ();

			InsertRelatedLine ();
		}

		void InsertRelatedLine ()
		{
			Gtk.TextIter line, line_end;

			line = Buffer.StartIter;
			line.ForwardLines (1);

			Buffer.Insert (line, Catalog.GetString ("Related to: "));

			line = Buffer.StartIter;
			line.ForwardLines (1);
			line_end = line;
			line_end.ForwardToLineEnd ();

			Buffer.ApplyTag ("italic", line, line_end);

			line = Buffer.StartIter;
			line.ForwardLines (1);
			line_end = line;
			line_end.ForwardToLineEnd ();

			string link_text = string.Empty;
			foreach (string link in linked_from) {
				if (link_text == string.Empty)
					link_text = link;
				else {
					link_text += ", ";
					link_text += link;
				}
			}
			link_text += "\n";

			Buffer.Insert (line_end, link_text);

			line = Buffer.StartIter;
			line.ForwardLines (1);
			line_end = line;
			line_end.ForwardToLineEnd ();

			Buffer.ApplyTag ("related-to", line, line_end);
		}

		void RemoveRelatedLine ()
		{
			Gtk.TextTag tag = Buffer.TagTable.Lookup ("related-to");

			Gtk.TextIter line = Buffer.StartIter;
			line.ForwardToTagToggle (tag);

			Gtk.TextIter line_end = line;
			line_end.ForwardToTagToggle (tag);
			
			Buffer.Delete (line, line_end);
		}

		delegate void LinkCreatedHandler (Note note, string linked_text);

		static event LinkCreatedHandler LinkCreated;
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

		protected override void OnNoteOpened ()
		{
			Buffer.TagApplied += TagApplied;
			Preferences.SettingChanged += OnEnableSpellcheckChanged;

			if ((bool) Preferences.Get (Preferences.ENABLE_SPELLCHECKING)) {
				obj_ptr = gtkspell_new_attach (Window.Editor.Handle, 
							       null, 
							       IntPtr.Zero);
				FixupOldGtkSpell ();
			}
		}

		[System.Diagnostics.Conditional ("OLD_GTKSPELL")] 
		void FixupOldGtkSpell ()
		{
			// NOTE: Older versions of GtkSpell before 2.0.6 use red
			// foreground color and a single underline.  This
			// conflicts with internal note links.  So fix it up to
			// use the "normal" foreground and the "error"
			// underline.
			Gtk.TextTag misspell = Buffer.TagTable.Lookup ("gtkspell-misspelled");

			if (misspell != null) {
				Gtk.TextTag normal = Buffer.TagTable.Lookup ("normal");
				misspell.ForegroundGdk = normal.ForegroundGdk;
				// Force the value to 4 since error underlining
				// isn't mapped in Gtk# yet.
				misspell.Underline = (Pango.Underline) 4;
			}
		}

		void OnEnableSpellcheckChanged (object sender, GConf.NotifyEventArgs args)
		{
			if (args.Key != Preferences.ENABLE_SPELLCHECKING)
				return;

			if ((bool) args.Value) {
				if (obj_ptr == IntPtr.Zero) {
					obj_ptr = gtkspell_new_attach (Window.Editor.Handle, 
								       null, 
								       IntPtr.Zero);
					FixupOldGtkSpell ();
				}
			} else {
				if (obj_ptr != IntPtr.Zero) {
					gtkspell_detach (obj_ptr);
					obj_ptr = IntPtr.Zero;
				}
			}
		}

		void TagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (args.Tag.Name == "gtkspell-misspelled") {
				// Remove misspelled tag for links 
				foreach (Gtk.TextTag tag in args.StartChar.Tags) {
					if (tag.Name != null && tag.Name.StartsWith ("link:")) {
						Buffer.RemoveTag ("gtkspell-misspelled", 
								  args.StartChar, 
								  args.EndChar);
						break;
					}
				}
			} else if (args.Tag.Name != null && 
				   args.Tag.Name.StartsWith ("link:")) {
				Gtk.TextTag misspell = Buffer.TagTable.Lookup ("gtkspell-misspelled");
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

		const string URL_REGEX = 
			@"((\b((news|http|https|ftp|file|irc)://|mailto:|(www|ftp)\.|\S*@\S*\.)|(^|\s)~?/\S+/)\S*\b/?)";

		static Regex regex;

		static NoteUrlWatcher ()
		{
			regex = new Regex (URL_REGEX, 
					   RegexOptions.IgnoreCase | RegexOptions.Compiled);
		}

		protected override void Initialize ()
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

			url_tag.TextEvent += OnTextEvent;

			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;			
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

			Gtk.TextIter start = args.Iter, end = args.Iter;

			start.BackwardToTagToggle (tag);
			end.ForwardToTagToggle (tag);

			string url = start.GetText (end);

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

			Console.WriteLine ("Opening url '{0}' on click...", url);

			try {
				Gnome.Url.Show (url);

				/* Close note on middle-click */
				if (button_ev.Button == 2) {
					Window.Hide ();

					// Kill the middle button paste...
					args.RetVal = true;
				}
			} catch (GLib.GException e) {
				ShowOpeningLocationError (url, e.Message);

				// Kill the middle button paste...
				args.RetVal = true;
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
	}

	public class NoteLinkWatcher : NotePlugin
	{
		class TrieController
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
				Console.WriteLine ("UPDATING TITLE CACHE!!");

				ArrayList titles = new ArrayList (manager.Notes.Count);
				foreach (Note note in manager.Notes) {
					titles.Add (note.Title);
				}

				title_trie = new TrieTree (titles, false /* !case_sensitive */);
			}

			public TrieTree TitleTrie 
			{
				get { return title_trie; }
			}
		}

		static TrieController trie_controller;

		protected override void Initialize () 
		{
			Manager.NoteDeleted += OnNoteDeleted;
			Manager.NoteAdded += OnNoteAdded;
			Manager.NoteRenamed += OnNoteRenamed;
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

			Gtk.TextTag url_tag = Buffer.TagTable.Lookup ("link:url");
			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			Gtk.TextTag broken_link_tag = Buffer.TagTable.Lookup ("link:broken");

			string new_title_lower = added.Title.ToLower ();

			string buffer_text = Buffer.StartIter.GetText (Buffer.EndIter);
			buffer_text = buffer_text.ToLower ();

			int idx = 0;

			while (true) {
				idx = buffer_text.IndexOf (new_title_lower, idx);
				if (idx < 0)
					break;

				Gtk.TextIter start = Buffer.GetIterAtOffset (idx);
				Gtk.TextIter end = Buffer.GetIterAtOffset (idx + added.Title.Length);

				if (!start.HasTag (url_tag)) {
					Buffer.RemoveTag (broken_link_tag, start, end);
					Buffer.ApplyTag (link_tag, start, end);
				}

				idx += new_title_lower.Length;
			}
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
			if (!ContainsText (old_title))
				return;

			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");

			string old_title_lower = old_title.ToLower ();

			TextTagEnumerator enumerator = new TextTagEnumerator (Buffer, link_tag);
			foreach (TextRange range in enumerator) {
				Console.WriteLine ("RENAME: Checking '{0}' in note '{1}'", range.Text, Note.Title);

				if (range.Text.ToLower () != old_title_lower)
					continue;

				Console.WriteLine ("Replacing with '{0}'", renamed.Title);

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

			public Highlighter (Note         note, 
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

			public void Highlight (TrieTree trie)
			{
				trie.FindMatches (start.GetText (end), 
						  new MatchHandler (TitleFound));
			}

			void TitleFound (string haystack, int start_idx, int end_idx)
			{
				string title = haystack.Substring (start_idx, end_idx - start_idx);
				if (title == note.Title)
					return;

				Note link = note.Manager.Find (title);
				if (link == null)
					return;
				
				Gtk.TextIter title_start = start;
				title_start.ForwardChars (start_idx);

				// Don't create links inside URLs
				if (title_start.HasTag (url_tag))
					return;

				Gtk.TextIter title_end = start;
				title_end.ForwardChars (end_idx);

				Console.WriteLine ("Matching Note title '{0}'...", title);

				note.Buffer.RemoveTag (broken_link_tag,
						       title_start,
						       title_end);
				note.Buffer.ApplyTag (link_tag,
						      title_start,
						      title_end);
			}
		}

		TrieTree TitleTrie
		{
			get {
				if (trie_controller == null)
					trie_controller = new TrieController (Manager);

				return trie_controller.TitleTrie;
			}
		}		

		void HighlightInBlock (Gtk.TextIter start, Gtk.TextIter end) 
		{
			Highlighter high = new Highlighter (Note, start, end);
			high.Highlight (TitleTrie);
		}

		void UnhighlightInBlock (Gtk.TextIter start, Gtk.TextIter end) 
		{
			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			Buffer.RemoveTag (link_tag, start, end);
		}

		void GetBlockExtents (ref Gtk.TextIter start, ref Gtk.TextIter end) 
		{
			start.LineOffset = 0;
			end.ForwardToLineEnd ();
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Gtk.TextIter start = args.Start;
			Gtk.TextIter end = args.End;

			GetBlockExtents (ref start, ref end);

			UnhighlightInBlock (start, end);
			HighlightInBlock (start, end);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			Gtk.TextIter end = args.Pos;

			GetBlockExtents (ref start, ref end);

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

		protected override void OnNoteOpened ()
		{
			broken_link_tag = Buffer.TagTable.Lookup ("link:broken");
			if (broken_link_tag == null) {
				Console.WriteLine ("ERROR: Broken link tags not registered for buffer.");
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

		static MouseHandWatcher ()
		{
			normal_cursor = new Gdk.Cursor (Gdk.CursorType.Xterm);
			hand_cursor = new Gdk.Cursor (Gdk.CursorType.Hand2);
		}

		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void OnNoteOpened ()
		{
			link_tag = Buffer.TagTable.Lookup ("link:internal");
			link_tag.TextEvent += OnLinkTextEvent;

			broken_link_tag = Buffer.TagTable.Lookup ("link:broken");
			broken_link_tag.TextEvent += OnLinkTextEvent;

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

	public class IndentWatcher : NotePlugin
	{
		protected override void Initialize ()
		{
			// Do nothing.
		}

		protected override void OnNoteOpened ()
		{
			Window.Editor.KeyPressEvent += OnEditorKeyPress;
			Buffer.InsertText += OnInsertText;
		}

		[GLib.ConnectBefore]
		void OnEditorKeyPress (object sender, Gtk.KeyPressEventArgs args) 
		{
			switch (args.Event.Key) {
				/*
			case Gdk.Key.asterisk:
				Console.WriteLine ("Got Asterisk!");

				{
				Gtk.TextIter insert = Buffer.GetIterAtMark (Buffer.InsertMark);
				if (!insert.StartsLine ())
					break;

				Gtk.TextIter para_end = insert;
				para_end.ForwardToLineEnd ();

				Console.WriteLine ("Setting hanging indent!");

				Gtk.TextTag indent_tag = new Gtk.TextTag (null);
				indent_tag.Indent = -10;
				indent_tag.Data ["has_indent"] = true;

				Buffer.TagTable.Add (indent_tag);
				Buffer.ApplyTag (indent_tag, insert, para_end);
				}
				break;
				*/
				
			case Gdk.Key.Tab:
			case Gdk.Key.KP_Tab:
				Console.WriteLine ("Got Tab!");

				Gtk.TextIter insert = Buffer.GetIterAtMark (Buffer.InsertMark);
				if (!insert.StartsLine ())
					break;

				Console.WriteLine ("Going to indent!");

				Gtk.TextIter para_end = insert;
				para_end.ForwardToLineEnd ();

				bool indent_exists = false;

				foreach (Gtk.TextTag tag in insert.Tags) {
					if (tag.Data ["has_margin"] != null) {
						Console.WriteLine ("Has Indent Already! Updating!");
						tag.LeftMargin = tag.LeftMargin + 40;
						indent_exists = true;
						break;
					}
				}

				if (!indent_exists) {
					Gtk.TextTag indent_tag = new Gtk.TextTag (null);
					indent_tag.LeftMargin = 40;
					indent_tag.Data ["has_margin"] = true;

					Buffer.TagTable.Add (indent_tag);
					Buffer.ApplyTag (indent_tag, insert, para_end);
				}

				args.RetVal = true;
				break;

			case Gdk.Key.BackSpace:
				Console.WriteLine ("Got Backspace!");

				Gtk.TextIter insert2 = Buffer.GetIterAtMark (Buffer.InsertMark);
				if (!insert2.StartsLine ())
					break;

				Console.WriteLine ("Going to unindent!");

				Gtk.TextIter para_end2 = insert2;
				para_end2.ForwardToLineEnd ();

				foreach (Gtk.TextTag tag in insert2.Tags) {
					if (tag.Data ["has_margin"] != null) {
						Console.WriteLine ("Has Indent Already! Updating!");
						tag.LeftMargin = tag.LeftMargin - 40;
						if (tag.LeftMargin == 0) {
							Console.WriteLine ("Killing empty indent!");
							Buffer.RemoveTag (tag, insert2, para_end2);
						}

						args.RetVal = true;
						break;
					}
				}
				break;
			}
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Console.WriteLine ("Trying hanging indent! line-offset:{0}", args.Pos.LineOffset);

			if (args.Text != "*" || args.Pos.LineOffset != 1)
				return;

			Gtk.TextIter para_start = args.Pos;
			para_start.BackwardChar ();

			Gtk.TextTag indent_tag = new Gtk.TextTag (null);
			indent_tag.Indent = -10;
			indent_tag.Data ["has_indent"] = true;

			Buffer.TagTable.Add (indent_tag);
			Buffer.ApplyTag (indent_tag, para_start, args.Pos);
		}

		// 
		// <list>
		// <anonymous list-prefix="124">Content content content</anonymous>
		// <anonymous indent="40">
		//   <anonymous list-prefix="123">
		//     Content content content
		//   </anonymous>
		// </anonymous>
		// </list>

		string GetListPrefix (Gtk.TextIter iter)
		{
			Gtk.TextTag number_list = Buffer.TagTable.Lookup ("number-list");
			Gtk.TextTag bullet_list = Buffer.TagTable.Lookup ("bullet-list");

			if (iter.HasTag (bullet_list))
				return "* ";

			int add = 0;

			while (iter.HasTag (number_list)) {
				foreach (Gtk.TextTag tag in iter.Tags) {
					if (tag.Data ["list-prefix"] != null) {
						int last_num = (int) tag.Data ["list-prefix"];
						last_num += add;
						return last_num.ToString () + ". ";
					}
				}

				iter.BackwardLine ();
				add++;
			}

			return null;
		}
	}

#if BROKEN
	public class SpacingWatcher 
	{
		Note note;
		NoteBuffer buffer;
		Gtk.TextView editor;

		public SpacingWatcher (Note note)
		{
			this.note = note;
			this.buffer = note.Buffer;
			this.editor = note.Window.Editor;

			buffer.InsertText += OnInsertText;
			//buffer.DeleteRange += OnDeleteRange;
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
		}

		class AddHRule
		{
			Gtk.TextBuffer buffer;
			Gtk.TextMark mark;
			Gtk.TextView view;

			public AddHRule (Gtk.TextIter start,
					 Gtk.TextIter end,
					 Gtk.TextView view)
			{
				buffer = view.Buffer;
				this.view = view;
				mark = buffer.CreateMark (null, start, true);

				buffer.ApplyTag ("centered", start, end);

				// add this in idle so we don't invalidate text iters
				GLib.Idle.Add (new GLib.IdleHandler (AddHRuleIdle));
			}

			bool AddHRuleIdle ()
			{
				Console.WriteLine ("Got Separator Line");

				Gtk.TextIter start = buffer.GetIterAtMark (mark);

				Gtk.TextChildAnchor anchor;
				anchor = buffer.CreateChildAnchor (start);

				Gtk.Widget rule = new Gtk.HSeparator ();
				rule.WidthRequest = 200;
				rule.Show ();

				view.AddChildAtAnchor (rule, anchor);

				return false;
			}
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter line_start = args.Pos;
			line_start.LineOffset = 0;

			Gtk.TextIter line_end = args.Pos;
			line_end.ForwardToLineEnd ();

			if (line_start.GetText (line_end) == "...") {
				new AddHRule (line_start, line_end, editor);
			}
		}
	}

	public class NoteListWatcher
	{
		Note note;
		NoteBuffer buffer;
		Gtk.TextView editor;

		// Apply list:bullet, with the number of whitespace characters
		// at the start of line as the indent level.  On newline, check
		// the offset of the last line, and copy it (will need to insert
		// the offset characters in an idle, i guess, or maybe create
		// and apply a tag on the fly with LeftMargin attribute).  When
		// unserializing, insert an image/marker at the beginning of
		// text (need to worry about varing width numbers).
		//
		// Can we alter the selection to not select these regions?  This
		// might be fixed by using an applied tag.

		public NoteListWatcher (Note note)
		{
			this.note = note;
			this.buffer = note.Buffer;
			this.editor = note.Window.Editor;

			buffer.InsertText += OnInsertText;
			buffer.DeleteRange += OnDeleteRange;
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			if (args.Text != "\t" && args.Text != "*")
				return;

			Gtk.TextIter line_start = args.Pos;
			line_start.LineOffset = 0;

			if (line_start.Char == string.Empty)
				return;

			int indent = 0;
			while (line_start.Char == "\t") {
				indent++;
				line_start.ForwardChar ();
				Console.WriteLine ("Got indent {0}!", indent);
			}

			if (indent == 0)
				return;

			if (!line_start.Equal (args.Pos))
				return;

			foreach (Gtk.TextTag tag in line_start.Tags) {
				if (tag.Data ["mybutt"] != null) {
					tag.LeftMargin += 20;
					break;
				}
			}

			Gtk.TextIter line_end = args.Pos;
			line_end.ForwardToLineEnd ();

			/*
			if (line_start.Char [0] == '*')
				new AddBullet (line_start, line_end, editor);
			else if (line_start.Char [0] == '-')
				new AddDash (line_start, line_end, editor);
			else if (Char.IsDigit (line_start.Char [0]))
				new AddNumber (line_start, line_end, editor);
			*/
		}

		[GLib.ConnectBefore]
		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			if (args.Start.Char == "*")
				Console.WriteLine ("Unindenting!");
		}
	}
#endif // BROKEN

}
