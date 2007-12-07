using System;
using Tomboy;

namespace Tomboy.NoteOfTheDay
{
	public class NoteOfTheDayPreferencesFactory : AddinPreferenceFactory
	{
		public override Gtk.Widget CreatePreferenceWidget ()
		{
			return new NoteOfTheDayPreferences ();
		}
	}
}
