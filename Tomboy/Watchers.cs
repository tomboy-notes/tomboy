
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web;
using Mono.Posix;

namespace Tomboy
{
	public abstract class NotePlugin
	{
		Note note;

		public void Initialize (Note note)
		{
			this.note = note;
			Initialize ();
		}

		public abstract void Initialize ();

		public Note Note
		{
			get { return note; }
		}

		public NoteBuffer Buffer
		{
			get { return note.Buffer; }
		}

		public NoteWindow Window
		{
			get { return note.Window; }
		}

		public NoteManager Manager
		{
			get { return note.Manager; }
		}
	}

	public class NoteRenameWatcher : NotePlugin
	{
		Gtk.TextMark first_line_end;
		uint rename_timeout_id;

		public override void Initialize ()
		{
			Note.Opened += OnNoteOpened;
		}

		void OnNoteOpened (object sender, EventArgs args)
		{
			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;

			Gtk.TextIter line_end = Buffer.StartIter;
			line_end.ForwardLine ();

			Console.WriteLine ("Applying <note-title> to '{0}'",
					   Buffer.StartIter.GetText (line_end));

			Buffer.ApplyTag ("note-title", Buffer.StartIter, line_end);

			first_line_end = Buffer.CreateMark (null, 
							    line_end, 
							    false /* keep to the right */);
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			if (args.Start.Line != 0)
				return;

			UpdateTitle ();
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			if (args.Pos.Line != 0)
				return;

			UpdateTitle ();
		}

		void UpdateTitle ()
		{
			// Replace the existing save timeout...
			if (rename_timeout_id != 0)
				GLib.Source.Remove (rename_timeout_id);

			// Wait .5 seconds before renaming...
			rename_timeout_id = 
				GLib.Timeout.Add (500, 
						  new GLib.TimeoutHandler (UpdateTitleTimeout));

			Gtk.TextIter line_end = Buffer.GetIterAtMark (first_line_end);
			Buffer.ApplyTag ("note-title", Buffer.StartIter, line_end);

			// Only set window title here, to give feedback that we
			// are indeed changing the title.
			Window.Title = Buffer.StartIter.GetText (line_end).Trim ();
		}

		bool UpdateTitleTimeout ()
		{
			Gtk.TextIter line_end = Buffer.GetIterAtMark (first_line_end);
			string title = Buffer.StartIter.GetText (line_end).Trim ();

			Note existing = Manager.Find (title);

			if (existing != null && existing != this.Note) {
				ShowNameClashError (Note, title);
			} else {
				Note.Title = title;
			}

			return false;
		}

		void ShowNameClashError (Note note, string title)
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
		[DllImport ("libgtkspell.so.0")]
		static extern void gtkspell_new_attach (IntPtr text_view, 
							string locale, 
							IntPtr error);

		public override void Initialize ()
		{
			Note.Opened += OnNoteOpened;
		}

		public void OnNoteOpened (object sender, EventArgs args)
		{
			Buffer.TagApplied += TagApplied;

			gtkspell_new_attach (Window.Editor.Handle, 
					     null, 
					     IntPtr.Zero);

			FixupOldGtkSpell ();
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
				Buffer.RemoveTag ("gtkspell-misspelled", 
						  args.StartChar, 
						  args.EndChar);
			}
		}
	}

	public class NoteUrlWatcher : NotePlugin
	{
		Gtk.TextTag url_tag;

		const string URL_REGEX = 
			@"((\b((news|http|https|ftp|file|irc)://|mailto:|(www|ftp)\.|\S*@\S*\.)|/\S+/)\S*\b/?)";

		static Regex regex;

		static NoteUrlWatcher ()
		{
			regex = new Regex (URL_REGEX, 
					   RegexOptions.IgnoreCase | RegexOptions.Compiled);
		}

		public override void Initialize ()
		{
			Note.Opened += OnNoteOpened;
		}

		public void OnNoteOpened (object sender, EventArgs args)
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
		ArrayList note_titles;
		int longest_title;

		public override void Initialize () 
		{
			Note.Opened += OnNoteOpened;

			Manager.NoteDeleted += OnNoteDeleted;
			Manager.NoteAdded += OnNoteAdded;
			Manager.NoteRenamed += OnNoteRenamed;

			note_titles = new ArrayList ();
		}

		void OnNoteOpened (object sender, EventArgs args)
		{
			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;

			UpdateTitleCache ();

			// Do we need this?  Causes unnecessary write on open.
			//HighlightInBlock (Buffer.Text.ToLower (), Buffer.StartIter);
		}

		// Updating this on NoteChanged allows us to fetch a smaller
		// block to look for links when typing, and avoid lowercasing...
		void UpdateTitleCache ()
		{
			longest_title = 0;
			note_titles.Clear ();

			foreach (Note note in Manager.Notes) {
				if (note.Title.Length > longest_title)
					longest_title = note.Title.Length;

				if (note.Title.Length > 0)
					note_titles.Add (note.Title.ToLower ());
			}
		}

		void ReplaceTagOnMatch (Gtk.TextTag find_tag,
					Gtk.TextTag replace_with_tag,
					string      match)
		{
			Gtk.TextIter iter = Buffer.StartIter;

			while (iter.ForwardToTagToggle (find_tag) &&
			       !iter.Equal (Buffer.EndIter)) {
				if (!iter.BeginsTag (find_tag))
					continue;

				Gtk.TextIter end = iter;
				if (!end.ForwardToTagToggle (find_tag))
					break;

				string tag_content = iter.GetText (end).ToLower ();

				if (tag_content == match) {
					Buffer.RemoveTag (find_tag, iter, end);
					Buffer.ApplyTag (replace_with_tag, iter, end);
				}

				iter = end;
			}
		}

		bool ContainsText (string text)
		{
			/* Encode any entities */
			string encoded_text = text.ToLower ();
			encoded_text = HttpUtility.HtmlEncode (encoded_text);

			return Note.Text.ToLower ().IndexOf (encoded_text) > -1;
		}

		// This can be called whether Note is showing or not.  If
		// showing, manually apply the link tags to Buffer.  Otherwise
		// just replace the xml Note.Text and queue a save.
		void OnNoteAdded (object sender, Note added)
		{
			UpdateTitleCache ();

			if (added == this.Note)
				return;

			if (ContainsText (added.Title)) {
				HighlightInBlock (Buffer.Text.ToLower (), Buffer.StartIter);
			}
		}

		// This can be called whether Note is showing or not.  If
		// showing, manually apply the link tags to Buffer.  Otherwise
		// just replace the xml Note.Text and queue a save.
		void OnNoteDeleted (object sender, Note deleted)
		{
			UpdateTitleCache ();

			if (deleted == this.Note)
				return;

			if (ContainsText (deleted.Title)) {
				Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
				Gtk.TextTag broken_link_tag = Buffer.TagTable.Lookup ("link:broken");

				// Switch any link:internal tags which point to
				// the deleted note to link:broken
				ReplaceTagOnMatch (link_tag,
						   broken_link_tag,
						   deleted.Title.ToLower ());
			}
		}

		// This can be called whether Note is showing or not.  If
		// showing, manually change the Buffer content.  Otherwise
		// just replace the xml Note.Text and queue a save.
		void OnNoteRenamed (Note renamed, string old_title)
		{
			UpdateTitleCache ();

			if (ContainsText (old_title)) {
				Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
				
				Gtk.TextIter iter = Buffer.StartIter;
				string old_title_lower = old_title.ToLower ();

				while (iter.ForwardToTagToggle (link_tag) &&
				       !iter.Equal (Buffer.EndIter)) {
					if (!iter.BeginsTag (link_tag))
						continue;

					Gtk.TextIter end = iter;
					if (!end.ForwardToTagToggle (link_tag))
						break;

					string tag_content = iter.GetText (end).ToLower ();

					if (tag_content == old_title_lower) {
						Console.WriteLine ("Replacing with '{0}'", renamed.Title);

						int iter_offset = iter.Offset;
						int end_offset = end.Offset;

						Buffer.Delete (iter, end);

						iter = Buffer.GetIterAtOffset (iter_offset);
						end = Buffer.GetIterAtOffset (end_offset);

						Buffer.Insert (iter, renamed.Title);

						iter = Buffer.GetIterAtOffset (iter_offset);
						end = Buffer.GetIterAtOffset (end_offset);
					}

					iter = end;
				}
			}
		}

		void HighlightInBlock (string lower_text, Gtk.TextIter cursor) 
		{
			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			Gtk.TextTag broken_link_tag = Buffer.TagTable.Lookup ("link:broken");

			foreach (string title in note_titles) {
				int last_idx = 0;

				if (title == Note.Title)
					continue;

				while (true) {
					int idx = lower_text.IndexOf (title, last_idx);
					if (idx < 0)
						break;

					Gtk.TextIter title_start = cursor;
					title_start.ForwardChars (idx);

					Gtk.TextIter title_end = title_start;
					title_end.ForwardChars (title.Length);

					Console.WriteLine ("Matching Note title '{0}'...", title);

					Buffer.RemoveTag (broken_link_tag,
							  title_start,
							  title_end);
					Buffer.ApplyTag (link_tag,
							 title_start,
							 title_end);

					last_idx = idx + title.Length;
				}
			}
		}

		void UnhighlightInBlock (string text, Gtk.TextIter cursor, Gtk.TextIter end) 
		{
			Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:internal");
			Buffer.RemoveTag (link_tag, cursor, end);
		}

		string GetBlockAroundCursor (ref Gtk.TextIter start, ref Gtk.TextIter end) 
		{
			start.LineOffset = 0;
			end.ForwardToLineEnd ();

			return start.GetText (end).ToLower ();
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			Gtk.TextIter start = args.Start;
			Gtk.TextIter end = args.End;

			// Avoid title line
			if (start.Line == 0)
				return;

			string block = GetBlockAroundCursor (ref start, ref end);

			UnhighlightInBlock (block, start, end);
			HighlightInBlock (block, start);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			start.BackwardChars (args.Length);

			Gtk.TextIter end = args.Pos;

			// Avoid title line
			if (start.Line == 0)
				return;

			string block = GetBlockAroundCursor (ref start, ref end);

			UnhighlightInBlock (block, start, end);
			HighlightInBlock (block, start);
		}
	}

	public class NoteWikiWatcher : NotePlugin
	{
		Gtk.TextTag broken_link_tag;

		const string WIKIWORD_REGEX = @"\b(([A-Z]+[a-z0-9]+){2}([A-Za-z0-9])*)\b";

		static Regex regex; 

		static NoteWikiWatcher ()
		{
			regex = new Regex (WIKIWORD_REGEX, RegexOptions.Compiled);
		}

		public override void Initialize ()
		{
			Note.Opened += OnNoteOpened;
		}

		void OnNoteOpened (object sender, EventArgs args)
		{
			broken_link_tag = Buffer.TagTable.Lookup ("link:broken");
			if (broken_link_tag == null) {
				Console.WriteLine ("ERROR: Broken link tags not registered for buffer.");
				return;
			}

			Buffer.InsertText += OnInsertText;
			Buffer.DeleteRange += OnDeleteRange;
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

		public override void Initialize ()
		{
			Note.Opened += OnNoteOpened;
		}

		public void OnNoteOpened (object sender, EventArgs args)
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
		public override void Initialize ()
		{
			Note.Opened += OnNoteOpened;
		}

		public void OnNoteOpened (object sender, EventArgs args)
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
