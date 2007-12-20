using System;

namespace Tomboy
{
	/// <summary>
	/// Implement this interface to provide a new tab in
	/// Tomboy's Preferences Dialog.  If you are writing
	/// a standard add-in, DO NOT ABUSE THIS (you should
	/// normally extend the /Tomboy/AddinPreferences
	/// extension point).
	/// </summary>
	public abstract class PreferenceTabAddin : AbstractAddin
	{
		/// <summary>
		/// Returns a Gtk.Widget to place in a new tab in Tomboy's
		/// preferences dialog.
		/// <param name="parent">The preferences dialog.  Add-ins should
		/// use this for connecting to Hidden or other events as needed.
		/// Another use would be to pop open dialogs, so they can properly
		/// set their parent.
		/// </param>
		/// <param name="tabLabel">The string to be used in the tab's
		/// label.</param>
		/// <param name="preferenceWidget">The Gtk.Widget to use as the
		/// content of the tab page.</param>
		/// <returns>Returns <value>true</value> if the widget is
		/// valid/created or <value>false</value> otherwise.</returns>
		/// </summary>
		public abstract bool GetPreferenceTabWidget (
									PreferencesDialog parent,
									out string tabLabel,
									out Gtk.Widget preferenceWidget);
	}
}
