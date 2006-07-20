
using System;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Unix;

namespace Tomboy
{
	public class NoteEditor : Gtk.TextView
	{
		public NoteEditor (Gtk.TextBuffer buffer)
			: base (buffer)
		{
			WrapMode = Gtk.WrapMode.Word;
			LeftMargin = DefaultMargin;
			RightMargin = DefaultMargin;
			CanDefault = true;

			// Make sure the cursor position is visible
			ScrollMarkOnscreen (buffer.InsertMark);

			// Set Font from GConf preference
			if ((bool) Preferences.Get (Preferences.ENABLE_CUSTOM_FONT)) {
				string font_string = (string) 
					Preferences.Get (Preferences.CUSTOM_FONT_FACE);
				ModifyFont (Pango.FontDescription.FromString (font_string));
			}
			Preferences.SettingChanged += OnFontSettingChanged;

			// Set extra editor drag targets supported (in addition
			// to the default TextView's various text formats)...
			//// Gtk.TargetList list = Gtk.Drag.DestGetTargetList (this);

			IntPtr list_ptr = gtk_drag_dest_get_target_list (this.Handle);
			Gtk.TargetList list = new Gtk.TargetList (list_ptr);

			list.Add (Gdk.Atom.Intern ("text/uri-list", false), 0, 1);
			list.Add (Gdk.Atom.Intern ("_NETSCAPE_URL", false), 0, 1);
		}

		// FIXME: Gtk# broke compatibility at some point with the
		// methodref for DestGetTargetList.  We invoke it manually so
		// our binary will work for everyone.
		[DllImport("libgtk-win32-2.0-0.dll")]
		static extern IntPtr gtk_drag_dest_get_target_list (IntPtr raw);

		public static int DefaultMargin
		{
			get { return 8; }
		}

		//
		// Update the font based on the changed Preference dialog setting.
		//
		void OnFontSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			switch (args.Key) {
			case Preferences.ENABLE_CUSTOM_FONT:
				Logger.Log ("Switching note font {0}...", 
					    (bool) args.Value ? "ON" : "OFF");

				if ((bool) args.Value) {
					string font_string = (string) 
						Preferences.Get (Preferences.CUSTOM_FONT_FACE);
					ModifyFont (Pango.FontDescription.FromString (font_string));
				} else
					ModifyFont (new Pango.FontDescription ());

				break;

			case Preferences.CUSTOM_FONT_FACE:
				Logger.Log ("Switching note font to '{0}'...", 
					    (string) args.Value);

				ModifyFont (Pango.FontDescription.FromString ((string) args.Value));
				break;
			}
		}

		//
		// DND Drop handling
		//
		protected override void OnDragDataReceived (Gdk.DragContext context, 
							    int x,
							    int y,
							    Gtk.SelectionData selection_data,
							    uint info,
							    uint time)
		{
			bool has_url = false;

			foreach (Gdk.Atom target in context.Targets) {
				if (target.Name == "text/uri-list" ||
				    target.Name == "_NETSCAPE_URL") {
					has_url = true;
					break;
				}
			}

			if (has_url) {
				UriList uri_list = new UriList (selection_data);
				StringBuilder insert = new StringBuilder ();

				foreach (Uri uri in uri_list) {
					Logger.Log ("Got Dropped URI: {0}", uri);

					// FIXME: The space here is a hack
					// around a bug in the URL Regex which
					// matches across newlines.
					if (insert.Length > 0)
						insert.Append (" \n");

					if (uri.IsFile) 
						insert.Append (uri.LocalPath);
					else
						insert.Append (uri.ToString ());
				}

				if (insert.Length > 0) {
					Buffer.InsertWithTags (
						Buffer.GetIterAtMark (Buffer.InsertMark),
						insert.ToString (),
						Buffer.TagTable.Lookup ("link:url"));
				}

				Gtk.Drag.Finish (context, insert.Length > 0, false, time);
			} else {
				base.OnDragDataReceived (context, x, y, selection_data, info, time);
			}
		}
	}

	public class NoteWindow : ForcedPresentWindow 
	{
		Note note;

		Gtk.AccelGroup accel_group;
		Gtk.Toolbar toolbar;
		Gtk.Widget link_button;
		NoteTextMenu text_menu;
		Gtk.Menu plugin_menu;
		Gtk.TextView editor;
		Gtk.ScrolledWindow editor_window;

		GlobalKeybinder global_keys;
		InterruptableTimeout mark_set_timeout;

		static Gdk.Pixbuf stock_notes;

		static NoteWindow ()
		{
			stock_notes = new Gdk.Pixbuf (null, "stock_notes.png");
		}

		// 
		// Construct a window to display a note
		// 
		// Currently a toolbar with Link, Search, Text, Delete buttons
		// and a Gtk.TextView as the body.
		// 

		public NoteWindow (Note note) : 
			base (note.Title) 
		{
			this.note = note;
			this.Icon = stock_notes;
			this.SetDefaultSize (450, 360);

			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			text_menu = new NoteTextMenu (accel_group, note.Buffer, note.Buffer.Undoer);
			plugin_menu = MakePluginMenu ();

			toolbar = MakeToolbar ();
			toolbar.Show ();

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

			Gtk.VBox box = new Gtk.VBox (false, 2);
			box.PackStart (toolbar, false, false, 0);
			box.PackStart (editor_window, true, true, 0);
			box.Show ();

			// NOTE: Since some of our keybindings are only
			// available in the context menu, and the context menu
			// is created on demand, register them with the
			// global keybinder
			global_keys = new GlobalKeybinder (accel_group);

			// Close window (Ctrl-W)
			global_keys.AddAccelerator (new EventHandler (CloseWindowHandler),
						    (uint) Gdk.Key.w, 
						    Gdk.ModifierType.ControlMask,
						    Gtk.AccelFlags.Visible);

			// Close window (Escape)
			global_keys.AddAccelerator (new EventHandler (CloseWindowHandler),
						    (uint) Gdk.Key.Escape, 
						    0,
						    Gtk.AccelFlags.Visible);

			// Close all windows on current Desktop (Ctrl-Q)
			global_keys.AddAccelerator (new EventHandler (CloseAllWindowsHandler),
						    (uint) Gdk.Key.q, 
						    Gdk.ModifierType.ControlMask,
						    Gtk.AccelFlags.Visible);

			// Open Find Dialog (Ctrl-F)
			global_keys.AddAccelerator (new EventHandler (FindActivate),
						    (uint) Gdk.Key.f, 
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

			this.Add (box);
		}

		protected override bool OnDeleteEvent (Gdk.Event evnt)
		{
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
		}

		//
		// Delete this Note.
		//

		void DeleteButtonClicked () 
		{
			HIGMessageDialog dialog = 
				new HIGMessageDialog (
					note.Window,
					Gtk.DialogFlags.DestroyWithParent,
					Gtk.MessageType.Question,
					Gtk.ButtonsType.None,
					Catalog.GetString ("Really delete this note?"),
					Catalog.GetString ("If you delete a note it is " +
							   "permanently lost."));

			Gtk.Button button;

			button = new Gtk.Button (Gtk.Stock.Cancel);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, Gtk.ResponseType.Cancel);
			dialog.DefaultResponse = Gtk.ResponseType.Cancel;

			button = new Gtk.Button (Gtk.Stock.Delete);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, 666);

			int result = dialog.Run ();
			if (result == 666 ) {
				// This will destroy our window...
				note.Manager.Delete (note);
			}

			dialog.Destroy();
		}

		//
		// Public Children Accessors
		//

		public Gtk.TextView Editor {
			get { return editor; }
		}

		public Gtk.Toolbar Toolbar {
			get { return toolbar; }
		}

		public Gtk.Menu PluginMenu {
			get { return plugin_menu; }
		}

		public Gtk.Menu TextMenu {
			get { return text_menu; }
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

			Logger.Log ("Populating context menu...");

			// Remove the lame-o gigantic Insert Unicode Control
			// Characters menu item.
			Gtk.Widget lame_unicode;
			lame_unicode = (Gtk.Widget) 
				args.Menu.Children [args.Menu.Children.Length - 1];
			args.Menu.Remove (lame_unicode);

			Gtk.MenuItem spacer1 = new Gtk.SeparatorMenuItem ();
			spacer1.Show ();

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
				new Gtk.ImageMenuItem (Catalog.GetString ("_Search"));
			find_item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			find_item.Submenu = MakeFindMenu ();
			find_item.Show ();

			Gtk.MenuItem spacer2 = new Gtk.SeparatorMenuItem ();
			spacer2.Show ();

			args.Menu.Prepend (spacer1);
			args.Menu.Prepend (text_item);
			args.Menu.Prepend (find_item);
			args.Menu.Prepend (link);

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
			Gtk.Toolbar toolbar = new Gtk.Toolbar ();
			toolbar.Tooltips = true;

			Gtk.Widget find = 
				toolbar.AppendItem (
					Catalog.GetString ("Search"), 
					Catalog.GetString ("Search your notes"),
					null, 
					new Gtk.Image (Gtk.Stock.Find, toolbar.IconSize),
					new Gtk.SignalFunc (FindButtonClicked));
			find.AddAccelerator ("activate",
					     accel_group,
					     (uint) Gdk.Key.f, 
					     Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);

			link_button = 
				toolbar.AppendItem (
					Catalog.GetString ("Link"), 
					Catalog.GetString ("Link selected text to a new note"), 
					null, 
					new Gtk.Image (Gtk.Stock.JumpTo, toolbar.IconSize),
					new Gtk.SignalFunc (LinkButtonClicked));
			link_button.Sensitive = (note.Buffer.Selection != null);
			link_button.AddAccelerator ("activate",
						    accel_group,
						    (uint) Gdk.Key.l, 
						    Gdk.ModifierType.ControlMask,
						    Gtk.AccelFlags.Visible);

			ToolMenuButton text_button = 
				new ToolMenuButton (toolbar,
						    Gtk.Stock.SelectFont,
						    Catalog.GetString ("_Text"),
						    text_menu);
			text_button.IsImportant = true;
			text_button.Show ();
			toolbar.AppendWidget (text_button, 
					      Catalog.GetString ("Set properties of text"), 
					      null);

			ToolMenuButton plugin_button = 
				new ToolMenuButton (toolbar, 
						    Gtk.Stock.Execute,
						    Catalog.GetString ("T_ools"),
						    plugin_menu);
			plugin_button.Show ();
			toolbar.AppendWidget (plugin_button, 
					      Catalog.GetString ("Use tools on this note"), 
					      null);

			toolbar.AppendSpace ();

		        Gtk.Widget delete = 
				toolbar.AppendItem (
					Catalog.GetString ("Delete"), 
					Catalog.GetString ("Delete this note"), 
					null, 
					new Gtk.Image (Gtk.Stock.Delete, toolbar.IconSize),
					new Gtk.SignalFunc (DeleteButtonClicked));

			// Don't allow deleting the "Start Here" note...
			if (note.IsSpecial)
				delete.Sensitive = false;

			return toolbar;
		}

		//
		// Plugin toolbar menu
		//
		// Prefixed with Open Plugins Folder action, the rest being
		// populated by individual plugins using
		// NotePlugin.AddPluginMenuItem().
		//

		Gtk.Menu MakePluginMenu ()
		{
			Gtk.Menu menu = new Gtk.Menu ();

			Gtk.ImageMenuItem open;
			open = new Gtk.ImageMenuItem (Catalog.GetString ("_Open Plugins Folder"));
			open.Image = new Gtk.Image (Gtk.Stock.Open, Gtk.IconSize.Menu);
			open.Activated += OnOpenPluginsFolderActivate;
			open.Show ();
			menu.Add (open);

			Gtk.SeparatorMenuItem sep = new Gtk.SeparatorMenuItem ();
			sep.Show ();
			menu.Add (sep);

			return menu;
		}

		void OnOpenPluginsFolderActivate (object sender, EventArgs args)
		{
			note.Manager.PluginManager.ShowPluginsDirectory ();
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
				new Gtk.ImageMenuItem (Catalog.GetString ("_Search..."));
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

		public NoteFindDialog Find {
			get {
				return NoteFindDialog.GetInstance (note);
			}
		}

		void FindButtonClicked ()
		{
			Find.SearchText = note.Buffer.Selection;
			Find.Present ();
		}

		void FindNextActivate (object sender, EventArgs args)
		{
			Find.FindNextButton.Click ();
		}

		void FindPreviousActivate (object sender, EventArgs args)
		{
			Find.FindPreviousButton.Click ();
		}

		// 
		// Signature trampoline for editor context-menu "Find"
		//

		void FindActivate (object sender, EventArgs args)
		{
			FindButtonClicked ();
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

		// 
		// Signature trampoline for editor context-menu "Link to Note"
		//

		void LinkToNoteActivate (object sender, EventArgs args)
		{
			LinkButtonClicked ();
		}
	}

	public class NoteTextMenu : Gtk.Menu
	{
		Gtk.AccelGroup accel_group;
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
			this.accel_group = accel_group;
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
			normal.Toggled += FontSizeClicked;

			huge = new Gtk.RadioMenuItem (normal.Group, 
						      "<span size=\"x-large\">" +
						      Catalog.GetString ("Hu_ge") +
						      "</span>");
			MarkupLabel (huge);
			huge.Data ["Tag"] = "size:huge";
			huge.Toggled += FontSizeClicked;

			large = new Gtk.RadioMenuItem (huge.Group, 
						       "<span size=\"large\">" +
						       Catalog.GetString ("_Large") +
						       "</span>");
			MarkupLabel (large);
			large.Data ["Tag"] = "size:large";
			large.Toggled += FontSizeClicked;

			small = new Gtk.RadioMenuItem (large.Group, 
						       "<span size=\"small\">" +
						       Catalog.GetString ("S_mall") +
						       "</span>");
			MarkupLabel (small);
			small.Data ["Tag"] = "size:small";
			small.Toggled += FontSizeClicked;

			hidden_no_size = new Gtk.RadioMenuItem (small.Group, string.Empty);
			hidden_no_size.Hide ();

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
			ShowAll ();
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

		void FontSizeClicked (object sender, EventArgs args) 
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

		void UndoClicked (object sender, EventArgs args)
		{
			if (undo_manager.CanUndo) {
				Logger.Log ("Running undo...");
				undo_manager.Undo ();
			}
		}

		void RedoClicked (object sender, EventArgs args)
		{
			if (undo_manager.CanRedo) {
				Logger.Log ("Running redo...");
				undo_manager.Redo ();
			}
		}

		void UndoChanged (object sender, EventArgs args)
		{
			undo.Sensitive = undo_manager.CanUndo;
			redo.Sensitive = undo_manager.CanRedo;
		}
	}
}
