
using System;
using System.Runtime.InteropServices;
using Mono.Posix;

using PanelApplet;

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
			Console.WriteLine ("Applet Created...");

			manager = new NoteManager ();
			tray = new TomboyTray (manager);
			keybinder = new TomboyGConfXKeybinder (manager, tray);

			// Register the manager to handle remote requests.
			Tomboy.RegisterRemoteControl (manager);

			Add (tray);
			ShowAll ();

			// Keep around so our callbacks don't get reaped.
			menu_verbs = new BonoboUIVerb [] {
				new BonoboUIVerb ("Props", 
						  new ContextMenuItemCallback (ShowPreferencesVerb)),
				new BonoboUIVerb ("Plugins", 
						  new ContextMenuItemCallback (ShowPluginsVerb)),
				new BonoboUIVerb ("About", 
						  new ContextMenuItemCallback (ShowAboutVerb))
			};

			// FIXME: This silently fails for some unknown reason
			//SetupMenuFromResource (null, "GNOME_TomboyApplet.xml", menu_verbs);

			// Have to resort to this for now
			SetupMenuFromFile (Defines.DATADIR,
					   "GNOME_TomboyApplet.xml",
					   "Tomboy",
					   menu_verbs);

			// FIXME: Connecting to this crashes in the C# bindings.
			//ChangeBackground += OnChangeBackgroundEvent;
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

		void OnChangeBackgroundEvent (object sender, ChangeBackgroundArgs args)
		{
			// This is needed to support transparent panel
			// backgrounds correctly.

			Console.WriteLine ("OnChangeBackgroundEvent Called!");

			switch (args.Type) {
			case BackgroundType.NoBackground:
			case BackgroundType.PixmapBackground:
				Gtk.RcStyle rc_style = new Gtk.RcStyle ();

				tray.Image.ModifyStyle (rc_style);
				ModifyStyle (rc_style);
				break;

			case BackgroundType.ColorBackground:
				tray.Image.ModifyBg (Gtk.StateType.Normal, args.Color);
				ModifyBg (Gtk.StateType.Normal, args.Color);
				break;
			}
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
			: this (new NoteManager ())
		{
		}

		public TomboyTrayIcon (NoteManager manager)
		{
			this.Raw = egg_tray_icon_new (Catalog.GetString ("Tomboy Notes"));
			this.manager = manager;

			// Register the manager to handle remote requests.
			Tomboy.RegisterRemoteControl (manager);

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
			menu.AttachToWidget (parent, null);

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			menu.AccelGroup = accel_group;

			Gtk.ImageMenuItem item;

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Preferences..."));
			item.Image = new Gtk.Image (Gtk.Stock.Properties, Gtk.IconSize.Menu);
			item.Activated += ShowPreferences;
			menu.Append (item);

			item = new Gtk.ImageMenuItem (Catalog.GetString ("_Install Plugins..."));
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
			Console.WriteLine ("Quitting Tomboy.  Ciao!");
			Tomboy.Exit (0);
		}
	}
}
