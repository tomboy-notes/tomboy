
using System;
using System.IO;
using Mono.Unix;

namespace Tomboy 
{
	public class Tomboy : Application
	{
		static NoteManager manager;
		static TomboyTrayIcon tray_icon;
		static bool tray_icon_showing = false;
		static bool is_panel_applet = false;
		static PreferencesDialog prefs_dlg;
#if ENABLE_DBUS
		static RemoteControl remote_control;
#endif

		public static void Main (string [] args) 
		{
			// Initialize GETTEXT
			Catalog.Init ("tomboy", Defines.GNOME_LOCALE_DIR);
			
			TomboyCommandLine cmd_line = new TomboyCommandLine (args);

#if ENABLE_DBUS // Run command-line earlier with DBus enabled
			if (cmd_line.NeedsExecute) {
				// Execute args at an existing tomboy instance...
				cmd_line.Execute ();
				return;
			}
#endif // ENABLE_DBUS

			Initialize ("tomboy", "Tomboy", "tomboy", args);

			PluginManager.CheckPluginUnloading = cmd_line.CheckPluginUnloading;

			// Create the default note manager instance.
			string note_path = GetNotePath (cmd_line.NotePath);
			manager = new NoteManager (note_path);

			// Register the manager to handle remote requests.
			RegisterRemoteControl (manager);
			
			SetupGlobalActions ();

#if !ENABLE_DBUS
			if (cmd_line.NeedsExecute) {
				cmd_line.Execute ();
			}
#endif
			ActionManager am = Tomboy.ActionManager;

			if (cmd_line.UsePanelApplet) {
				tray_icon_showing = true;
				is_panel_applet = true;

				// Show the Close item and hide the Quit item
				am ["CloseWindowAction"].Visible = true;
				am ["QuitTomboyAction"].Visible = false;

				RegisterPanelAppletFactory ();
			} else {
				RegisterSessionManagerRestart (
					Environment.GetEnvironmentVariable ("TOMBOY_WRAPPER_PATH"),
					args,
					new string [] { "TOMBOY_PATH=" + note_path  });
				StartTrayIcon ();
			}
			
			Logger.Log ("All done.  Ciao!");
		}

		static string GetNotePath (string override_path)
		{
			// Default note location, as specified in --note-path or $TOMBOY_PATH
			string note_path = 
				(override_path != null) ? 
		                       override_path : 
				       Environment.GetEnvironmentVariable ("TOMBOY_PATH");
			if (note_path == null)
				note_path = "~/.tomboy";

			// Tilde expand
			return note_path.Replace ("~", Environment.GetEnvironmentVariable ("HOME"));
		}

		static void RegisterPanelAppletFactory ()
		{
			// This will block if there is no existing instance running
			// FIXME: Use custom built panel applet bindings to work around bug in GTK#
			_Gnome.PanelAppletFactory.Register (typeof (TomboyApplet));
		}

		static void StartTrayIcon ()
		{
			// Create the tray icon and run the main loop
			tray_icon = new TomboyTrayIcon (DefaultNoteManager);
			
			// Give the TrayIcon 2 seconds to appear.  If it
			// doesn't by then, open the SearchAllNotes window.
			tray_icon.Embedded += TrayIconEmbedded;
			GLib.Timeout.Add (2000, CheckTrayIconShowing);
			tray_icon.Show ();

			StartMainLoop ();
		}
		
		// This event is signaled when Tomboy's TrayIcon is added to the
		// Notification Area.  If it's never signaled, the Notification Area
		// is not available.
		static void TrayIconEmbedded (object sender, EventArgs args)
		{
			tray_icon_showing = true;
		}
		
		static bool CheckTrayIconShowing ()
		{
			// Check to make sure the tray icon is showing.  If it's not,
			// it's likely that the Notification Area isn't available.  So
			// instead, launch the Search All Notes window so the user can
			// can still use Tomboy.
			if (tray_icon_showing == false)
				ActionManager ["ShowSearchAllNotesAction"].Activate ();
			
			return false; // prevent GLib.Timeout from calling this method again
		}

		static void RegisterRemoteControl (NoteManager manager)
		{
#if ENABLE_DBUS
			try {
				remote_control = RemoteControlProxy.Register (manager);
				if (remote_control != null) {
					Logger.Log ("Tomboy remote control active.");
				} else {
					// If Tomboy is already running, open the search window
					// so the user gets some sort of feedback when they
					// attempt to run Tomboy again.
					RemoteControl remote = null;
					try {
						remote = RemoteControlProxy.GetInstance ();
						remote.DisplaySearch ();
					} catch {}

					Logger.Log ("Tomboy is already running.  Exiting...");
					System.Environment.Exit (-1);
				}
			} catch (Exception e) {
				Logger.Log ("Tomboy remote control disabled (DBus exception): {0}",
						   e.Message);
			}
#endif
		}

		// These actions can be called from anywhere in Tomboy
		static void SetupGlobalActions ()
		{
			ActionManager am = Tomboy.ActionManager;
			am ["NewNoteAction"].Activated += OnNewNoteAction;
			am ["QuitTomboyAction"].Activated += OnQuitTomboyAction;
			am ["ShowPreferencesAction"].Activated += OnShowPreferencesAction;
			am ["ShowHelpAction"].Activated += OnShowHelpAction;
			am ["ShowAboutAction"].Activated += OnShowAboutAction;
			am ["TrayNewNoteAction"].Activated += OnNewNoteAction;
			am ["ShowSearchAllNotesAction"].Activated += OpenSearchAll;
		}
		
		static void OnNewNoteAction (object sender, EventArgs args)
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
		
		static void OnQuitTomboyAction (object sender, EventArgs args)
		{
			if (Tomboy.IsPanelApplet)
				return; // Ignore the quit action

			Logger.Log ("Quitting Tomboy.  Ciao!");
			Exit (0);
		}
		
		static void OnShowPreferencesAction (object sender, EventArgs args)
		{
			if (prefs_dlg == null) {
				prefs_dlg = new PreferencesDialog (manager.PluginManager);
				prefs_dlg.Response += OnPreferencesResponse;
			}
			prefs_dlg.Present ();
		}

		static void OnPreferencesResponse (object sender, Gtk.ResponseArgs args)
		{
			((Gtk.Widget) sender).Destroy ();
			prefs_dlg = null;
		}
		
		static void OnShowHelpAction (object sender, EventArgs args)
		{
			// Pass in null for the screen when we're running as a panel applet
			GuiUtils.ShowHelp("tomboy.xml", null,
					tray_icon == null ? null : tray_icon.TomboyTray.Screen,
					null);
		}
		
		static void OnShowAboutAction (object sender, EventArgs args)
		{
			string [] authors = new string [] {
				"Alex Graveley <alex@beatniksoftware.com>",
				"Boyd Timothy <btimothy@gmail.com>",
				"Chris Scobell <chris@thescobells.com>",
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
				Catalog.GetString ("Copyright \xa9 2004-2007 Alex Graveley");
			about.Comments = Catalog.GetString ("A simple and easy to use desktop " +
							    "note-taking application.");
			about.Website = "http://www.gnome.org/projects/tomboy/";
			about.WebsiteLabel = Catalog.GetString("Homepage & Donations");
			about.Authors = authors;
			about.Documenters = documenters;
			about.TranslatorCredits = translators;
			about.IconName = "tomboy";
			about.Run ();
			about.Destroy ();
		}
		
		static void OpenSearchAll (object sender, EventArgs args)
		{
			NoteRecentChanges.GetInstance (manager).Present ();
		}
		
		public static NoteManager DefaultNoteManager
		{
			get { return manager; }
		}
		
		public static bool TrayIconShowing
		{
			get { return tray_icon_showing; }
		}
		
		public static bool IsPanelApplet
		{
			get { return is_panel_applet; }
		}
		
		public static TomboyTray Tray
		{
			get { return tray_icon.TomboyTray; }
		}
	}

	public class TomboyCommandLine
	{
		bool new_note;
		bool panel_applet;
		string new_note_name;
		bool open_start_here;
		string open_note_uri;
		string open_note_name;
		string highlight_search;
		string note_path;
		string search_text;
		bool open_search;
		bool check_plugin_unloading;

		public TomboyCommandLine (string [] args)
		{
			Parse (args);
		}

		public bool UsePanelApplet
		{
			get { return panel_applet; }
		}

		public bool NeedsExecute
		{
			get { 
				return new_note || 
						open_note_name != null ||
						open_note_uri != null || 
						open_search ||
						open_start_here;
			}
		}

		public string NotePath
		{
			get { return note_path; }
		}

		public bool CheckPluginUnloading
		{
			get { return check_plugin_unloading; }
		}

		public static void PrintAbout () 
		{
            string about = 
				Catalog.GetString (
					"Tomboy: A simple, easy to use desktop note-taking " +
					"application.\n" +
					"Copyright (C) 2004-2006 Alex Graveley " +
					"<alex@beatniksoftware.com>\n\n");

			Console.Write (about);
		}

		public static void PrintUsage () 
		{
			string usage = 
				Catalog.GetString (
					"Usage:\n" +
					"  --version\t\t\tPrint version information.\n" +
					"  --help\t\t\tPrint this usage message.\n" +
					"  --note-path [path]\t\tLoad/store note data in this " +
					"directory.\n" +
					"  --search [text]\t\tOpen the search all notes window with " +
					"the search text.\n");

#if ENABLE_DBUS
			usage += 
				Catalog.GetString (
					"  --new-note\t\t\tCreate and display a new note.\n" +
					"  --new-note [title]\t\tCreate and display a new note, " +
					"with a title.\n" +
					"  --open-note [title/url]\tDisplay the existing note " +
					"matching title.\n" +
					"  --start-here\t\t\tDisplay the 'Start Here' note.\n" +
					"  --highlight-search [text]\tSearch and highlight text " +
					"in the opened note.\n");
#endif

			usage +=
				Catalog.GetString (
					"  --check-plugin-unloading\tCheck if plugins are " +
					"unloaded properly.\n");

#if !ENABLE_DBUS
			usage += Catalog.GetString ("D-BUS remote control disabled.\n");
#endif

			Console.WriteLine (usage);
		}

		public static void PrintVersion()
		{
			Console.WriteLine (Catalog.GetString ("Version {0}"), Defines.VERSION);
		}

		public void Parse (string [] args)
		{
			for (int idx = 0; idx < args.Length; idx++) {
				bool quit = false;

				switch (args [idx]) {
#if ENABLE_DBUS
				case "--new-note":
					// Get optional name for new note...
					if (idx + 1 < args.Length
							&& args [idx + 1] != null
							&& args [idx + 1] != String.Empty
							&& args [idx + 1][0] != '-') {
						new_note_name = args [++idx];
					}

					new_note = true;
					break;

				case "--open-note":
					// Get required name for note to open...
					if (idx + 1 >= args.Length ||
							(args [idx + 1] != null
								&& args [idx + 1] != String.Empty
								&& args [idx + 1][0] == '-')) {
						PrintUsage ();
						quit = true;
					}

					++idx;
					
					// If the argument looks like a Uri, treat it like a Uri.
					if (args [idx].StartsWith ("note://tomboy/"))
						open_note_uri = args [idx];
					else
						open_note_name = args [idx];

					break;

				case "--start-here":
					// Open the Start Here note
					open_start_here = true;
					break;

				case "--highlight-search":
					// Get required search string to highlight
					if (idx + 1 >= args.Length ||
							(args [idx + 1] != null
								&& args [idx + 1] != String.Empty
								&& args [idx + 1][0] == '-')) {
						PrintUsage ();
						quit = true;
					}

					++idx;
					highlight_search = args [idx];
					break;
#else
				case "--new-note":
				case "--open-note":
				case "--start-here":
				case "--highlight-search":
					string unknown_opt = 
						Catalog.GetString (
							"Tomboy: unsupported option '{0}'\n" +
							"Try 'tomboy --help' for more " +
							"information.\n" +
							"D-BUS remote control disabled.");
					Console.WriteLine (unknown_opt, args [idx]);
					quit = true;
					break;
#endif // ENABLE_DBUS

				case "--panel-applet":
					panel_applet = true;
					break;

				case "--note-path":
					if (idx + 1 >= args.Length || 
							(args [idx + 1] != null
								&& args [idx + 1] != String.Empty
								&& args [idx + 1][0] == '-')) {
						PrintUsage ();
						quit = true;
					}

					note_path = args [++idx];

					if (!Directory.Exists (note_path)) {
						Console.WriteLine (
							"Tomboy: Invalid note path: " +
							"\"{0}\" does not exist.",
							note_path);
						quit = true;
					}

					break;

				case "--search":
					// Get optional search text...
					if (idx + 1 < args.Length
							&& args [idx + 1] != null
							&& args [idx + 1] != String.Empty
							&& args [idx + 1][0] != '-') {
						search_text = args [++idx];
					}
					
					open_search = true;
					break;

				case "--check-plugin-unloading":
					check_plugin_unloading = true;
					break;

				case "--version":
					PrintAbout ();
					PrintVersion();
					quit = true;
					break;

				case "--help":
				case "--usage":
					PrintAbout ();
					PrintUsage ();
					quit = true;
					break;

				default:
					break;
				}

				if (quit == true)
					System.Environment.Exit (1);
			}
		}

		public void Execute ()
		{
#if ENABLE_DBUS
			RemoteControl remote = null;
			try {
				remote = RemoteControlProxy.GetInstance ();
			} catch (Exception e) {
				Logger.Log ("Unable to connect to Tomboy remote control: {0}",
					e.Message);
			}

			if (remote == null)
				return;

			if (new_note) {
				string new_uri;

				if (new_note_name != null) {
					new_uri = remote.FindNote (new_note_name);

					if (new_uri == null || new_uri == string.Empty)
						new_uri = remote.CreateNamedNote (new_note_name);
				} else
					new_uri = remote.CreateNote ();

				if (new_uri != null)
					remote.DisplayNote (new_uri);
			}

			if (open_start_here)
				open_note_uri = remote.FindStartHereNote ();

			if (open_note_name != null)
				open_note_uri = remote.FindNote (open_note_name);
			
			if (open_note_uri != null) {
				if (highlight_search != null)
					remote.DisplayNoteWithSearch (open_note_uri, 
								      highlight_search);
				else
					remote.DisplayNote (open_note_uri);
			}
			
			if (open_search) {
				if (search_text != null)
					remote.DisplaySearchWithText (search_text);
				else
					remote.DisplaySearch ();
			}
#else
			if (open_search) {
				NoteRecentChanges recent_changes =
					NoteRecentChanges.GetInstance (Tomboy.DefaultNoteManager);
				if (recent_changes == null)
					return;
				
				if (search_text != null)
					recent_changes.SearchText = search_text;

				recent_changes.Present ();
			}
#endif // ENABLE_DBUS
		}
	}
}
