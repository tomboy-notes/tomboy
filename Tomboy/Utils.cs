
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Posix;

namespace Tomboy
{
	public class GuiUtils 
	{
		public static void GetMenuPosition (Gtk.Menu menu, 
						    out int  x, 
						    out int  y, 
						    out bool push_in)
		{
			Gtk.Requisition menu_req = menu.SizeRequest ();

			menu.AttachWidget.GdkWindow.GetOrigin (out x, out y);

			if (y + menu_req.Height >= menu.AttachWidget.Screen.Height)
				y -= menu_req.Height;
			else
				y += menu.AttachWidget.Allocation.Height;

			push_in = true;
		}

		static void DeactivateMenu (object sender, EventArgs args) 
		{
			Gtk.Menu menu = (Gtk.Menu) sender;
			menu.Popdown ();
		}

		// Place the menu underneath an arbitrary parent widget.  The
		// parent widget must be set using menu.AttachToWidget before
		// calling this
		public static void PopupMenu (Gtk.Menu menu, Gdk.EventButton ev)
		{
			menu.Deactivated += DeactivateMenu;
			menu.Popup (null, 
				    null, 
				    new Gtk.MenuPositionFunc (GetMenuPosition), 
				    IntPtr.Zero, 
				    (ev == null) ? 0 : ev.Button, 
				    (ev == null) ? Gtk.Global.CurrentEventTime : ev.Time);
		}

		public static Gdk.Pixbuf GetMiniIcon (string resource_name) 
		{
			Gdk.Pixbuf noicon = new Gdk.Pixbuf (null, resource_name);
			return noicon.ScaleSimple (24, 24, Gdk.InterpType.Nearest);
		}

		public static Gtk.Button MakeImageButton (Gtk.Image image, string label)
		{
			Gtk.HBox box = new Gtk.HBox (false, 2);
			box.PackStart (image, false, false, 0);
			box.PackEnd (new Gtk.Label (label), false, false, 0);
			box.ShowAll ();

			Gtk.Button button = new Gtk.Button ();

			Gtk.Alignment align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 0.0f);
			align.Add (box);
			align.Show ();

			button.Add (align);
			return button;
		}			

		public static Gtk.Button MakeImageButton (string stock_id, string label)
		{
			Gtk.Image image = new Gtk.Image (stock_id, Gtk.IconSize.Button);
			return MakeImageButton (image, label);
		}
	}

	public class GlobalKeybinder 
	{
		Gtk.AccelGroup accel_group;
		Gtk.Menu fake_menu;

		public GlobalKeybinder (Gtk.AccelGroup accel_group) 
		{
			this.accel_group = accel_group;

			fake_menu = new Gtk.Menu ();
			fake_menu.AccelGroup = accel_group;
		}

		public void AddAccelerator (EventHandler handler,
					    uint key,
					    Gdk.ModifierType modifiers,
					    Gtk.AccelFlags flags)
		{
			Gtk.MenuItem foo = new Gtk.MenuItem ();
			foo.Activated += handler;
			foo.AddAccelerator ("activate",
					    accel_group,
					    key, 
					    modifiers,
					    flags);
			foo.Show ();

			fake_menu.Append (foo);
		}
	}

	public class XKeybinder 
	{
		[DllImport("libtrayicon")]
		static extern void tomboy_keybinder_init ();

		[DllImport("libtrayicon")]
		static extern void tomboy_keybinder_bind (string keystring,
							  BindkeyHandler handler);

		[DllImport("libtrayicon")]
		static extern void tomboy_keybinder_unbind (string keystring,
							    BindkeyHandler handler);

		public delegate void BindkeyHandler (string key, IntPtr user_data);

		ArrayList      bindings;
		BindkeyHandler key_handler;

		struct Binding {
			internal string       keystring;
			internal EventHandler handler;
		}

		public XKeybinder ()
			: base ()
		{
			bindings = new ArrayList ();
			key_handler = new BindkeyHandler (KeybindingPressed);
			
			tomboy_keybinder_init ();
		}

		void KeybindingPressed (string keystring, IntPtr user_data)
		{
			foreach (Binding bind in bindings) {
				if (bind.keystring == keystring) {
					bind.handler (this, new EventArgs ());
				}
			}
		}

		public void Bind (string       keystring, 
				  EventHandler handler)
		{
			Binding bind = new Binding ();
			bind.keystring = keystring;
			bind.handler = handler;
			bindings.Add (bind);
			
			tomboy_keybinder_bind (bind.keystring, key_handler);
		}

		public void Unbind (string keystring)
		{
			foreach (Binding bind in bindings) {
				if (bind.keystring == keystring) {
					tomboy_keybinder_unbind (bind.keystring,
								 key_handler);

					bindings.Remove (bind);
					break;
				}
			}
		}

		public virtual void UnbindAll ()
		{
			foreach (Binding bind in bindings) {
				tomboy_keybinder_unbind (bind.keystring, key_handler);
			}

			bindings.Clear ();
		}
	}

	public class GConfXKeybinder : XKeybinder
	{
		GConf.Client client;
		ArrayList bindings;
		
		public GConfXKeybinder ()
		{
			client = new GConf.Client ();
			bindings = new ArrayList ();
		}

		public void Bind (string       gconf_path, 
				  string       default_binding, 
				  EventHandler handler)
		{
			try {
				Binding binding = new Binding (gconf_path, 
							       default_binding,
							       handler,
							       this);
				bindings.Add (binding);
			} catch (Exception e) {
				Console.WriteLine ("Error Adding global keybinding:");
				Console.WriteLine (e);
			}
		}

		public override void UnbindAll ()
		{
			try {
				bindings.Clear ();
				base.UnbindAll ();
			} catch (Exception e) {
				Console.WriteLine ("Error Removing global keybinding:");
				Console.WriteLine (e);
			}
		}

		class Binding 
		{
			public string   gconf_path;
			public string   key_sequence;
			EventHandler    handler;
			GConfXKeybinder parent;

			public Binding (string          gconf_path, 
					string          default_binding,
					EventHandler    handler,
					GConfXKeybinder parent)
			{
				this.gconf_path = gconf_path;
				this.key_sequence = default_binding;
				this.handler = handler;
				this.parent = parent;

				try {
					key_sequence = (string) parent.client.Get (gconf_path);
				} catch (Exception e) {
					Console.WriteLine ("GConf key '{0}' does not exist, using default.", 
							   gconf_path);
				}

				SetBinding ();

				parent.client.AddNotify (
					gconf_path, 
					new GConf.NotifyEventHandler (BindingChanged));
			}

			void BindingChanged (object sender, GConf.NotifyEventArgs args)
			{
				if (args.Key == gconf_path) {
					Console.WriteLine ("Binding for '{0}' changed to '{1}'!",
							   gconf_path,
							   args.Value);

					UnsetBinding ();

					key_sequence = (string) args.Value;
					SetBinding ();
				}
			}

			public void SetBinding ()
			{
				if (key_sequence == null || 
				    key_sequence == String.Empty || 
				    key_sequence == "disabled")
					return;

				Console.WriteLine ("Binding key '{0}' for '{1}'",
						   key_sequence,
						   gconf_path);

				parent.Bind (key_sequence, handler);
			}

			public void UnsetBinding ()
			{
				if (key_sequence == null)
					return;

				Console.WriteLine ("Unbinding key '{0}' for '{1}'",
						   key_sequence,
						   gconf_path);

				parent.Unbind (key_sequence);
			}
		}
	}

	public class TomboyGConfXKeybinder : GConfXKeybinder
	{
		const string TOMBOY = "/apps/tomboy/";
		const string BINDINGS = TOMBOY + "global_keybindings/";

		public const string MENU_BINDING = BINDINGS + "show_note_menu";
		public const string START_BINDING = BINDINGS + "open_start_here";
		public const string NEW_NOTE_BINDING = BINDINGS + "create_new_note";
		public const string SEARCH_BINDING = BINDINGS + "open_search";
		public const string RECENT_BINDING = BINDINGS + "open_recent_changes";

		NoteManager manager;
		TomboyTray  tray;

		public TomboyGConfXKeybinder (NoteManager manager, TomboyTray tray)
			: base ()
		{
			this.manager = manager;
			this.tray = tray;

			Bind (MENU_BINDING, 
			      "<Alt>F12", 
			      new EventHandler (KeyShowMenu));
			Bind (START_BINDING, 
			      "<Alt>F11", 
			      new EventHandler (KeyOpenStartHere));
			Bind (NEW_NOTE_BINDING, 
			      "disabled", 
			      new EventHandler (KeyCreateNewNote));
			Bind (SEARCH_BINDING, 
			      "disabled", 
			      new EventHandler (KeyOpenSearch));
			Bind (RECENT_BINDING, 
			      "disabled", 
			      new EventHandler (KeyOpenRecentChanges));
		}

		void KeyShowMenu (object sender, EventArgs args)
		{
			tray.ShowMenu ();
		}

		void KeyOpenStartHere (object sender, EventArgs args)
		{
			Note note = manager.Find (Catalog.GetString ("Start Here"));
			if (note != null)
				note.Window.Present ();
		}

		void KeyCreateNewNote (object sender, EventArgs args)
		{
			Note new_note = manager.Create ();
			new_note.Window.Show ();
		}

		void KeyOpenSearch (object sender, EventArgs args)
		{
			NoteFindDialog find_dialog = NoteFindDialog.GetInstance (manager);
			find_dialog.Present ();
		}

		void KeyOpenRecentChanges (object sender, EventArgs args)
		{
			Gtk.Window recent = new NoteRecentChanges (manager);
			recent.Show ();
		}
	}

	public class HIGMessageDialog : Gtk.Dialog
	{
		Gtk.AccelGroup accel_group;

		public HIGMessageDialog (Gtk.Window parent,
					 Gtk.DialogFlags flags,
					 Gtk.MessageType type,
					 Gtk.ButtonsType buttons,
					 string          header,
					 string          msg)
			: base ()
		{
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = "";

			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;

			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			hbox.BorderWidth = 5;
			hbox.Show ();
			VBox.PackStart (hbox, false, false, 0);

			Gtk.Image image = null;

			switch (type) {
			case Gtk.MessageType.Error:
				image = new Gtk.Image ("gtk-dialog-error", Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Question:
				image = new Gtk.Image ("gtk-dialog-question", Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Info:
				image = new Gtk.Image ("gtk-dialog-info", Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Warning:
				image = new Gtk.Image ("gtk-dialog-warning", Gtk.IconSize.Dialog);
				break;
			}

			image.Show ();
			hbox.PackStart (image, false, false, 0);
			
			Gtk.VBox label_vbox = new Gtk.VBox (false, 0);
			label_vbox.Show ();
			hbox.PackStart (label_vbox, true, true, 0);

			string title = String.Format ("<span weight='bold' size='larger'>{0}" +
						      "</span>\n",
						      header);

			Gtk.Label label;

			label = new Gtk.Label (title);
			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.LineWrap = true;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			label_vbox.PackStart (label, false, false, 0);

			label = new Gtk.Label (msg);
			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.LineWrap = true;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			label_vbox.PackStart (label, false, false, 0);
			
			switch (buttons) {
			case Gtk.ButtonsType.None:
				break;
			case Gtk.ButtonsType.Ok:
				AddButton (Gtk.Stock.Ok, Gtk.ResponseType.Ok, true);
				break;
			case Gtk.ButtonsType.Close:
				AddButton (Gtk.Stock.Close, Gtk.ResponseType.Close, true);
				break;
			case Gtk.ButtonsType.Cancel:
				AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, true);
				break;
			case Gtk.ButtonsType.YesNo:
				AddButton (Gtk.Stock.No, Gtk.ResponseType.No, false);
				AddButton (Gtk.Stock.Yes, Gtk.ResponseType.Yes, true);
				break;
			case Gtk.ButtonsType.OkCancel:
				AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, false);
				AddButton (Gtk.Stock.Ok, Gtk.ResponseType.Ok, true);
				break;
			}

			if (parent != null)
				TransientFor = parent;

			if ((int) (flags & Gtk.DialogFlags.Modal) != 0)
				Modal = true;

			if ((int) (flags & Gtk.DialogFlags.DestroyWithParent) != 0)
				DestroyWithParent = true;
		}

		void AddButton (string stock_id, Gtk.ResponseType response, bool is_default)
		{
			Gtk.Button button = new Gtk.Button (stock_id);
			button.CanDefault = true;
			button.Show ();

			AddActionWidget (button, response);

			if (is_default) {
				DefaultResponse = response;
				button.AddAccelerator ("activate",
						       accel_group,
						       (uint) Gdk.Key.Escape, 
						       0,
						       Gtk.AccelFlags.Visible);
			}
		}
	}
}
