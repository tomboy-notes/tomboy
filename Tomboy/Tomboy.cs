
using System;

namespace Tomboy 
{
	public class Tomboy 
	{
		static TomboyTray instance;
		static NoteManager manager;

		public static void Main (string [] args) 
		{
			Gnome.Program program = new Gnome.Program ("Tomboy", 
								   "0.0", 
								   Gnome.Modules.UI, 
								   args);

			/* Restart if we are running when the session ends */
			Gnome.Client client = Gnome.Global.MasterClient ();
			client.RestartStyle = Gnome.RestartStyle.IfRunning;

			manager = new NoteManager ();
			instance = new TomboyTray (manager);

			program.Run ();
		}
	}
}
