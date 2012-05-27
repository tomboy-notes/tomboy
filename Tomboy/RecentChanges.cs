using System;
using System.Collections.Generic;
using System.Text;
using Mono.Unix;
using Gtk;

namespace Tomboy
{
	public class NoteRecentChanges : ForcedPresentWindow
	{

		private enum Target
		{
			Text,
			Uri,
			Path,
		}

		NoteManager manager;

		Gtk.MenuBar menu_bar;
		Gtk.ComboBoxEntry find_combo;
		Gtk.Button clear_search_button;
		Gtk.Statusbar status_bar;
		Gtk.ScrolledWindow matches_window;
		Gtk.HPaned hpaned;
		Gtk.VBox content_vbox;
		Gtk.TreeViewColumn matches_column;
		Gtk.HBox no_matches_box;	//to display a message with buttons when no results are found.
		
		Notebooks.NotebooksTreeView notebooksTree;

		// Use the following like a Set
		Dictionary<Tag, Tag> selected_tags;

		Gtk.TreeView tree;
		Gtk.ListStore store;
		Gtk.TreeModelFilter store_filter;
		Gtk.TreeModelSort store_sort;

		/// <summary>
		/// Stores search results as integers hashed by note uri.
		/// </summary>
		Dictionary<string, int> current_matches;

		InterruptableTimeout entry_changed_timeout;

		Gtk.TargetEntry [] targets;
		int clickX, clickY;

		static Type [] column_types =
		new Type [] {
			typeof (Gdk.Pixbuf), // icon
			typeof (string),     // title
			typeof (string),     // change date
			typeof (Note),       // note
		};

		static Gdk.Pixbuf note_icon;
		static Gdk.Pixbuf all_notes_icon;
		static Gdk.Pixbuf unfiled_notes_icon;
		static Gdk.Pixbuf notebook_icon;
		static List<string> previous_searches;
		static NoteRecentChanges instance;

		static NoteRecentChanges ()
		{
			note_icon = GuiUtils.GetIcon ("note", 22);
			all_notes_icon = GuiUtils.GetIcon ("filter-note-all", 22);
			unfiled_notes_icon = GuiUtils.GetIcon ("filter-note-unfiled", 22);
			notebook_icon = GuiUtils.GetIcon ("notebook", 22);
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
			this.DefaultWidth = 450;
			this.DefaultHeight = 400;
			this.current_matches = new Dictionary<string, int> ();
			this.Resizable = true;

			selected_tags = new Dictionary<Tag, Tag> ();

			AddAccelGroup (Tomboy.ActionManager.UI.AccelGroup);

			menu_bar = CreateMenuBar ();

			Gtk.Label label = new Gtk.Label (Catalog.GetString ("_Search:"));
			label.Xalign = 0.0f;

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

			Gtk.Table table = new Gtk.Table (1, 3, false);
			table.Attach (label, 0, 1, 0, 1,
			              Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);
			table.Attach (find_combo, 1, 2, 0, 1,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);
			table.Attach (clear_search_button,
				      2, 3, 0, 1,
			              Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);
			table.ColumnSpacing = 4;
			table.ShowAll ();

			Gtk.HBox hbox = new Gtk.HBox (false, 0);
			hbox.PackStart (table, true, true, 0);
			hbox.ShowAll ();

			// Notebooks Pane
			Gtk.Widget notebooksPane = MakeNotebooksPane ();
			notebooksPane.Show ();

			MakeRecentTree ();
			tree.Show ();

			status_bar = new Gtk.Statusbar ();
			status_bar.HasResizeGrip = true;
			status_bar.Show ();

			// Update on changes to notes
			manager.NoteDeleted += OnNotesDeleted;
			manager.NoteAdded += OnNotesChanged;
			manager.NoteRenamed += OnNoteRenamed;
			manager.NoteSaved += OnNoteSaved;

			// List all the current notes
			UpdateResults ();

			matches_window = new Gtk.ScrolledWindow ();
			matches_window.ShadowType = Gtk.ShadowType.In;

			matches_window.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			matches_window.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			matches_window.Add (tree);
			matches_window.Show ();

			hpaned = new Gtk.HPaned ();
			hpaned.Position = 150;
			hpaned.Add1 (notebooksPane);
			hpaned.Add2 (matches_window);
			hpaned.Show ();

			RestorePosition ();

			Gtk.VBox vbox = new Gtk.VBox (false, 8);
			vbox.BorderWidth = 6;
			vbox.PackStart (hbox, false, false, 4);
			vbox.PackStart (hpaned, true, true, 0);
			vbox.PackStart (status_bar, false, false, 0);
			vbox.Show ();

			// Use another VBox to place the MenuBar
			// right at thetop of the window.
			content_vbox = new Gtk.VBox (false, 0);
#if !MAC
			content_vbox.PackStart (menu_bar, false, false, 0);
#endif
			content_vbox.PackStart (vbox, true, true, 0);
			content_vbox.Show ();

			this.Add (content_vbox);
			this.DeleteEvent += OnDelete;
			this.KeyPressEvent += OnKeyPressed; // For Escape

			// Watch when notes are added to notebooks so the search
			// results will be updated immediately instead of waiting
			// until the note's QueueSave () kicks in.
			Notebooks.NotebookManager.NoteAddedToNotebook += OnNoteAddedToNotebook;
			Notebooks.NotebookManager.NoteRemovedFromNotebook += OnNoteRemovedFromNotebook;
			
			// Set the focus chain for the top-most containers Bug #512175
			Gtk.Widget[] vbox_focus = new Gtk.Widget[2];
			vbox_focus[0] = hbox;
			vbox_focus[1] = hpaned;
			vbox.FocusChain = vbox_focus;

			// Set focus chain for sub widgits of first top-most container
			Gtk.Widget[] table_focus = new Gtk.Widget[2];
			table_focus[0] = find_combo;
			table_focus[1] = matches_window;
			hbox.FocusChain = table_focus;
			
			// set focus chain for sub widgits of seconf top-most container
			Gtk.Widget[] hpaned_focus = new Gtk.Widget[2];
			hpaned_focus[0] = matches_window;
			hpaned_focus[1] = notebooksPane;
			hpaned.FocusChain = hpaned_focus;
			
			// get back to the beginning of the focus chain
			Gtk.Widget[] scroll_right = new Gtk.Widget[1];
			scroll_right[0] = tree;
			matches_window.FocusChain = scroll_right;
			
			Tomboy.ExitingEvent += OnExitingEvent;
		}

		public new void Present ()
		{
			base.Present ();
			
			find_combo.Entry.GrabFocus ();
		}

		Gtk.MenuBar CreateMenuBar ()
		{
			ActionManager am = Tomboy.ActionManager;
			Gtk.MenuBar menubar =
				am.GetWidget ("/MainWindowMenubar") as Gtk.MenuBar;

			am ["OpenNoteAction"].Activated += OnOpenNote;
			am ["DeleteNoteAction"].Activated += OnDeleteNote;
			am ["NewNotebookNoteAction"].Activated += OnNewNotebookNote;
			am ["OpenNotebookTemplateNoteAction"].Activated += OnOpenNotebookTemplateNote;
			am ["NewNotebookAction"].Activated += OnNewNotebook;
			am ["DeleteNotebookAction"].Activated += OnDeleteNotebook;
			am ["CloseWindowAction"].Activated += OnCloseWindow;
			if (Tomboy.TrayIconShowing == false &&
			    (bool) Preferences.Get (Preferences.ENABLE_TRAY_ICON))
				am ["CloseWindowAction"].Visible = false;

			// Allow Escape to close the window as well as <Control>W
			// Should be able to add Escape to the CloseAction.  Can't do that
			// until someone fixes AccelGroup.Connect:
			//     http://bugzilla.ximian.com/show_bug.cgi?id=76988)
			//
			// am.UI.AccelGroup.Connect ((uint) Gdk.Key.Escape,
			//   0,
			//   Gtk.AccelFlags.Mask,
			//   OnCloseWindow);

			return menubar;
		}

		Gtk.Widget MakeNotebooksPane ()
		{
			notebooksTree = new Notebooks.NotebooksTreeView (Notebooks.NotebookManager.NotebooksWithSpecialItems);
			notebooksTree.Selection.Mode = Gtk.SelectionMode.Single;
			notebooksTree.HeadersVisible = true;
			notebooksTree.RulesHint = false;

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn column = new Gtk.TreeViewColumn ();
			column.Title = Catalog.GetString ("Notebooks");
			column.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			column.Resizable = false;

			renderer = new Gtk.CellRendererPixbuf ();
			column.PackStart (renderer, false);
			column.SetCellDataFunc (renderer,
				new Gtk.TreeCellDataFunc (NotebookPixbufCellDataFunc));

			var textRenderer = new Gtk.CellRendererText ();
			// TODO: Make special notebooks' rows uneditable
			textRenderer.Editable = true;
			column.PackStart (textRenderer, true);
			column.SetCellDataFunc (textRenderer,
				new Gtk.TreeCellDataFunc (NotebookTextCellDataFunc));
			textRenderer.Edited += OnNotebookRowEdited;

			notebooksTree.AppendColumn (column);

			notebooksTree.RowActivated += OnNotebookRowActivated;
			notebooksTree.Selection.Changed += OnNotebookSelectionChanged;
			notebooksTree.ButtonPressEvent += OnNotebooksTreeButtonPressed;
			notebooksTree.KeyPressEvent += OnNotebooksKeyPressed;

			notebooksTree.Show ();
			Gtk.ScrolledWindow sw = new Gtk.ScrolledWindow ();
			sw.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.ShadowType = Gtk.ShadowType.In;
			sw.Add (notebooksTree);
			sw.Show ();

			return sw;
		}

		void MakeRecentTree ()
		{
		    targets =
			new Gtk.TargetEntry [] {
				new Gtk.TargetEntry ("STRING",
				Gtk.TargetFlags.App,
				(uint) Target.Text),
				new Gtk.TargetEntry ("text/plain",
				Gtk.TargetFlags.App,
				(uint) Target.Text),
				new Gtk.TargetEntry ("text/uri-list",
				Gtk.TargetFlags.App,
				(uint) Target.Uri),
				new Gtk.TargetEntry ("text/path-list",
				Gtk.TargetFlags.App,
				(uint) Target.Path),
			};

			tree = new RecentTreeView ();
			tree.HeadersVisible = true;
			tree.RulesHint = true;
			tree.RowActivated += OnRowActivated;
			tree.Selection.Mode = Gtk.SelectionMode.Multiple;
			tree.Selection.Changed += OnSelectionChanged;
			tree.ButtonPressEvent += OnTreeViewButtonPressed;
			tree.KeyPressEvent += OnTreeViewKeyPressed;
			tree.MotionNotifyEvent += OnTreeViewMotionNotify;
			tree.ButtonReleaseEvent += OnTreeViewButtonReleased;
			tree.DragDataGet += OnTreeViewDragDataGet;
			tree.FocusInEvent += OnTreeViewFocused;
			tree.FocusOutEvent += OnTreeViewFocusedOut;

			tree.EnableModelDragSource (Gdk.ModifierType.Button1Mask | Gdk.ModifierType.Button3Mask,
						    targets,
						    Gdk.DragAction.Move);

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn title = new Gtk.TreeViewColumn ();
			title.Title = Catalog.GetString ("Note");
			title.MinWidth = 150; // Fix for bgo 575337 - "Matches" column causes notes name not to be shown. jjennings jul 13, 2011
			title.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			title.Expand = true;
			title.Resizable = true;

			renderer = new Gtk.CellRendererPixbuf ();
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "pixbuf", 0 /* icon */);

			renderer = new Gtk.CellRendererText ();
			(renderer as Gtk.CellRendererText).Ellipsize =
				Pango.EllipsizeMode.End;
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
			change.Resizable = false;

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
			// Save the currently selected notes
			List<Note> selected_notes = GetSelectedNotes ();

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

					store.AppendValues (note_icon,  /* icon */
							    note.Title, /* title */
							    nice_date,  /* change date */
							    note);      /* note */
				cnt++;
			}

			tree.Model = store_sort;

			PerformSearch ();

			if (sort_column >= 0) {
				// Set the sort column after loading data, since we
				// don't want to resort on every append.
				store_sort.SetSortColumnId (sort_column, sort_type);
			}

			// Restore the previous selection
			if (selected_notes != null && selected_notes.Count > 0) {
				SelectNotes (selected_notes);
			}
		}

		void SelectNotes (List<Note> notes)
		{
			Gtk.TreeIter iter;

			if (store_sort.IterChildren (out iter) == false)
				return;

			do {
				Note iter_note = (Note) store_sort.GetValue (iter, 3 /* note */);
				if (notes.IndexOf (iter_note) >= 0) {
					// Found one
					tree.Selection.SelectIter (iter);
					//ScrollToIter (tree, iter);
				}
			} while (store_sort.IterNext (ref iter));
		}

		private void ScrollToIter (Gtk.TreeView tree, Gtk.TreeIter iter)
		{
			Gtk.TreePath path = tree.Model.GetPath (iter);
			if (path != null)
				tree.ScrollToCell (path, null, false, 0, 0);
		}

		void PerformSearch()
		{
			// For some reason, the matches column must be rebuilt
			// every time because otherwise, it's not sortable.
			RemoveMatchesColumn ();
			Search search = new Search(manager);

			string text = SearchText;
			if (text == null) {
				current_matches.Clear ();
				store_filter.Refilter ();
				UpdateTotalNoteCount (store_sort.IterNChildren ());
				if (tree.IsRealized)
					tree.ScrollToPoint (0, 0);
				return;
			}
			text = text.ToLower ();

			current_matches.Clear ();

			// Search using the currently selected notebook
			Notebooks.Notebook selected_notebook = GetSelectedNotebook ();
			if (selected_notebook is Notebooks.SpecialNotebook)
				selected_notebook = null;

			IDictionary<Note,int> results =
				search.SearchNotes(text, false, selected_notebook);
			
			// if no results found in current notebook ask user whether
			// to search in all notebooks
			if (results.Count == 0 && selected_notebook != null) {
				NoMatchesFoundAction ();
			}
			else {
				foreach (Note note in results.Keys){
					current_matches.Add (note.Uri, results[note]);
				}

				AddMatchesColumn ();
				store_filter.Refilter ();
				tree.ScrollToPoint (0, 0);
				UpdateMatchNoteCount (current_matches.Count);
			}
		}

		void AddMatchesColumn ()
		{
			if (matches_column == null) {
				Gtk.CellRenderer renderer;

				matches_column = new Gtk.TreeViewColumn ();
				matches_column.Title = Catalog.GetString ("Matches");
				matches_column.Sizing = Gtk.TreeViewColumnSizing.Autosize;
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
				int match_count;
				if (current_matches.TryGetValue (note.Uri, out match_count)) {
					if (match_count == int.MaxValue) {
						match_str = string.Format (
								    Catalog.GetString ("Title match"));
					} else if (match_count > 0) {
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

		void UpdateTotalNoteCount (int total)
		{
			string status = string.Format (
					Catalog.GetPluralString ("Total: {0} note",
								 "Total: {0} notes",
								 total),
					total);
			status_bar.Pop (0);
			status_bar.Push (0, status);
		}

		void UpdateMatchNoteCount (int matches)
		{
			string status = string.Format (
					Catalog.GetPluralString ("Matches: {0} note",
								 "Matches: {0} notes",
								 matches),
					matches);
			status_bar.Pop (0);
			status_bar.Push (0, status);
		}
		
		// called when no search results are found in the selected notebook
		void NoMatchesFoundAction ()
		{
			hpaned.Remove (matches_window);
			String message = Catalog.GetString ("No results found " +
				"in the selected notebook.\nClick here to " +
				"search across all notes.");
			Gtk.LinkButton link_button = new Gtk.LinkButton ("", message);
			Gtk.LinkButton.SetUriHook(ShowAllSearchResults);
			link_button.TooltipText = Catalog.GetString 
				("Click here to search across all notebooks");
			link_button.Show();
			Gtk.Table no_matches_found_table = new Gtk.Table (1, 3, false);
			no_matches_found_table.Attach (link_button, 1, 2, 0, 1,
			                               Gtk.AttachOptions.Fill | Gtk.AttachOptions.Shrink,
			                 Gtk.AttachOptions.Shrink,
			                0, 0
			              );
			
			no_matches_found_table.ColumnSpacing = 4;
			no_matches_found_table.ShowAll ();
			no_matches_box = new HBox (false, 0);
			no_matches_box.PackStart (no_matches_found_table, true, true, 0);
			no_matches_box.Show ();
			hpaned.Add2 (no_matches_box);
		}
		
		void RestoreMatchesWindow ()
		{
			if (no_matches_box != null) {
				hpaned.Remove (no_matches_box);
				hpaned.Add2 (matches_window);
				no_matches_box = null;
				RestorePosition();
			}	
		}
		
		private void ShowAllSearchResults (Gtk.LinkButton button, String param)
		{
			TreeIter iter;
			notebooksTree.Model.GetIterFirst (out iter);
			notebooksTree.Selection.SelectIter (iter);
		}		
		

		/// <summary>
		/// Filter out notes based on the current search string
		/// and selected tags.  Also prevent template notes from
		/// appearing.
		/// </summary>
		bool FilterNotes (Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			Note note = model.GetValue (iter, 3 /* note */) as Note;
			if (note == null)
				return false;

			// Don't show the template notes in the list
			Tag template_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);
			if (note.ContainsTag (template_tag))
				return false;

			Notebooks.Notebook selected_notebook = GetSelectedNotebook ();
			if (selected_notebook is Notebooks.UnfiledNotesNotebook) {
				// If the note belongs to a notebook, return false
				// since the only notes that should be shown in this
				// case are notes that are unfiled (not in a notebook).
				if (Notebooks.NotebookManager.GetNotebookFromNote (note) != null)
					return false;
			}

			bool passes_search_filter = FilterBySearch (note);
			if (passes_search_filter == false)
				return false; // don't waste time checking tags if it's already false

			bool passes_tag_filter = FilterByTag (note);

			// Must pass both filters to appear in the list
			return passes_tag_filter && passes_search_filter;
		       // return true;
		}

		bool FilterTags (Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			Tag t = model.GetValue (iter, 0 /* note */) as Tag;
			if(t.IsProperty || t.IsSystem)
				return false;

			return true;
		}

		// <summary>
		// Return true if the specified note should be shown in the list
		// based on the current selection of tags.  If no tags are selected,
		// all notes should be allowed.
		// </summary>
		bool FilterByTag (Note note)
		{
			if (selected_tags.Count == 0)
				return true;

			// FIXME: Ugh!  NOT an O(1) operation.  Is there a better way?
			foreach (Tag tag in note.Tags) {
				if (selected_tags.ContainsKey (tag))
					return true;
			}

			return false;
		}

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

			return note != null && current_matches.ContainsKey (note.Uri);
		}

		void OnCaseSensitiveToggled (object sender, EventArgs args)
		{
			PerformSearch ();
		}
		
		void OnNotesDeleted (object sender, Note deleted)
		{
			RestoreMatchesWindow ();
		}

		void OnNotesChanged (object sender, Note changed)
		{
			RestoreMatchesWindow ();
			UpdateResults ();
		}

		void OnNoteRenamed (Note note, string old_title)
		{
			RestoreMatchesWindow ();
			UpdateResults ();
		}

		void OnNoteSaved (Note note)
		{
			var rect = tree.VisibleRect;
			RestoreMatchesWindow ();
			UpdateResults ();
			tree.ScrollToPoint (rect.X, rect.Y);
		}

		void OnTreeViewDragDataGet (object sender, Gtk.DragDataGetArgs args)
		{
			List<Note> selected_notes = GetSelectedNotes ();
			if (selected_notes == null || selected_notes.Count == 0)
				return;

			string uris = string.Empty;
			string paths = string.Empty;
			foreach (Note note in selected_notes) {
				uris += note.Uri + "\r\n";
				paths += "file://" + note.FilePath + "\r\n";
			}

			if(args.Info == (uint) Target.Path)
				args.SelectionData.Set (Gdk.Atom.Intern ("text/path-list", false),
							8,
							Encoding.UTF8.GetBytes (paths));

			else
				args.SelectionData.Set (Gdk.Atom.Intern ("text/uri-list", false),
							8,
							Encoding.UTF8.GetBytes (uris));

			if (selected_notes.Count == 1)
				args.SelectionData.Text = selected_notes [0].Title;
			else
				args.SelectionData.Text = Catalog.GetString ("Notes");
		}

		void OnNotebookRowEdited (object sender, Gtk.EditedArgs args)
		{
			if (Notebooks.NotebookManager.NotebookExists (args.NewText) ||
			    string.IsNullOrEmpty (args.NewText))
				return;
			var oldNotebook = GetSelectedNotebook ();
			if (oldNotebook is Notebooks.SpecialNotebook)
				return;
			var newNotebook = Notebooks.NotebookManager.GetOrCreateNotebook (args.NewText);
			Logger.Debug ("Renaming notebook '{0}' to '{1}'",
			              oldNotebook.Name,
			              args.NewText);
			foreach (Note note in oldNotebook.Tag.Notes)
				Notebooks.NotebookManager.MoveNoteToNotebook (note, newNotebook);
			Notebooks.NotebookManager.DeleteNotebook (oldNotebook);
			Gtk.TreeIter iter;
			if (Notebooks.NotebookManager.GetNotebookIter (newNotebook, out iter)) {
				// TODO: Why doesn't this work?
				notebooksTree.Selection.SelectIter (iter);
			}
		}

		void OnSelectionChanged (object sender, EventArgs args)
		{
			List<Note> selected_notes = GetSelectedNotes ();
			if (selected_notes == null || selected_notes.Count == 0) {
				Tomboy.ActionManager ["OpenNoteAction"].Sensitive = false;
				Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = false;
			} else if (selected_notes.Count > 0) {
				Tomboy.ActionManager ["OpenNoteAction"].Sensitive = true;
				Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = true;
			} else {
				// Many notes are selected
				Tomboy.ActionManager ["OpenNoteAction"].Sensitive = false;
				Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = true;
			}
		}

		[GLib.ConnectBefore]
		void OnTreeViewButtonPressed (object sender, Gtk.ButtonPressEventArgs args)
		{
			if (args.Event.Window != this.tree.BinWindow) {
				return;
			}

			Gtk.TreePath path = null;
			Gtk.TreeViewColumn column = null;

			tree.GetPathAtPos ((int)args.Event.X, (int)args.Event.Y,
							   out path, out column);
			if (path == null)
				return;

			clickX = (int)args.Event.X;
			clickY = (int)args.Event.Y;

			switch (args.Event.Type) {
			case Gdk.EventType.TwoButtonPress:
				if (args.Event.Button != 1 || (args.Event.State &
						(Gdk.ModifierType.ControlMask | Gdk.ModifierType.ShiftMask)) != 0) {
					break;
				}

				tree.Selection.UnselectAll ();
				tree.Selection.SelectPath (path);
				tree.ActivateRow (path, column);
				break;
			case Gdk.EventType.ButtonPress:
				if (args.Event.Button == 3) {
					Gtk.Menu menu = Tomboy.ActionManager.GetWidget (
						"/MainWindowContextMenu") as Gtk.Menu;
					PopupContextMenuAtLocation (menu,
						(int)args.Event.X,
						(int)args.Event.Y);

					// Return true so that the base handler won't
					// run, which causes the selection to change to
					// the row that was right-clicked.
					args.RetVal = true;
					break;
				}

				if (tree.Selection.PathIsSelected (path) && (args.Event.State &
						(Gdk.ModifierType.ControlMask | Gdk.ModifierType.ShiftMask)) == 0) {
					if (column != null && args.Event.Button == 1) {
						Gtk.CellRenderer renderer = column.CellRenderers [0];
						Gdk.Rectangle background_area = tree.GetBackgroundArea (path, column);
						Gdk.Rectangle cell_area = tree.GetCellArea (path, column);

						renderer.Activate (args.Event,
										   tree,
										   path.ToString (),
										   background_area,
										   cell_area,
										   Gtk.CellRendererState.Selected);

						Gtk.TreeIter iter;
						if (tree.Model.GetIter (out iter, path)) {
							tree.Model.EmitRowChanged (path, iter);
						}
					}

					args.RetVal = true;
				}

				break;
			default:
				args.RetVal = false;
				break;
			}
		}

		[GLib.ConnectBefore]
		void OnTreeViewKeyPressed (object sender, Gtk.KeyPressEventArgs args)
		{
			switch (args.Event.Key) {
			case Gdk.Key.Menu:
				// Pop up the context menu if a note is selected
				List<Note> selected_notes = GetSelectedNotes ();
				if (selected_notes != null && selected_notes.Count > 0) {
						Gtk.Menu menu = Tomboy.ActionManager.GetWidget (
						"/MainWindowContextMenu") as Gtk.Menu;
					PopupContextMenuAtLocation (menu, 0, 0);
					args.RetVal = true;
				}

				break;
			case Gdk.Key.Return:
			case Gdk.Key.KP_Enter:
				// Open all selected notes
				OnOpenNote (this, args);
				args.RetVal = true;
				break;
			}
		}

		[GLib.ConnectBefore]
		void OnTreeViewMotionNotify (object sender, Gtk.MotionNotifyEventArgs args)
		{
			if ((args.Event.State & Gdk.ModifierType.Button1Mask) == 0) {
				return;
			} else if (args.Event.Window != tree.BinWindow) {
				return;
			}

			args.RetVal = true;

			if (!Gtk.Drag.CheckThreshold (tree, clickX, clickY, (int)args.Event.X, (int)args.Event.Y)) {
				return;
			}

			Gtk.TreePath path;
			if (!tree.GetPathAtPos ((int)args.Event.X, (int)args.Event.Y, out path)) {
				return;
			}

			Gtk.Drag.Begin (tree, new Gtk.TargetList (targets),
							Gdk.DragAction.Move, 1, args.Event);
		}

		void OnTreeViewButtonReleased (object sender, Gtk.ButtonReleaseEventArgs args)
		{
			if (!Gtk.Drag.CheckThreshold (tree, clickX, clickY, (int)args.Event.X, (int)args.Event.Y) &&
					((args.Event.State & (Gdk.ModifierType.ControlMask | Gdk.ModifierType.ShiftMask)) == 0) &&
					tree.Selection.CountSelectedRows () > 1) {

				Gtk.TreePath path;
				tree.GetPathAtPos ((int)args.Event.X, (int)args.Event.Y, out path);
				tree.Selection.UnselectAll ();
				tree.Selection.SelectPath (path);
			}
		}
		
		// called when the user moves the focus into the notes TreeView
		void OnTreeViewFocused (object sender, EventArgs args)
		{
			// enable the Delete Note option in the menu bar 
			Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = true;
		}
		
		// called when the focus moves out of the notes TreeView
		void OnTreeViewFocusedOut (object sender, EventArgs args)
		{
			// Disable the Delete Note option in the menu bar (bug #647462)
			Tomboy.ActionManager ["DeleteNoteAction"].Sensitive = false;
		}

		void PopupContextMenuAtLocation (Gtk.Menu menu, int x, int y)
		{
			menu.ShowAll ();
			Gtk.MenuPositionFunc pos_menu_func = null;

			// Set up the funtion to position the context menu
			// if we were called by the keyboard Gdk.Key.Menu.
			if (x == 0 && y == 0)
				pos_menu_func = PositionContextMenu;

			try {
				menu.Popup (null, null,
					    pos_menu_func,
					    0,
					    Gtk.Global.CurrentEventTime);
			} catch {
				Logger.Debug ("Menu popup failed with custom MenuPositionFunc; trying again without");
				menu.Popup (null, null,
					    null,
					    0,
					    Gtk.Global.CurrentEventTime);
			}
		}

		// This is needed for when the user opens
		// the context menu with the keyboard.
		void PositionContextMenu (Gtk.Menu menu,
					  out int x, out int y, out bool push_in)
		{
			int pos_x;
			int pos_y;
			Gtk.TreePath path;
			Gtk.TreePath [] selected_rows;
			Gdk.Rectangle cell_rect;

			// Set default "return" values
			push_in = true;
			x = 0;
			y = 0;

			// Are we currently in the note list?
			// else, assume we're in the notebook list
			Gtk.TreeView currentTree = (tree.HasFocus) ? tree : notebooksTree;

			selected_rows = currentTree.Selection.GetSelectedRows ();
			// Get TreeView's coordinates
			currentTree.GdkWindow.GetOrigin (out pos_x, out pos_y);

			if (selected_rows.Length > 0) {
				// Popup near the selection
				path = selected_rows [0];
				cell_rect = currentTree.GetCellArea (path, currentTree.Columns [0]);

				// Add 100 to x, so it isn't too close to the left border
				x = pos_x + cell_rect.X + 100;
				// Add 47 to y, so it's right at the bottom of the selected row
				y = pos_y + cell_rect.Y + 47;

			}
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

		List<Note> GetSelectedNotes ()
		{
			Gtk.TreeModel model;
			List<Note> selected_notes = new List<Note> ();

			Gtk.TreePath [] selected_rows =
				tree.Selection.GetSelectedRows (out model);
			foreach (Gtk.TreePath path in selected_rows) {
				Note note = GetNote (path);
				if (note == null)
					continue;

				selected_notes.Add (note);
			}

			return selected_notes;
		}

		public Note GetNote(Gtk.TreeIter iter)
		{
			return tree.Model.GetValue(iter, 3 /* note */) as Note;
		}

		public Note GetNote(Gtk.TreePath path)
		{
			Gtk.TreeIter iter = Gtk.TreeIter.Zero;

			if(tree.Model.GetIter(out iter, path)) {
				return GetNote(iter);
			}

			return null;
		}

		void OnOpenNote (object sender, EventArgs args)
		{
			List<Note> selected_notes = GetSelectedNotes ();
			if (selected_notes != null)
				foreach (Note note in selected_notes)
					note.Window.Present ();
		}

		void OnDeleteNote (object sender, EventArgs args)
		{
			List<Note> selected_notes = GetSelectedNotes ();
			if (selected_notes == null || selected_notes.Count == 0)
				return;
			
			NoteUtils.ShowDeletionDialog (selected_notes, this);
			UpdateResults (); //Update results after all notes have been deleted
		}

		void OnCloseWindow (object sender, EventArgs args)
		{
			// Disconnect external signal handlers to prevent bloweup
			manager.NoteDeleted -= OnNotesChanged;
			manager.NoteAdded -= OnNotesChanged;
			manager.NoteRenamed -= OnNoteRenamed;
			manager.NoteSaved -= OnNoteSaved;

			Notebooks.NotebookManager.NoteAddedToNotebook -= OnNoteAddedToNotebook;
			Notebooks.NotebookManager.NoteRemovedFromNotebook -= OnNoteRemovedFromNotebook;

			// The following code has to be done for the MenuBar to
			// appear properly the next time this window is opened.
			if (menu_bar != null) {
				content_vbox.Remove (menu_bar);
				ActionManager am = Tomboy.ActionManager;
				am ["OpenNoteAction"].Activated -= OnOpenNote;
				am ["DeleteNoteAction"].Activated -= OnDeleteNote;
				am ["NewNotebookAction"].Activated -= OnNewNotebook;
				am ["DeleteNotebookAction"].Activated -= OnDeleteNotebook;
				am ["NewNotebookNoteAction"].Activated -= OnNewNotebookNote;
				am ["OpenNotebookTemplateNoteAction"].Activated -= OnOpenNotebookTemplateNote;
				am ["CloseWindowAction"].Activated -= OnCloseWindow;
			}

			SavePosition ();
			Tomboy.ExitingEvent -= OnExitingEvent;

			Hide ();
			Destroy ();
			instance = null;
#if !MAC
			if (Tomboy.TrayIconShowing == false &&
			    (bool) Preferences.Get (Preferences.ENABLE_TRAY_ICON))
				Tomboy.ActionManager ["QuitTomboyAction"].Activate ();
#endif
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
			}
		}

		protected override void OnShown ()
		{
			// Select "All Notes" in the notebooks list
			SelectAllNotesNotebook ();

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

			int matches_a;
			int matches_b;
			bool has_matches_a = current_matches.TryGetValue (note_a.Uri, out matches_a);
			bool has_matches_b = current_matches.TryGetValue (note_b.Uri, out matches_b);

			if (!has_matches_a || !has_matches_b) {
				if (has_matches_a)
					return 1;

				return -1;
			}

			int result = matches_a - matches_b;
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
							result = result * -1; // reverse sign
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
			
			RestoreMatchesWindow ();
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
				previous_searches = new List<string> ();

			bool repeat = false;

			string lower = text.ToLower();
			foreach (string prev in previous_searches) {
				if (prev.ToLower() == lower)
					repeat = true;
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

		private void NotebookPixbufCellDataFunc (Gtk.TreeViewColumn treeColumn,
				Gtk.CellRenderer renderer, Gtk.TreeModel model,
				Gtk.TreeIter iter)
		{
			Notebooks.Notebook notebook = model.GetValue (iter, 0) as Notebooks.Notebook;
			if (notebook == null)
				return;

			Gtk.CellRendererPixbuf crp = renderer as Gtk.CellRendererPixbuf;
			if (notebook is Notebooks.AllNotesNotebook) {
				crp.Pixbuf = all_notes_icon;
			} else if (notebook is Notebooks.UnfiledNotesNotebook) {
				crp.Pixbuf = unfiled_notes_icon;
			} else {
				crp.Pixbuf = notebook_icon;
			}
		}

		private void NotebookTextCellDataFunc (Gtk.TreeViewColumn treeColumn,
				Gtk.CellRenderer renderer, Gtk.TreeModel model,
				Gtk.TreeIter iter)
		{
			Gtk.CellRendererText crt = renderer as Gtk.CellRendererText;
			crt.Ellipsize = Pango.EllipsizeMode.End;
			Notebooks.Notebook notebook = model.GetValue (iter, 0) as Notebooks.Notebook;
			if (notebook == null) {
				crt.Text = String.Empty;
				return;
			}

			crt.Text = notebook.Name;

			if (notebook is Notebooks.SpecialNotebook) {
				// Bold the "Special" Notebooks
				crt.Markup = string.Format ("<span weight=\"bold\">{0}</span>", notebook.Name);
				crt.Editable = false;
			} else {
				crt.Text = notebook.Name;
				crt.Editable = true;
			}
		}

		private void OnNotebookSelectionChanged (object sender, EventArgs args)
		{
			RestoreMatchesWindow ();
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			if (notebook == null) {
				// Clear out the currently selected tags so that no notebook is selected
				selected_tags.Clear ();

				// Select the "All Notes" item without causing
				// this handler to be called again
				notebooksTree.Selection.Changed -= OnNotebookSelectionChanged;
				SelectAllNotesNotebook ();
				Tomboy.ActionManager ["DeleteNotebookAction"].Sensitive = false;
				notebooksTree.Selection.Changed += OnNotebookSelectionChanged;
			} else {
				selected_tags.Clear ();
				if (notebook.Tag != null)
					selected_tags.Add (notebook.Tag, notebook.Tag);
				if (notebook is Notebooks.SpecialNotebook) {
					Tomboy.ActionManager ["DeleteNotebookAction"].Sensitive = false;
				} else {
					Tomboy.ActionManager ["DeleteNotebookAction"].Sensitive = true;
				}
			}

			UpdateResults ();
		}

		void OnNewNotebook (object sender, EventArgs args)
		{
			Notebooks.NotebookManager.PromptCreateNewNotebook (this);
		}

		void OnDeleteNotebook (object sender, EventArgs args)
		{
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			if (notebook == null)
				return;

			Notebooks.NotebookManager.PromptDeleteNotebook (this, notebook);
		}

		// Create a new note in the notebook when activated
		private void OnNotebookRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			OnNewNotebookNote (sender, EventArgs.Empty);
		}

		private void OnNewNotebookNote (object sender, EventArgs args)
		{
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			if (notebook == null || notebook is Notebooks.SpecialNotebook) {
				// Just create a standard note (not in a notebook)
				Tomboy.ActionManager ["NewNoteAction"].Activate ();
				return;
			}

			// Look for the template note and create a new note
			Note templateNote = notebook.GetTemplateNote ();
			Note note;

			note = manager.Create ();
			if (templateNote != null) {
				// Use the body from the template note
				string xmlContent = templateNote.XmlContent.Replace (XmlEncoder.Encode (templateNote.Title),
					XmlEncoder.Encode (note.Title));
				xmlContent = NoteManager.SanitizeXmlContent (xmlContent);
				note.XmlContent = xmlContent;
			}

			note.AddTag (notebook.Tag);
			note.Window.Show ();
		}

		private void OnOpenNotebookTemplateNote (object sender, EventArgs args)
		{
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			if (notebook == null)
				return;

			Note templateNote = notebook.GetTemplateNote ();
			if (templateNote == null)
				return; // something seriously went wrong

			templateNote.Window.Present ();
		}

		/// <summary>
		/// Returns the currently selected notebook in the "Search All Notes Window".
		/// </summary>
		/// <returns>
		/// The selected notebook or null if no notebook is selected. <see cref="Notebooks.Notebook"/>
		/// </returns>
		public Notebooks.Notebook GetSelectedNotebook ()
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;

			Gtk.TreeSelection selection = notebooksTree.Selection;
			if (selection == null || selection.GetSelected (out model, out iter) == false)
				return null; // Nothing selected

			return model.GetValue (iter, 0) as Notebooks.Notebook;
		}

		private void SelectAllNotesNotebook ()
		{
			Gtk.TreeIter iter;
			if (notebooksTree.Model.GetIterFirst (out iter) == true) {
				notebooksTree.Selection.SelectIter (iter);
			}
		}

		[GLib.ConnectBefore]
		void OnNotebooksTreeButtonPressed (object sender, Gtk.ButtonPressEventArgs args)
		{
			switch (args.Event.Button) {
				case 3: // third mouse button (right-click)
					Notebooks.Notebook notebook = GetSelectedNotebook ();
					if (notebook == null)
						return; // Don't pop open a submenu

					Gtk.TreePath path = null;
					Gtk.TreeViewColumn column = null;

					bool rowClicked = true;

					if (notebooksTree.GetPathAtPos ((int) args.Event.X,
							(int) args.Event.Y, out path, out column) == false)
						rowClicked = false;

					Gtk.TreeSelection selection = notebooksTree.Selection;
					if (selection.CountSelectedRows () == 0)
						rowClicked = false;

					Gtk.Menu menu = null;
					if (rowClicked)
						menu = Tomboy.ActionManager.GetWidget (
							"/NotebooksTreeContextMenu") as Gtk.Menu;
					else
						menu = Tomboy.ActionManager.GetWidget (
							"/NotebooksTreeNoRowContextMenu") as Gtk.Menu;

					PopupContextMenuAtLocation (menu,
								(int) args.Event.X,
								(int) args.Event.Y);

				break;
			}
		}

		void OnNotebooksKeyPressed (object sender, Gtk.KeyPressEventArgs args)
		{
			switch (args.Event.Key) {
				case Gdk.Key.Escape:
					// Allow Escape to close the window
					OnCloseWindow (this, EventArgs.Empty);
					break;
				case Gdk.Key.Menu:
					// Pop up the context menu if a notebook is selected
					Notebooks.Notebook notebook = GetSelectedNotebook ();
					if (notebook == null || notebook is Notebooks.SpecialNotebook)
						return; // Don't pop open a submenu
					
					Gtk.Menu menu = Tomboy.ActionManager.GetWidget (
						"/NotebooksTreeContextMenu") as Gtk.Menu;
					PopupContextMenuAtLocation (menu, 0, 0);

					break;
			}
		}

		private void OnNoteAddedToNotebook (Note note, Notebooks.Notebook notebook)
		{
			RestoreMatchesWindow ();
			UpdateResults ();
		}

		private void OnNoteRemovedFromNotebook (Note note, Notebooks.Notebook notebook)
		{
			RestoreMatchesWindow ();
			UpdateResults ();
		}

		public string SearchText
		{
			get {
				// Entry may be null if search window closes
				// early (bug #544996).
				if (find_combo == null || find_combo.Entry == null)
					return null;
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

		/// <summary>
		/// Save the position and size of the RecentChanges window
		/// </summary>
		private void SavePosition ()
		{
			int x;
			int y;
			int width;
			int height;
			int mon;

			GetPosition(out x, out y);
			GetSize(out width, out height);
			mon = Screen.GetMonitorAtPoint(x, y);
			
			Preferences.Set (Preferences.SEARCH_WINDOW_X_POS, x);
			Preferences.Set (Preferences.SEARCH_WINDOW_Y_POS, y);
			Preferences.Set (Preferences.SEARCH_WINDOW_WIDTH, width);
			Preferences.Set (Preferences.SEARCH_WINDOW_HEIGHT, height);
			Preferences.Set (Preferences.SEARCH_WINDOW_SPLITTER_POS, hpaned.Position);
			Preferences.Set (Preferences.SEARCH_WINDOW_MONITOR_NUM, mon);
		}

		private void RestorePosition ()
		{
			object x = Preferences.Get (Preferences.SEARCH_WINDOW_X_POS);
			object y = Preferences.Get (Preferences.SEARCH_WINDOW_Y_POS);
			object width = Preferences.Get (Preferences.SEARCH_WINDOW_WIDTH);
			object height = Preferences.Get (Preferences.SEARCH_WINDOW_HEIGHT);
			object splitter_pos = Preferences.Get (Preferences.SEARCH_WINDOW_SPLITTER_POS);
			object mon = Preferences.Get (Preferences.SEARCH_WINDOW_MONITOR_NUM);
			int new_mon, new_x, new_y;

			if (x == null || !(x is int)
				|| y == null || !(y is int)
				|| width == null || !(width is int)
				|| height == null || !(height is int)
				|| splitter_pos == null || !(splitter_pos is int)
				|| mon == null || !(mon is int))
			return;

			new_mon = Screen.GetMonitorAtPoint ((int) x, (int) y);
			Logger.Info ("Monitor number returned by GetMonitorAtPoint (actual) is: {0}", new_mon);
			Logger.Info ("Saved monitor number is: {0}", mon);
			Logger.Info ("Saved Search window position is {0} x {1}", (int) x, (int) y);

			// If saved monitor number doesn't match the one returned by GetMonitorAtPoint for saved coords
			// then it means that something has changed in the monitors layout and saved coordinates may not be valid.
			// Therefore we'll restore the window to the center of the monitor closest to the saved coordinates.
			/// It will be returned by the same GetMonitorAtPoint call.
			if (new_mon == (int) mon) {
				Logger.Info ("Saved monitor number does match the actual one - restoring as-is");
				new_x = (int) x;
				new_y = (int) y;
			} else {
				Logger.Info ("Saved monitor number does NOT match the actual one - restoring to the center");
				//getting the monitor size to calculate the center
				Gdk.Rectangle new_mon_geom = Screen.GetMonitorGeometry (new_mon);
				new_x = new_mon_geom.Right/2 - (int) width/2;
				new_y = new_mon_geom.Bottom/2 - (int) height/2;
			}

			Logger.Info ("Restoring Search window to position {0} x {1} at monitor {2}", new_x, new_y, new_mon);
			DefaultSize =
				new Gdk.Size ((int) width, (int) height);
			Move (new_x, new_y);
			hpaned.Position = (int) splitter_pos;

		}

		private void OnExitingEvent (object sender, EventArgs args)
		{
			SavePosition ();
		}

		// This one presents a List of notes that Search has found
		public List<Note> GetFilteredNotes ()
		{
			Gtk.TreeIter iter;
			List<Note> filtered_notes = new List<Note> ();

			if (store_sort.IterChildren (out iter) == false)
				return filtered_notes; /* if nothing was found we return empty list */

			// Getting filtered out notes (found by search) to our list
			// 3 is a column where note itself is stored
			do {
				filtered_notes.Add ((Note) store_sort.GetValue (iter, 3));
			} while (store_sort.IterNext (ref iter));

			return filtered_notes;
		}
	}
}
