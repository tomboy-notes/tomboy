
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Mono.Unix;

// Work around bug in Gtk# panel applet bindings by using a local copy with
// fixed OnBackgroundChanged marshalling.
using _Gnome;

namespace Tomboy
{
	public class TomboyApplet : PanelApplet
	{
		NoteManager manager;
		TomboyTray tray;
		TomboyGConfXKeybinder keybinder;

		BonoboUIVerb [] menu_verbs;

		public TomboyApplet (IntPtr raw)
			: base (raw)
		{
		}

		public override string IID 
		{
			get { return "OAFIID:TomboyApplet"; }
		}

		public override string FactoryIID 
		{
			get { return "OAFIID:TomboyApplet_Factory"; }
		}

		public override void Creation ()
		{
			Logger.Log ("Applet Created...");

			manager = Tomboy.DefaultNoteManager;
			tray = new TomboyTray (manager);
			keybinder = new TomboyGConfXKeybinder (manager, tray);

			Flags |= PanelAppletFlags.ExpandMinor;

			Add (tray);
			OnChangeSize (Size);
			ShowAll ();

			// Keep referenced so our callbacks don't get reaped.
			menu_verbs = new BonoboUIVerb [] {
				new BonoboUIVerb (
					"Props", new ContextMenuItemCallback (ShowPreferencesVerb)),
				new BonoboUIVerb (
					"Plugins", new ContextMenuItemCallback (ShowPluginsVerb)),
				new BonoboUIVerb (
					"About", new ContextMenuItemCallback (ShowAboutVerb))
			};

			SetupMenuFromResource (null, "GNOME_TomboyApplet.xml", menu_verbs);
		}

		new void SetupMenuFromResource (Assembly asm, 
						string resource, 
						BonoboUIVerb [] verbs)
		{
			if (asm == null)
				asm = GetType ().Assembly;

			Stream stream = asm.GetManifestResourceStream (resource);
			if (stream != null) {
				StreamReader reader = new StreamReader (stream);
				String xml = reader.ReadToEnd ();
				reader.Close ();
				stream.Close ();

				SetupMenu (xml, verbs);
			}
		}

		void ShowPreferencesVerb ()
		{
			tray.ShowPreferences ();
		}

		void ShowPluginsVerb ()
		{
			manager.PluginManager.ShowPluginsDirectory ();
		}

		void ShowAboutVerb ()
		{
			tray.ShowAbout ();
		}

		protected override void OnChangeBackground (PanelAppletBackgroundType type, 
							    Gdk.Color                 color, 
							    Gdk.Pixmap                pixmap)
		{
			if (tray == null)
				return;

			Gtk.RcStyle rc_style = new Gtk.RcStyle ();
			tray.Style = null;
			tray.ModifyStyle (rc_style);

			switch (type) {
			case PanelAppletBackgroundType.ColorBackground:
				tray.ModifyBg (Gtk.StateType.Normal, color);
				break;
			case PanelAppletBackgroundType.NoBackground:
				break;
			case PanelAppletBackgroundType.PixmapBackground:
				Gtk.Style copy = tray.Style.Copy();
				copy.SetBgPixmap (Gtk.StateType.Normal, pixmap);
				tray.Style = copy;
				break;
			}
		}

		protected override void OnChangeSize (uint size)
		{
			if (tray == null)
				return;

			tray.SetSizeRequest ((int) size, (int) size);
		}
	}

	public class TomboyTrayIcon : Gtk.Plug
	{
		NoteManager manager;
		TomboyTray tray;
		TomboyGConfXKeybinder keybinder;

		[DllImport ("libtomboy")]
		private static extern IntPtr egg_tray_icon_new (string name);

		public TomboyTrayIcon ()
			: this (Tomboy.DefaultNoteManager)
		{
		}

		public TomboyTrayIcon (NoteManager manager)
		{
			this.Raw = egg_tray_icon_new (Catalog.GetString ("Tomboy Notes"));
			this.manager = manager;

			tray = new TomboyTray (manager);
			tray.ButtonPressEvent += ButtonPress;

			keybinder = new TomboyGConfXKeybinder (manager, tray);

			Add (tray);
			ShowAll ();
		}

		void ButtonPress (object sender, Gtk.ButtonPressEventArgs args) 
		{
			Gtk.Widget parent = (Gtk.Widget) sender;

			if (args.Event.Button == 3) {
				Gtk.Menu menu = MakeRightClickMenu (parent);
				GuiUtils.PopupMenu (menu, args.Event);
				args.RetVal = true;
			}
		}

		Gtk.Menu MakeRightClickMenu (Gtk.Widget parent)
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AttachToWidget (parent, GuiUtils.DetachMenu);

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			menu.AccelGroup = accel_group;

			Gtk.ImageMenuItem item;

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Preferences..."));
			item.Image = new Gtk.Image (Gtk.Stock.Properties, Gtk.IconSize.Menu);
			item.Activated += ShowPreferences;
			menu.Append (item);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Open Plugins Folder"));
			item.Image = new Gtk.Image (Gtk.Stock.Execute, Gtk.IconSize.Menu);
			item.Activated += ShowPlugins;
			menu.Append (item);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_About Tomboy"));
			item.Image = new Gtk.Image (Gnome.Stock.About, Gtk.IconSize.Menu);
			item.Activated += ShowAbout;
			menu.Append (item);

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Quit"));
			item.Image = new Gtk.Image (Gtk.Stock.Quit, Gtk.IconSize.Menu);
			item.Activated += Quit;
			menu.Append (item);

			menu.ShowAll ();
			return menu;
		}

		void ShowPreferences (object sender, EventArgs args)
		{
			tray.ShowPreferences ();
		}

		void ShowPlugins (object sender, EventArgs args)
		{
			manager.PluginManager.ShowPluginsDirectory ();
		}

		void ShowAbout (object sender, EventArgs args)
		{
			tray.ShowAbout ();
		}

		void Quit (object sender, EventArgs args)
		{
			Logger.Log ("Quitting Tomboy.  Ciao!");
			Tomboy.Exit (0);
		}
	}
}
