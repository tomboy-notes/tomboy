
using System;
using System.Collections;
using System.Text;
using Mono.Posix;
using System.Runtime.InteropServices;

namespace Tomboy
{
	public class TomboyTray : Gtk.EventBox
	{
		NoteManager manager;
		Gtk.Tooltips tips;
		Gtk.Image image;

		static Gdk.Pixbuf tintin;
		static Gdk.Pixbuf tintin_large;
		static Gdk.Pixbuf stock_notes;

		static TomboyTray ()
		{
			tintin = GuiUtils.GetMiniIcon ("tintin.png");
			tintin_large = GuiUtils.GetIcon ("tintin.png");
			stock_notes = GuiUtils.GetMiniIcon ("stock_notes.png");
		}

		public TomboyTray (NoteManager manager) 
			: base ()
		{
			this.manager = manager;
			this.image = new Gtk.Image (tintin);

			this.CanFocus = true;
			this.ButtonPressEvent += ButtonPress;
			this.Add (image);
			this.ShowAll ();

			string tip_text = Catalog.GetString ("Tomboy Notes");

			if ((bool) Preferences.Get (Preferences.ENABLE_KEYBINDINGS)) {
				string shortcut = 
					GConfKeybindingToAccel.GetShortcut (
						Preferences.KEYBINDING_SHOW_NOTE_MENU);
				if (shortcut != null)
					tip_text += String.Format (" ({0})", shortcut);
			}

			tips = new Gtk.Tooltips ();
			tips.SetTip (this, tip_text, null);
			tips.Enable ();
			tips.Sink ();

			SetupDragAndDrop ();
		}

		void ButtonPress (object sender, Gtk.ButtonPressEventArgs args) 
		{
			Gtk.Widget parent = (Gtk.Widget) sender;

			if (args.Event.Button == 1) {
				Gtk.Menu recent_menu = MakeRecentNotesMenu (parent);
				GuiUtils.PopupMenu (recent_menu, args.Event);
				args.RetVal = true;
			}
		}

		Gtk.Menu MakeRecentNotesMenu (Gtk.Widget parent) 
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AttachToWidget (parent, null);

			bool enable_keybindings = (bool) 
				Preferences.Get (Preferences.ENABLE_KEYBINDINGS);

			Gtk.ImageMenuItem item;

			item = new Gtk.ImageMenuItem (Catalog.GetString ("Create _New Note"));
			item.Image = new Gtk.Image (Gtk.Stock.New, Gtk.IconSize.Menu);
			item.Activated += CreateNewNote;
			menu.Append (item);

			if (enable_keybindings)
				GConfKeybindingToAccel.AddAccelerator (
					item, 
					Preferences.KEYBINDING_CREATE_NEW_NOTE);

			// FIXME: Pull this from GConf
			int min_size = 5;
			int max_size = 18;
			int list_size = 0;

			DateTime two_days_ago = DateTime.Now.AddDays (-2);

			// List the i most recently changed notes, and any
			// currently opened notes...
			foreach (Note note in manager.Notes) {
				if (note.IsSpecial)
					continue;

				if (note.IsOpened || 
				    note.ChangeDate > two_days_ago ||
				    list_size < min_size) {
					item = MakeNoteMenuItem (note);
					menu.Append (item);
				}

				list_size++;
				if (list_size == max_size)
					break;
			}

			Note start = manager.Find (Catalog.GetString ("Start Here"));
			if (start != null) {
				item = MakeNoteMenuItem (start);
				menu.Append (item);

				if (enable_keybindings)
					GConfKeybindingToAccel.AddAccelerator (
						item, 
						Preferences.KEYBINDING_OPEN_START_HERE);
			}

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Recent Changes"));
			item.Image = new Gtk.Image (Gtk.Stock.SortAscending, Gtk.IconSize.Menu);
			item.Activated += ViewRecentChanges;
			menu.Append (item);

			if (enable_keybindings)
				GConfKeybindingToAccel.AddAccelerator (
					item, 
					Preferences.KEYBINDING_OPEN_RECENT_CHANGES);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Search Notes..."));
			item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			item.Activated += SearchNotes;
			menu.Append (item);

			if (enable_keybindings)
				GConfKeybindingToAccel.AddAccelerator (
					item, 
					Preferences.KEYBINDING_OPEN_SEARCH);

			menu.ShowAll ();
			return menu;
		}

		Gtk.ImageMenuItem MakeNoteMenuItem (Note note)
		{
			string display_name = note.Title;
			if (note.IsNew)
				display_name += Catalog.GetString (" (new)");

			display_name = FormatForLabel (display_name);

			Gtk.ImageMenuItem item = new Gtk.ImageMenuItem (display_name);
			item.Image = new Gtk.Image (stock_notes);
			item.Data ["Note"] = note;
			item.Activated += ShowNote;

			return item;
		}

		string FormatForLabel (string name)
		{
			// Replace underscores ("_") with double-underscores ("__")
			// so Note menuitems are not created with mnemonics.
			return name.Replace ("_", "__");
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

		// Used by TomboyApplet to modify the icon background.
		public Gtk.Image Image
		{
			get { return image; }
		}

		public void ShowMenu ()
		{
			Gtk.Menu recent_menu = MakeRecentNotesMenu (this);
			GuiUtils.PopupMenu (recent_menu, null);
		}

		public void ShowPreferences ()
		{
			PreferencesDialog prefs = new PreferencesDialog ();
			prefs.Run ();
			prefs.Destroy ();
		}

		public void ShowAbout ()
		{
			string [] authors = new string [] {
				"Alex Graveley <alex@beatniksoftware.com>"
			};

			string [] documenters = new string [] {
				"Alex Graveley <alex@beatniksoftware.com>"
			};

			string translators = 
				"Albanian (sq) - Laurent Dhima  <laurenti@alblinux.net>\n" +
				"British English (en_GB) - David Lodge <dave@cirt.net>\n" +
			        "Bulgarian (bg) - Rostislav Raykov <zbrox@i-space.org>\n" +
				"Canadian English (en_CA) - Adam Weinberger <adamw@gnome.org>\n" +
				"Catalan (ca) - Jordi Mas <jmas@softcatala.org>\n" +
			        "Chinese Simplified (zh_CN) - Funda Wang <fundawang@linux.net.cn>\n" +
			        "Czech (cs) - Miloslav Trmac <mitr@volny.cz>\n" +
				"Dutch; Flemish (nl) - Paul Bolle <pebolle@tiscali.nl>\n" +
			        "Danish (da) - Martin Willemoes Hansen <mwh@sysrq.dk>\n" +
				"French (fr) - Beno\xeet Dejean <tazforever@dlfp.org>\n" +
				"German (de) - Philipp Meier <meier@fnogol.de>\n" +
				"Hungarian (hu) - T\xf6r\xf6k G\u00e1bor <gabo@linuxportal.hu>\n" +
			        "Italian (it) - Amos Brocco <amos.brocco@mriyasoftware.com>\n" +
				"Icelandic (is) - Gunnar Steinn Magn\xfasson <gsm@gunnarsteinn.com>\n" +
				"Japanese (ja) - Takeshi AIHANA <aihana@gnome.gr.jp>\n" +
			        "Korean (ko) - Young-Ho Cha\n" +
			        "Norwegian (nb, no) - Sigurd Gartmann <sigurd-translate@brogar.org>\n" +
				"Polish (pl) - Marek Habersack <grendel@caudium.net>\n" +
				"Portugese, Brazilian (pt_BR) - F\u00e1bio Mendes <niels_bohr@uol.com.br>\n" +
			        "Punjabi (pa) - Amanpreet Singh Alam <amanpreetalam@yahoo.com>\n" +
				"Spanish (es) - Antonio Ognio <gnrfan@gnrfan.org>\n" +
				"Swedish (sv) - Christian Rose <menthos@menthos.com>\n";

			Gnome.About about = 
				new Gnome.About (
					"Tomboy", 
					Defines.VERSION,
					Catalog.GetString ("Copyright \xa9 2004, 2005 Alex Graveley"),
					Catalog.GetString ("A simple and easy to use desktop " +
							   "note-taking application."),
					authors, 
					documenters, 
					translators,
					tintin_large);
			about.Icon = tintin_large;
			about.Show ();
		}

		// Support dropping text/uri-lists and _NETSCAPE_URLs currently.
		void SetupDragAndDrop ()
		{
			Gtk.TargetEntry [] targets = 
				new Gtk.TargetEntry [] {
					new Gtk.TargetEntry ("text/uri-list", 0, 0),
					new Gtk.TargetEntry ("_NETSCAPE_URL", 0, 0)
				};

			Gtk.Drag.DestSet (this, 
					  Gtk.DestDefaults.All, 
					  targets,
					  Gdk.DragAction.Copy);

			DragDataReceived += OnDragDataReceived;
		}

		// Pop up Start Here and insert dropped links, in the form:
		//	Wednesday, December 8, 6:45 AM
		//	http://luna/kwiki/index.cgi?AdelaideUniThoughts
		//	http://www.beatniksoftware.com/blog/
		// And select the inserted text.
		//
		// FIXME: Make undoable, make sure our date-sizing tag never "bleeds".
		//
		void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
		{
			UriList uri_list = new UriList (args.SelectionData);
			if (uri_list.Count == 0)
				return;

			Note link_note = manager.Find (Catalog.GetString ("Start Here"));
			link_note.Window.Present ();

			NoteBuffer buffer = link_note.Buffer;
			StringBuilder insert_text = new StringBuilder ();

			insert_text.Append ("\n"); // initial newline

			string date_format = Catalog.GetString ("dddd, MMMM d, h:mm tt");
			insert_text.Append (DateTime.Now.ToString (date_format));

			foreach (Uri uri in uri_list) {
				insert_text.Append ("\n");

				if (uri.IsFile) 
					insert_text.Append (uri.LocalPath);
				else
					insert_text.Append (uri.ToString ());
			}

			insert_text.Append ("\n"); // trailing newline

			buffer.Undoer.FreezeUndo ();

			// Insert the date and list of links...
			Gtk.TextIter cursor = buffer.StartIter;
			cursor.ForwardLines (1); // skip title

			buffer.Insert (cursor, insert_text.ToString ());

			// Make the date string a small font...
			cursor = buffer.StartIter;
			cursor.ForwardLines (2); // skip title & leading newline

			Gtk.TextIter end = cursor;
			end.ForwardToLineEnd (); // end of date

			buffer.ApplyTag ("size:small", cursor, end);

			// Select the text we've inserted (avoid trailing newline)...
			end = cursor;
			end.ForwardChars (insert_text.Length - 1);

			buffer.MoveMark (buffer.SelectionBound, cursor);
			buffer.MoveMark (buffer.InsertMark, end);

			buffer.Undoer.ThawUndo ();
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

		[DllImport("libtomboy")]
		static extern bool egg_accelerator_parse_virtual (string keystring,
								  out uint keysym,
								  out uint virtual_mods);

		[DllImport("libtomboy")]
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
				string binding = (string) Preferences.Get (gconf_path);
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
