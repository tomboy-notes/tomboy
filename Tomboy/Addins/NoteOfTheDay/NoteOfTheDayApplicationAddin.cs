
using System;
using System.Runtime.InteropServices;

using Gtk;

using Tomboy;

namespace Tomboy.NoteOfTheDay
{
	public class NoteOfTheDayApplicationAddin : ApplicationAddin
	{
		bool initialized = false;
		bool timeout_owner;
		static InterruptableTimeout timeout;
		NoteManager manager;

		// Called only by instance with timeout_owner set.
		void CheckNewDay (object sender, EventArgs args)
		{
			Note notd = NoteOfTheDay.GetNoteByDate (manager, DateTime.Today);
			if (notd == null) {
				NoteOfTheDay.CleanupOld (manager);

				// Create a new NotD if the day has changed
				NoteOfTheDay.Create (manager, DateTime.Now);
			}

			// Re-run every minute
			timeout.Reset (1000 * 60);
		}

		public override void Initialize ()
		{
			if (timeout == null) {
				timeout = new InterruptableTimeout ();
				timeout.Timeout += CheckNewDay;
				timeout.Reset (0);
				timeout_owner = true;
			}
			manager = Tomboy.DefaultNoteManager;
			initialized = true;
		}

		public override void Shutdown ()
		{
			if (timeout_owner) {
				NoteOfTheDay.CleanupOld (manager);
				timeout.Timeout -= CheckNewDay;
				timeout.Cancel();
				timeout = null;
			}

			initialized = false;
		}

		public override bool Initialized
		{
			get {
				return initialized;
			}
		}
	}
}
