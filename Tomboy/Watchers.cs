
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mono.Unix;

namespace Tomboy
{
	public class NoteRenameWatcher : NoteAddin
	{
		bool editing_title;
		Gtk.TextTag title_tag;
		HIGMessageDialog title_taken_dialog = null;

		public override void Initialize ()
		{
			title_tag = Note.TagTable.Lookup ("note-title");
		}

		public override void Shutdown ()
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
			get {
				return Buffer.StartIter;
			}
		}

		public override void OnNoteOpened ()
		{
			Buffer.MarkSet += OnMarkSet;
			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;

			Window.Editor.FocusOutEvent += OnEditorFocusOut;

			// FIXME: Needed because we hide on delete event, and
			// just hide on accelerator key, so we can't use delete
			// event.  This means the window will flash if closed
			// with a name clash.
			Window.UnmapEvent += OnWindowClosed;

			// Clean up title line
			Buffer.RemoveAllTags (TitleStart, TitleEnd);
			Buffer.ApplyTag (title_tag, TitleStart, TitleEnd);
		}

		void OnEditorFocusOut (object sender, Gtk.FocusOutEventArgs args)
		{
			// TODO: Duplicated from Update(); refactor instead
			if (editing_title) {
				Changed ();
				UpdateNoteTitle ();
				editing_title = false;
			}
		}

		// This only gets called on an explicit move, not when typing
		void OnMarkSet (object sender, Gtk.MarkSetArgs args)
		{
			if (args.Mark == Buffer.InsertMark) {
				Update ();
			}
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Update ();

			Gtk.TextIter end = args.Pos;
			end.ForwardToLineEnd ();

			// Avoid lingering note-title after a multi-line insert...
			Buffer.RemoveTag (title_tag, TitleEnd, end);
			
			//In the case of large copy and paste operations, show the end of the block
			this.Window.Editor.ScrollMarkOnscreen (this.Buffer.InsertMark);
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Update ();
		}

		void Update ()
		{
			Gtk.TextIter insert = Buffer.GetIterAtMark (Buffer.InsertMark);
			Gtk.TextIter selection = Buffer.GetIterAtMark (Buffer.SelectionBound);

			// FIXME: Handle middle-click paste when insert or
			// selection isn't on line 0, which means we won't know
			// about the edit.

			if (insert.Line == 0 || selection.Line == 0) {
				if (!editing_title)
					editing_title = true;
				Changed ();
			} else {
				if (editing_title) {
					Changed ();
					UpdateNoteTitle ();
					editing_title = false;
				}
			}
		}

		void Changed ()
		{
			// Make sure the title line is big and red...
			Buffer.RemoveAllTags (TitleStart, TitleEnd);
			Buffer.ApplyTag (title_tag, TitleStart, TitleEnd);

			// NOTE: Use "(Untitled #)" for empty first lines...
			string title = TitleStart.GetSlice (TitleEnd).Trim ();
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

			Logger.Debug ("Renaming note from {0} to {1}", Note.Title, title);
			Note.SetTitle (title, true);

			return true;
		}

		void ShowNameClashError (string title)
		{
			// Select text from TitleStart to TitleEnd
			Buffer.MoveMark (Buffer.SelectionBound, TitleStart);
			Buffer.MoveMark (Buffer.InsertMark, TitleEnd);

			string message =
			        String.Format (Catalog.GetString ("A note with the title " +
			                                          "<b>{0}</b> already exists. " +
			                                          "Please choose another name " +
			                                          "for this note before " +
			                                          "continuing."),
			                       title);

			/// Only pop open a warning dialog when one isn't already present
			/// Had to add this check because this method is being called twice.
			if (title_taken_dialog == null) {
				title_taken_dialog =
				        new HIGMessageDialog (Window,
				                              Gtk.DialogFlags.DestroyWithParent,
				                              Gtk.MessageType.Warning,
				                              Gtk.ButtonsType.Ok,
				                              Catalog.GetString ("Note title taken"),
				                              message);
				title_taken_dialog.Modal = true;
				title_taken_dialog.Response +=
				delegate (object sender, Gtk.ResponseArgs args) {
					title_taken_dialog.Destroy ();
					title_taken_dialog = null;
				};
			}

			title_taken_dialog.Present ();
		}
	}

	#if FIXED_GTKSPELL
	public class NoteSpellChecker : NoteAddin
	{
		IntPtr obj_ptr = IntPtr.Zero;

		static bool gtkspell_available_tested;
		static bool gtkspell_available_result;

		[DllImport ("libgtkspell")]
		static extern IntPtr gtkspell_new_attach (IntPtr text_view,
			                string locale,
			                IntPtr error);

		[DllImport ("libgtkspell")]
		static extern void gtkspell_detach (IntPtr obj);

		static bool DetectGtkSpellAvailable()
		{
			try {
				Gtk.TextView test_view = new Gtk.TextView ();
				IntPtr test_ptr = gtkspell_new_attach (test_view.Handle,
				                                       null,
				                                       IntPtr.Zero);
				if (test_ptr != IntPtr.Zero)
					gtkspell_detach (test_ptr);
				return true;
			} catch {
			return false;
		}
	}

	public static bool GtkSpellAvailable
	{
		get {
			if (!gtkspell_available_tested) {
					gtkspell_available_result = DetectGtkSpellAvailable ();
					gtkspell_available_tested = true;
				}
				return gtkspell_available_result;
			}
		}

		public NoteSpellChecker ()
		{
			if (!GtkSpellAvailable) {
				throw new Exception();
			}
		}

		public override void Initialize ()
		{
			// Do nothing.
		}

		public override void Shutdown ()
		{
			// Do nothing.
		}

		public override void OnNoteOpened ()
		{
			Preferences.SettingChanged += OnEnableSpellcheckChanged;

			if ((bool) Preferences.Get (Preferences.ENABLE_SPELLCHECKING)) {
				Attach ();
			}
		}

		void Attach ()
		{
			// Make sure we add this tag before attaching, so
			// gtkspell will use our version.
			if (Note.TagTable.Lookup ("gtkspell-misspelled") == null) {
				NoteTag tag = new NoteTag ("gtkspell-misspelled");
				tag.CanSerialize = false;
				tag.CanSpellCheck = true;
				tag.Underline = Pango.Underline.Error;
				Note.TagTable.Add (tag);
			}

			Buffer.TagApplied += TagApplied;

			if (obj_ptr == IntPtr.Zero) {
				obj_ptr = gtkspell_new_attach (Window.Editor.Handle,
				                               null,
				                               IntPtr.Zero);
			}
		}

		void Detach ()
		{
			Buffer.TagApplied -= TagApplied;

			if (obj_ptr != IntPtr.Zero) {
				gtkspell_detach (obj_ptr);
				obj_ptr = IntPtr.Zero;
			}
		}

		void OnEnableSpellcheckChanged (object sender, NotifyEventArgs args)
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
			bool remove = false;

			if (args.Tag.Name == "gtkspell-misspelled") {
				// Remove misspelled tag for links & title
				foreach (Gtk.TextTag tag in args.StartChar.Tags) {
					if (tag != args.Tag &&
					                !NoteTagTable.TagIsSpellCheckable (tag)) {
						remove = true;
						break;
					}
				}
			} else if (!NoteTagTable.TagIsSpellCheckable (args.Tag)) {
				remove = true;
			}

			if (remove) {
				Buffer.RemoveTag ("gtkspell-misspelled",
				                  args.StartChar,
				                  args.EndChar);
			}
		}
	}
	#else
	// Add in a "dummy" NoteSpellChecker class so that Mono.Addins doesn't
	// complain at startup.  NoteSpellChecker is specified in Tomboy.addin.xml.
	public class NoteSpellChecker : NoteAddin
	{
		public override void Initialize ()
		{
		}
		
		public override void Shutdown ()
		{
		}
		
		public override void OnNoteOpened ()
		{
		}
	}
	#endif

	public class NoteUrlWatcher : NoteAddin
	{
		Gtk.TextMark click_mark;

		const string URL_REGEX =
			@"((\b((news|http|https|ftp|file|irc)://|mailto:|(www|ftp)\.|\S*@\S*\.)|(?<=^|\s)/\S+/|(?<=^|\s)~/\S+)\S*\b/?)";
		

		static Regex regex;
		static bool text_event_connected;

		static NoteUrlWatcher ()
		{
			regex = new Regex (URL_REGEX,
			                   RegexOptions.IgnoreCase | RegexOptions.Compiled);
			text_event_connected = false;
		}

		public override void Initialize ()
		{
			// Do nothing
		}

		public override void Shutdown ()
		{
			// Do nothing.
		}

		public override void OnNoteOpened ()
		{
			// NOTE: This hack helps avoid multiple URL opens
			// now that Notes always perform
			// TagTable sharing.  This is because if the TagTable is
			// shared, we will connect to the same Tag's event
			// source each time a note is opened, and get called
			// multiple times for each button press.  Fixes bug
			// #305813.
			if (!text_event_connected) {
				Note.TagTable.UrlTag.Activated += OnUrlTagActivated;
				text_event_connected = true;
			}

			click_mark = Buffer.CreateMark (null, Buffer.StartIter, true);

			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;

			Window.Editor.ButtonPressEvent += OnButtonPress;
			Window.Editor.PopulatePopup += OnPopulatePopup;
			Window.Editor.PopupMenu += OnPopupMenu;
		}

		string GetUrl (Gtk.TextIter start, Gtk.TextIter end)
		{
			string url = start.GetSlice (end);

			// FIXME: Needed because the file match is greedy and
			// eats a leading space.
			url = url.Trim ();

			// Simple url massaging.  Add to 'http://' to the front
			// of www.foo.com, 'mailto:' to alex@foo.com, 'file://'
			// to /home/alex/foo.
			if (url.StartsWith ("www."))
				url = "http://" + url;
			else if (url.StartsWith ("/") &&
			                url.LastIndexOf ("/") > 1)
				url = "file://" + url;
			else if (url.StartsWith ("~/"))
				url = "file://" +
				      Path.Combine (Environment.GetEnvironmentVariable ("HOME"),
				                    url.Substring (2));
			else if (Regex.IsMatch (url, 
				@"^(?!(news|mailto|http|https|ftp|file|irc):).+@.{2,}$",
				RegexOptions.IgnoreCase))
				url = "mailto:" + url;

			return url;
		}

		void OpenUrl (string url)
		{
			if (url != string.Empty) {
				Logger.Debug ("Opening url '{0}'...", url);
				Services.NativeApplication.OpenUrl (url, Note.Window.Screen);
			}
		}

		bool OnUrlTagActivated (NoteTag      sender,
		                        NoteEditor   editor,
		                        Gtk.TextIter start,
		                        Gtk.TextIter end)
		{
			string url = GetUrl (start, end);
			try {
				OpenUrl (url);
			} catch (GLib.GException e) {
				GuiUtils.ShowOpeningLocationError (Window, url, e.Message);
			}

			// Kill the middle button paste...
			return true;
		}

		void ApplyUrlToBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			NoteBuffer.GetBlockExtents (ref start,
			                            ref end,
			                            256 /* max url length */,
			                            Note.TagTable.UrlTag);

			Buffer.RemoveTag (Note.TagTable.UrlTag, start, end);

			for (Match match = regex.Match (start.GetSlice (end));
			                match.Success;
			                match = match.NextMatch ()) {
				System.Text.RegularExpressions.Group group = match.Groups [1];

				/*
				Logger.Log ("Highlighting url: '{0}' at offset {1}",
				     group,
				     group.Index);
				*/

				Gtk.TextIter start_cpy = start;
				start_cpy.ForwardChars (group.Index);

				end = start_cpy;
				end.ForwardChars (group.Length);

				Buffer.ApplyTag (Note.TagTable.UrlTag, start_cpy, end);
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
			NoteTag url_tag = Note.TagTable.UrlTag;
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

			Gtk.TextIter start, end;
			NoteTag url_tag = Note.TagTable.UrlTag;
			url_tag.GetExtents (click_iter, out start, out end);

			OnUrlTagActivated (url_tag, (NoteEditor) Window.Editor, start, end);
		}

		void CopyLinkActivate (object sender, EventArgs args)
		{
			Gtk.TextIter click_iter = Buffer.GetIterAtMark (click_mark);

			Gtk.TextIter start, end;
			Note.TagTable.UrlTag.GetExtents (click_iter, out start, out end);

			string url = GetUrl (start, end);

			Gtk.Clipboard clip = Window.Editor.GetClipboard (Gdk.Selection.Clipboard);
			clip.Text = url;
		}
	}

	public class NoteLinkWatcher : NoteAddin
	{
		static bool text_event_connected;

		public override void Initialize ()
		{
			Manager.NoteDeleted += OnNoteDeleted;
			Manager.NoteAdded += OnNoteAdded;
			Manager.NoteRenamed += OnNoteRenamed;
		}

		public override void Shutdown ()
		{
			Manager.NoteDeleted -= OnNoteDeleted;
			Manager.NoteAdded -= OnNoteAdded;
			Manager.NoteRenamed -= OnNoteRenamed;
		}

		public override void OnNoteOpened ()
		{
			// NOTE: This avoids multiple link opens
			// now that notes always perform TagTable
			// sharing.  This is because if the TagTable is shared,
			// we will connect to the same Tag's event source each
			// time a note is opened, and get called multiple times
			// for each button press.  Fixes bug #305813.
			if (!text_event_connected) {
				Note.TagTable.LinkTag.Activated += OnLinkTagActivated;
				Note.TagTable.BrokenLinkTag.Activated += OnLinkTagActivated;
				text_event_connected = true;
			}

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
			HighlightInBlock (Buffer.StartIter, Buffer.EndIter);
		}

		void OnNoteDeleted (object sender, Note deleted)
		{
			if (deleted == this.Note)
				return;

			if (!ContainsText (deleted.Title))
				return;

			string old_title_lower = deleted.Title.ToLower ();

			// Turn all link:internal to link:broken for the deleted note.
			NoteTag link_tag = Note.TagTable.LinkTag;
			NoteTag broken_link_tag = Note.TagTable.BrokenLinkTag;
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
			if (ContainsText (renamed.Title))
				HighlightNoteInBlock (renamed, Buffer.StartIter, Buffer.EndIter);
		}

		void DoHighlight (TrieHit hit, Gtk.TextIter start, Gtk.TextIter end)
		{
			// Some of these checks should be replaced with fixes to
			// TitleTrie.FindMatches, probably.
			if (hit.Value == null) {
				Logger.Debug ("DoHighlight: null pointer error for '{0}'." , hit.Key);
				return;
			}
			
			if (Manager.Find(hit.Key) == null) {
				Logger.Debug ("DoHighlight: '{0}' links to non-existing note." , hit.Key);
				return;
			}
			
			Note hit_note = (Note) hit.Value;

			if (String.Compare (hit.Key.ToString(), hit_note.Title.ToString(), true ) != 0) { // == 0 if same string  
				Logger.Debug ("DoHighlight: '{0}' links wrongly to note '{1}'." , hit.Key, hit_note.Title);
				return;
			}
			
			if (hit_note == this.Note)
				return;

			Gtk.TextIter title_start = start;
			title_start.ForwardChars (hit.Start);

			Gtk.TextIter title_end = start;
			title_end.ForwardChars (hit.End);

			// Only link against whole words/phrases
			if ((!title_start.StartsWord () && !title_start.StartsSentence ()) ||
			                (!title_end.EndsWord() && !title_end.EndsSentence()))
				return;

			// Don't create links inside URLs
			if (title_start.HasTag (Note.TagTable.UrlTag))
				return;

			Logger.Debug ("Matching Note title '{0}' at {1}-{2}...",
			            hit.Key,
			            hit.Start,
			            hit.End);

			Buffer.RemoveTag (Note.TagTable.BrokenLinkTag, title_start, title_end);
			Buffer.ApplyTag (Note.TagTable.LinkTag, title_start, title_end);
		}

		void HighlightNoteInBlock (Note find_note, Gtk.TextIter start, Gtk.TextIter end)
		{
			string buffer_text = start.GetText (end).ToLower();
			string find_title_lower = find_note.Title.ToLower ();
			int idx = 0;

			while (true) {
				idx = buffer_text.IndexOf (find_title_lower, idx);
				if (idx < 0)
					break;

				TrieHit hit = new TrieHit (idx,
				                           idx + find_title_lower.Length,
				                           find_title_lower,
				                           find_note);
				DoHighlight (hit, start, end);

				idx += find_title_lower.Length;
			}
		}

		void HighlightInBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			IList<TrieHit> hits = Manager.TitleTrie.FindMatches (start.GetSlice (end));
			foreach (TrieHit hit in hits) {
				DoHighlight (hit, start, end);
			}
		}

		void UnhighlightInBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			Buffer.RemoveTag (Note.TagTable.LinkTag, start, end);
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Gtk.TextIter start = args.Start;
			Gtk.TextIter end = args.End;

			NoteBuffer.GetBlockExtents (ref start,
			                            ref end,
			                            Manager.TitleTrie.MaxLength,
			                            Note.TagTable.LinkTag);

			UnhighlightInBlock (start, end);
			HighlightInBlock (start, end);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			Gtk.TextIter end = args.Pos;

			NoteBuffer.GetBlockExtents (ref start,
			                            ref end,
			                            Manager.TitleTrie.MaxLength,
			                            Note.TagTable.LinkTag);

			UnhighlightInBlock (start, end);
			HighlightInBlock (start, end);
		}

		bool OpenOrCreateLink (Gtk.TextIter start, Gtk.TextIter end)
		{
			string link_name = start.GetText (end);
			Note link = Manager.Find (link_name);

			if (link == null) {
				Logger.Debug ("Creating note '{0}'...", link_name);
				try {
					link = Manager.Create (link_name);
				} catch {
				// Fail silently.
			}
		}

		// FIXME: We used to also check here for (link != this.Note), but
		// somehow this was causing problems receiving clicks for the
		// wrong instance of a note (see bug #413234).  Since a
		// link:internal tag is never applied around text that's the same
		// as the current note's title, it's safe to omit this check and
		// also works around the bug.
		if (link != null) {
				Logger.Debug ("Opening note '{0}' on click...", link_name);
				link.Window.Present ();
				return true;
			}

			return false;
		}

		bool OnLinkTagActivated (NoteTag      sender,
		                         NoteEditor   editor,
		                         Gtk.TextIter start,
		                         Gtk.TextIter end)
		{
			return OpenOrCreateLink (start, end);
		}
	}

	public class NoteWikiWatcher : NoteAddin
	{
		Gtk.TextTag broken_link_tag;

		// NOTE: \p{Lu} is Unicode uppercase and \p{Ll} is lowercase.
		const string WIKIWORD_REGEX = @"\b((\p{Lu}+[\p{Ll}0-9]+){2}([\p{Lu}\p{Ll}0-9])*)\b";

		static Regex regex;

		static NoteWikiWatcher ()
		{
			regex = new Regex (WIKIWORD_REGEX, RegexOptions.Compiled);
		}

		public override void Initialize ()
		{
			broken_link_tag = Note.TagTable.Lookup ("link:broken");
		}

		public override void Shutdown ()
		{
			// Do nothing.
		}

		public override void OnNoteOpened ()
		{
			if ((bool) Preferences.Get (Preferences.ENABLE_WIKIWORDS)) {
				Buffer.InsertText += OnInsertText;
				Buffer.DeleteRange += OnDeleteRange;
			}
			Preferences.SettingChanged += OnEnableWikiwordsChanged;
		}

		void OnEnableWikiwordsChanged (object sender, NotifyEventArgs args)
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

		void ApplyWikiwordToBlock (Gtk.TextIter start, Gtk.TextIter end)
		{
			NoteBuffer.GetBlockExtents (ref start,
			                            ref end,
			                            80 /* max wiki name */,
			                            broken_link_tag);

			Buffer.RemoveTag (broken_link_tag, start, end);

			for (Match match = regex.Match (start.GetText (end));
			                match.Success;
			                match = match.NextMatch ()) {
				System.Text.RegularExpressions.Group group = match.Groups [1];

				Logger.Debug ("Highlighting wikiword: '{0}' at offset {1}",
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

	public class MouseHandWatcher : NoteAddin
	{
		bool hovering_on_link;

		static Gdk.Cursor normal_cursor;
		static Gdk.Cursor hand_cursor;

		static MouseHandWatcher ()
		{
			normal_cursor = new Gdk.Cursor (Gdk.CursorType.Xterm);
			hand_cursor = new Gdk.Cursor (Gdk.CursorType.Hand2);
		}

		public override void Initialize ()
		{
			// Do nothing.
		}

		public override void Shutdown ()
		{
			// Do nothing.
		}

		public override void OnNoteOpened ()
		{
			Gtk.TextView editor = Window.Editor;
			editor.MotionNotifyEvent += OnEditorMotion;
			editor.KeyPressEvent += OnEditorKeyPress;
			editor.KeyReleaseEvent += OnEditorKeyRelease;
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
				Gtk.TextIter iter = Buffer.GetIterAtMark (Buffer.InsertMark);

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (NoteTagTable.TagIsActivatable (tag)) {
						args.RetVal = tag.ProcessEvent (Window.Editor,
						                                args.Event,
						                                iter);
						if ((bool) args.RetVal)
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
			bool hovering = false;

			// Figure out if we're on a link by getting the text
			// iter at the mouse point, and checking for tags that
			// start with "link:"...

			int buffer_x, buffer_y;
			Window.Editor.WindowToBufferCoords (Gtk.TextWindowType.Widget,
			                                    (int)args.Event.X,
			                                    (int)args.Event.Y,
			                                    out buffer_x,
			                                    out buffer_y);

			Gtk.TextIter iter = Window.Editor.GetIterAtLocation (buffer_x, buffer_y);

			foreach (Gtk.TextTag tag in iter.Tags) {
				if (NoteTagTable.TagIsActivatable (tag)) {
					hovering = true;
					break;
				}
			}

			// Don't show hand if Shift or Control is pressed
			bool avoid_hand = (args.Event.State & (Gdk.ModifierType.ShiftMask |
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

	public class NoteTagsWatcher : NoteAddin
	{
		static NoteTagsWatcher ()
		{
		}

		public override void Initialize ()
		{
			Note.TagAdded += OnTagAdded;
			Note.TagRemoving += OnTagRemoving;
			Note.TagRemoved += OnTagRemoved;
		}

		public override void Shutdown ()
		{
			Note.TagAdded -= OnTagAdded;
			Note.TagRemoving -= OnTagRemoving;
			Note.TagRemoved -= OnTagRemoved;
		}

		public override void OnNoteOpened ()
		{
			// FIXME: Just for kicks, spit out the current tags
			Logger.Debug ("{0} tags:", Note.Title);
			foreach (Tag tag in Note.Tags) {
				Logger.Debug ("\t{0}", tag.Name);
			}
		}

		void OnTagAdded (Note note, Tag tag)
		{
			Logger.Debug ("Tag added to {0}: {1}", note.Title, tag.Name);
		}

		void OnTagRemoving (Note note, Tag tag)
		{
			Logger.Debug ("Removing tag from {0}: {1}", note.Title, tag.Name);
		}

		// <summary>
		// Keep the TagManager clean by removing tags that are no longer
		// tagging any other notes.
		// </summary>
		void OnTagRemoved (Note note, string tag_name)
		{
			Tag tag = TagManager.GetTag (tag_name);
			Logger.Debug ("Watchers.OnTagRemoved popularity count: {0}", tag.Popularity);
			if (tag.Popularity == 0)
				TagManager.RemoveTag (tag);
		}
	}
}
