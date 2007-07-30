
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mono.Unix;

namespace Tomboy
{
	public class NoteRecentChanges : ForcedPresentWindow
	{
		NoteManager manager;

		Gtk.MenuBar menu_bar;
		Gtk.ComboBoxEntry find_combo;
		Gtk.Button clear_search_button;
		Gtk.CheckButton case_sensitive;
		Gtk.Label note_count;
		Gtk.ScrolledWindow matches_window;
		Gtk.VBox content_vbox;
		Gtk.TreeViewColumn matches_column;

//		Gtk.TreeView tags_tree;
//		Gtk.TreeModel tags_store;
		// Use the following like a Set
//		Dictionary<Tag, Tag> selected_tags;
		
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
			note_icon = GuiUtils.GetIcon ("tomboy-note", 22);
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
			: base (Catalog.GetString ("Search All Notes"))
		{
			this.manager = manager;
			this.IconName = "tomboy";
			this.DefaultWidth = 200;
			this.current_matches = new Hashtable ();
			
//			selected_tags = new Dictionary<Tag, Tag> ();
			
			AddAccelGroup (Tomboy.ActionManager.UI.AccelGroup);

			menu_bar = CreateMenuBar ();
			
			Gtk.Image image = new Gtk.Image (GuiUtils.GetIcon ("system-search", 48));

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
			
//			tags_tree = MakeTagsTree ();
//			tags_store = TagManager.Tags;
//			TagManager.TagRemoved += OnTagRemoved;
//			tags_tree.Model = tags_store;
//			tags_tree.Show ();

//			Gtk.ScrolledWindow tags_sw = new Gtk.ScrolledWindow ();
//			tags_sw.ShadowType = Gtk.ShadowType.In;

			// Reign in the window size if there are tags with long
			// names, or a lot of tags...

//			Gtk.Requisition tags_tree_req = tags_tree.SizeRequest ();
//			if (tags_tree_req.Width > 150)
//				tags_sw.WidthRequest = 150;
			
//			tags_sw.Add (tags_tree);
//			tags_sw.Show ();

			MakeRecentTree ();
			tree.Show ();

			note_count = new Gtk.Label ();
			note_count.Xalign = 0;
			note_count.Show ();

			// Update on changes to notes
			manager.NoteDeleted += OnNotesChanged;
			manager.NoteAdded += OnNotesChanged;
			manager.NoteRenamed += OnNoteRenamed;
			manager.NoteSaved += OnNoteSaved;

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
			
//			Gtk.HPaned hpaned = new Gtk.HPaned ();
//			hpaned.Position = 150;
//			hpaned.Add1 (tags_sw);
//			hpaned.Add2 (matches_window);
//			hpaned.Show ();

			Gtk.HBox status_box = new Gtk.HBox (false, 8);
			status_box.PackStart (note_count, true, true, 0);
			status_box.Show ();
			
			Gtk.VBox vbox = new Gtk.VBox (false, 8);
			vbox.BorderWidth = 6;
			vbox.PackStart (hbox, false, false, 0);
//			vbox.PackStart (hpaned, true, true, 0);
			vbox.PackStart (matches_window, true, true, 0);
			vbox.PackStart (status_box, false, false, 0);
			vbox.Show ();

			// Use another VBox to place the MenuBar
			// right at thetop of the window.
			content_vbox = new Gtk.VBox (false, 0);
			content_vbox.PackStart (menu_bar, false, false, 0);
			content_vbox.PackStart (vbox, true, true, 0);
			content_vbox.Show ();

			this.Add (content_vbox);
			this.DeleteEvent += OnDelete;
			this.KeyPressEvent += OnKeyPressed; // For Escape
		}
		
		Gtk.MenuBar CreateMenuBar ()
		{
			ActionManager am = Tomboy.ActionManager;
			Gtk.MenuBar menubar =
				am.GetWidget ("/MainWindowMenubar") as Gtk.MenuBar;
			
			am ["OpenNoteAction"].Activated += OnOpenNote;
			am ["DeleteNoteAction"].Activated += OnDeleteNote;
			am ["CloseWindowAction"].Activated += OnCloseWindow;
			if (Tomboy.TrayIconShowing == false)
				am ["CloseWindowAction"].Visible = false;
			
			// Allow Escape to close the window as well as <Control>W
			// Should be able to add Escape to the CloseAction.  Can't do that
			// until someone fixes AccelGroup.Connect:
			//     http://bugzilla.ximian.com/show_bug.cgi?id=76988)
			//
			//	am.UI.AccelGroup.Connect ((uint) Gdk.Key.Escape,
			//			0,
			//			Gtk.AccelFlags.Mask,
			//			OnCloseWindow);
			
			return menubar;
		}
		
/*
		Gtk.TreeView MakeTagsTree ()
		{
			Gtk.TreeView t;
			
			t = new Gtk.TreeView ();
			t.HeadersVisible = true;
			t.RulesHint = true;
			
			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn tags_column = new Gtk.TreeViewColumn ();
			tags_column.Title = Catalog.GetString ("Tags");
			tags_column.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			tags_column.Resizable = false;

			renderer = new Gtk.CellRendererToggle ();
			(renderer as Gtk.CellRendererToggle).Toggled += OnTagToggled;
			tags_column.PackStart (renderer, false);
			tags_column.SetCellDataFunc (renderer,
					new Gtk.TreeCellDataFunc (TagsToggleCellDataFunc));

			renderer = new Gtk.CellRendererText ();
			tags_column.PackStart (renderer, true);
			tags_column.SetCellDataFunc (renderer,
					new Gtk.TreeCellDataFunc (TagsNameCellDataFunc));

			t.AppendColumn (tags_column);
			
			return t;
		}
*/
		
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
			tree.Selection.Changed += OnSelectionChanged;
			tree.ButtonPressEvent += OnButtonPressed;

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
			// Restore the currently highlighted note
			Note selected_note = null;
			Gtk.TreeIter selected_iter;
			if (store_sort != null && 
					tree.Selection.GetSelected (out selected_iter)) {
				selected_note =
					(Note) store_sort.GetValue (selected_iter, 3 /* note */);
			}

			int sort_column = 2; /* change date */
			Gtk.SortType sort_type = Gtk.SortType.Descending;
			if (store_sort != null)
				store_sort.GetSortColumnId (out sort_column, out sort_type);

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
				string nice_date =
					GuiUtils.GetPrettyPrintDate (note.ChangeDate, true);

				Gtk.TreeIter iter =
					store.AppendValues (note_icon,  /* icon */
						    note.Title, /* title */
						    nice_date,  /* change date */
						    note);      /* note */
				cnt++;
			}

			tree.Model = store_sort;
			
			note_count.Text = string.Format (
				Catalog.GetPluralString("Total: {0} note",
							"Total: {0} notes",
							cnt),
				cnt);

			PerformSearch ();

			if (sort_column >= 0) {
				// Set the sort column after loading data, since we
				// don't want to resort on every append.
				store_sort.SetSortColumnId (sort_column, sort_type);
			}
			
			// Restore the previous selection
			if (selected_note != null) {
				SelectNote (selected_note);
			}
		}
		
		void SelectNote (Note note)
		{
			Gtk.TreeIter iter;
			
			if (store_sort.IterChildren (out iter) == false)
				return;
			
			do {
				Note iter_note = (Note) store_sort.GetValue (iter, 3 /* note */);
				if (iter_note == note) {
					// Found it!
					tree.Selection.SelectIter (iter);
					break;
				}
			} while (store_sort.IterNext (ref iter));
		}
		
		void PerformSearch ()
		{
			// For some reason, the matches column must be rebuilt
			// every time because otherwise, it's not sortable.
			RemoveMatchesColumn ();

			string text = SearchText;
			if (text == null) {
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
			
			current_matches.Clear ();
			
			foreach (Note note in manager.Notes) {
				// Check the note's raw XML for at least one
				// match, to avoid deserializing Buffers
				// unnecessarily.
				if (CheckNoteHasMatch (note, 
						       encoded_words, 
						       case_sensitive.Active)) {
					int match_count =
							FindMatchCountInNote (note.TextContent,
													words,
													case_sensitive.Active);
					if (match_count > 0)
						current_matches [note.Uri] = match_count;
				}
			}
			
			UpdateNoteCount (store.IterNChildren (), current_matches.Count);
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
				matches_column.SortIndicator = true;
				matches_column.Reorderable = false;
				matches_column.SortOrder = Gtk.SortType.Descending;
				matches_column.Clickable = true;
				store_sort.SetSortFunc (4 /* matches */,
					new Gtk.TreeIterCompareFunc (CompareSearchHits));

				tree.AppendColumn (matches_column);
				store_sort.SetSortColumnId (4, Gtk.SortType.Descending);
			}
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
			
			string match_str = "";
			
			Note note = (Note) model.GetValue (iter, 3 /* note */);
			if (note != null) {
				object matches = current_matches [note.Uri];
				if (matches != null) {
					int match_count = (int) matches;
					if (match_count > 0) {
						match_str = string.Format (
							Catalog.GetPluralString ("{0} match",
										 "{0} matches",
										 match_count),
							match_count);
					}
				}
			}
			
			crt.Text = match_str;
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
		
		int FindMatchCountInNote (string note_text, string [] words, bool match_case)
		{
			int matches = 0;
			
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
							return 0;
					}
					
					this_word_found = true;
					
					matches++;
					
					idx += word.Length;
				}
			}
			
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
		/// and selected tags.
		/// </summary>
		bool FilterNotes (Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			Note note = model.GetValue (iter, 3 /* note */) as Note;
			if (note == null)
				return false;
			
			bool passes_search_filter = FilterBySearch (note);
			if (passes_search_filter == false)
				return false; // don't waste time checking tags if it's already false
			
//			bool passes_tag_filter = FilterByTag (note);
			
//			// Must pass both filters to appear in the list
//			return passes_tag_filter && passes_search_filter;
			return true;
		}
		
		// <summary>
		// Return true if the specified note should be shown in the list
		// based on the current selection of tags.  If no tags are selected,
		// all notes should be allowed.
		// </summary>
//		bool FilterByTag (Note note)
//		{
//			if (selected_tags.Count == 0)
//				return true;
//			
//			// FIXME: Ugh!  NOT an O(1) operation.  Is there a better way?
//			List<Tag> tags = note.Tags;
//			foreach (Tag tag in note.Tags) {
//				if (selected_tags.ContainsKey (tag))
//					return true;
//			}
//			
//			return false;
//		}
		
		// <summary>
		// Return true if the specified note should be shown in the list
		// based on the search string.  If no search string is specified,
		// all notes should be allowed.
		// </summary>
		bool FilterBySearch (Note note)
		{
			if (SearchText == null)
				return true;
			
			if (current_matches.Count == 0)
				return false;

			return note != null && current_matches [note.Uri] != null;
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
		
		void OnNoteSaved (Note note)
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
		
		void OnSelectionChanged (object sender, EventArgs args)
		{
			Note note = GetSelectedNote ();
			if (note != null) {
				Tomboy.ActionManager ["OpenNoteAction"].Sensitive = true;
				Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = true;
			} else {
				Tomboy.ActionManager ["OpenNoteAction"].Sensitive = false;
				Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = false;
			}
		}
		
		[GLib.ConnectBefore]
		void OnButtonPressed (object sender, Gtk.ButtonPressEventArgs args)
		{
			switch (args.Event.Button) {
			case 3: // third mouse button (right-click)
				Gtk.TreePath path = null;
				Gtk.TreeViewColumn column = null;
				
				if (tree.GetPathAtPos ((int) args.Event.X,
						(int) args.Event.Y,
						out path,
						out column) == false)
					break;
				
				Gtk.TreeSelection selection = tree.Selection;
				if (selection.CountSelectedRows () == 0)
					break;
				
				PopupContextMenuAtLocation ((int) args.Event.X,
						(int) args.Event.Y);

				break;
			}
		}
		
		void PopupContextMenuAtLocation (int x, int y)
		{
			Gtk.Menu menu = Tomboy.ActionManager.GetWidget (
					"/MainWindowContextMenu") as Gtk.Menu;
			menu.ShowAll ();
			Gtk.MenuPositionFunc pos_menu_func = null;
			
			// Set up the funtion to position the context menu
			// if we were called by the keyboard Gdk.Key.Menu.
			if (x == 0 && y == 0)
				pos_menu_func = PositionContextMenu;
				
			menu.Popup (null, null,
					pos_menu_func,
					0,
					Gtk.Global.CurrentEventTime);
		}
		
		// This is needed for when the user opens
		// the context menu with the keyboard.
		void PositionContextMenu (Gtk.Menu menu,
				out int x, out int y, out bool push_in)
		{
			Gtk.TreeIter iter;
			Gtk.TreePath path;
			Gtk.TreeSelection selection;
			
			// Set default "return" values
			push_in = false; // not used
			x = 0;
			y = 0;
			
			selection = tree.Selection;
			if (!selection.GetSelected (out iter))
				return;
			
			path = store_sort.GetPath (iter);
			
			int pos_x = 0;
			int pos_y = 0;
			
			GetWidgetScreenPos (tree, ref pos_x, ref pos_y);
			Gdk.Rectangle cell_rect = tree.GetCellArea (path, tree.Columns [0]);
			
			// Add 100 to x so it's not be at the far left
			x = pos_x + cell_rect.X + 100;
			y = pos_y + cell_rect.Y;
		}
		
		// Walk the widget hiearchy to figure out
		// where to position the context menu.
		void GetWidgetScreenPos (Gtk.Widget widget, ref int x, ref int y)
		{
			int widget_x;
			int widget_y;
			
			if (widget is Gtk.Window) {
				((Gtk.Window) widget).GetPosition (out widget_x, out widget_y);
			} else {
				GetWidgetScreenPos (widget.Parent, ref x, ref y);
				
				// Special case the TreeView because it adds
				// too much since it's in a scrolled window.
				if (widget == tree) {
					widget_x = 2;
					widget_y = 2;
				} else {
					Gdk.Rectangle widget_rect = widget.Allocation;
					widget_x = widget_rect.X;
					widget_y = widget_rect.Y;
				}
			}
			
			x += widget_x;
			y += widget_y;
		}

		Note GetSelectedNote ()
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;

			if (!tree.Selection.GetSelected (out model, out iter))
				return null;

			return (Note) model.GetValue (iter, 3 /* note */);
		}

		void OnOpenNote (object sender, EventArgs args)
		{
			Note note = GetSelectedNote ();
			if (note == null)
				return;
				
			note.Window.Present ();
		}
		
		void OnDeleteNote (object sender, EventArgs args)
		{
			Note note = GetSelectedNote ();
			if (note == null)
				return;
			
			NoteUtils.ShowDeletionDialog (note, this);
		}
		
		void OnCloseWindow (object sender, EventArgs args)
		{
			// Disconnect external signal handlers to prevent bloweup
			manager.NoteDeleted -= OnNotesChanged;
			manager.NoteAdded -= OnNotesChanged;
			manager.NoteRenamed -= OnNoteRenamed;
			manager.NoteSaved -= OnNoteSaved;
			
			// The following code has to be done for the MenuBar to
			// appear properly the next time this window is opened.
			if (menu_bar != null) {
				content_vbox.Remove (menu_bar);
				ActionManager am = Tomboy.ActionManager;
				am ["OpenNoteAction"].Activated -= OnOpenNote;
				am ["DeleteNoteAction"].Activated -= OnDeleteNote;
				am ["CloseWindowAction"].Activated -= OnCloseWindow;
			}
			
			Hide ();
			Destroy ();
			instance = null;

			if (Tomboy.TrayIconShowing == false)
				Tomboy.ActionManager ["QuitTomboyAction"].Activate ();
		}
		
		void OnDelete (object sender, Gtk.DeleteEventArgs args)
		{
			OnCloseWindow (sender, EventArgs.Empty);
			args.RetVal = true;
		}
		
		void OnKeyPressed (object sender, Gtk.KeyPressEventArgs args)
		{
			switch (args.Event.Key) {
			case Gdk.Key.Escape:
				// Allow Escape to close the window
				OnCloseWindow (this, EventArgs.Empty);
				break;
			case Gdk.Key.Menu:
				// Pop up the context menu if a note is selected
				Note note = GetSelectedNote ();
				if (note != null)
					PopupContextMenuAtLocation (0, 0);

				break;
			}
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
			
			object matches_a = current_matches [note_a.Uri];
			object matches_b = current_matches [note_b.Uri];
			
			if (matches_a == null || matches_b == null) {
				if (matches_a != null)
					return 1;

				return -1;
			}
			
			int result = (int) matches_a - (int) matches_b;
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
		
/*
		// <summary>
		// Pay attention to when tags are removed so selected_tags
		// remains up-to-date if a selected tag is removed from
		// the system.
		// </summary>
		void OnTagRemoved (string tag_name)
		{
			if (selected_tags.Count == 0)
				return;
			
			Tag tag_to_remove = null;
			foreach (Tag tag in selected_tags.Keys) {	
				if (string.Compare (tag.NormalizedName, tag_name) == 0) {
					tag_to_remove = tag;
					break;
				}
			}
			
			if (tag_to_remove != null) {
				selected_tags.Remove (tag_to_remove);
				UpdateResults ();
			}
		}
		
		void OnTagToggled (object sender, Gtk.ToggledArgs args)
		{
			Gtk.CellRendererToggle crt = sender as Gtk.CellRendererToggle;

			Gtk.TreePath path = new Gtk.TreePath (args.Path);
			Gtk.TreeIter iter;
			if (tags_store.GetIter (out iter, path) == false)
				return;
			
			Tag tag = tags_store.GetValue (iter, 0) as Tag;
			if (tag == null)
				return;
			
			if (crt.Active) {
				// Uncheck the tag (remove the tag from selected_tags)
				if (selected_tags.ContainsKey (tag))
					selected_tags.Remove (tag);
			} else {
				// Check the tag (add it to selected_tags)
				selected_tags [tag] = tag;
			}
			
			UpdateResults ();
		}
		
		void TagsToggleCellDataFunc (Gtk.TreeViewColumn tree_column,
				Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
				Gtk.TreeIter iter)
		{
			Gtk.CellRendererToggle crt = cell as Gtk.CellRendererToggle;
			Tag tag = tree_model.GetValue (iter, 0) as Tag;
			if (tag == null)
				crt.Active = false;
			else {
				crt.Active = selected_tags.ContainsKey (tag);
			}
		}
		
		void TagsNameCellDataFunc (Gtk.TreeViewColumn tree_column,
				Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
				Gtk.TreeIter iter)
		{
			Gtk.CellRendererText crt = cell as Gtk.CellRendererText;
			Tag tag = tree_model.GetValue (iter, 0) as Tag;
			if (tag == null)
				crt.Text = String.Empty;
			else
				crt.Text = tag.Name;
		}
*/
		
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
