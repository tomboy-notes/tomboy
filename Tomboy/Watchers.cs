
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Posix;

namespace Tomboy
{
	public class NoteRenameWatcher
	{
		Note note;
		Gtk.Window window;
		NoteBuffer buffer;
		Gtk.TextMark first_line_end;
		uint rename_timeout_id;

		public NoteRenameWatcher (Note note)
		{
			this.note = note;
			this.window = note.Window;
			this.buffer = note.Buffer;
			
			buffer.InsertText += new Gtk.InsertTextHandler (OnInsertText);
			buffer.DeleteRange += new Gtk.DeleteRangeHandler (OnDeleteRange);

			Gtk.TextIter line_end = buffer.StartIter;
			line_end.ForwardLine ();

			Console.WriteLine ("Applying <note-title> to '{0}'",
					   buffer.StartIter.GetText (line_end));

			buffer.ApplyTag ("note-title", buffer.StartIter, line_end);

			first_line_end = buffer.CreateMark (null, 
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

			Gtk.TextIter line_end = buffer.GetIterAtMark (first_line_end);
			string title = buffer.StartIter.GetText (line_end);

			buffer.ApplyTag ("note-title", buffer.StartIter, line_end);

			// Only set window title here, to give feedback that we
			// are indeed changing the title.
			window.Title = title.Trim ();
		}

		bool UpdateTitleTimeout ()
		{
			Gtk.TextIter line_end = buffer.GetIterAtMark (first_line_end);
			string title = buffer.StartIter.GetText (line_end);

			note.Title = title.Trim ();

			return false;
		}
	}

	public class NoteSpellChecker
	{
		Gtk.TextBuffer buffer;

		[DllImport ("libgtkspell.so.0")]
		static extern void gtkspell_new_attach (IntPtr text_view, 
							string locale, 
							IntPtr error);

		public NoteSpellChecker (Note note)
		{
			this.buffer = note.Buffer;

			buffer.TagApplied += new Gtk.TagAppliedHandler (TagApplied);

			gtkspell_new_attach (note.Window.Editor.Handle, 
					     null, 
					     IntPtr.Zero);

			// NOTE: Older versions of GtkSpell before 2.0.6 use red
			// foreground color and a single underline.  This
			// conflicts with internal note links.  So fix it up to
			// use the "normal" foreground and the "error"
			// underline.
			Gtk.TextTag misspell = buffer.TagTable.Lookup ("gtkspell-misspelled");
			if (misspell != null) {
				Gtk.TextTag normal = buffer.TagTable.Lookup ("normal");
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
					if (tag.Name.StartsWith ("link:")) {
						buffer.RemoveTag ("gtkspell-misspelled", 
								  args.StartChar, 
								  args.EndChar);
						break;
					}
				}
			} else if (args.Tag.Name.StartsWith ("link:")) {
				buffer.RemoveTag ("gtkspell-misspelled", 
						  args.StartChar, 
						  args.EndChar);
			}
		}
	}

	public class NoteUrlWatcher
	{
		Note note;
		NoteBuffer buffer;
		Gtk.TextTag url_tag;

		public NoteUrlWatcher (Note note)
		{
			this.note = note;
			this.buffer = note.Buffer;

			url_tag = buffer.TagTable.Lookup ("link:url");
			if (url_tag == null) {
				Console.WriteLine ("Tag 'link:url' not registered for buffer");
				return;
			}

			url_tag.TextEvent += new Gtk.TextEventHandler (OnTextEvent);

			buffer.InsertText += new Gtk.InsertTextHandler (OnInsertText);
			buffer.DeleteRange += new Gtk.DeleteRangeHandler (OnDeleteRange);
		}

		void OnTextEvent (object sender, Gtk.TextEventArgs args)
		{
			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Gdk.EventButton button_ev = new Gdk.EventButton (args.Event.Handle);
			if (button_ev.Button != 1)
				return;

			Gtk.TextIter start = args.Iter, end = args.Iter;

			start.BackwardToTagToggle (url_tag);
			end.ForwardToTagToggle (url_tag);

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
			} catch (GLib.GException e) {
				ShowOpeningLocationError (url, e.Message);
				args.RetVal = true;
			}
		}

		void ShowOpeningLocationError (string url, string error)
		{
			string label_text = 
				String.Format ("<span size=\"large\"><b>" +
					       Catalog.GetString ("Unable to open location...") +
					       "</b></span>\n\n" +
					       "{0}",
					       error);

			Gtk.Label label = new Gtk.Label (label_text);
			label.UseMarkup = true;
			label.Wrap = true;
			label.Show ();

			Gtk.Button button = new Gtk.Button (Gtk.Stock.Ok);
			button.Show ();

			Gtk.Dialog dialog = 
				new Gtk.Dialog (Catalog.GetString ("Unable to open location"),
						note.Window,
						Gtk.DialogFlags.DestroyWithParent);
			dialog.AddActionWidget (button, Gtk.ResponseType.Ok);
			dialog.VBox.PackStart (label, false, false, 0);

			dialog.Run ();
			dialog.Destroy ();
		}

		bool CheckIsUrl (string word)
		{
			if (word.StartsWith ("http://") ||
			    word.StartsWith ("https://") ||
			    word.StartsWith ("ftp://") ||
			    word.StartsWith ("file://") ||
			    word.StartsWith ("mailto://") ||
			    word.StartsWith ("www.") ||
			    (word.StartsWith ("/") && word.LastIndexOf ("/") > 1) ||
			    (word.IndexOf ("@") > 1 && word.IndexOf (".") > 3))
				return true;
			else
				return false;
		}

		Gtk.TextIter ApplyUrlAtIter (Gtk.TextIter iter)
		{
			const string ending_punctuation = ".,!?;:";
			Gtk.TextIter start, end, tag_end;

			string word = buffer.GetCurrentBlock (iter, out start, out end);
			if (word != null) {
				tag_end = end;
				tag_end.BackwardChar ();

				// avoid trailing punctuation
				if (ending_punctuation.IndexOf (tag_end.Char [0]) == -1) 
					tag_end = end;

				if (CheckIsUrl (word))
					buffer.ApplyTag (url_tag, start, tag_end);
				else
					buffer.RemoveTag (url_tag, start, tag_end);
			}

			return end;
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			ApplyUrlAtIter (args.Start);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			if (args.Length == 1) {
				ApplyUrlAtIter (args.Pos);
			} else {
				Gtk.TextIter insert_start = args.Pos;
				insert_start.BackwardChars (args.Length);

				while (insert_start.Offset < args.Pos.Offset) {
					insert_start = ApplyUrlAtIter (insert_start);

					while (true) {
						insert_start.ForwardChar ();
						if (insert_start.IsEnd ||
						    !Char.IsWhiteSpace (insert_start.Char [0]))
							break;
					}
				}
			}
		}
	}

	public class NoteLinkWatcher
	{
		Note note;
		NoteManager manager;
		NoteBuffer buffer;

		Gtk.TextTag link_tag;
		Gtk.TextTag broken_link_tag;

		ArrayList note_titles = new ArrayList ();
		int longest_title;

		public NoteLinkWatcher (Note note)
		{
			this.note = note;
			this.buffer = note.Buffer;
			this.manager = note.Manager;

			link_tag = buffer.TagTable.Lookup ("link:internal");
			broken_link_tag = buffer.TagTable.Lookup ("link:broken");

			if (link_tag == null || broken_link_tag == null) {
				Console.WriteLine ("ERROR: Link tags not registered for buffer.");
				return;
			}

			link_tag.TextEvent += new Gtk.TextEventHandler (OnLinkTextEvent);
			broken_link_tag.TextEvent += new Gtk.TextEventHandler (OnBrokenTextEvent);

			buffer.InsertText += new Gtk.InsertTextHandler (OnInsertText);
			buffer.DeleteRange += new Gtk.DeleteRangeHandler (OnDeleteRange);

			manager.NotesChanged += new NotesChangedHandler (OnNotesChanged);
			manager.NoteRenamed += new NoteRenameHandler (OnNoteRenamed);

			UpdateTitleCache ();

			// Avoid highlighting title
			Gtk.TextIter content_start = buffer.StartIter;
			content_start.ForwardLine ();

			HighlightInBlock (buffer.Text.ToLower (), buffer.StartIter);
		}

		// Updating this on NoteChanged allows us to fetch a smaller
		// block to look for links when typing, and avoid lowercasing...
		void UpdateTitleCache ()
		{
			longest_title = 0;
			note_titles.Clear ();

			foreach (Note note in manager.Notes) {
				if (note.Title.Length > longest_title)
					longest_title = note.Title.Length;

				if (note.Title.Length > 0)
					note_titles.Add (note.Title.ToLower ());
			}
		}

		void OnNotesChanged (object sender, Note added, Note deleted)
		{
			UpdateTitleCache ();

			if (added != null) {
				HighlightInBlock (buffer.Text.ToLower (), buffer.StartIter);
			}

			if (deleted != null && deleted.Buffer != buffer) {
				string deleted_title = deleted.Title.ToLower ();
				Gtk.TextIter iter = buffer.StartIter;

				while (iter.ForwardToTagToggle (link_tag) &&
				       !iter.Equal (buffer.EndIter)) {
					if (!iter.BeginsTag (link_tag))
						continue;

					Gtk.TextIter end = iter;
					if (!end.ForwardToTagToggle (link_tag))
						break;

					string tag_content = iter.GetText (end).ToLower ();

					if (tag_content == deleted_title) {
						buffer.RemoveTag (link_tag, iter, end);
						buffer.ApplyTag (broken_link_tag, iter, end);
					}

					iter = end;
				}
			}
		}

		void OnNoteRenamed (Note note, string old_title)
		{
			Gtk.TextIter iter = buffer.StartIter;
			string old_title_lower = old_title.ToLower ();

			Console.WriteLine ("OnNoteRenamed called!");

			UpdateTitleCache ();

			while (iter.ForwardToTagToggle (link_tag) &&
			       !iter.Equal (buffer.EndIter)) {
				if (!iter.BeginsTag (link_tag))
					continue;

				Gtk.TextIter end = iter;
				if (!end.ForwardToTagToggle (link_tag))
					break;

				string tag_content = iter.GetText (end).ToLower ();

				Console.WriteLine ("Got tag content '{0}'", tag_content);

				if (tag_content == old_title_lower) {
					Console.WriteLine ("Replacing with '{0}'", note.Title);

					int iter_offset = iter.Offset;
					int end_offset = end.Offset;

					buffer.Delete (iter, end);

					iter = buffer.GetIterAtOffset (iter_offset);
					end = buffer.GetIterAtOffset (end_offset);

					buffer.Insert (iter, note.Title);

					iter = buffer.GetIterAtOffset (iter_offset);
					end = buffer.GetIterAtOffset (end_offset);
				}

				iter = end;
			}			
		}

		bool OpenOrCreateLink (Gtk.TextIter start, Gtk.TextIter end, bool create)
		{
			string link_name = start.GetText (end);
			Note link = manager.Find (link_name);

			if (link == null && create) {
				Console.WriteLine ("Creating note '{0}'...", link_name);
				link = manager.Create (link_name);
			}

			if (link != null && link != note) {
				Console.WriteLine ("Opening note '{0}' on click...", link_name);
				link.Window.Present ();
				return true;
			}

			return false;
		}

		void OnLinkTextEvent (object sender, Gtk.TextEventArgs args)
		{
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

			start.BackwardToTagToggle (link_tag);
			end.ForwardToTagToggle (link_tag);

			if (!OpenOrCreateLink (start, end, false))
				return;

			if (button_ev.Button == 2)
				note.Window.Hide ();
		}

		void OnBrokenTextEvent (object sender, Gtk.TextEventArgs args)
		{
			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Gdk.EventButton button_ev = new Gdk.EventButton (args.Event.Handle);
			if (button_ev.Button != 1 && button_ev.Button != 2)
				return;

			Gtk.TextIter start = args.Iter, end = args.Iter;

			start.BackwardToTagToggle (broken_link_tag);
			end.ForwardToTagToggle (broken_link_tag);

			if (!OpenOrCreateLink (start, end, true))
				return;

			buffer.RemoveTag (broken_link_tag, start, end);
			buffer.ApplyTag (link_tag, start, end);

			if (button_ev.Button == 2)
				note.Window.Hide ();
		}

		void HighlightInBlock (string lower_text, Gtk.TextIter cursor) 
		{
			foreach (string title in note_titles) {
				int last_idx = 0;

				while (true) {
					int idx = lower_text.IndexOf (title, last_idx);
					if (idx < 0)
						break;

					Gtk.TextIter title_start = cursor;
					title_start.ForwardChars (idx);

					Gtk.TextIter title_end = title_start;
					title_end.ForwardChars (title.Length);

					Console.WriteLine ("Matching Note title '{0}'...", title);

					buffer.ApplyTag (link_tag,
							 title_start,
							 title_end);

					last_idx = idx + title.Length;
				}
			}
		}

		void UnhighlightInBlock (string text, Gtk.TextIter cursor, Gtk.TextIter end) 
		{
			Gtk.TextIter iter = cursor;

			while (iter.ForwardToTagToggle (link_tag) &&
			       iter.InRange (cursor, end)) {
				if (!iter.BeginsTag (link_tag))
					continue;

				Gtk.TextIter tag_end = iter;
				if (!tag_end.ForwardToTagToggle (link_tag))
					break;

				string lower_text = iter.GetText (tag_end).ToLower ();
				bool match = false;

				foreach (string title in note_titles) {
					if (title == lower_text) {
						match = true;
						break;
					}
				}

				if (!match) 
					buffer.RemoveTag (link_tag, iter, tag_end);
			}
		}

		string GetBlockAroundCursor (ref Gtk.TextIter start, ref Gtk.TextIter end) 
		{
			if (!start.BackwardChars (longest_title))
				start = buffer.StartIter;

			if (!end.ForwardChars (longest_title))
				end = buffer.EndIter;

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

			HighlightInBlock (block, start);
			UnhighlightInBlock (block, start, end);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			Gtk.TextIter start = args.Pos;
			Gtk.TextIter end = args.Pos;

			// Avoid title line
			if (start.Line == 0)
				return;

			string block = GetBlockAroundCursor (ref start, ref end);

			HighlightInBlock (block, start);
			UnhighlightInBlock (block, start, end);
		}
	}

	public class NoteWikiWatcher
	{
		Note note;
		NoteBuffer buffer;
		Gtk.TextTag link_tag;
		Gtk.TextTag broken_link_tag;

		public NoteWikiWatcher (Note note)
		{
			this.note = note;
			this.buffer = note.Buffer;

			link_tag = buffer.TagTable.Lookup ("link:internal");
			broken_link_tag = buffer.TagTable.Lookup ("link:broken");

			if (link_tag == null || broken_link_tag == null) {
				Console.WriteLine ("ERROR: Link tags not registered for buffer.");
				return;
			}

			buffer.InsertText += new Gtk.InsertTextHandler (OnInsertText);
			buffer.DeleteRange += new Gtk.DeleteRangeHandler (OnDeleteRange);
		}

		bool WordIsCamelCase (string word)
		{
			if (word == string.Empty || !Char.IsUpper (word [0]))
				return false;

			// Avoid patronymic name prefixes
			if (word.StartsWith ("Mc") || word.StartsWith ("Mac"))
				return false;

			int upper_cnt = 1;
			for (int i = 1; i < word.Length; i++)
				if (Char.IsUpper (word [i]))
					upper_cnt++;

			// Avoid all-caps, and regular capitalized words
			if (upper_cnt == word.Length || upper_cnt < 2)
				return false;

			return true;
		}

		Gtk.TextIter CheckCurrentWord (Gtk.TextIter iter)
		{
			Gtk.TextIter start, end, tag_end;

			string word = buffer.GetCurrentBlock (iter, out start, out end);
			if (word != null) {
				tag_end = end;
				tag_end.BackwardChar ();

				// avoid trailing punctuation
				if (Char.IsPunctuation (tag_end.Char [0]))
					tag_end = end;

				if (WordIsCamelCase (word)) {
					if (note.Manager.Find (word) != null)
						buffer.ApplyTag (link_tag, start, end);
					else
						buffer.ApplyTag (broken_link_tag, start, end);
				} else {
					buffer.RemoveTag (broken_link_tag, start, end);
				}
			}

			return end;
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			CheckCurrentWord (args.Start);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			if (args.Length == 1) {
				CheckCurrentWord (args.Pos);
			} else {
				Gtk.TextIter insert_start = args.Pos;
				insert_start.BackwardChars (args.Length);

				while (insert_start.Offset < args.Pos.Offset) {
					insert_start = CheckCurrentWord (insert_start);

					while (true) {
						insert_start.ForwardChar ();
						if (insert_start.IsEnd ||
						    !Char.IsWhiteSpace (insert_start.Char [0]))
							break;
					}
				}
			}
		}

		// TODO: Handle topic/toc creation using =, ==, ===, ====
		//       horizontal spacer with ----
	}

	public class MouseHandWatcher
	{
		Gtk.TextView editor;
		bool hovering_on_link;

		static Gdk.Cursor normal_cursor;
		static Gdk.Cursor hand_cursor;

		static MouseHandWatcher ()
		{
			normal_cursor = new Gdk.Cursor (Gdk.CursorType.Xterm);
			hand_cursor = new Gdk.Cursor (Gdk.CursorType.Hand2);
		}

		public MouseHandWatcher (Note note)
		{
			editor = note.Window.Editor;
			editor.MotionNotifyEvent += OnEditorMotion;
		}

		[GLib.ConnectBefore]
		void OnEditorMotion (object sender, Gtk.MotionNotifyEventArgs args)
		{
			int pointer_x, pointer_y;
			Gdk.ModifierType pointer_mask;

			editor.GdkWindow.GetPointer (out pointer_x, 
						     out pointer_y, 
						     out pointer_mask);

			bool hovering = false;

			/* Don't show hand if Shift or Control is pressed */
			if ((int) (pointer_mask & (Gdk.ModifierType.ShiftMask |
						   Gdk.ModifierType.ControlMask)) == 0) {
				int buffer_x, buffer_y;
				editor.WindowToBufferCoords (Gtk.TextWindowType.Widget,
							     pointer_x, 
							     pointer_y,
							     out buffer_x, 
							     out buffer_y);

				Gtk.TextIter iter = editor.GetIterAtLocation (buffer_x, buffer_y);

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (tag.Name.StartsWith ("link:")) {
						hovering = true;
						break;
					}
				}
			}

			if (hovering != hovering_on_link) {
				hovering_on_link = hovering;

				Gdk.Window win = editor.GetWindow (Gtk.TextWindowType.Text);
				if (hovering)
					win.Cursor = hand_cursor;
				else 
					win.Cursor = normal_cursor;
			}
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

			buffer.InsertText += new Gtk.InsertTextHandler (OnInsertText);
			//buffer.DeleteRange += new Gtk.DeleteRangeHandler (OnDeleteRange);
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

			buffer.InsertText += new Gtk.InsertTextHandler (OnInsertText);
			buffer.DeleteRange += new Gtk.DeleteRangeHandler (OnDeleteRange);			
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
