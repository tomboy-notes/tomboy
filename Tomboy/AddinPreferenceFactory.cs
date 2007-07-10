using System;

namespace Tomboy
{
	public abstract class AddinPreferenceFactory
	{
		/// <summary>
		/// Returns a Gtk.Widget that will be placed inside of a Gtk.Dialog
		/// when the user chooses to view/set the preferences of an Addin.
		/// </summary>
		public abstract Gtk.Widget CreatePreferenceWidget ();
	}
}
