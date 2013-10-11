using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Unix;
using Gtk;

namespace Tomboy
{
	public class NoteWindow : ForcedPresentWindow
	{
		Note note;

		Gtk.AccelGroup accel_group;
		Gtk.Toolbar toolbar;
		Gtk.Tooltip toolbar_tips;
		Gtk.ToolButton link_button;
		NoteTextMenu text_menu;
		Gtk.Menu plugin_menu;
		Gtk.ImageMenuItem sync_menu_item;
		Gtk.TextView editor;
		Gtk.ScrolledWindow editor_window;
		NoteFindBar find_bar;
		Gtk.ToolButton delete;
		Gtk.Box template_widget;

		GlobalKeybinder global_keys;
		InterruptableTimeout mark_set_timeout;

		//
		// Construct a window to display a note
		//
		// Currently a toolbar with Link, Search, Text, Delete buttons
		// and a Gtk.TextView as the body.
		//

		public NoteWindow (Note note)
			: base (note.Title)
		{
			this.note = note;
			this.IconName = "tomboy";
			this.SetDefaultSize (450, 360);
			Resizable = true;

			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			text_menu = new NoteTextMenu (accel_group, note.Buffer, note.Buffer.Undoer);

			// Add the Find menu item to the toolbar Text menu.  It
			// should only show up in the toplevel Text menu, since
			// the context menu already has a Find submenu.

			Gtk.SeparatorMenuItem spacer = new Gtk.SeparatorMenuItem ();
			spacer.Show ();
			text_menu.Append (spacer);

			Gtk.ImageMenuItem find_item =
			        new Gtk.ImageMenuItem (Catalog.GetString("Find in This Note"));
			find_item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			find_item.Activated += FindActivate;
			find_item.AddAccelerator ("activate",
			                          accel_group,
			                          (uint) Gdk.Key.f,
			                          Gdk.ModifierType.ControlMask,
			                          Gtk.AccelFlags.Visible);
			find_item.Show ();
			text_menu.Append (find_item);

			plugin_menu = MakePluginMenu ();

			toolbar = MakeToolbar ();
			toolbar.Show ();


			template_widget = MakeTemplateBar ();

			// The main editor widget
			editor = new NoteEditor (note.Buffer);
			editor.PopulatePopup += OnPopulatePopup;
			editor.Show ();

			// Sensitize the Link toolbar button on text selection
			mark_set_timeout = new InterruptableTimeout();
			mark_set_timeout.Timeout += UpdateLinkButtonSensitivity;
			note.Buffer.MarkSet += OnSelectionMarkSet;

			// FIXME: I think it would be really nice to let the
			//        window get bigger up till it grows more than
			//        60% of the screen, and then show scrollbars.
			editor_window = new Gtk.ScrolledWindow ();
			editor_window.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			editor_window.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			editor_window.Add (editor);
			editor_window.Show ();

			FocusChild = editor;

			find_bar = new NoteFindBar (note);
			find_bar.Visible = false;
			find_bar.NoShowAll = true;
			find_bar.Hidden += FindBarHidden;

			Gtk.VBox box = new Gtk.VBox (false, 2);
			box.PackStart (toolbar, false, false, 0);
			box.PackStart (template_widget, false, false, 0);
			box.PackStart (editor_window, true, true, 0);
			box.PackStart (find_bar, false, false, 0);

			box.Show ();

			// Don't set up Ctrl-W or Ctrl-N if Emacs is in use
			bool using_emacs = false;
			string gtk_key_theme = (string)
				Preferences.Get ("/desktop/gnome/interface/gtk_key_theme");
			if (gtk_key_theme != null && gtk_key_theme.CompareTo ("Emacs") == 0)
				using_emacs = true;

			// NOTE: Since some of our keybindings are only
			// available in the context menu, and the context menu
			// is created on demand, register them with the
			// global keybinder
			global_keys = new GlobalKeybinder (accel_group);

			// Close window (Ctrl-W)
			if (!using_emacs)
				global_keys.AddAccelerator (new EventHandler (CloseWindowHandler),
				                            (uint) Gdk.Key.w,
				                            Gdk.ModifierType.ControlMask,
				                            Gtk.AccelFlags.Visible);

			// Escape has been moved to be handled by a KeyPress Handler so that
			// Escape can be used to close the FindBar.

			// Close all windows on current Desktop (Ctrl-Q)
			global_keys.AddAccelerator (new EventHandler (CloseAllWindowsHandler),
			                            (uint) Gdk.Key.q,
			                            Gdk.ModifierType.ControlMask,
			                            Gtk.AccelFlags.Visible);

			// Find Next (Ctrl-G)
			global_keys.AddAccelerator (new EventHandler (FindNextActivate),
			                            (uint) Gdk.Key.g,
			                            Gdk.ModifierType.ControlMask,
			                            Gtk.AccelFlags.Visible);

			// Find Previous (Ctrl-Shift-G)
			global_keys.AddAccelerator (new EventHandler (FindPreviousActivate),
			                            (uint) Gdk.Key.g,
			                            (Gdk.ModifierType.ControlMask |
			                             Gdk.ModifierType.ShiftMask),
			                            Gtk.AccelFlags.Visible);

			// Open Help (F1)
			global_keys.AddAccelerator (new EventHandler (OpenHelpActivate),
			                            (uint) Gdk.Key.F1,
			                            0,
			                            0);

			// Create a new note
			if (!using_emacs)
				global_keys.AddAccelerator (new EventHandler (CreateNewNote),
				                            (uint) Gdk.Key.n,
				                            Gdk.ModifierType.ControlMask,
				                            Gtk.AccelFlags.Visible);

			// Have Esc key close the find bar or note window
			KeyPressEvent += KeyPressed;

			// Increase Indent
			global_keys.AddAccelerator (new EventHandler (ChangeDepthRightHandler),
			                            (uint) Gdk.Key.Right,
			                            Gdk.ModifierType.Mod1Mask,
			                            Gtk.AccelFlags.Visible);

			// Decrease Indent
			global_keys.AddAccelerator (new EventHandler (ChangeDepthLeftHandler),
			                            (uint) Gdk.Key.Left,
			                            Gdk.ModifierType.Mod1Mask,
			                            Gtk.AccelFlags.Visible);

			this.Add (box);
		}

		protected override bool OnDeleteEvent (Gdk.Event evnt)
		{
			Preferences.SettingChanged -= Preferences_SettingChanged;

			CloseWindowHandler (null, null);
			return true;
		}

		protected override void OnHidden ()
		{
			base.OnHidden ();

			// Workaround Gtk bug, where adding or changing Widgets
			// while the Window is hidden causes it to be reshown at
			// 0,0...
			int x, y;
			GetPosition (out x, out y);
			Move (x, y);
		}

		void KeyPressed (object sender, Gtk.KeyPressEventArgs args)
		{
			args.RetVal = true;

			switch (args.Event.Key)
			{
			case Gdk.Key.Escape:
				if (find_bar != null && find_bar.Visible)
					find_bar.Hide ();
				else if ((bool) Preferences.Get(Preferences.ENABLE_CLOSE_NOTE_ON_ESCAPE))
					CloseWindowHandler (null, null);
				break;
			default:
				args.RetVal = false;
				break;
			}
		}

		// FIXME: Need to just emit a delete event, and do this work in
		// the default delete handler, so that plugins can attach to
		// delete event and have it always work.
		void CloseWindowHandler (object sender, EventArgs args)
		{
			// Unmaximize before hiding to avoid reopening
			// pseudo-maximized
			if ((GdkWindow.State & Gdk.WindowState.Maximized) > 0)
				Unmaximize ();
			Hide ();
		}

		[DllImport("libtomboy")]
		static extern int tomboy_window_get_workspace (IntPtr win_raw);

		void CloseAllWindowsHandler (object sender, EventArgs args)
		{
#if WIN32 || MAC
			Tomboy.Exit (0);
#else
			int workspace = tomboy_window_get_workspace (note.Window.Handle);

			foreach (Note iter in note.Manager.Notes) {
				if (!iter.IsOpened)
					continue;

				// Close windows on the same workspace, or all
				// open windows if no workspace.
				if (workspace < 0 ||
				    tomboy_window_get_workspace (iter.Window.Handle) == workspace) {
					iter.Window.CloseWindowHandler (null, null);
				}
			}
#endif
		}

		//
		// Delete this Note.
		//

		void OnDeleteButtonClicked (object sender, EventArgs args)
		{
			// Prompt for note deletion
			List<Note> single_note_list = new List<Note> (1);
			single_note_list.Add (note);
			NoteUtils.ShowDeletionDialog (single_note_list, this);
		}

		//
		// Public Children Accessors
		//

		public Gtk.TextView Editor {
			get {
				return editor;
			}
		}

		public Gtk.Toolbar Toolbar {
			get {
				return toolbar;
			}
		}

		/// <summary>
		/// The Delete Toolbar Button
		/// </summary>
		public Gtk.ToolButton DeleteButton
		{
			get {
				return delete;
			}
		}

		public Gtk.Menu PluginMenu {
			get {
				return plugin_menu;
			}
		}

		public Gtk.Menu TextMenu {
			get {
				return text_menu;
			}
		}

		public Gtk.AccelGroup AccelGroup {
			get {
				return accel_group;
			}
		}

		//
		// Sensitize the Link toolbar button on text selection
		//

		void OnSelectionMarkSet (object sender, Gtk.MarkSetArgs args)
		{
			// FIXME: Process in a timeout due to GTK+ bug #172050.
			mark_set_timeout.Reset (0);
		}

		void UpdateLinkButtonSensitivity (object sender, EventArgs args)
		{
			link_button.Sensitive = (note.Buffer.Selection != null);
		}

		//
		// Right-click menu
		//
		// Add Undo, Redo, Link, Link To menu, Font menu to the start of
		// the editor's context menu.
		//
		[GLib.ConnectBefore]
		void OnPopulatePopup (object sender, Gtk.PopulatePopupArgs args)
		{
			args.Menu.AccelGroup = accel_group;

			Logger.Debug ("Populating context menu...");

			// Remove the lame-o gigantic Insert Unicode Control
			// Characters menu item.
			Gtk.Widget lame_unicode;
			lame_unicode = (Gtk.Widget)
			               args.Menu.Children [args.Menu.Children.Length - 1];
			args.Menu.Remove (lame_unicode);

			Gtk.MenuItem spacer1 = new Gtk.SeparatorMenuItem ();
			spacer1.Show ();

			Gtk.ImageMenuItem search = new Gtk.ImageMenuItem (
			        Catalog.GetString ("_Search All Notes"));
			search.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			search.Activated += SearchActivate;
			search.AddAccelerator ("activate",
			                       accel_group,
			                       (uint) Gdk.Key.f,
			                       (Gdk.ModifierType.ControlMask |
			                        Gdk.ModifierType.ShiftMask),
			                       Gtk.AccelFlags.Visible);
			search.Show ();

			Gtk.ImageMenuItem link =
			        new Gtk.ImageMenuItem (Catalog.GetString ("_Link to New Note"));
			link.Image = new Gtk.Image (Gtk.Stock.JumpTo, Gtk.IconSize.Menu);
			link.Sensitive = (note.Buffer.Selection != null);
			link.Activated += LinkToNoteActivate;
			link.AddAccelerator ("activate",
			                     accel_group,
			                     (uint) Gdk.Key.l,
			                     Gdk.ModifierType.ControlMask,
			                     Gtk.AccelFlags.Visible);
			link.Show ();

			Gtk.ImageMenuItem text_item =
			        new Gtk.ImageMenuItem (Catalog.GetString ("Te_xt"));
			text_item.Image = new Gtk.Image (Gtk.Stock.SelectFont, Gtk.IconSize.Menu);
			text_item.Submenu = new NoteTextMenu (accel_group,
			                                      note.Buffer,
			                                      note.Buffer.Undoer);
			text_item.Show ();

			Gtk.ImageMenuItem find_item =
			        new Gtk.ImageMenuItem (Catalog.GetString ("_Find in This Note"));
			find_item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			find_item.Submenu = MakeFindMenu ();
			find_item.Show ();

			Gtk.MenuItem spacer2 = new Gtk.SeparatorMenuItem ();
			spacer2.Show ();

			args.Menu.Prepend (spacer1);
			args.Menu.Prepend (text_item);
			args.Menu.Prepend (find_item);
			args.Menu.Prepend (link);
			args.Menu.Prepend (search);

			Gtk.MenuItem close_all =
			        new Gtk.MenuItem (Catalog.GetString ("Clos_e All Notes"));
			close_all.Activated += CloseAllWindowsHandler;
			close_all.AddAccelerator ("activate",
			                          accel_group,
			                          (uint) Gdk.Key.q,
			                          Gdk.ModifierType.ControlMask,
			                          Gtk.AccelFlags.Visible);
			close_all.Show ();

			Gtk.ImageMenuItem close_window =
			        new Gtk.ImageMenuItem (Catalog.GetString ("_Close"));
			close_window.Image = new Gtk.Image (Gtk.Stock.Close, Gtk.IconSize.Menu);
			close_window.Activated += CloseWindowHandler;
			close_window.AddAccelerator ("activate",
			                             accel_group,
			                             (uint) Gdk.Key.w,
			                             Gdk.ModifierType.ControlMask,
			                             Gtk.AccelFlags.Visible);
			close_window.Show ();

			args.Menu.Append (close_all);
			args.Menu.Append (close_window);
		}

		//
		// Toolbar
		//
		// Add Link button, Font menu, Delete button to the window's
		// toolbar.
		//

		Gtk.Toolbar MakeToolbar ()
		{
			Gtk.Toolbar tb = new Gtk.Toolbar ();
			tb.HasTooltip = true;

//			toolbar_tips = new Gtk.Tooltip ()

			Gtk.ToolButton search = new Gtk.ToolButton (
				new Gtk.Image (Gtk.Stock.Find, tb.IconSize),
				Catalog.GetString ("Search"));
			search.IsImportant = true;
			search.Clicked += SearchActivate;
			// TODO: If we ever add a way to customize internal keybindings, this will need to change
//			toolbar_tips.Text = Catalog.GetString ("Search your notes") + " (Ctrl-Shift-F)";
			search.AddAccelerator ("clicked",
			                       accel_group,
			                       (uint) Gdk.Key.f,
			                       (Gdk.ModifierType.ControlMask |
			                        Gdk.ModifierType.ShiftMask),
			                       Gtk.AccelFlags.Visible);
			search.ShowAll ();
			tb.Insert (search, -1);

			link_button = new Gtk.ToolButton (
				new Gtk.Image (Gtk.Stock.JumpTo, tb.IconSize),
				Catalog.GetString ("Link"));
			link_button.IsImportant = true;
			link_button.Sensitive = (note.Buffer.Selection != null);
			link_button.Clicked += LinkToNoteActivate;
			// TODO: If we ever add a way to customize internal keybindings, this will need to change
//			toolbar_tips.SetTip (
//				link_button,
//				Catalog.GetString ("Link selected text to a new note") + " (Ctrl-L)",
//				null);
			link_button.AddAccelerator ("clicked",
			                            accel_group,
			                            (uint) Gdk.Key.l,
			                            Gdk.ModifierType.ControlMask,
			                            Gtk.AccelFlags.Visible);
			link_button.ShowAll ();
			tb.Insert (link_button, -1);

			ToolMenuButton text_button =
			        new ToolMenuButton (tb,
			                            Gtk.Stock.SelectFont,
			                            Catalog.GetString ("_Text"),
			                            text_menu);
			text_button.IsImportant = true;
			text_button.ShowAll ();
			tb.Insert (text_button, -1);
//			toolbar_tips.SetTip (text_button, Catalog.GetString ("Set properties of text"), null);

			ToolMenuButton plugin_button =
			        new ToolMenuButton (tb,
			                            Gtk.Stock.Execute,
			                            Catalog.GetString ("T_ools"),
			                            plugin_menu);
			plugin_button.ShowAll ();
			tb.Insert (plugin_button, -1);
//			toolbar_tips.SetTip (plugin_button, Catalog.GetString ("Use tools on this note"), null);

			tb.Insert (new Gtk.SeparatorToolItem (), -1);

			delete = new Gtk.ToolButton (Gtk.Stock.Delete);
			delete.Clicked += OnDeleteButtonClicked;
			delete.ShowAll ();
			tb.Insert (delete, -1);
//			toolbar_tips.SetTip (delete, Catalog.GetString ("Delete this note"), null);

			// Don't allow deleting the "Start Here" note...
			if (note.IsSpecial)
				delete.Sensitive = false;

			tb.Insert (new Gtk.SeparatorToolItem (), -1);

			sync_menu_item = new Gtk.ImageMenuItem (Catalog.GetString ("Synchronize Notes"));
			sync_menu_item.Image = new Gtk.Image (Gtk.Stock.Convert, Gtk.IconSize.Menu);
			sync_menu_item.Activated += SyncItemSelected;
			sync_menu_item.Show ();
			PluginMenu.Add (sync_menu_item);

			// We might want to know when various settings are altered.
			Preferences.SettingChanged += Preferences_SettingChanged;

			// Update items based on configuration.
			UpdateMenuItems ();

			tb.ShowAll ();
			return tb;
		}

		void Preferences_SettingChanged (object sender, EventArgs args)
		{
			// Update items based on configuration.
			UpdateMenuItems ();
		}

		void UpdateMenuItems ()
		{
			// Is synchronization configured and active?
			string sync_addin_id = Preferences.Get (Preferences.SYNC_SELECTED_SERVICE_ADDIN) as string;
			sync_menu_item.Sensitive = !string.IsNullOrEmpty (sync_addin_id);
		}

		void SyncItemSelected (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["NoteSynchronizationAction"].Activate ();
		}

		//
		// Plugin toolbar menu
		//
		// This menu can be
		// populated by individual plugins using
		// NotePlugin.AddPluginMenuItem().
		//

		Gtk.Menu MakePluginMenu ()
		{
			Gtk.Menu menu = new Gtk.Menu ();
			return menu;
		}

		private Gtk.Box MakeTemplateBar ()
		{
			// TODO: Move these to static area
			Tag template_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);
			Tag template_save_size_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSaveSizeSystemTag);
			Tag template_save_selection_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSaveSelectionSystemTag);
			Tag template_save_title_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSaveTitleSystemTag);

			var bar = new Gtk.VBox ();

			var infoLabel  = new Gtk.Label (Catalog.GetString ("This note is a template note. It determines " +
			                                                   "the default content of regular notes, and will " +
			                                                   "not show up in the note menu or search window."));
			infoLabel.Wrap = true;

			var untemplateButton = new Gtk.Button ();
			untemplateButton.Label = Catalog.GetString ("Convert to regular note");
			untemplateButton.Clicked += (o, e) => {
				note.RemoveTag (template_tag);
			};

			var saveSizeCheckbutton = new Gtk.CheckButton (Catalog.GetString ("Save Si_ze"));
			saveSizeCheckbutton.Active = note.ContainsTag (template_save_size_tag);
			saveSizeCheckbutton.Toggled += (o, e) => {
				if (saveSizeCheckbutton.Active)
					note.AddTag (template_save_size_tag);
				else
					note.RemoveTag (template_save_size_tag);
			};

			var saveSelectionCheckbutton = new Gtk.CheckButton (Catalog.GetString ("Save Se_lection"));
			saveSelectionCheckbutton.Active = note.ContainsTag (template_save_selection_tag);
			saveSelectionCheckbutton.Toggled += (o, e) => {
				if (saveSelectionCheckbutton.Active)
					note.AddTag (template_save_selection_tag);
				else
					note.RemoveTag (template_save_selection_tag);
			};
			
			var saveTitleCheckbutton = new Gtk.CheckButton (Catalog.GetString ("Save _Title"));
			saveTitleCheckbutton.Active = note.ContainsTag (template_save_title_tag);
			saveTitleCheckbutton.Toggled += (o, e) => {
				if (saveTitleCheckbutton.Active)
					note.AddTag (template_save_title_tag);
				else
					note.RemoveTag (template_save_title_tag);
			};

			bar.PackStart (infoLabel, true, true, 0);
			bar.PackStart (untemplateButton, true, true, 0);
			bar.PackStart (saveSizeCheckbutton, true, true, 0);
			bar.PackStart (saveSelectionCheckbutton, true, true, 0);
			bar.PackStart (saveTitleCheckbutton, true, true, 0);

			if (note.ContainsTag (template_tag))
				bar.ShowAll ();

			note.TagAdded += delegate (Note taggedNote, Tag tag) {
				if (taggedNote == note && tag == template_tag)
					bar.ShowAll ();
			};

			note.TagRemoved += delegate (Note taggedNote, string tag) {
				if (taggedNote == note && tag == template_tag.NormalizedName)
					bar.Hide ();
			};

			return bar;
		}

		//
		// Find context menu
		//
		// Find, Find Next, Find Previous menu items.  Next nd previous
		// are only sensitized when there are search results for this
		// buffer to iterate.
		//

		Gtk.Menu MakeFindMenu ()
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AccelGroup = accel_group;

			Gtk.ImageMenuItem find =
			        new Gtk.ImageMenuItem (Catalog.GetString ("_Find..."));
			find.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			find.Activated += FindActivate;
			find.AddAccelerator ("activate",
			                     accel_group,
			                     (uint) Gdk.Key.f,
			                     Gdk.ModifierType.ControlMask,
			                     Gtk.AccelFlags.Visible);
			find.Show ();

			Gtk.ImageMenuItem find_next =
			        new Gtk.ImageMenuItem (Catalog.GetString ("Find _Next"));
			find_next.Image = new Gtk.Image (Gtk.Stock.GoForward, Gtk.IconSize.Menu);
			find_next.Sensitive = Find.FindNextButton.Sensitive;

			find_next.Activated += FindNextActivate;
			find_next.AddAccelerator ("activate",
			                          accel_group,
			                          (uint) Gdk.Key.g,
			                          Gdk.ModifierType.ControlMask,
			                          Gtk.AccelFlags.Visible);
			find_next.Show ();

			Gtk.ImageMenuItem find_previous =
			        new Gtk.ImageMenuItem (Catalog.GetString ("Find _Previous"));
			find_previous.Image = new Gtk.Image (Gtk.Stock.GoBack, Gtk.IconSize.Menu);
			find_previous.Sensitive = Find.FindPreviousButton.Sensitive;

			find_previous.Activated += FindPreviousActivate;
			find_previous.AddAccelerator ("activate",
			                              accel_group,
			                              (uint) Gdk.Key.g,
			                              (Gdk.ModifierType.ControlMask |
			                               Gdk.ModifierType.ShiftMask),
			                              Gtk.AccelFlags.Visible);
			find_previous.Show ();

			menu.Append (find);
			menu.Append (find_next);
			menu.Append (find_previous);

			return menu;
		}

		//
		// Open the find dialog, passing any currently selected text
		//

		public NoteFindBar Find {
			get {
				return find_bar;
			}
		}

		void FindButtonClicked ()
		{
			Find.ShowAll ();
			Find.Visible = true;
			Find.SearchText = note.Buffer.Selection;
		}

		void FindActivate (object sender, EventArgs args)
		{
			FindButtonClicked ();
		}

		void FindNextActivate (object sender, EventArgs args)
		{
			Find.FindNextButton.Click ();
		}

		void FindPreviousActivate (object sender, EventArgs args)
		{
			Find.FindPreviousButton.Click ();
		}

		void FindBarHidden (object sender, EventArgs args)
		{
			// Reposition the current focus back to the editor so the
			// cursor will be ready for typing.
			editor.GrabFocus ();
		}

		//
		// Link menu item activate
		//
		// Create a new note, names according to the buffer's selected
		// text.  Does nothing if there is no active selection.
		//

		void LinkButtonClicked ()
		{
			string select = note.Buffer.Selection;
			if (select == null)
				return;

			string body_unused;
			string title = NoteManager.SplitTitleFromContent (select, out body_unused);
			if (title == null)
				return;

			Note match = note.Manager.Find (title);
			if (match == null) {
				try {
					match = note.Manager.Create (select);
				} catch (Exception e) {
					HIGMessageDialog dialog =
					        new HIGMessageDialog (
					        this,
					        Gtk.DialogFlags.DestroyWithParent,
					        Gtk.MessageType.Error,
					        Gtk.ButtonsType.Ok,
					        Catalog.GetString ("Cannot create note"),
					        e.Message);
					dialog.Run ();
					dialog.Destroy ();
					return;
				}
			}

			match.Window.Present ();
		}

		void LinkToNoteActivate (object sender, EventArgs args)
		{
			LinkButtonClicked ();
		}

		void OpenHelpActivate (object sender, EventArgs args)
		{
			GuiUtils.ShowHelp ("tomboy", "editing-notes", Screen, this);
		}

		void CreateNewNote (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["NewNoteAction"].Activate ();
		}

		void SearchButtonClicked ()
		{
			NoteRecentChanges search = NoteRecentChanges.GetInstance (note.Manager);
			if (note.Buffer.Selection != null) {
				search.SearchText = note.Buffer.Selection;
			}
			search.Present ();
		}

		void SearchActivate (object sender, EventArgs args)
		{
			SearchButtonClicked ();
		}

		void ChangeDepthRightHandler (object sender, EventArgs args)
		{
			((NoteBuffer)editor.Buffer).ChangeCursorDepthDirectional (true);
		}

		void ChangeDepthLeftHandler (object sender, EventArgs args)
		{
			((NoteBuffer)editor.Buffer).ChangeCursorDepthDirectional (false);
		}
	}

	public class NoteFindBar : Gtk.HBox
	{
		private Note note;

		Gtk.Entry entry;
		Gtk.Button next_button;
		Gtk.Button prev_button;
		Gtk.Label labelCount = new Gtk.Label (); //Label of current find matches and current position in List of Matches

		List<Match> current_matches;
		string prev_search_text;

		InterruptableTimeout entry_changed_timeout;
		InterruptableTimeout note_changed_timeout;

		bool shift_key_pressed;

		public NoteFindBar (Note note)
			: base (false, 0)
		{
			this.note = note;

			BorderWidth = 2;

			Gtk.Button button = new Gtk.Button ();
			button.Image = new Gtk.Image (Gtk.Stock.Close, Gtk.IconSize.Menu);
			button.Relief = Gtk.ReliefStyle.None;
			button.Clicked += HideFindBar;
			button.Show ();
			PackStart (button, false, false, 4);

			Gtk.Label label = new Gtk.Label (Catalog.GetString ("_Find:"));
			label.Show ();
			PackStart (label, false, false, 0);

			entry = new Gtk.Entry ();
			label.MnemonicWidget = entry;
			entry.Changed += OnFindEntryChanged;
			entry.Activated += OnFindEntryActivated;
			entry.Show ();
			PackStart (entry, true, true, 0);
			
			labelCount.Show ();
			PackStart (labelCount, false,false, 0);

			prev_button = new Gtk.Button (Catalog.GetString ("_Previous"));
			prev_button.Image = new Gtk.Arrow (Gtk.ArrowType.Left, Gtk.ShadowType.None);
			prev_button.Relief = Gtk.ReliefStyle.None;
			prev_button.Sensitive = false;
			prev_button.FocusOnClick = false;
			prev_button.Clicked += OnPrevClicked;
			prev_button.Show ();
			PackStart (prev_button, false, false, 0);

			next_button = new Gtk.Button (Catalog.GetString ("_Next"));
			next_button.Image = new Gtk.Arrow (Gtk.ArrowType.Right, Gtk.ShadowType.None);
			next_button.Relief = Gtk.ReliefStyle.None;
			next_button.Sensitive = false;
			next_button.FocusOnClick = false;
			next_button.Clicked += OnNextClicked;
			next_button.Show ();
			PackStart (next_button, false, false, 0);

			// Bind ESC to close the FindBar if it's open and has
			// focus or the window otherwise.  Also bind Return and
			// Shift+Return to advance the search if the search
			// entry has focus.
			shift_key_pressed = false;
			entry.KeyPressEvent += KeyPressed;
			entry.KeyReleaseEvent += KeyReleased;
		}
		
		/// <summary>
		/// Updates the match count.
		/// </summary>
		/// <param name='location'>
		/// Current location in the List of Matched notes
		/// </param>
		protected void UpdateMatchCount (int location)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;
			// x of x - specifically Number of Matches and what location is the cursor in the list of matches
			labelCount.Text = String.Format (Catalog.GetString("{0} of {1}"), (location + 1), current_matches.Count);
		}
		
		/// <summary>
		/// Clears the match count.
		/// </summary>
		protected void ClearMatchCount ()
		{
			labelCount.Text = "";
		}
		
		protected override void OnShown ()
		{
			entry.GrabFocus ();

			// Highlight words from a previous existing search
			HighlightMatches (true);

			// Call PerformSearch on newly inserted text when
			// the FindBar is visible
			note.Buffer.InsertText += OnInsertText;
			note.Buffer.DeleteRange += OnDeleteRange;

			base.OnShown ();
		}

		protected override void OnHidden ()
		{
			HighlightMatches (false);

			// Prevent searching when the FindBar is not visible
			note.Buffer.InsertText -= OnInsertText;
			note.Buffer.DeleteRange -= OnDeleteRange;

			base.OnHidden ();
		}

		void HideFindBar (object sender, EventArgs args)
		{
			Hide ();
		}

		void OnPrevClicked (object sender, EventArgs args)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;

			for (int i = current_matches.Count; i > 0; i--) {
				Match match = current_matches [i - 1] as Match;

				NoteBuffer buffer = match.Buffer;
				Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);
				Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

				if (end.Offset < cursor.Offset) {
					UpdateMatchCount (current_matches.IndexOf (match));
					JumpToMatch (match);
					return;
				}
			}

			
			UpdateMatchCount (current_matches.Count - 1);
			// Wrap to first match
			JumpToMatch (current_matches [current_matches.Count - 1] as Match);
		}

		void OnNextClicked (object sender, EventArgs args)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;

			for (int i = 0; i < current_matches.Count; i++) {
				Match match = current_matches [i] as Match;

				NoteBuffer buffer = match.Buffer;
				Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);
				Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);

				if (start.Offset >= cursor.Offset) {
					UpdateMatchCount (current_matches.IndexOf (match));
					JumpToMatch (match);
					return;
				}
			}

			UpdateMatchCount(0);
			// Else wrap to first match
			JumpToMatch (current_matches [0] as Match);
		}

		void JumpToMatch (Match match)
		{
			NoteBuffer buffer = match.Buffer;

			Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);
			Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

			// Move cursor to end of match, and select match text
			buffer.PlaceCursor (end);
			buffer.MoveMark (buffer.SelectionBound, start);

			Gtk.TextView editor = note.Window.Editor;
			editor.ScrollMarkOnscreen (buffer.InsertMark);
		}

		void OnFindEntryActivated (object sender, EventArgs args)
		{
			if (entry_changed_timeout != null) {
				entry_changed_timeout.Cancel ();
				entry_changed_timeout = null;
			}

			if (prev_search_text != null &&
			                SearchText != null &&
			                prev_search_text.CompareTo (SearchText) == 0)
				next_button.Click ();
			else
				PerformSearch (true);
		}

		void OnFindEntryChanged (object sender, EventArgs args)
		{
			if (entry_changed_timeout == null) {
				entry_changed_timeout = new InterruptableTimeout ();
				entry_changed_timeout.Timeout += EntryChangedTimeout;
			}

			if (SearchText == null) {
				PerformSearch (false);
			} else {
				entry_changed_timeout.Reset (500);
			}
		}

		// Called after .5 seconds of typing inactivity, or on explicit
		// activate.  Redo the search and update the results...
		void EntryChangedTimeout (object sender, EventArgs args)
		{
			entry_changed_timeout = null;

			if (SearchText == null)
				return;

			PerformSearch (true);
		}

		void PerformSearch (bool scroll_to_hit)
		{
			CleanupMatches ();

			string text = SearchText;
			if (text == null)
				return;

			text = text.ToLower ();

			string [] words = Search.SplitWatchingQuotes (text);

			current_matches =
			        FindMatchesInBuffer (note.Buffer, words);

			prev_search_text = SearchText;

			if (current_matches != null) {
				HighlightMatches (true);

				// Select/scroll to the first match
				if (scroll_to_hit)
					OnNextClicked (this, EventArgs.Empty);
			}

			UpdateSensitivity ();
		}

		void UpdateSensitivity ()
		{
			if (SearchText == null) {
				next_button.Sensitive = false;
				prev_button.Sensitive = false;
				ClearMatchCount ();
			}

			if (current_matches != null && current_matches.Count > 0) {
				next_button.Sensitive = true;
				prev_button.Sensitive = true;
			} else {
				next_button.Sensitive = false;
				prev_button.Sensitive = false;
				ClearMatchCount ();
			}
		}

		void UpdateSearch ()
		{
			if (note_changed_timeout == null) {
				note_changed_timeout = new InterruptableTimeout ();
				note_changed_timeout.Timeout += NoteChangedTimeout;
			}

			if (SearchText == null) {
				PerformSearch (false);
			} else {
				note_changed_timeout.Reset (500);
			}
		}

		// Called after .5 seconds of typing inactivity to update
		// the search when the text of a note changes.  This prevents
		// the search from running on every single change made in a
		// note.
		void NoteChangedTimeout (object sender, EventArgs args)
		{
			note_changed_timeout = null;

			if (SearchText == null)
				return;

			PerformSearch (false);
		}

		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			UpdateSearch ();
		}

		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			UpdateSearch ();
		}

		//
		// KeyPress and KeyRelease handlers
		//

		void KeyPressed (object sender, Gtk.KeyPressEventArgs args)
		{
			args.RetVal = true;

			switch (args.Event.Key)
			{
			case Gdk.Key.Escape:
				Hide ();
				break;
			case Gdk.Key.Shift_L:
			case Gdk.Key.Shift_R:
				shift_key_pressed = true;
				args.RetVal = false;
				break;
			case Gdk.Key.Return:
				if (shift_key_pressed)
					prev_button.Click ();
				break;
			default:
				args.RetVal = false;
				break;
			}
		}

		void KeyReleased (object sender, Gtk.KeyReleaseEventArgs args)
		{
			args.RetVal = false;

			switch (args.Event.Key)
			{
			case Gdk.Key.Shift_L:
			case Gdk.Key.Shift_R:
				shift_key_pressed = false;
				break;
			}
		}

		public Gtk.Button FindNextButton
		{
			get {
				return next_button;
			}
		}

		public Gtk.Button FindPreviousButton
		{
			get {
				return prev_button;
			}
		}

		public string SearchText
		{
			get {
				string text = entry.Text;
				if (text.Trim () == String.Empty)
					return null;

				return text.Trim ();
			}
			set {
				if (value != null && value != string.Empty)
					entry.Text = value;

				entry.GrabFocus ();
			}
		}

		void HighlightMatches (bool highlight)
		{
			if (current_matches == null || current_matches.Count == 0)
				return;

			foreach (Match match in current_matches) {
				NoteBuffer buffer = match.Buffer;

				if (match.Highlighting != highlight) {
					Gtk.TextIter start = buffer.GetIterAtMark (match.StartMark);
					Gtk.TextIter end = buffer.GetIterAtMark (match.EndMark);

					match.Highlighting = highlight;

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
				HighlightMatches (false /* unhighlight */);

				foreach (Match match in current_matches) {
					match.Buffer.DeleteMark (match.StartMark);
					match.Buffer.DeleteMark (match.EndMark);
				}

				current_matches = null;
			}

			UpdateSensitivity ();
		}

		List<Match> FindMatchesInBuffer (NoteBuffer buffer, string [] words)
		{
			List<Match> matches = new List<Match> ();

			string note_text = buffer.GetSlice (buffer.StartIter,
			                                    buffer.EndIter,
			                                    false /* hidden_chars */);
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
	}

	public class NoteTextMenu : Gtk.Menu
	{
		NoteBuffer buffer;
		UndoManager undo_manager;
		bool event_freeze;

		Gtk.ImageMenuItem undo;
		Gtk.ImageMenuItem redo;
		Gtk.CheckMenuItem bold;
		Gtk.CheckMenuItem italic;
		Gtk.CheckMenuItem strikeout;
		Gtk.RadioMenuItem normal;
		Gtk.RadioMenuItem huge;
		Gtk.RadioMenuItem large;
		Gtk.RadioMenuItem small;
		Gtk.CheckMenuItem highlight;
		Gtk.CheckMenuItem bullets;
		Gtk.ImageMenuItem increase_indent;
		Gtk.ImageMenuItem decrease_indent;
		Gtk.MenuItem increase_font;
		Gtk.MenuItem decrease_font;

		// There is a bug in GTK+ that lends to improperly themed Menus
		// and MenuItems when you derive from one of those classes; this
		// is an internal Menu that sits around to provide proper theme
		// information to override on this derived Menu
		Gtk.Menu theme_hack_menu;

		// Active when the text size is indeterminable, such as when in
		// the note's title line.
		Gtk.RadioMenuItem hidden_no_size;

		// FIXME: Tags applied to a word should hold over the space
		// between the next word, as thats where you'll start typeing.
		// Tags are only active -after- a character with that tag.  This
		// is different from the way gtk-textbuffer applies tags.

		//
		// Text menu
		//
		// Menu for font style and size, and set the active radio
		// menuitem depending on the cursor poition.
		//

		public NoteTextMenu (Gtk.AccelGroup accel_group,
		                     NoteBuffer     buffer,
		                     UndoManager    undo_manager)
			: base ()
		{
			this.buffer = buffer;
			this.undo_manager = undo_manager;

			if (undo_manager != null) {
				undo = new Gtk.ImageMenuItem (Gtk.Stock.Undo, accel_group);
				undo.Activated += UndoClicked;
				undo.AddAccelerator ("activate",
				                     accel_group,
				                     (uint) Gdk.Key.z,
				                     Gdk.ModifierType.ControlMask,
				                     Gtk.AccelFlags.Visible);
				undo.Show ();
				Append (undo);

				redo = new Gtk.ImageMenuItem (Gtk.Stock.Redo, accel_group);
				redo.Activated += RedoClicked;
				redo.AddAccelerator ("activate",
				                     accel_group,
				                     (uint) Gdk.Key.z,
				                     (Gdk.ModifierType.ControlMask |
				                      Gdk.ModifierType.ShiftMask),
				                     Gtk.AccelFlags.Visible);
				redo.Show ();
				Append (redo);

				Gtk.SeparatorMenuItem undo_spacer = new Gtk.SeparatorMenuItem ();
				Append (undo_spacer);

				// Listen to events so we can sensitize and
				// enable keybinding
				undo_manager.UndoChanged += UndoChanged;
			}

			bold = new Gtk.CheckMenuItem ("<b>" +
			                              Catalog.GetString ("_Bold") +
			                              "</b>");
			MarkupLabel (bold);
			bold.Data ["Tag"] = "bold";
			bold.Activated += FontStyleClicked;
			bold.AddAccelerator ("activate",
			                     accel_group,
			                     (uint) Gdk.Key.b,
			                     Gdk.ModifierType.ControlMask,
			                     Gtk.AccelFlags.Visible);

			italic = new Gtk.CheckMenuItem ("<i>" +
			                                Catalog.GetString ("_Italic") +
			                                "</i>");
			MarkupLabel (italic);
			italic.Data ["Tag"] = "italic";
			italic.Activated += FontStyleClicked;
			italic.AddAccelerator ("activate",
			                       accel_group,
			                       (uint) Gdk.Key.i,
			                       Gdk.ModifierType.ControlMask,
			                       Gtk.AccelFlags.Visible);

			strikeout = new Gtk.CheckMenuItem ("<s>" +
			                                   Catalog.GetString ("_Strikeout") +
			                                   "</s>");
			MarkupLabel (strikeout);
			strikeout.Data ["Tag"] = "strikethrough";
			strikeout.Activated += FontStyleClicked;
			strikeout.AddAccelerator ("activate",
			                          accel_group,
			                          (uint) Gdk.Key.s,
			                          Gdk.ModifierType.ControlMask,
			                          Gtk.AccelFlags.Visible);

			highlight = new Gtk.CheckMenuItem ("<span background='yellow'>" +
			                                   Catalog.GetString ("_Highlight") +
			                                   "</span>");
			MarkupLabel (highlight);
			highlight.Data ["Tag"] = "highlight";
			highlight.Activated += FontStyleClicked;
			highlight.AddAccelerator ("activate",
			                          accel_group,
			                          (uint) Gdk.Key.h,
			                          Gdk.ModifierType.ControlMask,
			                          Gtk.AccelFlags.Visible);

			Gtk.SeparatorMenuItem spacer1 = new Gtk.SeparatorMenuItem ();

			Gtk.MenuItem font_size = new Gtk.MenuItem (Catalog.GetString ("Font Size"));
			font_size.Sensitive = false;

			normal = new Gtk.RadioMenuItem (Catalog.GetString ("_Normal"));
			MarkupLabel (normal);
			normal.Active = true;
			normal.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.Key_0,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			normal.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.KP_0,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			normal.Activated += FontSizeActivated;

			huge = new Gtk.RadioMenuItem (normal.Group,
			                              "<span size=\"x-large\">" +
			                              Catalog.GetString ("Hu_ge") +
			                              "</span>");
			MarkupLabel (huge);
			huge.Data ["Tag"] = "size:huge";
			huge.Activated += FontSizeActivated;

			large = new Gtk.RadioMenuItem (huge.Group,
			                               "<span size=\"large\">" +
			                               Catalog.GetString ("_Large") +
			                               "</span>");
			MarkupLabel (large);
			large.Data ["Tag"] = "size:large";
			large.Activated += FontSizeActivated;

			small = new Gtk.RadioMenuItem (large.Group,
			                               "<span size=\"small\">" +
			                               Catalog.GetString ("S_mall") +
			                               "</span>");
			MarkupLabel (small);
			small.Data ["Tag"] = "size:small";
			small.Activated += FontSizeActivated;

			hidden_no_size = new Gtk.RadioMenuItem (small.Group, string.Empty);
			hidden_no_size.Hide ();

			increase_font = new Gtk.MenuItem (Catalog.GetString ("Increase Font Size"));
			increase_font.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.plus,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			increase_font.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.KP_Add,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			increase_font.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.equal,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			increase_font.Activated += IncreaseFontClicked;

			decrease_font = new Gtk.MenuItem (Catalog.GetString ("Decrease Font Size"));
			decrease_font.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.minus,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			decrease_font.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.KP_Subtract,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			decrease_font.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.underscore,
						Gdk.ModifierType.ControlMask,
						Gtk.AccelFlags.Visible);
			decrease_font.Activated += DecreaseFontClicked;

			Gtk.SeparatorMenuItem spacer2 = new Gtk.SeparatorMenuItem ();

			bullets = new Gtk.CheckMenuItem (Catalog.GetString ("Bullets"));
			bullets.Activated += ToggleBulletsClicked;

			increase_indent = new Gtk.ImageMenuItem (Gtk.Stock.Indent, accel_group);
			increase_indent.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.Right,
						Gdk.ModifierType.Mod1Mask,
						Gtk.AccelFlags.Visible);
			increase_indent.Activated += IncreaseIndentClicked;
			increase_indent.Show ();

			decrease_indent = new Gtk.ImageMenuItem (Gtk.Stock.Unindent, accel_group);
			decrease_indent.AddAccelerator ("activate",
						accel_group,
						(uint) Gdk.Key.Left,
						Gdk.ModifierType.Mod1Mask,
						Gtk.AccelFlags.Visible);
			decrease_indent.Activated += DecreaseIndentClicked;
			decrease_indent.Show ();

			RefreshState ();

			Append (bold);
			Append (italic);
			Append (strikeout);
			Append (highlight);
			Append (spacer1);
			Append (font_size);
			Append (small);
			Append (normal);
			Append (large);
			Append (huge);
			Append (increase_font);
			Append (decrease_font);
			Append (spacer2);
			Append (bullets);
			Append (increase_indent);
			Append (decrease_indent);
			ShowAll ();

			theme_hack_menu = new Menu ();
			theme_hack_menu.Realize ();
			theme_hack_menu.StyleSet += delegate {
				ModifyBg (StateType.Normal, theme_hack_menu.Style.Background (StateType.Normal));
			};
		}

		protected override void OnShown ()
		{
			RefreshState ();
			base.OnShown ();
		}

		void MarkupLabel (Gtk.MenuItem item)
		{
			Gtk.Label label = (Gtk.Label) item.Child;
			label.UseMarkup = true;
			label.UseUnderline = true;
		}

		void RefreshSizingState ()
		{
			Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);
			Gtk.TextIter selection = buffer.GetIterAtMark (buffer.SelectionBound);

			// When on title line, activate the hidden menu item
			if (cursor.Line == 0 || selection.Line == 0) {
				hidden_no_size.Active = true;
				return;
			}

			bool has_size = false;

			has_size |= huge.Active = buffer.IsActiveTag ("size:huge");
			has_size |= large.Active = buffer.IsActiveTag ("size:large");
			has_size |= small.Active = buffer.IsActiveTag ("size:small");

			normal.Active = !has_size;
		}

		public void RefreshState ()
		{
			event_freeze = true;

			bold.Active = buffer.IsActiveTag ("bold");
			italic.Active = buffer.IsActiveTag ("italic");
			strikeout.Active = buffer.IsActiveTag ("strikethrough");
			highlight.Active = buffer.IsActiveTag ("highlight");

			bool inside_bullets = buffer.IsBulletedListActive ();
			bool can_make_bulleted_list = buffer.CanMakeBulletedList ();
			bullets.Activated -= ToggleBulletsClicked;
			bullets.Active = inside_bullets;
			bullets.Activated += ToggleBulletsClicked;
			bullets.Sensitive = can_make_bulleted_list;
			increase_indent.Sensitive = inside_bullets;
			decrease_indent.Sensitive = inside_bullets;

			RefreshSizingState ();

			if (undo_manager != null) {
				undo.Sensitive = undo_manager.CanUndo;
				redo.Sensitive = undo_manager.CanRedo;
			}

			event_freeze = false;
		}

		//
		// Font-style menu item activate
		//
		// Toggle the style tag for the current text.  Style tags are
		// stored in a "Tag" member of the menuitem's Data.
		//

		void FontStyleClicked (object sender, EventArgs args)
		{
			if (event_freeze)
				return;

			Gtk.Widget item = (Gtk.Widget) sender;
			string tag = (string) item.Data ["Tag"];

			if (tag != null)
				buffer.ToggleActiveTag (tag);
		}

		//
		// Font-style menu item activate
		//
		// Set the font size tag for the current text.  Style tags are
		// stored in a "Tag" member of the menuitem's Data.
		//

		// FIXME: Change this back to use FontSizeToggled instead of using the
		// Activated signal.  Fix the Text menu so it doesn't show a specific
		// font size already selected if multiple sizes are highlighted. The
		// Activated event is used here to fix
		// http://bugzilla.gnome.org/show_bug.cgi?id=412404.
		void FontSizeActivated (object sender, EventArgs args)
		{
			if (event_freeze)
				return;

			Gtk.RadioMenuItem item = (Gtk.RadioMenuItem) sender;
			if (!item.Active)
				return;

			buffer.RemoveActiveTag ("size:huge");
			buffer.RemoveActiveTag ("size:large");
			buffer.RemoveActiveTag ("size:small");

			string tag = (string) item.Data ["Tag"];
			if (tag != null)
				buffer.SetActiveTag (tag);
		}

		void IncreaseFontClicked (object sender, EventArgs args)
		{
			if (event_freeze)
				return;

			if (buffer.IsActiveTag ("size:small")) {
				buffer.RemoveActiveTag ("size:small");
			} else if (buffer.IsActiveTag ("size:large")) {
				buffer.RemoveActiveTag ("size:large");
				buffer.SetActiveTag ("size:huge");
			} else if (buffer.IsActiveTag ("size:huge")) {
				// Maximum font size, do nothing
			} else {
				// Current font size is normal
				buffer.SetActiveTag ("size:large");
			}
		}

		void DecreaseFontClicked (object sender, EventArgs args)
		{
			if (event_freeze)
				return;

			if (buffer.IsActiveTag ("size:small")) {
				// Minimum font size, do nothing
			} else if (buffer.IsActiveTag ("size:large")) {
				buffer.RemoveActiveTag ("size:large");
			} else if (buffer.IsActiveTag ("size:huge")) {
				buffer.RemoveActiveTag ("size:huge");
				buffer.SetActiveTag ("size:large");
			} else {
				// Current font size is normal
				buffer.SetActiveTag ("size:small");
			}
		}

		void UndoClicked (object sender, EventArgs args)
		{
			if (undo_manager.CanUndo) {
				Logger.Debug ("Running undo...");
				undo_manager.Undo ();
			}
		}

		void RedoClicked (object sender, EventArgs args)
		{
			if (undo_manager.CanRedo) {
				Logger.Debug ("Running redo...");
				undo_manager.Redo ();
			}
		}

		void UndoChanged (object sender, EventArgs args)
		{
			undo.Sensitive = undo_manager.CanUndo;
			redo.Sensitive = undo_manager.CanRedo;
		}

		//
		// Bulleted list handlers
		//
		void ToggleBulletsClicked (object sender, EventArgs args)
		{
			buffer.ToggleSelectionBullets ();
		}

		void IncreaseIndentClicked (object sender, EventArgs args)
		{
			buffer.IncreaseCursorDepth ();
		}

		void DecreaseIndentClicked (object sender, EventArgs args)
		{
			buffer.DecreaseCursorDepth ();
		}
	}
}
