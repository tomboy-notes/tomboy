
using System;
using System.Collections;
using System.Runtime.InteropServices;

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
		Gtk.TextView view;
		Gtk.TextBuffer buffer;

		Gtk.TextTag url_tag;
		Gtk.TextTag link_tag;
		Gtk.TextTag broken_link_tag;

		[DllImport ("libgtkspell.so.0")]
		static extern void gtkspell_new_attach (IntPtr text_view, 
							string locale, 
							IntPtr error);

		public NoteSpellChecker (Note note)
		{
			this.buffer = note.Buffer;

			url_tag = buffer.TagTable.Lookup ("link:url");
			link_tag = buffer.TagTable.Lookup ("link:internal");
			broken_link_tag = buffer.TagTable.Lookup ("link:broken");

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
				// Remove misspelled tag for urls (words tagged
				// with "link:url")
				if (args.StartChar.BeginsTag (url_tag) || 
				    args.StartChar.HasTag (url_tag) ||
				    args.StartChar.BeginsTag (link_tag) || 
				    args.StartChar.HasTag (link_tag) ||
				    args.StartChar.BeginsTag (broken_link_tag) || 
				    args.StartChar.HasTag (broken_link_tag))
					buffer.RemoveTag ("gtkspell-misspelled", 
							  args.StartChar, 
							  args.EndChar);
			} else if (args.Tag.Name == "link:url" ||
				   args.Tag.Name == "link:internal" ||
				   args.Tag.Name == "link:broken") {
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
					       "Unable to open location..." +
					       "</b></span>\n\n" +
					       "{0}",
					       error);

			Gtk.Label label = new Gtk.Label (label_text);
			label.UseMarkup = true;
			label.Wrap = true;
			label.Show ();

			Gtk.Button button = new Gtk.Button (Gtk.Stock.Ok);
			button.Show ();

			Gtk.Dialog dialog = new Gtk.Dialog ("Unable to open location",
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

		void OpenOrCreateLink (Gtk.TextIter start, Gtk.TextIter end, bool create)
		{
			string note_name = start.GetText (end);
			Note note = manager.Find (note_name);

			if (note == null && create) {
				Console.WriteLine ("Creating note '{0}'...", note_name);
				note = manager.Create (note_name);
			}

			if (note != null) {
				Console.WriteLine ("Opening note '{0}' on click...", note_name);
				note.Window.Present ();
			}
		}

		void OnLinkTextEvent (object sender, Gtk.TextEventArgs args)
		{
			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Gdk.EventButton button_ev = new Gdk.EventButton (args.Event.Handle);
			if (button_ev.Button != 1)
				return;

			Gtk.TextIter start = args.Iter, end = args.Iter;

			start.BackwardToTagToggle (link_tag);
			end.ForwardToTagToggle (link_tag);

			OpenOrCreateLink (start, end, false);
		}

		void OnBrokenTextEvent (object sender, Gtk.TextEventArgs args)
		{
			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Gdk.EventButton button_ev = new Gdk.EventButton (args.Event.Handle);
			if (button_ev.Button != 1)
				return;

			Gtk.TextIter start = args.Iter, end = args.Iter;

			start.BackwardToTagToggle (broken_link_tag);
			end.ForwardToTagToggle (broken_link_tag);

			OpenOrCreateLink (start, end, true);

			buffer.RemoveTag (broken_link_tag, start, end);
			buffer.ApplyTag (link_tag, start, end);
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
			if (!Char.IsUpper (word [0]))
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
}
