
using System;
using System.IO;
using Mono.Posix;

namespace Tomboy 
{
	public class Tomboy 
	{
		static Gnome.Program program;
		static Syscall.sighandler_t sig_handler;

		static TomboyTray instance;
		static NoteManager manager;
		static TomboyGConfXKeybinder keybinder;

		public static void Main (string [] args) 
		{
			program = new Gnome.Program ("Tomboy", 
						     Defines.VERSION, 
						     Gnome.Modules.UI, 
						     args);

			// Initialize GETTEXT
			Catalog.Init ("tomboy", Defines.GNOME_LOCALE_DIR);

			manager = new NoteManager ();
			instance = new TomboyTray (manager);
			keybinder = new TomboyGConfXKeybinder (manager, instance);

			RegisterSignalHandlers ();
			RegisterSessionRestart (args);

			program.Run ();
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
			System.Environment.Exit (0);
		}
	}
}
