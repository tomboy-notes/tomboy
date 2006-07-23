
using System;
using System.IO;
using Mono.Unix;
using Mono.Unix.Native;

namespace Tomboy 
{
	public class Tomboy 
	{
		static Gnome.Program program;
		static NoteManager manager;
		static Object dbus_connection;

		public static void Main (string [] args) 
		{
			RegisterSignalHandlers ();

			// Initialize GETTEXT
			Catalog.Init ("tomboy", Defines.GNOME_LOCALE_DIR);

			TomboyCommandLine cmd_line = new TomboyCommandLine (args);

			if (cmd_line.NeedsExecute) {
				// Execute args at an existing tomboy instance...
				cmd_line.Execute ();
				return;
			}

			program = new Gnome.Program ("Tomboy", 
						     Defines.VERSION, 
						     Gnome.Modules.UI, 
						     args);

			// Create the default note manager instance.
			if (cmd_line.NotePath != null) {
				manager = new NoteManager (cmd_line.NotePath);
			} else {
				manager = new NoteManager ();
			}

			// Register the manager to handle remote requests.
			RegisterRemoteControl (manager);

			if (cmd_line.UsePanelApplet) {
				RegisterPanelAppletFactory (); 
			} else {
				StartTrayIcon ();
			}

			Logger.Log ("All done.  Ciao!");
		}

		static void StartTrayIcon ()
		{
			// Create the tray icon and run the main loop
			TomboyTrayIcon tray_icon = new TomboyTrayIcon (DefaultNoteManager);
			tray_icon.Show ();
			program.Run ();
		}

		static void RegisterPanelAppletFactory ()
		{
			// This will block if there is no existing instance running
			Gnome.PanelAppletFactory.Register (typeof (TomboyApplet));
		}

		static void RegisterRemoteControl (NoteManager manager)
		{
#if ENABLE_DBUS
			try {
				dbus_connection = DBus.Bus.GetSessionBus ();
				DBus.Service service = 
					new DBus.Service ((DBus.Connection) dbus_connection, 
							  RemoteControlProxy.Namespace);

				RemoteControl remote_control = new RemoteControl (manager);
				service.RegisterObject (remote_control, RemoteControlProxy.Path);

				Logger.Log ("Tomboy remote control active.");
			} catch (Exception e) {
				Logger.Log ("Tomboy remote control disabled: {0}",
						   e.Message);
			}
#endif
		}

		static void RegisterSignalHandlers ()
		{
			// Connect to SIGTERM and SIGQUIT, so we don't lose
			// unsaved notes on exit...
			Stdlib.signal (Signum.SIGTERM, OnExitSignal);
			Stdlib.signal (Signum.SIGQUIT, OnExitSignal);
		}

		static void OnExitSignal (int signal)
		{
			if (ExitingEvent != null)
				ExitingEvent (null, new EventArgs ());

			if (signal >= 0)
				System.Environment.Exit (0);
		}

		public static event EventHandler ExitingEvent;

		public static void Exit (int exitcode)
		{
			OnExitSignal (-1);
			System.Environment.Exit (exitcode);
		}

		public static NoteManager DefaultNoteManager
		{
			get { return manager; }
		}
	}

	public class TomboyCommandLine
	{
		bool new_note;
		bool panel_applet;
		string new_note_name;
		string open_note_uri;
		string open_note_name;
		string highlight_search;
		string note_path;

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
				return new_note || open_note_name != null || open_note_uri != null;
			}
		}

		public string NotePath
		{
			get { return note_path; }
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
					"directory.\n");

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
#else
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
					if (idx + 1 < args.Length && args [idx + 1][0] != '-') {
						new_note_name = args [++idx];
					}

					new_note = true;
					break;

				case "--open-note":
					// Get required name for note to open...
					if (idx + 1 >= args.Length || args [idx + 1][0] == '-') {
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
					open_note_name = Catalog.GetString ("Start Here");
					break;

				case "--highlight-search":
					// Get required search string to highlight
					if (idx + 1 >= args.Length || args [idx + 1][0] == '-') {
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
					if (idx + 1 >= args.Length || args [idx + 1][0] == '-') {
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
			DBus.Connection connection = DBus.Bus.GetSessionBus ();
			DBus.Service service = new DBus.Service (connection, 
								 RemoteControlProxy.Namespace);

			RemoteControlProxy remote = (RemoteControlProxy) 
				service.GetObject (typeof (RemoteControlProxy),
						   RemoteControlProxy.Path);

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

			if (open_note_name != null)
				open_note_uri = remote.FindNote (open_note_name);

			if (open_note_uri != null) {
				if (highlight_search != null)
					remote.DisplayNoteWithSearch (open_note_uri, 
								      highlight_search);
				else
					remote.DisplayNote (open_note_uri);
			}
#endif // ENABLE_DBUS
		}
	}
}
