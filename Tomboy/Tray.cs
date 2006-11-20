
using System;
using System.Collections;
using System.Text;
using Mono.Unix;
using System.Runtime.InteropServices;

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
			note_icon = GuiUtils.GetIcon ("tomboy-note", 16);
			pinup = GuiUtils.GetIcon ("pinup", 16);
			pinup_active = GuiUtils.GetIcon ("pinup-active", 16);
			pindown = GuiUtils.GetIcon ("pindown", 16);
		}

		public NoteMenuItem (Note note, bool show_pin)
			: base (GetDisplayName(note))
		{
			this.note = note;
			Image = new Gtk.Image (note_icon);

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
			if (note.IsNew)
				display_name += Catalog.GetString (" (new)");
			return FormatForLabel (display_name);
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

	public class TomboyTray : Gtk.EventBox
	{
		NoteManager manager;
		Gtk.Tooltips tips;
		Gtk.Image image;
		PreferencesDialog prefs_dlg;
		int icon_size_last = -1;

		public TomboyTray (NoteManager manager) 
			: base ()
		{
			this.manager = manager;
			this.image = new Gtk.Image ("tomboy", Gtk.IconSize.Menu);

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

			InitSomeStuff ();
		}

		void ButtonPress (object sender, Gtk.ButtonPressEventArgs args) 
		{
			Gtk.Widget parent = (Gtk.Widget) sender;

			switch (args.Event.Button) {
			case 1:
				Gtk.Menu recent_menu = MakeRecentNotesMenu (parent);
				GuiUtils.PopupMenu (recent_menu, args.Event);
				args.RetVal = true;
				break;
			case 2:
				// Give some visual feedback
				Gtk.Drag.Highlight (this);
				args.RetVal = PastePrimaryClipboard ();
				Gtk.Drag.Unhighlight (this);
				break;
			}
		}

		void PrependTimestampedText (Note note, DateTime timestamp, string text)
		{
			NoteBuffer buffer = note.Buffer;
			StringBuilder insert_text = new StringBuilder ();

			insert_text.Append ("\n"); // initial newline
			string date_format = Catalog.GetString ("dddd, MMMM d, h:mm tt");
			insert_text.Append (timestamp.ToString (date_format));
			insert_text.Append ("\n"); // begin content
			insert_text.Append (text);
			insert_text.Append ("\n"); // trailing newline

			buffer.Undoer.FreezeUndo ();

			// Insert the date and list of links...
			Gtk.TextIter cursor = buffer.StartIter;
			cursor.ForwardLines (1); // skip title

			buffer.Insert (ref cursor, insert_text.ToString ());

			// Make the date string a small font...
			cursor = buffer.StartIter;
			cursor.ForwardLines (2); // skip title & leading newline

			Gtk.TextIter end = cursor;
			end.ForwardToLineEnd (); // end of date

			buffer.ApplyTag ("datetime", cursor, end);

			// Select the text we've inserted (avoid trailing newline)...
			end = cursor;
			end.ForwardChars (insert_text.Length - 1);

			buffer.MoveMark (buffer.SelectionBound, cursor);
			buffer.MoveMark (buffer.InsertMark, end);

			buffer.Undoer.ThawUndo ();
		}

		bool PastePrimaryClipboard ()
		{
			Gtk.Clipboard clip = GetClipboard (Gdk.Selection.Primary);
			string text = clip.WaitForText ();

			if (text == null || text.Trim() == string.Empty)
				return false;

			Note link_note = manager.Find (Catalog.GetString ("Start Here"));
			if (link_note == null)
				return false;

			link_note.Window.Present ();
			PrependTimestampedText (link_note, 
						DateTime.Now, 
						text);

			return true;
		}

		Gtk.Menu MakeRecentNotesMenu (Gtk.Widget parent) 
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AttachToWidget (parent, GuiUtils.DetachMenu);

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

			int min_size = (int) Preferences.Get (Preferences.MENU_NOTE_COUNT);
			int max_size = 18;
			int list_size = 0;

			DateTime days_ago = DateTime.Today.AddDays (-3);

			// List the i most recently changed notes, any currently
			// opened notes, and any pinned notes...
			foreach (Note note in manager.Notes) {
				if (note.IsSpecial)
					continue;

				bool show = false;
				
				if ((note.IsOpened && note.Window.IsMapped) ||
				    note.ChangeDate > days_ago ||
				    list_size < min_size) {
					if (list_size <= max_size)
						show = true;
				} else if (note.IsPinned) {
					show = true;
				}

				if (show) {
					item = new NoteMenuItem (note, true);
					menu.Append (item);
					list_size++;
				}
			}

			Note start = manager.Find (Catalog.GetString ("Start Here"));
			if (start != null) {
				item = new NoteMenuItem (start, false);
				menu.Append (item);

				if (enable_keybindings)
					GConfKeybindingToAccel.AddAccelerator (
						item, 
						Preferences.KEYBINDING_OPEN_START_HERE);
			}

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Table of Contents"));
			item.Image = new Gtk.Image (Gtk.Stock.SortAscending, Gtk.IconSize.Menu);
			item.Activated += ViewRecentChanges;
			menu.Append (item);

			if (enable_keybindings)
				GConfKeybindingToAccel.AddAccelerator (
					item, 
					Preferences.KEYBINDING_OPEN_RECENT_CHANGES);

			menu.ShowAll ();
			return menu;
		}

		void CreateNewNote (object sender, EventArgs args) 
		{
			try {
				Note new_note = manager.Create ();
				new_note.Window.Show ();
			} catch (Exception e) {
				HIGMessageDialog dialog = 
					new HIGMessageDialog (
						null,
						0,
						Gtk.MessageType.Error,
						Gtk.ButtonsType.Ok,
						Catalog.GetString ("Cannot create new note"),
						e.Message);
				dialog.Run ();
				dialog.Destroy ();
			}
		}

		void ViewRecentChanges (object sender, EventArgs args)
		{
			NoteRecentChanges.GetInstance (manager).Present ();
		}

		// Used by TomboyApplet to modify the icon background.
		public Gtk.Image Image
		{
			get { return image; }
		}

		public void ShowMenu (bool select_first_item)
		{
			Gtk.Menu recent_menu = MakeRecentNotesMenu (this);
			if (select_first_item)
				recent_menu.SelectFirst (false);

			GuiUtils.PopupMenu (recent_menu, null);
		}

		void OnPreferencesResponse (object sender, Gtk.ResponseArgs args)
		{
			((Gtk.Widget) sender).Destroy ();
			prefs_dlg = null;
		}

		public void ShowPreferences ()
		{
			if (prefs_dlg == null) {
				prefs_dlg = new PreferencesDialog ();
				prefs_dlg.Response += OnPreferencesResponse;
			}
			prefs_dlg.Present ();
		}

		public void ShowAbout ()
		{
			string [] authors = new string [] {
				"Alex Graveley <alex@beatniksoftware.com>",
				"Boyd Timothy <btimothy@gmail.com>",
				"David Trowbridge <trowbrds@gmail.com>",
				"Ryan Lortie <desrt@desrt.ca>",
				"Sandy Armstrong <sanfordarmstrong@gmail.com>",
				"Sebastian Rittau <srittau@jroger.in-berlin.de>"
			};

			string [] documenters = new string [] {
				"Alex Graveley <alex@beatniksoftware.com>"
			};

			string translators = Catalog.GetString ("translator-credits");
			if (translators == "translator-credits")
				translators = null;

			Gtk.AboutDialog about = new Gtk.AboutDialog ();
			about.Name = "Tomboy";
			about.Version = Defines.VERSION;
			about.Logo = GuiUtils.GetIcon ("tomboy", 48);
			about.Copyright = 
				Catalog.GetString ("Copyright \xa9 2004-2006 Alex Graveley");
			about.Comments = Catalog.GetString ("A simple and easy to use desktop " +
							    "note-taking application.");
			about.Website = "http://www.beatniksoftware.com/tomboy";
			about.WebsiteLabel = Catalog.GetString("Homepage & Donations");
			about.Authors = authors;
			about.Documenters = documenters;
			about.TranslatorCredits = translators;
			about.IconName = "tomboy";
			about.Run ();
			about.Destroy ();
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

			StringBuilder insert_text = new StringBuilder ();
			bool more_than_one = false;

			foreach (Uri uri in uri_list) {
				if (more_than_one)
					insert_text.Append ("\n");

				if (uri.IsFile) 
					insert_text.Append (uri.LocalPath);
				else
					insert_text.Append (uri.ToString ());

				more_than_one = true;
			}

			Note link_note = manager.Find (Catalog.GetString ("Start Here"));
			if (link_note != null) {
				link_note.Window.Present ();
				PrependTimestampedText (link_note, 
							DateTime.Now, 
							insert_text.ToString ());
			}
		}

		protected override void OnSizeAllocated (Gdk.Rectangle rect)
		{
			base.OnSizeAllocated (rect);

			int icon_size = Math.Min (rect.Height, rect.Width);
			if (icon_size_last != icon_size) {
				icon_size_last = icon_size;
				image.Pixbuf = GuiUtils.GetIcon (GetIconName (), icon_size);
			}
		}

		void ReloadIcon ()
		{
			icon_size_last = -1;
			QueueResize ();
		}

		// NOTHING TO SEE HERE
		void InitSomeStuff ()
		{
			manager.NoteDeleted += OnNoteDeletedUpdateIcon;
			manager.NoteAdded += OnNoteAddedUpdateIcon;
			manager.NoteRenamed += OnNoteRenamedUpdateIcon;
		}

		void OnNoteDeletedUpdateIcon (object sender, Note changed)
		{
			if (changed.Title == "Tintin")
				ReloadIcon ();
		}

		void OnNoteAddedUpdateIcon (object sender, Note changed)
		{
			if (changed.Title == "Tintin")
				ReloadIcon ();
		}

		void OnNoteRenamedUpdateIcon (Note note, string old_title)
		{
			if (note.Title == "Tintin" || old_title == "Tintin")
				ReloadIcon ();
		}

		string GetIconName ()
		{
			if (manager.Find ("Tintin") != null)
				return "tintin";
			return "tomboy";
		}
		// GO ABOUT YOUR BUSINESS
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
