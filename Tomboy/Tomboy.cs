
using System;
using System.IO;
using Mono.Posix;

namespace Tomboy 
{
	public class Tomboy 
	{
		static Gnome.Program program;
		static Syscall.sighandler_t sig_handler;

		static TomboyTray tray;
		static NoteManager manager;
		static TomboyGConfXKeybinder keybinder;

		public static void Main (string [] args) 
		{
			// Initialize GETTEXT
			Catalog.Init ("tomboy", Defines.GNOME_LOCALE_DIR);

			// Execute any args at an existing tomboy instance
			if (TomboyRemoteExecute.Execute (args))
				return;

			program = new Gnome.Program ("Tomboy", 
						     Defines.VERSION, 
						     Gnome.Modules.UI, 
						     args);

			manager = new NoteManager ();
			tray = new TomboyTray (manager);
			keybinder = new TomboyGConfXKeybinder (manager, tray);

			RegisterSignalHandlers ();
			RegisterSessionRestart (args);
			RegisterRemoteControl (manager);

			program.Run ();
		}

		static void RegisterRemoteControl (NoteManager manager)
		{
#if ENABLE_DBUS
			try {
				DBus.Connection connection = DBus.Bus.GetSessionBus ();
				DBus.Service service = 
					new DBus.Service (connection, 
							  RemoteControlProxy.Namespace);

				RemoteControl remote_control = new RemoteControl (manager);
				service.RegisterObject (remote_control, RemoteControlProxy.Path);

				Console.WriteLine ("Tomboy remote control active.");
			} catch (Exception e) {
				Console.WriteLine ("Tomboy remote control disabled: {0}",
						   e.Message);
			}
#endif
		}

		static void RegisterSessionRestart (string [] args)
		{
			// $TOMBOY_WRAPPER_PATH gets set by the wrapper script...
			string wrapper = Environment.GetEnvironmentVariable ("TOMBOY_WRAPPER_PATH");
			if (wrapper == null)
				return;

			// Get the args for session restart...
			string [] restart_args = new string [args.Length + 1];
			restart_args [0] = wrapper;
			args.CopyTo (restart_args, 1);

			// Restart if we are running when the session ends...
			Gnome.Client client = Gnome.Global.MasterClient ();
			client.RestartStyle = Gnome.RestartStyle.IfRunning;
			client.SetRestartCommand (restart_args.Length, restart_args);
		}

		static void RegisterSignalHandlers ()
		{
			sig_handler = OnExitSignal;

			// Connect to SIGTERM and SIGQUIT, so we don't lose
			// unsaved notes on exit...
			Syscall.signal ((int) Signals.SIGTERM, sig_handler);
			Syscall.signal ((int) Signals.SIGQUIT, sig_handler);
		}

		static void OnExitSignal (int signal)
		{
			Console.WriteLine ("Saving unsaved notes...");

			foreach (Note note in manager.Notes) {
				note.Save ();
			}

			program.Quit ();
			//System.Environment.Exit (0);
		}
	}

	public class TomboyRemoteExecute
	{
		bool new_note;
		string new_note_name;
		string open_note_name;

		public static bool Execute (string [] args)
		{
#if ENABLE_DBUS
			TomboyRemoteExecute obj = new TomboyRemoteExecute ();
			return obj.Parse (args) || obj.Execute ();
#else
			if (args.Length != 0)
				PrintUsage ();
			return args.Length != 0;
#endif
		}

		public static void PrintUsage () 
		{
                        string usage = 
				Catalog.GetString (
					"Tomboy: A simple, easy to use desktop note-taking application.\n" +
					"Copyright (C) 2004 Alex Graveley <alex@beatniksoftware.com>\n\n");

#if ENABLE_DBUS
			usage += 
				Catalog.GetString (
					"Usage:\n" +
					"  --new-note\t\tCreate and display a new note\n" +
					"  --new-note [title]\tCreate and display a new note, with a title\n" +
					"  --open-note [title]\tDisplay the existing note matching title\n" +
					"  --help\t\tPrint this usage message.\n");
#else
			usage += "Tomboy remote control disabled.";
#endif

                        Console.WriteLine (usage);
                }

#if ENABLE_DBUS
		public bool Parse (string [] args)
		{
			for (int idx = 0; idx < args.Length; idx++) {
				switch (args [idx]) {
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
						return true;
					}

					open_note_name = args [++idx];
					break;

				case "--help":
				case "--usage":
				default:
					PrintUsage ();
					return true;
				}
			}

			return false;
		}

		public bool Execute ()
		{
			if (!new_note && open_note_name == null)
				return false;

			DBus.Connection connection = DBus.Bus.GetSessionBus ();
			DBus.Service service = new DBus.Service (connection, 
								 RemoteControlProxy.Namespace);

			RemoteControlProxy remote = (RemoteControlProxy) 
				service.GetObject (typeof (RemoteControlProxy),
						   RemoteControlProxy.Path);

			bool quit = false;

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

				quit = true;
			}

			if (open_note_name != null) {
				string uri = remote.FindNote (open_note_name);

				if (uri != null)
					remote.DisplayNote (uri);

				quit = true;
			}

			return quit;
		}
#endif
	}
}
