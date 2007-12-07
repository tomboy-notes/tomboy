using System;
using Tomboy;

namespace Tomboy.Bugzilla
{
	public class BugzillaPreferenceFactory : AddinPreferenceFactory
	{
		public override Gtk.Widget CreatePreferenceWidget ()
		{
			return new BugzillaPreferences ();
		}
	}
}
