
using System;
using System.Collections;
using Mono.Posix;
using System.Runtime.InteropServices;

namespace Tomboy
{
	public class TomboyTray 
	{
		NoteManager manager;
		Egg.TrayIcon icon;
		Gtk.Tooltips tips;

		static Gdk.Pixbuf tintin;
		static Gdk.Pixbuf stock_notes;

		static TomboyTray ()
		{
			tintin = GuiUtils.GetMiniIcon ("tintin.png");
			stock_notes = GuiUtils.GetMiniIcon ("stock_notes.png");
		}

		public TomboyTray (NoteManager manager) 
		{
			this.manager = manager;

			Gtk.EventBox ev = new Gtk.EventBox ();
			ev.CanFocus = true;
			ev.ButtonPressEvent += ButtonPress;
			ev.Add (new Gtk.Image (tintin));

			string tip_text = "Tomboy Notes";

			string shortcut = 
				GConfKeybindingToAccel.GetShortcut (
					TomboyGConfXKeybinder.MENU_BINDING);
			if (shortcut != null)
				tip_text += String.Format (" ({0})", shortcut);

			tips = new Gtk.Tooltips ();
			tips.SetTip (ev, tip_text, null);
			tips.Enable ();

			icon = new Egg.TrayIcon (Catalog.GetString ("Tomboy"));
			icon.Add (ev);
			icon.ShowAll ();
		}

		public void ShowMenu ()
		{
			Gtk.Menu recent_menu = MakeRecentNotesMenu (icon);
			GuiUtils.PopupMenu (recent_menu, null);
		}

		void ButtonPress (object sender, Gtk.ButtonPressEventArgs args) 
		{
			Gtk.Widget parent = (Gtk.Widget) sender;
			Gtk.Menu recent_menu = MakeRecentNotesMenu (parent);
			GuiUtils.PopupMenu (recent_menu, args.Event);
		}

		Gtk.Menu MakeRecentNotesMenu (Gtk.Widget parent) 
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AttachToWidget (parent, null);

			Gtk.ImageMenuItem item;

			item = new Gtk.ImageMenuItem (Catalog.GetString ("Create _New Note"));
			item.Image = new Gtk.Image (Gtk.Stock.New, Gtk.IconSize.Menu);
			item.Activated += CreateNewNote;
			menu.Append (item);

			GConfKeybindingToAccel.AddAccelerator (
				item, 
				TomboyGConfXKeybinder.NEW_NOTE_BINDING);

			// FIXME: Pull this from GConf
			int list_size = 5; // Number of recent entries to list

			// List the i most recently changed notes, and any
			// currently opened notes...
			foreach (Note note in manager.Notes) {
				if (note.IsSpecial)
					continue;

				if (note.IsOpened || list_size > 0) {
					item = MakeNoteMenuItem (note);
					menu.Append (item);
				}

				list_size--;
			}

			uint keyval;
			Gdk.ModifierType mods;

			Note start = manager.Find (Catalog.GetString ("Start Here"));
			if (start != null) {
				item = MakeNoteMenuItem (start);
				menu.Append (item);

				GConfKeybindingToAccel.AddAccelerator (
					item, 
					TomboyGConfXKeybinder.START_BINDING);
			}

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Recent Changes"));
			item.Image = new Gtk.Image (Gtk.Stock.SortAscending, Gtk.IconSize.Menu);
			item.Activated += ViewRecentChanges;
			menu.Append (item);

			GConfKeybindingToAccel.AddAccelerator (
				item, 
				TomboyGConfXKeybinder.RECENT_BINDING);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Search Notes..."));
			item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			item.Activated += SearchNotes;
			menu.Append (item);

			GConfKeybindingToAccel.AddAccelerator (
				item, 
				TomboyGConfXKeybinder.SEARCH_BINDING);

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Quit"));
			item.Image = new Gtk.Image (Gtk.Stock.Quit, Gtk.IconSize.Menu);
			item.Activated += Quit;
			menu.Append (item);

			menu.ShowAll ();
			return menu;
		}

		Gtk.ImageMenuItem MakeNoteMenuItem (Note note)
		{
			string display_name = note.Title;
			if (note.IsNew)
				display_name += Catalog.GetString (" (new)");

			Gtk.ImageMenuItem item = new Gtk.ImageMenuItem (display_name);
			item.Image = new Gtk.Image (stock_notes);
			item.Data ["Note"] = note;
			item.Activated += ShowNote;

			return item;
		}

		void ShowNote (object sender, EventArgs args) 
		{
			Note note = (Note) ((Gtk.Widget) sender).Data ["Note"];
			if (note != null)
				note.Window.Present ();
		}

		void CreateNewNote (object sender, EventArgs args) 
		{
			Note new_note = manager.Create ();
			new_note.Window.Show ();
		}

		void SearchNotes (object sender, EventArgs args) 
		{
			NoteFindDialog find_dialog = NoteFindDialog.GetInstance (manager);
			find_dialog.Present ();
		}

		void ViewRecentChanges (object sender, EventArgs args)
		{
			Gtk.Window recent = new NoteRecentChanges (manager);
			recent.Show ();
		}

		void Quit (object sender, EventArgs args)
		{
			Console.WriteLine ("Quitting Tomboy.  Ciao!");
			Environment.Exit (0);
		}

		// FIXME: If receiving a drag, pop up last window used, or a new
		//        window, or the recent list?  I think recent list
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
		static GConf.Client client;
		static Gtk.AccelGroup accel_group;

		static GConfKeybindingToAccel ()
		{
			client = new GConf.Client ();
			accel_group = new Gtk.AccelGroup ();
		}

		public static string GetShortcut (string gconf_path)
		{
			try {
				string binding = (string) client.Get (gconf_path);
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

		[DllImport("libtrayicon")]
		static extern bool egg_accelerator_parse_virtual (string keystring,
								  out uint keysym,
								  out uint virtual_mods);

		[DllImport("libtrayicon")]
		static extern void egg_keymap_resolve_virtual_modifiers (IntPtr keymap,
									 uint virtual_mods,
									 out Gdk.ModifierType real_mods);

		public static bool GetAccelKeys (string               gconf_path, 
						 out uint             keyval, 
						 out Gdk.ModifierType mods)
		{
			keyval = 0;
			mods = 0;

			try {
				string binding = (string) client.Get (gconf_path);
				if (binding == null || 
				    binding == String.Empty || 
				    binding == "disabled")
					return false;

				uint virtual_mods = 0;
				if (!egg_accelerator_parse_virtual (binding,
								    out keyval,
								    out virtual_mods))
					return false;

				Gdk.Keymap keymap = Gdk.Keymap.Default;
				egg_keymap_resolve_virtual_modifiers (keymap.Handle,
								      virtual_mods,
								      out mods);

				return true;
			} catch {
				return false;
			}
		}

		public static void AddAccelerator (Gtk.MenuItem item, string gconf_path)
		{
			uint keyval;
			Gdk.ModifierType mods;

			if (GetAccelKeys (gconf_path, out keyval, out mods))
				item.AddAccelerator ("activate",
						     accel_group,
						     keyval,
						     mods,
						     Gtk.AccelFlags.Visible);
		}
	}
}
