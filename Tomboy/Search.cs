
using System;
using System.Collections;

namespace Tomboy 
{
	public class NoteFindDialog : Gtk.Window
	{
		NoteManager manager;
		NoteBuffer owning_buffer;

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

		ArrayList current_matches;
		uint changed_timeout_id = 0;
		Gtk.Window last_opened_window;

		static string [] previous_searches;
		static Gdk.Pixbuf search_image;
		static Gdk.Pixbuf stock_notes;

		static NoteFindDialog ()
		{
			search_image = new Gdk.Pixbuf (null, "gnome-stock-searchtool.png");
			stock_notes = GuiUtils.GetMiniIcon ("stock_notes.png");
		}

		public NoteFindDialog (NoteManager manager, NoteBuffer buffer, string search_string)
			: this (manager, false)
		{
			owning_buffer = buffer;

			if (search_string != null) {
				find_combo.Entry.Text = search_string;
				UpdateResults ();
			}
		}

		public NoteFindDialog (NoteManager manager, bool search_all)
			: base (search_all ? "Search Notes" : "Search in Note")
		{
			this.manager = manager;
			this.Icon = search_image;

			// For Escape (Close), Ctrl-G (Find next), and
			// Ctrl-Shift-G (Find Previous)
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			// Allow resizing if showing the results list
			this.Resizable = search_all;

			Gtk.Label label = new Gtk.Label ("Find:");

			find_combo = new Gtk.Combo ();
			find_combo.AllowEmpty = false;
			find_combo.CaseSensitive = false;
			find_combo.Entry.ActivatesDefault = true;
			find_combo.Entry.Changed += new EventHandler (OnEntryChanged);
			if (previous_searches != null)
				find_combo.PopdownStrings = previous_searches;

			search_all_notes = new Gtk.CheckButton ("Search _All Notes");
			search_all_notes.Active = search_all;
			search_all_notes.Sensitive = !search_all;
			search_all_notes.Toggled += new EventHandler (OnAllNotesToggled);

			case_sensitive = new Gtk.CheckButton ("_Case Sensitive");
			case_sensitive.Toggled += new EventHandler (OnCaseSensitiveToggled);

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

			// Buttons at bottom: close, previous, find next

			close_button = new Gtk.Button (Gtk.Stock.Close);
			close_button.Clicked += new EventHandler (OnCloseClicked);
			close_button.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.Escape, 
						     0,
						     Gtk.AccelFlags.Visible);

			find_prev_button = 
				GuiUtils.MakeImageButton (Gtk.Stock.GoBack, "_Previous");
			find_prev_button.Clicked += new EventHandler (OnFindPreviousClicked);
			find_prev_button.Sensitive = false;
			find_prev_button.AddAccelerator ("activate",
							 accel_group,
							 (uint) Gdk.Key.g, 
							 (Gdk.ModifierType.ControlMask | 
							  Gdk.ModifierType.ShiftMask),
							 Gtk.AccelFlags.Visible);

			find_next_button = 
				GuiUtils.MakeImageButton (Gtk.Stock.GoForward, "Find _Next");
			find_next_button.Clicked += new EventHandler (OnFindNextClicked);
			find_next_button.Sensitive = false;
			find_next_button.AddAccelerator ("activate",
							 accel_group,
							 (uint) Gdk.Key.g, 
							 Gdk.ModifierType.ControlMask,
							 Gtk.AccelFlags.Visible);

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
			this.DeleteEvent += new Gtk.DeleteEventHandler (HideOnDelete);
			this.Shown += new EventHandler (HighlightOnShown);
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

			store = new Gtk.ListStore (types);
			store.SetSortFunc (3 /* note */,
					   new Gtk.TreeIterCompareFunc (CompareDates),
					   IntPtr.Zero, 
					   null);

			tree = new Gtk.TreeView (store);
			tree.HeadersVisible = true;
			tree.RulesHint = true;

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn title = new Gtk.TreeViewColumn ();
			title.Title = "Search Results";
			title.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			title.Resizable = true;
			
			renderer = new Gtk.CellRendererPixbuf ();
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "pixbuf", 0 /* icon */);

			renderer = new Gtk.CellRendererText ();
			title.PackStart (renderer, true);
			title.AddAttribute (renderer, "text", 1 /* title */);

			renderer = new Gtk.CellRendererText ();
			renderer.Data ["xalign"] = 1.0;
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "text", 2 /* match count */);

			title.SortColumnId = 3; /* note */
			tree.AppendColumn (title);

			tree.RowActivated += new Gtk.RowActivatedHandler (OnRowActivated);
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
		}

		ArrayList FindMatches (NoteBuffer buffer, string [] words, bool match_case)
		{
			ArrayList matches = new ArrayList ();

			string note_text = buffer.GetText (buffer.StartIter, 
							   buffer.EndIter, 
							   false /* hidden_chars */);
			if (!match_case)
				note_text = note_text.ToLower ();

			foreach (string word in words) {
				int idx = 0;

				while (true) {
					Console.WriteLine ("Looking for {0} at offset {1}", 
							   word, 
							   idx);

					if (match_case)
						idx = note_text.IndexOf (word, idx);
					else {
						string lower_word = word.ToLower ();
						idx = note_text.IndexOf (lower_word, idx);
					}

					if (idx == -1)
						break;

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

		void HighlightMatches (ArrayList matches, bool highlight)
		{
			if (matches == null)
				return;

			foreach (Match match in matches) {
				NoteBuffer buffer = match.Buffer;

				if (match.Highlighting != highlight) {
					Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);
					Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

					if (highlight)
						buffer.ApplyTag ("find-match", start, end);
					else
						buffer.RemoveTag ("find-match", start, end);

					match.Highlighting = highlight;
				}
			}
		}

		void CleanupMatches (ArrayList matches) 
		{
			HighlightMatches (matches, false /* unhighlight */);

			foreach (Match match in matches) {
				match.Buffer.DeleteMark (match.StartMark);
				match.Buffer.DeleteMark (match.EndMark);
			}
		}

		// Signal handlers...

		void OnFindNextClicked (object sender, EventArgs args)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;

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

			// CleanupMatches?

			last_opened_window = null;
			this.TransientFor = null;

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
			UpdateResults ();
		}

		void OnAllNotesToggled (object sender, EventArgs args)
		{
			if (search_all_notes.Active) {
				matches_window.Show ();
				Resizable = true;
			} else {
				matches_window.Hide ();
				Resizable = false;
			}

			UpdateResults ();
		}

		void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			Gtk.TreeIter iter;
			if (!store.GetIter (out iter, args.Path)) 
				return;

			Note note = (Note) store.GetValue (iter, 3 /* note */);

			// If the note window was not already open, hide it
			// when we select another row...
			if (!note.Window.IsMapped) {
				// Hide the window we opened last...
				if (last_opened_window != null &&
				    last_opened_window != note.Window) {
					last_opened_window.Hide ();
					this.TransientFor = null;
				}

				last_opened_window = note.Window;
				this.TransientFor = last_opened_window;
			}
			note.Window.Present ();
			this.Present ();

			find_next_button.Sensitive = true;
			find_prev_button.Sensitive = true;

			if (current_matches != null)
				HighlightMatches (current_matches, false /* unhighlight */);

			current_matches = (ArrayList) store.GetValue (iter, 4 /* matches */);
			HighlightMatches (current_matches, true);
		}

		void OnEntryChanged (object sender, EventArgs args)
		{
			if (changed_timeout_id != 0)
				GLib.Source.Remove (changed_timeout_id);

			changed_timeout_id = 
				GLib.Timeout.Add (500, new GLib.TimeoutHandler (UpdateResults));
		}

		// Called in after .25 seconds of typing inactivity.  Redo the
		// search, and update the results...
		bool UpdateResults ()
		{
			if (current_matches != null) {
				CleanupMatches (current_matches);
				current_matches = null;

				find_next_button.Sensitive = false;
				find_prev_button.Sensitive = false;
			}

			string text = find_combo.Entry.Text;

			if (text == null || text == String.Empty) {
				changed_timeout_id = 0;
				return false;
			}

			string [] words = text.Split (' ', '\t', '\n');

			if (search_all_notes.Active) {
				bool found_one = false;
				store.Clear ();

				// Append in reverse chrono order (the order
				// returned in manager.Notes)
				foreach (Note note in manager.Notes) {
					ArrayList note_matches = 
						FindMatches (note.Buffer, 
							     words, 
							     case_sensitive.Active);

					if (note_matches != null) {
						AppendResultTreeView (store, note, note_matches);
						found_one = true;
					}
				}

				if (found_one) 
					tree.Sensitive = true;
				else {
					tree.Sensitive = false;
					AppendNoMatchesTreeView (store);
				}
			}
			else if (owning_buffer != null) {
				Console.WriteLine ("Looking for {0}", find_combo.Entry.Text);

				current_matches = FindMatches (owning_buffer, 
							       words, 
							       case_sensitive.Active);
				if (current_matches != null) {
					find_next_button.Sensitive = true;
					find_prev_button.Sensitive = true;

					HighlightMatches (current_matches, true);
				}
			}

			if (previous_searches == null)
				previous_searches = new string [] { find_combo.Entry.Text };
			else {
				string [] new_prev = new string [previous_searches.Length + 1];
				previous_searches.CopyTo (new_prev, 1); // copy starting at idx 1
				previous_searches = new_prev;
				previous_searches [0] = find_combo.Entry.Text;
			}

			find_combo.PopdownStrings = previous_searches;

			changed_timeout_id = 0;
			return false;
		}

		void AppendResultTreeView (Gtk.ListStore store, Note note, ArrayList matches)
		{
			string match_cnt;

			if (matches.Count > 1)
				match_cnt = String.Format ("({0} matches)", matches.Count);
			else
				match_cnt = String.Format ("({0} match)", matches.Count);

			Gtk.TreeIter iter = store.Append ();
			store.SetValue (iter, 0 /* icon */, stock_notes);
			store.SetValue (iter, 1 /* title */, note.Title);
			store.SetValue (iter, 2 /* match count */, match_cnt);
			store.SetValue (iter, 3 /* note */, note);
			store.SetValue (iter, 4 /* matches */, matches);
		}

		void AppendNoMatchesTreeView (Gtk.ListStore store)
		{
			Gtk.TreeIter iter = store.Append ();
			store.SetValue (iter, 1 /* title */, "No notes found");
		}

		int CompareDates (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			Console.WriteLine ("CompareDates Called!");

			Note note_a = (Note) model.GetValue (a, 3 /* note */);
			Note note_b = (Note) model.GetValue (b, 3 /* note */);

			if (note_a == null || note_b == null)
				return -1;
			else
				return DateTime.Compare (note_a.SaveDate, note_b.SaveDate);
		}

		public Gtk.Button FindNextButton 
		{
			get { return find_next_button; }
		}

		public Gtk.Button FindPreviousButton 
		{
			get { return find_prev_button; }
		}

		public NoteBuffer OwningBuffer 
		{
			get { return owning_buffer; }
			set {
				if (owning_buffer != value) {
					owning_buffer = value;
					if (find_combo.Entry.Text != null)
						UpdateResults ();
				}
			}
		}
	}
}
