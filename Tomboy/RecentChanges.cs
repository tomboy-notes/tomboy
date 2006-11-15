
using System;
using System.Collections;
using System.Text;
using Mono.Unix;

namespace Tomboy
{
	public class NoteRecentChanges : Gtk.Window
	{
		NoteManager manager;

		Gtk.AccelGroup accel_group;
		Gtk.ComboBoxEntry find_combo;
		Gtk.Button clear_search_button;
		Gtk.CheckButton case_sensitive;
		Gtk.Label note_count;
		Gtk.Button close_button;
		Gtk.ScrolledWindow matches_window;
		Gtk.VBox content_vbox;
		Gtk.TreeViewColumn matches_column;
		
		int search_hit_max;
		int search_hit_min;

		Gtk.TreeView tree;
		Gtk.ListStore store;
		
		Gtk.TreeModelFilter store_filter;
		Gtk.TreeModelSort store_sort;
		
		/// <summary>
		/// Stores search results as ArrayLists hashed by note uri.
		/// </summary>
		Hashtable current_matches;
		
		InterruptableTimeout entry_changed_timeout;
		
		static Type [] column_types = 
			new Type [] {
				typeof (Gdk.Pixbuf), // icon
				typeof (string),     // title
				typeof (string),     // change date
				typeof (Note),       // note
			};

		static Gdk.Pixbuf note_icon;
		static ArrayList previous_searches;
		static NoteRecentChanges instance;

		static NoteRecentChanges ()
		{
			note_icon = GuiUtils.GetIcon ("tomboy", 22);
		}

		public static NoteRecentChanges GetInstance (NoteManager manager)
		{
			if (instance == null)
				instance = new NoteRecentChanges (manager);
			System.Diagnostics.Debug.Assert (
				instance.manager == manager, 
				"Multiple NoteManagers not supported");
			return instance;
		}

		protected NoteRecentChanges (NoteManager manager)
			: base (Catalog.GetString ("Table of Contents"))
		{
			this.manager = manager;
			this.IconName = "tomboy";
			this.DefaultWidth = 200;
			this.current_matches = new Hashtable ();
			this.search_hit_max = 0;
			this.search_hit_min = 0;

			// For Escape (Close)
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			Gtk.Image image = new Gtk.Image (Gtk.Stock.SortAscending, 
							 Gtk.IconSize.Dialog);

			Gtk.Label label = new Gtk.Label (Catalog.GetString ("_Search:"));
			label.Xalign = 1;
			
			find_combo = Gtk.ComboBoxEntry.NewText ();
			label.MnemonicWidget = find_combo;
			find_combo.Changed += OnEntryChanged;
			find_combo.Entry.ActivatesDefault = false;
			find_combo.Entry.Activated += OnEntryActivated;
			if (previous_searches != null) {
				foreach (string prev in previous_searches) {
					find_combo.AppendText (prev);
				}
			}
			
			clear_search_button = new Gtk.Button (new Gtk.Image (Gtk.Stock.Clear, 
									     Gtk.IconSize.Menu));
			clear_search_button.Sensitive = false;
			clear_search_button.Clicked += ClearSearchClicked;
			clear_search_button.Show ();
			
			case_sensitive = 
				new Gtk.CheckButton (Catalog.GetString ("C_ase Sensitive"));
			case_sensitive.Toggled += OnCaseSensitiveToggled;

			Gtk.Table table = new Gtk.Table (2, 3, false);
			table.Attach (label, 0, 1, 0, 1, Gtk.AttachOptions.Shrink, 0, 0, 0);
			table.Attach (find_combo, 1, 2, 0, 1);
			table.Attach (case_sensitive, 1, 2, 1, 2);
			table.Attach (clear_search_button, 
				      2, 3, 0, 1, 
				      Gtk.AttachOptions.Shrink, 0, 0, 0);
			table.ColumnSpacing = 4;
			table.ShowAll ();

			Gtk.HBox hbox = new Gtk.HBox (false, 2);
			hbox.BorderWidth = 8;
			hbox.PackStart (image, false, false, 4);
			hbox.PackStart (table, true, true, 0);
			hbox.ShowAll ();

			MakeRecentTree ();
			tree.Show ();

			note_count = new Gtk.Label ();
			note_count.Show ();

			// Update on changes to notes
			manager.NoteDeleted += OnNotesChanged;
			manager.NoteAdded += OnNotesChanged;
			manager.NoteRenamed += OnNoteRenamed;

			// List all the current notes
			UpdateResults ();

			matches_window = new Gtk.ScrolledWindow ();
			matches_window.ShadowType = Gtk.ShadowType.In;

			// Reign in the window size if there are notes with long
			// names, or a lot of notes...

			Gtk.Requisition tree_req = tree.SizeRequest ();
			if (tree_req.Height > 420)
				matches_window.HeightRequest = 420;
			else
				matches_window.VscrollbarPolicy = Gtk.PolicyType.Never;

			if (tree_req.Width > 480)
				matches_window.WidthRequest = 480;
			else
				matches_window.HscrollbarPolicy = Gtk.PolicyType.Never;

			matches_window.Add (tree);
			matches_window.Show ();

			close_button = new Gtk.Button (Gtk.Stock.Close);
			close_button.Clicked += CloseClicked;
			close_button.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.Escape, 
						     0,
						     Gtk.AccelFlags.Visible);
			close_button.Show ();

			Gtk.HButtonBox button_box = new Gtk.HButtonBox ();
			button_box.Layout = Gtk.ButtonBoxStyle.Edge;
			button_box.Spacing = 8;
			button_box.PackStart (note_count);
			button_box.PackStart (close_button);
			button_box.Show ();

			content_vbox = new Gtk.VBox (false, 8);
			content_vbox.BorderWidth = 6;
			content_vbox.PackStart (hbox, false, false, 0);
			content_vbox.PackStart (matches_window);
			content_vbox.PackStart (button_box, false, false, 0);
			content_vbox.Show ();

			this.Add (content_vbox);
			this.DeleteEvent += OnDelete;
		}

		void MakeRecentTree ()
		{
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

			tree = new Gtk.TreeView ();
			tree.HeadersVisible = true;
			tree.RulesHint = true;
			tree.RowActivated += OnRowActivated;
			tree.DragDataGet += OnDragDataGet;
			tree.KeyPressEvent += OnKeyPressed;

			tree.EnableModelDragSource (Gdk.ModifierType.Button1Mask,
						    targets,
						    Gdk.DragAction.Copy);

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn title = new Gtk.TreeViewColumn ();
			title.Title = Catalog.GetString ("Note");
			title.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			title.Resizable = true;
			
			renderer = new Gtk.CellRendererPixbuf ();
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "pixbuf", 0 /* icon */);

			renderer = new Gtk.CellRendererText ();
			title.PackStart (renderer, true);
			title.AddAttribute (renderer, "text", 1 /* title */);
			title.SortColumnId = 1; /* title */
			title.SortIndicator = false;
			title.Reorderable = false;
			title.SortOrder = Gtk.SortType.Ascending;

			tree.AppendColumn (title);

			Gtk.TreeViewColumn change = new Gtk.TreeViewColumn ();
			change.Title = Catalog.GetString ("Last Changed");
			change.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			change.Resizable = true;

			renderer = new Gtk.CellRendererText ();
			renderer.Data ["xalign"] = 1.0;
			change.PackStart (renderer, false);
			change.AddAttribute (renderer, "text", 2 /* change date */);
			change.SortColumnId = 2; /* change date */
			change.SortIndicator = false;
			change.Reorderable = false;
			change.SortOrder = Gtk.SortType.Descending;

			tree.AppendColumn (change);
		}

		void UpdateResults ()
		{
			// FIXME: Restore the currently highlighted note

			int sort_column = 2; /* change date */
			Gtk.SortType sort_type = Gtk.SortType.Descending;
			if (store != null) {
				store.GetSortColumnId (out sort_column, out sort_type);
			}

			store = new Gtk.ListStore (column_types);
			
			store_filter = new Gtk.TreeModelFilter (store, null);
			store_filter.VisibleFunc = FilterNotes;
			store_sort = new Gtk.TreeModelSort (store_filter);
			store_sort.SetSortFunc (1 /* title */,
				new Gtk.TreeIterCompareFunc (CompareTitles));
			store_sort.SetSortFunc (2 /* change date */,
				new Gtk.TreeIterCompareFunc (CompareDates));

			int cnt = 0;
			foreach (Note note in manager.Notes) {
				string nice_date = PrettyPrintDate (note.ChangeDate);

				store.AppendValues (note_icon,  /* icon */
						    note.Title, /* title */
						    nice_date,  /* change date */
						    note);      /* note */
				cnt++;
			}

			// Set the sort column after loading data, since we
			// don't want to resort on every append.
			store_sort.SetSortColumnId (sort_column, sort_type);

			tree.Model = store_sort;
			
			note_count.Text = string.Format (
				Catalog.GetPluralString("Total: {0} note",
							"Total: {0} notes",
							cnt),
				cnt);

			PerformSearch ();
		}
		
		void PerformSearch ()
		{
			string text = SearchText;
			if (text == null) {
				RemoveMatchesColumn ();
				current_matches.Clear ();
				store_filter.Refilter ();
				UpdateNoteCount (store.IterNChildren (), -1);
				if (tree.IsRealized)
					tree.ScrollToPoint (0, 0);
				return;
			}
			
			if (!case_sensitive.Active)
				text = text.ToLower ();
			
			string [] words = text.Split (' ', '\t', '\n');
			
			// Used for matching in the raw note XML
			string [] encoded_words = XmlEncoder.Encode (text).Split (' ', '\t', '\n');
			
			bool found_one = false;
			search_hit_max = 0;
			search_hit_min = Int32.MaxValue;
			current_matches.Clear ();
			
			foreach (Note note in manager.Notes) {
				// Check the note's raw XML for at least
				// one match, to avoid deserializing
				// Buffers unnecessarily.
				if (!CheckNoteHasMatch (note, encoded_words, case_sensitive.Active))
					continue;
				
				ArrayList note_matches =
					FindMatchesInBuffer (note.Buffer, 
							     words, 
							     case_sensitive.Active);
				
				if (note_matches == null)
					continue;
				
				int match_count = note_matches.Count;
				
				// Update the max and min hits
				if (match_count > search_hit_max)
					search_hit_max = match_count;
				if (match_count < search_hit_min)
					search_hit_min = match_count;

				current_matches [note.Uri] = note_matches;
				found_one = true;
			}
			
			if (found_one)
				UpdateNoteCount (store.IterNChildren (), current_matches.Count);
			else
				UpdateNoteCount (store.IterNChildren (), 0);

			AddMatchesColumn ();
			store_filter.Refilter ();
			tree.ScrollToPoint (0, 0);
		}
		
		void AddMatchesColumn ()
		{
			if (matches_column == null) {
				Gtk.CellRenderer renderer;
				
				matches_column = new Gtk.TreeViewColumn ();
				matches_column.Title = Catalog.GetString ("Matches");
				matches_column.Sizing = Gtk.TreeViewColumnSizing.Fixed;
				matches_column.Resizable = false;
				
				renderer = new Gtk.CellRendererText ();
				renderer.Width = 75;
				matches_column.PackStart (renderer, false);
				matches_column.SetCellDataFunc (
					renderer, 
					new Gtk.TreeCellDataFunc (MatchesColumnDataFunc));
				matches_column.SortColumnId = 4;
				matches_column.SortIndicator = false;
				matches_column.Reorderable = false;
				matches_column.SortOrder = Gtk.SortType.Descending;
				store_sort.SetSortFunc (4 /* matches */,
					new Gtk.TreeIterCompareFunc (CompareSearchHits));
			}
			
			if (tree.Columns.Length < 3)
				tree.AppendColumn (matches_column);

			store_sort.SetSortColumnId (4, Gtk.SortType.Descending);
		}
		
		void RemoveMatchesColumn ()
		{
			if (matches_column == null)
				return;

			tree.RemoveColumn (matches_column);
			matches_column = null;
			
			store_sort.SetSortColumnId (2, Gtk.SortType.Descending);
		}
		
		void MatchesColumnDataFunc (Gtk.TreeViewColumn column, 
					    Gtk.CellRenderer cell,
					    Gtk.TreeModel model, 
					    Gtk.TreeIter iter)
		{
			Gtk.CellRendererText crt = cell as Gtk.CellRendererText;
			if (crt == null)
				return;
			
			string match_cnt = "";
			
			Note note = (Note) model.GetValue (iter, 3 /* note */);
			if (note != null) {
				ArrayList matches = current_matches [note.Uri] as ArrayList;
				if (matches != null && matches.Count > 0) {
					match_cnt = string.Format (
						Catalog.GetPluralString ("{0} match",
									 "{0} matches",
									 matches.Count),
						matches.Count);
				}
			}
			
			crt.Text = match_cnt;
		}
		
		void UpdateNoteCount (int total, int matches)
		{
			string cnt = string.Format (
				Catalog.GetPluralString("Total: {0} note",
							"Total: {0} notes",
							total), 
				total);
			if (matches >= 0) {
				cnt += ", " + string.Format (
					Catalog.GetPluralString ("{0} match",
								 "{0} matches",
								 matches),
					matches);
			}
			note_count.Text = cnt;
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
		
		class Match 
		{
			public NoteBuffer   Buffer;
			public Gtk.TextMark StartMark;
			public Gtk.TextMark EndMark;
			public bool         Highlighting;
		}

		/// <summary>
		/// Filter out notes based on the current search string
		/// </summary>
		bool FilterNotes (Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			if (SearchText == null)
				return true;
			
			if (current_matches.Count == 0)
				return false;

			Note note = (Note) model.GetValue (iter, 3 /* note */);
			if (note == null)
				return false;

			ArrayList matches = current_matches [note.Uri] as ArrayList;
			if (matches == null)
				return false;
			else
				return true;
		}

		void OnCaseSensitiveToggled (object sender, EventArgs args)
		{
			PerformSearch ();
		}

		void OnNotesChanged (object sender, Note changed)
		{
			UpdateResults ();
		}

		void OnNoteRenamed (Note note, string old_title)
		{
			UpdateResults ();
		}

		void OnDragDataGet (object sender, Gtk.DragDataGetArgs args)
		{
			Note note = GetSelectedNote ();
			if (note == null)
				return;

			// FIXME: Gtk.SelectionData has no way to get the
			//        requested target.

			args.SelectionData.Set (Gdk.Atom.Intern ("text/uri-list", false),
						8,
						Encoding.UTF8.GetBytes (note.Uri));

			args.SelectionData.Text = note.Title;
		}

		Note GetSelectedNote ()
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;

			if (!tree.Selection.GetSelected (out model, out iter))
				return null;

			return (Note) model.GetValue (iter, 3 /* note */);
		}

		string PrettyPrintDate (DateTime date)
		{
			DateTime now = DateTime.Now;
			string short_time = date.ToShortTimeString ();

			if (date.Year == now.Year) {
				if (date.DayOfYear == now.DayOfYear)
					return String.Format (Catalog.GetString ("Today, {0}"), 
							      short_time);
				else if (date.DayOfYear == now.DayOfYear - 1)
					return String.Format (Catalog.GetString ("Yesterday, {0}"),
							      short_time);
				else if (date.DayOfYear > now.DayOfYear - 6)
					return String.Format (
						Catalog.GetString ("{0} days ago, {1}"), 
						now.DayOfYear - date.DayOfYear,
						short_time);
				else
					return date.ToString (
						Catalog.GetString ("MMMM d, h:mm tt"));
			} else
				return date.ToString (Catalog.GetString ("MMMM d yyyy, h:mm tt"));
		}

		void CloseClicked (object sender, EventArgs args)
		{
			Hide ();
			Destroy ();
			instance = null;
		}
		
		void OnDelete (object sender, Gtk.DeleteEventArgs args)
		{
			CloseClicked (sender, EventArgs.Empty);
			args.RetVal = true;
		}
		
		protected override void OnShown ()
		{
			find_combo.Entry.GrabFocus ();
			base.OnShown ();
		}
		
		int CompareTitles (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			string title_a = model.GetValue (a, 1 /* title */) as string;
			string title_b = model.GetValue (b, 1 /* title */) as string;
			
			if (title_a == null || title_b == null)
				return -1;
			
			return title_a.CompareTo (title_b);
		}
		
		int CompareDates (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			Note note_a = (Note) model.GetValue (a, 3 /* note */);
			Note note_b = (Note) model.GetValue (b, 3 /* note */);

			if (note_a == null || note_b == null)
				return -1;
			else
				return DateTime.Compare (note_a.ChangeDate, note_b.ChangeDate);
		}
		
		int CompareSearchHits (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			Note note_a = model.GetValue (a, 3 /* note */) as Note;
			Note note_b = model.GetValue (b, 3 /* note */) as Note;
			
			if (note_a == null || note_b == null) {
				return -1;
			}
			
			ArrayList matches_a = current_matches [note_a.Uri] as ArrayList;
			ArrayList matches_b = current_matches [note_b.Uri] as ArrayList;
			
			if (matches_a == null || matches_b == null) {
				if (matches_a != null)
					return 1;

				return -1;
			}
			
			int result = matches_a.Count - matches_b.Count;
			if (result == 0) {
				// Do a secondary sort by note title in alphabetical order
				result = CompareTitles (model, a, b);
				
				// Make sure to always sort alphabetically
				if (result != 0) {
					int sort_col_id;
					Gtk.SortType sort_type;
					if (store_sort.GetSortColumnId (out sort_col_id, 
									out sort_type)) {
						if (sort_type == Gtk.SortType.Descending)
							result = result * -1;	// reverse sign
					}
				}
				
				return result;
			}
			
			return result;
		}

		void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			Gtk.TreeIter iter;
			if (!store_sort.GetIter (out iter, args.Path)) 
				return;

			Note note = (Note) store_sort.GetValue (iter, 3 /* note */);

			note.Window.Present ();
			
			// Tell the new window to highlight the matches and
			// prepopulate the Firefox-style search
			if (SearchText != null) {
				NoteFindBar find = note.Window.Find;
				find.ShowAll ();
				find.Visible = true;
				find.SearchText = SearchText;
			}
		}

		void OnKeyPressed (object obj, Gtk.KeyPressEventArgs args)
		{
			Gdk.EventKey eventKey = args.Event;

			if (eventKey.Key == Gdk.Key.Delete && 
			    eventKey.State == 0 /* Gdk.ModifierType.None */) {
				// Get the selected note and prompt for deletion
				Note note = GetSelectedNote();
				if (note == null)
					return;

				NoteUtils.ShowDeletionDialog (note, this);
			}		
		}
		
		void OnEntryActivated (object sender, EventArgs args)
		{
			if (entry_changed_timeout != null)
				entry_changed_timeout.Cancel ();
			
			EntryChangedTimeout (null, null);
		}

		void OnEntryChanged (object sender, EventArgs args)
		{
			if (entry_changed_timeout == null) {
				entry_changed_timeout = new InterruptableTimeout ();
				entry_changed_timeout.Timeout += EntryChangedTimeout;
			}
			
			if (SearchText == null) {
				clear_search_button.Sensitive = false;
				PerformSearch ();
			} else {
				entry_changed_timeout.Reset (500);
				clear_search_button.Sensitive = true;
			}
		}
		
		// Called in after .5 seconds of typing inactivity, or on
		// explicit activate.  Redo the search, and update the
		// results...
		void EntryChangedTimeout (object sender, EventArgs args)
		{
			if (SearchText == null)
				return;

			PerformSearch ();
			AddToPreviousSearches (SearchText);
		}

		void AddToPreviousSearches (string text)
		{
			// Update previous searches, by adding a new term to the
			// list, or shuffling an existing term to the top...

			if (previous_searches == null)
				previous_searches = new ArrayList ();

			bool repeat = false;

			if (case_sensitive.Active) {
				repeat = previous_searches.Contains (text);
			} else {
				string lower = text.ToLower();
				foreach (string prev in previous_searches) {
					if (prev.ToLower() == lower)
						repeat = true;
				}
			}

			if (!repeat) {
				previous_searches.Insert (0, text);
				find_combo.PrependText (text);
			}
		}
		
		void ClearSearchClicked (object sender, EventArgs args)
		{
			find_combo.Entry.Text = "";
			find_combo.Entry.GrabFocus ();
		}
		
		public string SearchText
		{
			get {
				string text = find_combo.Entry.Text;
				text = text.Trim ();
				if (text == String.Empty)
					return null;
				return text;
			}
			set {
				if (value != null && value != "")
					find_combo.Entry.Text = value;
			}
		}
	}
}
