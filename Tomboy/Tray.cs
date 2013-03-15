using System;
using System.Collections.Generic;
using Mono.Unix;
using System.Runtime.InteropServices;
#if !WIN32 && !MAC
using GtkBeans;
#endif

namespace Tomboy
{
	public class NoteMenuItem : Gtk.ImageMenuItem
	{
		Note note;
		Gtk.Image pin_img;
		bool pinned;
		bool inhibit_activate;

		static Gdk.Pixbuf note_icon;
		static Gdk.Pixbuf pinup;
		static Gdk.Pixbuf pinup_active;
		static Gdk.Pixbuf pindown;

		static NoteMenuItem ()
		{
			// Cache this since we use it a lot.
			note_icon = GuiUtils.GetIcon ("note", 16);
			pinup = GuiUtils.GetIcon ("pin-up", 16);
			pinup_active = GuiUtils.GetIcon ("pin-active", 16);
			pindown = GuiUtils.GetIcon ("pin-down", 16);
		}

		public NoteMenuItem (Note note, bool show_pin)
			: base (GetDisplayName(note))
		{
			this.note = note;
			Image = new Gtk.Image (note_icon);
#if HAS_GTK_2_16
			this.SetAlwaysShowImage (true);
#endif
			if (show_pin) {
				Gtk.HBox box = new Gtk.HBox (false, 0);
				Gtk.Widget child = Child;
				Remove (child);
				box.PackStart (child, true, true, 0);
				Add (box);
				box.Show();

				pinned = note.IsPinned;
				pin_img = new Gtk.Image(pinned ? pindown : pinup);
				pin_img.Show();
				box.PackStart (pin_img, false, false, 0);
			}
		}

		static string FormatForLabel (string name)
		{
			// Replace underscores ("_") with double-underscores ("__")
			// so Note menuitems are not created with mnemonics.
			return name.Replace ("_", "__");
		}

		static string GetDisplayName (Note note)
		{
			string display_name = note.Title;
			int max_length = (int) Preferences.Get (Preferences.MENU_ITEM_MAX_LENGTH);

			if (note.IsNew) {
				string new_string = Catalog.GetString (" (new)");
				max_length -= new_string.Length;
				display_name = Ellipsify (display_name, max_length)
					+ new_string;
			} else {
				display_name = Ellipsify (display_name, max_length);
			}

			return FormatForLabel (display_name);
		}

		static string Ellipsify (string str, int max)
		{
			if(str.Length > max)
				return str.Substring(0, max - 1) + "...";
			return str;
		}

		protected override void OnActivated ()
		{
			if (!inhibit_activate) {
				if (note != null)
					note.Window.Present ();
			}
		}

		protected override bool OnButtonPressEvent (Gdk.EventButton ev)
		{
			if (pin_img != null &&
			                ev.X >= pin_img.Allocation.X &&
			                ev.X < pin_img.Allocation.X + pin_img.Allocation.Width) {
				pinned = note.IsPinned = !pinned;
				pin_img.Pixbuf = pinned ? pindown : pinup;
				inhibit_activate = true;
				return true;
			}
			return base.OnButtonPressEvent (ev);
		}

		protected override bool OnButtonReleaseEvent (Gdk.EventButton ev)
		{
			if (inhibit_activate) {
				inhibit_activate = false;
				return true;
			}
			return base.OnButtonReleaseEvent (ev);
		}

		protected override bool OnMotionNotifyEvent (Gdk.EventMotion ev)
		{
			if (!pinned && pin_img != null) {
				if (ev.X >= pin_img.Allocation.X &&
				                ev.X < pin_img.Allocation.X + pin_img.Allocation.Width) {
					if (pin_img.Pixbuf != pinup_active)
						pin_img.Pixbuf = pinup_active;
				} else if (pin_img.Pixbuf != pinup) {
					pin_img.Pixbuf = pinup;
				}
			}
			return base.OnMotionNotifyEvent (ev);
		}

		protected override bool OnLeaveNotifyEvent (Gdk.EventCrossing ev)
		{
			if (!pinned && pin_img != null) {
				pin_img.Pixbuf = pinup;
			}
			return base.OnLeaveNotifyEvent (ev);
		}
	}
	
	
	public class TomboyTrayIcon : Gtk.StatusIcon, ITomboyTray
	{
		TomboyTray tray;
		TomboyPrefsKeybinder keybinder;
		Gtk.Menu context_menu;
		Gtk.ImageMenuItem sync_menu_item;

		public TomboyTrayIcon (NoteManager manager)
		{
			tray = new TomboyTray (manager, this);
			keybinder = new TomboyPrefsKeybinder (manager, this);
			int panel_size = 22;
			// Load Icon to display in the notification area.
			// First we try the "tomboy-panel" icon. This icon can be replaced
			// by the user's icon theme. If the theme does not have this icon
			// then we fall back to the Tomboy Menu icon named "tomboy".
			Pixbuf = GuiUtils.GetIcon ("tomboy-panel", panel_size) ??
				GuiUtils.GetIcon ("tomboy", panel_size);

			Tooltip = TomboyTrayUtils.GetToolTipText ();

			Visible = (bool) Preferences.Get (Preferences.ENABLE_TRAY_ICON);
			Preferences.SettingChanged += (o, args) => {
				if (args.Key == Preferences.ENABLE_TRAY_ICON)
					Visible = (bool) args.Value;
			};

			Tomboy.ExitingEvent += OnExit;
#if MAC
			Visible = false;
#endif
		}
		
		public TomboyTray Tray
		{
			get {
				return tray;
			}
		}
		
		protected override void OnActivate()
		{
			ShowMenu (false);
		}
		
		protected override void OnPopupMenu (uint button, uint activate_time)
		{
			if (button == 3)
				GuiUtils.PopupMenu (GetRightClickMenu (),
				                    null, 
				                    new Gtk.MenuPositionFunc (GetTrayMenuPosition));
				
		}
		
		public void ShowMenu (bool select_first_item)
		{
			if (context_menu != null)
				context_menu.Hide ();

			tray.NoteManager.GtkInvoke (() => {
				TomboyTrayUtils.UpdateTomboyTrayMenu (tray, null);
				if (select_first_item)
					tray.TomboyTrayMenu.SelectFirst (false);

				GuiUtils.PopupMenu (tray.TomboyTrayMenu, null,
					new Gtk.MenuPositionFunc (GetTrayMenuPosition));
			});
		}
		
		public void GetTrayMenuPosition (Gtk.Menu menu,
		                             out int  x,
		                             out int  y,
		                             out bool push_in)
		{
			// some default values in case something goes wrong
			push_in = true;
			x = 0;
			y = 0;
			
			Gdk.Screen screen;
			Gdk.Rectangle area;
			try {
#if WIN32 || MAC
				menu.Screen.Display.GetPointer (out x, out y);
				screen = menu.Screen;
				area.Height = 0;
#else
				Gtk.Orientation orientation;
				GetGeometry (out screen, out area, out orientation);
				x = area.X;
				y = area.Y;
#endif

				Gtk.Requisition menu_req = menu.SizeRequest ();
				if (y + menu_req.Height >= screen.Height)
					y -= menu_req.Height;
				else
					y += area.Height;
			} catch (Exception e) {
				Logger.Error ("Exception in GetTrayMenuPosition: " + e.ToString ());
			}
		}

		void Preferences_SettingChanged (object sender, EventArgs args)
		{
			// Update items based on configuration.
			UpdateMenuItems ();
		}

		void UpdateMenuItems ()
		{
			// Is synchronization configured and active?
			string sync_addin_id = Preferences.Get (Preferences.SYNC_SELECTED_SERVICE_ADDIN)
				as string;
			sync_menu_item.Sensitive = !string.IsNullOrEmpty (sync_addin_id);
		}

		Gtk.Menu GetRightClickMenu ()
		{
			if (tray.TomboyTrayMenu != null)
				tray.TomboyTrayMenu.Hide ();

			if (context_menu != null) {
				context_menu.Hide ();
				return context_menu;
			}

			context_menu = new Gtk.Menu ();

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			context_menu.AccelGroup = accel_group;

			Gtk.ImageMenuItem item;

			sync_menu_item = new Gtk.ImageMenuItem (Catalog.GetString ("S_ynchronize Notes"));
			sync_menu_item.Image = new Gtk.Image (Gtk.Stock.Convert, Gtk.IconSize.Menu);
			UpdateMenuItems();
			Preferences.SettingChanged += Preferences_SettingChanged;
			sync_menu_item.Activated += SyncNotes;
			context_menu.Append (sync_menu_item);

			context_menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Preferences"));
			item.Image = new Gtk.Image (Gtk.Stock.Preferences, Gtk.IconSize.Menu);
			item.Activated += ShowPreferences;
			context_menu.Append (item);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Help"));
			item.Image = new Gtk.Image (Gtk.Stock.Help, Gtk.IconSize.Menu);
			item.Activated += ShowHelpContents;
			context_menu.Append (item);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_About Tomboy"));
			item.Image = new Gtk.Image (Gtk.Stock.About, Gtk.IconSize.Menu);
			item.Activated += ShowAbout;
			context_menu.Append (item);

			context_menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Quit"));
			item.Image = new Gtk.Image (Gtk.Stock.Quit, Gtk.IconSize.Menu);
			item.Activated += Quit;
			context_menu.Append (item);

			context_menu.ShowAll ();
			return context_menu;
		}

		void ShowPreferences (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["ShowPreferencesAction"].Activate ();
		}

		void SyncNotes (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["NoteSynchronizationAction"].Activate ();
		}

		void ShowHelpContents (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["ShowHelpAction"].Activate ();
		}

		void ShowAbout (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["ShowAboutAction"].Activate ();
		}

		void Quit (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["QuitTomboyAction"].Activate ();
		}

		void OnExit (object sender, EventArgs e)
		{
			Visible = false;
		}

		public bool MenuOpensUpward ()
		{
			bool open_upwards = false;
			int val = 0;
			Gdk.Screen screen = null;
#if WIN32 || MAC
			int x;
			tray.TomboyTrayMenu.Screen.Display.GetPointer (out x, out val);
			screen = tray.TomboyTrayMenu.Screen;
#else
			Gdk.Rectangle area;
			Gtk.Orientation orientation;
			GetGeometry (out screen, out area, out orientation);
			val = area.Y;
#endif

			Gtk.Requisition menu_req = tray.TomboyTrayMenu.SizeRequest ();
			if (val + menu_req.Height >= screen.Height)
				open_upwards = true;

			return open_upwards;
		}
	}

	// TODO: Some naming love would be nice
	public interface ITomboyTray
	{
		void ShowMenu (bool select_first_item);
		bool MenuOpensUpward ();
	}
	
	public class TomboyTray
	{
		NoteManager manager;
		ITomboyTray tray;
		bool menu_added = false;
		List<Gtk.MenuItem> recent_notes = new List<Gtk.MenuItem> ();
		Gtk.Menu tray_menu;
		
		protected TomboyTray (NoteManager manager)
		{
			this.manager = manager;
			
			tray_menu = MakeTrayNotesMenu ();
		}
		
		public TomboyTray (NoteManager manager, ITomboyTray tray)
			: this (manager)
		{
			this.tray = tray;
		}
		
		Gtk.Menu MakeTrayNotesMenu ()
		{
			Gtk.Menu menu =
			        Tomboy.ActionManager.GetWidget ("/TrayIconMenu") as Gtk.Menu;

			bool enable_keybindings = (bool)
			                          Preferences.Get (Preferences.ENABLE_KEYBINDINGS);
			if (enable_keybindings) {
				// Create New Note Keybinding
				Gtk.MenuItem item =
				        Tomboy.ActionManager.GetWidget (
				                "/TrayIconMenu/TrayNewNotePlaceholder/TrayNewNote") as Gtk.MenuItem;
				if (item != null)
					GConfKeybindingToAccel.AddAccelerator (
					        item,
					        Preferences.KEYBINDING_CREATE_NEW_NOTE);

				// Show Search All Notes Keybinding
				item =
				        Tomboy.ActionManager.GetWidget (
				                "/TrayIconMenu/ShowSearchAllNotes") as Gtk.MenuItem;
				if (item != null)
					GConfKeybindingToAccel.AddAccelerator (
					        item,
					        Preferences.KEYBINDING_OPEN_RECENT_CHANGES);

				// Open Start Here Keybinding
				item =
				        Tomboy.ActionManager.GetWidget (
				                "/TrayIconMenu/OpenStartHereNote") as Gtk.MenuItem;
				if (item != null)
					GConfKeybindingToAccel.AddAccelerator (
					        item,
					        Preferences.KEYBINDING_OPEN_START_HERE);
			}

			return menu;
		}
		
		void RemoveRecentlyChangedNotes ()
		{
			foreach (Gtk.Widget item in recent_notes) {
				tray_menu.Remove (item);
			}

			recent_notes.Clear ();
		}
		
		public void AddRecentlyChangedNotes ()
		{
			int min_size = (int) Preferences.Get (Preferences.MENU_NOTE_COUNT);
			int max_size = (int) Preferences.Get (Preferences.MENU_MAX_NOTE_COUNT);
			if (max_size < min_size)
				max_size = min_size;
			int list_size = 0;
			bool menuOpensUpward = tray.MenuOpensUpward ();
			NoteMenuItem item;

			// Remove the old dynamic items
			RemoveRecentlyChangedNotes ();

			// Assume menu opens downward, move common items to top of menu
			Gtk.MenuItem newNoteItem = Tomboy.ActionManager.GetWidget (
			                                   "/TrayIconMenu/TrayNewNotePlaceholder/TrayNewNote") as Gtk.MenuItem;
			Gtk.MenuItem searchNotesItem = Tomboy.ActionManager.GetWidget (
			                                       "/TrayIconMenu/ShowSearchAllNotes") as Gtk.MenuItem;
			tray_menu.ReorderChild (newNoteItem, 0);
			int insertion_point = 1; // If menu opens downward
			
			// Find all child widgets under the TrayNewNotePlaceholder
			// element.  Make sure those added by add-ins are
			// properly accounted for and reordered.
			List<Gtk.Widget> newNotePlaceholderWidgets = new List<Gtk.Widget> ();
			IList<Gtk.Widget> allChildWidgets =
				Tomboy.ActionManager.GetPlaceholderChildren ("/TrayIconMenu/TrayNewNotePlaceholder");
			foreach (Gtk.Widget child in allChildWidgets) {
				if (child is Gtk.MenuItem &&
				    child != newNoteItem) {
					newNotePlaceholderWidgets.Add (child);
					tray_menu.ReorderChild (child, insertion_point);
					insertion_point++;
				}
			}
			
			tray_menu.ReorderChild (searchNotesItem, insertion_point);
			insertion_point++;

			DateTime days_ago = DateTime.Today.AddDays (-3);
			
			// Prevent template notes from appearing in the menu
			Tag template_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);

			// List the most recently changed notes, any currently
			// opened notes, and any pinned notes...
			foreach (Note note in manager.Notes) {
				if (note.IsSpecial)
					continue;
				
				// Skip template notes
				if (note.ContainsTag (template_tag))
					continue;

				bool show = false;

				// Test for note.IsPinned first so that all of the pinned notes
				// are guaranteed to be included regardless of the size of the
				// list.
				if (note.IsPinned) {
					show = true;
				} else if ((note.IsOpened && note.Window.IsMapped) ||
				                note.ChangeDate > days_ago ||
				                list_size < min_size) {
					if (list_size <= max_size)
						show = true;
				}

				if (show) {
					item = new NoteMenuItem (note, true);
					// Add this widget to the menu (+insertion_point to add after new+search+...)
					tray_menu.Insert (item, list_size + insertion_point);
					// Keep track of this item so we can remove it later
					recent_notes.Add (item);

					list_size++;
				}
			}

			Note start = manager.FindByUri (NoteManager.StartNoteUri);
			if (start != null) {
				item = new NoteMenuItem (start, false);
				if (menuOpensUpward)
					tray_menu.Insert (item, list_size + insertion_point);
				else
					tray_menu.Insert (item, insertion_point);
				recent_notes.Add (item);

				list_size++;
				
				bool enable_keybindings = (bool)
					                  Preferences.Get (Preferences.ENABLE_KEYBINDINGS);
				if (enable_keybindings)
					GConfKeybindingToAccel.AddAccelerator (
					        item,
					        Preferences.KEYBINDING_OPEN_START_HERE);
			}


			// FIXME: Rearrange this stuff to have less wasteful reordering
			if (menuOpensUpward) {
				// Relocate common items to bottom of menu
				insertion_point -= 1;
				tray_menu.ReorderChild (searchNotesItem, list_size + insertion_point);
				foreach (Gtk.Widget widget in newNotePlaceholderWidgets)
					tray_menu.ReorderChild (widget, list_size + insertion_point);
				tray_menu.ReorderChild (newNoteItem, list_size + insertion_point);
				insertion_point = list_size;
			}

			Gtk.SeparatorMenuItem separator = new Gtk.SeparatorMenuItem ();
			tray_menu.Insert (separator, insertion_point);
			recent_notes.Add (separator);
		}

		public bool IsMenuAdded
		{
			get { return menu_added; }
			set { menu_added = value; }
		}

		public Gtk.Menu TomboyTrayMenu
		{
			get { return tray_menu; }
		}
		
		public ITomboyTray Tray
		{
			get { return tray; }
		}
		
		public NoteManager NoteManager
		{
			get { return manager; }
		}
	}
	
	public class TomboyTrayUtils
	{
	
		public static string GetToolTipText ()
		{
			string tip_text = Catalog.GetString ("Tomboy Notes");

			if ((bool) Preferences.Get (Preferences.ENABLE_KEYBINDINGS)) {
				string shortcut =
				        GConfKeybindingToAccel.GetShortcut (
				                Preferences.KEYBINDING_SHOW_NOTE_MENU);
				if (shortcut != null)
					tip_text += String.Format (" ({0})", shortcut);
			}
			
			return tip_text;
		}
		
		public static void UpdateTomboyTrayMenu (TomboyTray tray, Gtk.Widget parent)
		{
			if (!tray.IsMenuAdded) {
				if (parent != null)
					tray.TomboyTrayMenu.AttachToWidget (parent, GuiUtils.DetachMenu);
				tray.IsMenuAdded = true;
			}

			tray.AddRecentlyChangedNotes ();

			tray.TomboyTrayMenu.ShowAll ();
		}
	
	}

	//
	// This is a helper to take the XKeybinding string from GConf, and
	// convert it to a widget accelerator label, so note menu items can
	// display their global X keybinding.
	//
	// FIXME: It would be totally sweet to allow setting the accelerator
	// visually through the menuitem, and have the new value be stored in
	// GConf.
	//
	public class GConfKeybindingToAccel
	{
		static Gtk.AccelGroup accel_group;

		static GConfKeybindingToAccel ()
		{
			accel_group = new Gtk.AccelGroup ();
		}

		public static string GetShortcut (string gconf_path)
		{
			try {
				string binding = (string) Preferences.Get (gconf_path);
				if (binding == null ||
				                binding == String.Empty ||
				                binding == "disabled")
					return null;

				binding = binding.Replace ("<", "");
				binding = binding.Replace (">", "-");

				return binding;
			} catch {
				return null;
			}
		}

		public static void AddAccelerator (Gtk.MenuItem item, string gconf_path)
		{
			uint keyval;
			Gdk.ModifierType mods;

			if (Services.Keybinder.GetAccelKeys (gconf_path, out keyval, out mods))
				item.AddAccelerator ("activate",
				                     accel_group,
				                     keyval,
				                     mods,
				                     Gtk.AccelFlags.Visible);
		}
	}
}
