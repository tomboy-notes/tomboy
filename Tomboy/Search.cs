
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Posix;

namespace Tomboy 
{
	public class NoteFindDialog : ForcedPresentWindow
	{
		NoteManager manager;

		Gtk.AccelGroup accel_group;
		Gtk.Combo find_combo;
		Gtk.CheckButton search_all_notes;
		Gtk.CheckButton case_sensitive;
		Gtk.ScrolledWindow matches_window;
		Gtk.TreeView tree;
		Gtk.Button close_button;
		Gtk.Button find_next_button;
		Gtk.Button find_prev_button;
		Gtk.VBox content_vbox;

		Gtk.ListStore store;

		InterruptableTimeout entry_changed_timeout;

		Note current_note;
		ArrayList current_matches;

		static string [] previous_searches;
		static NoteFindDialog instance;
		static Gdk.Pixbuf search_image;
		static Gdk.Pixbuf stock_notes;

		static NoteFindDialog ()
		{
			search_image = new Gdk.Pixbuf (null, "gnome-stock-searchtool.png");
			stock_notes = GuiUtils.GetMiniIcon ("stock_notes.png");
		}

		public static NoteFindDialog GetInstance (Note note)
		{
			if (instance == null)
				instance = new NoteFindDialog (false);

			if (instance.current_note != note) {
				instance.current_note = note;
				instance.manager = note.Manager;
				instance.search_all_notes.Sensitive = true; // allow switching

				instance.UpdateResults ();

				// FIXME: Unconnect from previous note's change events
				instance.AddNoteChangeListener ();
			}

			return instance;
		}

		public static NoteFindDialog GetInstance (NoteManager manager)
		{
			if (instance == null)
				instance = new NoteFindDialog (true);

			if (instance.current_note != null || 
			    instance.manager != manager) {
				instance.current_note = null;
				instance.manager = manager;
				instance.search_all_notes.Active = true; // search all
				instance.search_all_notes.Sensitive = false; // force it

				instance.UpdateResults ();

				// FIXME: Unconnect from previous manager's change events
				instance.AddManagerChangeListeners ();
			}

			return instance;
		}

		// Creates singleton instance, and initializes default values
		// based on search_all.
		NoteFindDialog (bool search_all)
			: base (search_all ? 
					Catalog.GetString ("Search All Notes") : 
					Catalog.GetString ("Search Note"))
		{
			this.Icon = search_image;

			// For Escape (Close), Ctrl-G (Find next), and
			// Ctrl-Shift-G (Find Previous)
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			// Allow resizing if showing the results list
			this.Resizable = search_all;

			find_combo = new Gtk.Combo ();
			find_combo.AllowEmpty = false;
			find_combo.CaseSensitive = false;
			find_combo.DisableActivate ();
			find_combo.Entry.Activated += OnEntryActivated;
			find_combo.Entry.Changed += OnEntryChanged;
			if (previous_searches != null)
				find_combo.PopdownStrings = previous_searches;

			Gtk.Label label = new Gtk.Label (Catalog.GetString ("_Find:"));
			label.MnemonicWidget = find_combo.Entry;

			search_all_notes = 
				new Gtk.CheckButton (Catalog.GetString ("Search _All Notes"));
			search_all_notes.Active = search_all;
			search_all_notes.Sensitive = !search_all;
			search_all_notes.Toggled += OnAllNotesToggled;

			case_sensitive = 
				new Gtk.CheckButton (Catalog.GetString ("Case _Sensitive"));
			case_sensitive.Toggled += OnCaseSensitiveToggled;

			Gtk.Table widgets = new Gtk.Table (3, 2, false);
			widgets.Attach (label, 0, 1, 0, 1, 0, 0, 0, 0);
			widgets.Attach (find_combo, 1, 2, 0, 1);
			widgets.Attach (case_sensitive, 1, 2, 1, 2);
			widgets.Attach (search_all_notes, 1, 2, 2, 3);
			widgets.ColumnSpacing = 4;
			widgets.ShowAll ();

			Gtk.Image image = new Gtk.Image (search_image);
			image.Show ();

			Gtk.HBox hbox = new Gtk.HBox (false, 2);
			hbox.BorderWidth = 8;
			hbox.PackStart (image, false, false, 4);
			hbox.PackStart (widgets);
			hbox.Show ();

			// Search all notes result window

			MakeResultTreeView ();
			tree.Sensitive = false;
			tree.Show ();

			matches_window = new Gtk.ScrolledWindow ();
			matches_window.ShadowType = Gtk.ShadowType.In;
			matches_window.HeightRequest = 160;
			matches_window.Add (tree);
			if (search_all)
				matches_window.Show ();

			// Buttons at bottom: Close, Previous, Find Next

			close_button = new Gtk.Button (Gtk.Stock.Close);
			close_button.Clicked += OnCloseClicked;
			close_button.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.Escape, 
						     0,
						     Gtk.AccelFlags.Visible);

			find_prev_button = 
				GuiUtils.MakeImageButton (Gtk.Stock.GoBack, 
							  Catalog.GetString ("_Previous"));
			find_prev_button.Clicked += OnFindPreviousClicked;
			find_prev_button.Sensitive = false;
			find_prev_button.AddAccelerator ("activate",
							 accel_group,
							 (uint) Gdk.Key.g, 
							 (Gdk.ModifierType.ControlMask | 
							  Gdk.ModifierType.ShiftMask),
							 Gtk.AccelFlags.Visible);

			find_next_button = 
				GuiUtils.MakeImageButton (Gtk.Stock.GoForward, 
							  Catalog.GetString ("Find _Next"));
			find_next_button.Clicked += OnFindNextClicked;
			find_next_button.Sensitive = false;
			find_next_button.AddAccelerator ("activate",
							 accel_group,
							 (uint) Gdk.Key.g, 
							 Gdk.ModifierType.ControlMask,
							 Gtk.AccelFlags.Visible);

			// Set up Find Next as the default widget
			find_next_button.CanDefault = true;
			Default = find_next_button;

			Gtk.HButtonBox button_box = new Gtk.HButtonBox ();
			button_box.Layout = Gtk.ButtonBoxStyle.End;
			button_box.Spacing = 8;
			button_box.PackStart (close_button);
			button_box.PackStart (find_prev_button);
			button_box.PackStart (find_next_button);
			button_box.SetChildSecondary (close_button, true);
			button_box.ShowAll ();
			
			content_vbox = new Gtk.VBox (false, 2);
			content_vbox.BorderWidth = 6;
			content_vbox.PackStart (hbox, false, false, 0);
			content_vbox.PackStart (matches_window, true, true, 8);
			content_vbox.PackEnd (button_box, false, false, 0);
			content_vbox.Show ();

			this.Add (content_vbox);
			this.DeleteEvent += HideOnDelete;
			this.Shown += HighlightOnShown;
		}

		void MakeResultTreeView () 
		{
			Type [] types = new Type [] {
				typeof (Gdk.Pixbuf), // icon
				typeof (string),     // title
				typeof (string),     // match count
				typeof (Note),       // note
				typeof (ArrayList),  // matches
			};

			Gtk.TargetEntry [] targets = 
				new Gtk.TargetEntry [] {
					new Gtk.TargetEntry ("STRING", 
							     Gtk.TargetFlags.App,
							     0),
					new Gtk.TargetEntry ("text/plain", 
							     Gtk.TargetFlags.App,
							     0),
					new Gtk.TargetEntry ("text/uri-list", 
							     Gtk.TargetFlags.App,
							     1),
				};

			store = new Gtk.ListStore (types);
			store.SetSortFunc (3 /* note */,
					   new Gtk.TreeIterCompareFunc (CompareDates),
					   IntPtr.Zero, 
					   null);

			tree = new Gtk.TreeView (store);
			tree.HeadersVisible = true;
			tree.RulesHint = true;
			tree.RowActivated += OnRowActivated;
			tree.DragDataGet += OnDragDataGet;

			tree.EnableModelDragSource (Gdk.ModifierType.Button1Mask,
						    targets,
						    Gdk.DragAction.Copy);

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn title = new Gtk.TreeViewColumn ();
			title.Title = Catalog.GetString ("Search _Results");
			title.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			title.Resizable = true;
			
			renderer = new Gtk.CellRendererPixbuf ();
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "pixbuf", 0 /* icon */);

			renderer = new Gtk.CellRendererText ();
			title.PackStart (renderer, true);
			title.AddAttribute (renderer, "markup", 1 /* title */);

			renderer = new Gtk.CellRendererText ();
			renderer.Data ["xalign"] = 1.0;
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "text", 2 /* match count */);

			title.SortColumnId = 3; /* note */
			tree.AppendColumn (title);
		}

		class Match 
		{
			public NoteBuffer   Buffer;
			public Gtk.TextMark StartMark;
			public Gtk.TextMark EndMark;
			public bool         Highlighting;
		}

		void JumpToMatch (Match match)
		{
			NoteBuffer buffer = match.Buffer;

			Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);
			Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

			// Move cursor to end of match, and select match text
			buffer.PlaceCursor (end);
			buffer.MoveMark (buffer.SelectionBound, start);

			if (current_note != null) {
				Gtk.TextView editor = current_note.Window.Editor;
				editor.ScrollMarkOnscreen (buffer.InsertMark);
			}
		}

		ArrayList FindMatchesInBuffer (NoteBuffer buffer, string [] words, bool match_case)
		{
			ArrayList matches = new ArrayList ();

			string note_text = buffer.GetText (buffer.StartIter, 
							   buffer.EndIter, 
							   false /* hidden_chars */);
			if (!match_case)
				note_text = note_text.ToLower ();

			foreach (string word in words) {
				int idx = 0;
				bool this_word_found = false;

				if (word == String.Empty)
					continue;

				while (true) {					
					idx = note_text.IndexOf (word, idx);

					if (idx == -1) {
						if (this_word_found)
							break;
						else
							return null;
					}

					this_word_found = true;

					Gtk.TextIter start = buffer.GetIterAtOffset (idx);
					Gtk.TextIter end = start;
					end.ForwardChars (word.Length);

					Match match = new Match ();
					match.Buffer = buffer;
					match.StartMark = buffer.CreateMark (null, start, false);
					match.EndMark = buffer.CreateMark (null, end, true);
					match.Highlighting = false;

					matches.Add (match);

					idx += word.Length;
				}
			}

			if (matches.Count == 0)
				return null;
			else
				return matches;
		}

		bool CheckNoteHasMatch (Note note, string [] encoded_words, bool match_case)
		{
			string note_text = note.XmlContent;
			if (!match_case)
				note_text = note_text.ToLower ();

			foreach (string word in encoded_words) {
				if (note_text.IndexOf (word) > -1)
					continue;
				else
					return false;
			}

			return true;
		}

		void HighlightMatches (ArrayList matches, bool highlight)
		{
			if (matches == null)
				return;

			foreach (Match match in matches) {
				NoteBuffer buffer = match.Buffer;

				if (match.Highlighting != highlight) {
					Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);
					Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

					match.Highlighting = highlight && Visible;

					if (match.Highlighting)
						buffer.ApplyTag ("find-match", start, end);
					else
						buffer.RemoveTag ("find-match", start, end);
				}
			}
		}

		void CleanupMatches () 
		{
			if (current_matches != null) {
				HighlightMatches (current_matches, false /* unhighlight */);

				foreach (Match match in current_matches) {
					match.Buffer.DeleteMark (match.StartMark);
					match.Buffer.DeleteMark (match.EndMark);
				}

				current_matches = null;
			}

			find_next_button.Sensitive = false;
			find_prev_button.Sensitive = false;

			store.Clear ();
			tree.Sensitive = false;
		}

		// Signal handlers...

		void OnFindNextClicked (object sender, EventArgs args)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;

			Console.WriteLine ("Entering OnFindNextClicked...");

			for (int i = 0; i < current_matches.Count; i++) {
				Match match = (Match) current_matches [i];

				NoteBuffer buffer = match.Buffer;
				Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);
				Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);

				if (start.Offset >= cursor.Offset) {
					JumpToMatch (match);
					return;
				}
			}

			// Else wrap to first match
			JumpToMatch ((Match) current_matches [0]);

			Console.WriteLine ("Leaving OnFindNextClicked...");
		}

		void OnFindPreviousClicked (object sender, EventArgs args)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;

			for (int i = current_matches.Count; i > 0; i--) {
				Match match = (Match) current_matches [i - 1];

				NoteBuffer buffer = match.Buffer;
				Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);
				Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

				if (end.Offset < cursor.Offset) {
					JumpToMatch (match);
					return;
				}
			}

			// Wrap to first match
			JumpToMatch ((Match) current_matches [current_matches.Count - 1]);
		}

		void OnCloseClicked (object sender, EventArgs args)
		{
			if (current_matches != null)
				HighlightMatches (current_matches, false);

			Hide ();
		}

		void HideOnDelete (object sender, Gtk.DeleteEventArgs args)
		{
			OnCloseClicked (sender, null);
			args.RetVal = true;
		}

		void HighlightOnShown (object sender, EventArgs args)
		{
			if (current_matches != null)
				HighlightMatches (current_matches, true);
		}

		void OnCaseSensitiveToggled (object sender, EventArgs args)
		{
			find_combo.CaseSensitive = case_sensitive.Active;
			UpdateResults ();
		}

		void OnAllNotesToggled (object sender, EventArgs args)
		{
			if (search_all_notes.Active) {
				matches_window.Show ();
				Resizable = true;
				Title = Catalog.GetString ("Search All Notes");
			} else {
				matches_window.Hide ();
				Resizable = false;
				Title = Catalog.GetString ("Search Note");
			}

			UpdateResults ();
		}

		void OnDragDataGet (object sender, Gtk.DragDataGetArgs args)
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;

			if (!tree.Selection.GetSelected (out model, out iter))
				return;

			Note note = (Note) model.GetValue (iter, 3 /* note */);
			if (note == null)
				return;

			// FIXME: Gtk.SelectionData has no way to get the
			//        requested target.

			args.SelectionData.Set (Gdk.Atom.Intern ("text/uri-list", false),
						8,
						Encoding.UTF8.GetBytes (note.Uri));

			args.SelectionData.Text = note.Title;
		}

		void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			Gtk.TreeIter iter;
			if (!store.GetIter (out iter, args.Path)) 
				return;

			Note note = (Note) store.GetValue (iter, 3 /* note */);
			ArrayList matches = (ArrayList) store.GetValue (iter, 4 /* matches */);

			current_note = note;
			note.Window.Present ();

			if (current_matches != null)
				HighlightMatches (current_matches, false /* unhighlight */);

			HighlightMatches (matches, true /* highlight */);
			current_matches = matches;

			// We now definately have a current_note so allow
			// switching in and out of Search All...
			search_all_notes.Sensitive = true;

			find_next_button.Sensitive = true;
			find_prev_button.Sensitive = true;
		}

		void OnEntryActivated (object sender, EventArgs args)
		{
			if (entry_changed_timeout != null)
				entry_changed_timeout.Cancel ();

			// Update results
			EntryChangedTimeout (null, null);

			if (search_all_notes.Active) {
				// Highlight the result list
				tree.GrabFocus ();
			} else {
				// Jump to the first match. Activate so the
				// button visually clicks.
				if (find_next_button.Sensitive)
					ActivateDefault ();
			}
		}

		void OnEntryChanged (object sender, EventArgs args)
		{
			if (entry_changed_timeout == null) {
				entry_changed_timeout = new InterruptableTimeout ();
				entry_changed_timeout.Timeout += EntryChangedTimeout;
			}

			entry_changed_timeout.Reset (500);
		}

		// Called in after .5 seconds of typing inactivity, or on
		// explicit activate.  Redo the search, and update the
		// results...
		void EntryChangedTimeout (object sender, EventArgs args)
		{
			string text = find_combo.Entry.Text;
			if (text == null || text == String.Empty)
				return;

			UpdateResults ();
			AddToPreviousSearches (text);
		}

		void AddToPreviousSearches (string text)
		{
			// Update previous searches, by either creating the
			// initial list, or adding a new term to the list, or
			// shuffling an existing term to the top...
			if (previous_searches == null) {
				previous_searches = new string [] { text };
			} else {
				int existing_idx = Array.IndexOf (previous_searches, text);
				if (existing_idx > -1) {
					// move the current search to the front of the list
					string first = previous_searches [0];
					previous_searches [0] = previous_searches [existing_idx];
					previous_searches [existing_idx] = first;
				} else {
					string [] new_prev; 
					new_prev = new string [previous_searches.Length + 1];
					new_prev [0] = text;
					// copy starting at index 1
					previous_searches.CopyTo (new_prev, 1);
					previous_searches = new_prev;
				}
			}

			find_combo.PopdownStrings = previous_searches;
		}

		void AddManagerChangeListeners ()
		{
			// Update on changes to notes
			manager.NoteDeleted += OnNoteAddOrDelete;
			manager.NoteAdded += OnNoteAddOrDelete;
			manager.NoteRenamed += OnNoteRenamed;
		}

		void OnNoteAddOrDelete (object sender, Note changed)
		{
			UpdateResults ();
		}

		void OnNoteRenamed (Note note, string old_title)
		{
			UpdateResults ();
		}

		void AddNoteChangeListener ()
		{
			current_note.Buffer.InsertText += OnInsertText;
			current_note.Buffer.DeleteRange += OnDeleteRange;
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			UpdateResults ();
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			UpdateResults ();
		}

		void UpdateResults ()
		{
			CleanupMatches ();

			string text = find_combo.Entry.Text;
			if (text == null || text == String.Empty)
				return;

			if (!case_sensitive.Active)
				text = text.ToLower ();

			string [] words = text.Split (' ', '\t', '\n');

			// Used for matching in the raw note XML
			string [] encoded_words = XmlEncoder.Encode (text).Split (' ', '\t', '\n');

			if (search_all_notes.Active) {
				bool found_one = false;
				store.Clear ();

				// Append in reverse chrono order (the order
				// returned in manager.Notes)
				foreach (Note note in manager.Notes) {
					// Check the note's raw XML for at least
					// one match, to avoid deserializing
					// Buffers unnecessarily.
					if (!CheckNoteHasMatch (note, 
								encoded_words, 
								case_sensitive.Active))
						continue;

					ArrayList note_matches = 
						FindMatchesInBuffer (note.Buffer, 
								     words, 
								     case_sensitive.Active);

					if (note_matches == null) 
						continue;

					AppendResultTreeView (store, note, note_matches);

					if (current_note == note) {
						find_next_button.Sensitive = true;
						find_prev_button.Sensitive = true;

						current_matches = note_matches;
						HighlightMatches (current_matches, true);

						// FIXME: select this entry in
						// the treeview, and make the
						// text bold.
					}

					found_one = true;
				}

				if (found_one) 
					tree.Sensitive = true;
				else {
					tree.Sensitive = false;
					AppendNoMatchesTreeView (store);
				}
			}
			else if (current_note != null) {
				Console.WriteLine ("Looking for {0}", text);

				current_matches = FindMatchesInBuffer (current_note.Buffer, 
								       words, 
								       case_sensitive.Active);
				if (current_matches != null) {
					find_next_button.Sensitive = true;
					find_prev_button.Sensitive = true;

					HighlightMatches (current_matches, true);
				}
			}
		}

		[DllImport("libglib-2.0-0.dll")]
                static extern IntPtr g_markup_escape_text (string text, int len);

		// NOTE: Workaround a mapping bug in GLib.Markup.EscapeText,
		// where the length passed to g_markup_escape_text is passed the
		// character count, not the byte count of the string.
		static string MarkupEscapeText (string source)
		{
			int len = System.Text.Encoding.UTF8.GetByteCount (source);
			IntPtr markup = g_markup_escape_text (source, len);
			return GLib.Marshaller.PtrToStringGFree (markup);
		}

		void AppendResultTreeView (Gtk.ListStore store, Note note, ArrayList matches)
		{
			string title = MarkupEscapeText (note.Title);
			string match_cnt;

			match_cnt = 
			   String.Format (Catalog.GetPluralString ("({0} match)",
								   "({0} matches)",
								   matches.Count),
					  matches.Count);

			Gtk.TreeIter iter = store.Append ();
			store.SetValue (iter, 0 /* icon */, stock_notes);
			store.SetValue (iter, 1 /* title */, title);
			store.SetValue (iter, 2 /* match count */, match_cnt);
			store.SetValue (iter, 3 /* note */, note);
			store.SetValue (iter, 4 /* matches */, matches);
		}

		void AppendNoMatchesTreeView (Gtk.ListStore store)
		{
			Gtk.TreeIter iter = store.Append ();
			store.SetValue (iter, 
					1 /* title */, 
					Catalog.GetString ("No notes found"));
		}

		int CompareDates (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			Console.WriteLine ("CompareDates Called!");

			Note note_a = (Note) model.GetValue (a, 3 /* note */);
			Note note_b = (Note) model.GetValue (b, 3 /* note */);

			if (note_a == null || note_b == null)
				return -1;
			else
				return DateTime.Compare (note_a.ChangeDate, note_b.ChangeDate);
		}

		public Gtk.Button FindNextButton 
		{
			get { return find_next_button; }
		}

		public Gtk.Button FindPreviousButton 
		{
			get { return find_prev_button; }
		}

		public string SearchText
		{
			get { return find_combo.Entry.Text; }
			set {
				if (value != null)
					find_combo.Entry.Text = value;
			}
		}
	}
}
