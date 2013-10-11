// Plugin for Tomboy Advanced preferences tab
// (c) 2011-2013 Alex Tereschenko <frozen.and.blue@gmail.com>
// LGPL 2.1 or later

using System;
using Gtk;
using Mono.Unix;
using Tomboy;

namespace Tomboy.AdvancedPreferences
{
	/// <summary>
	/// Contains a class for Advanced preferences tab
	/// </summary>
	public class AdvancedPreferencesAddin : PreferenceTabAddin
	{

		public override bool GetPreferenceTabWidget (	PreferencesDialog parent,
								out string tabLabel,
								out Gtk.Widget preferenceWidget)
		{

			// Addin's tab caption
			tabLabel = Catalog.GetString ("Advanced");

			Gtk.VBox opts_list = new Gtk.VBox (false, 12);
			opts_list.BorderWidth = 12;
			opts_list.Show ();

			/*
			If you want to add new settings to the Advanced tab - follow the steps below:
				1) define a class which implements the functionality (see e.g. MenuMinMaxNoteCountPreference.cs);
				2) define property/method for that class that returns the widget you want to place onto the tab;
				3) (similar to the below) instantiate object of your class and PackStart its widget to opts_list;
				It's expected that the returned widget is already within Gtk.Alignment, so no further alignment done.
			*/

			// TODO: More elegant way of implementing this would be to create a collection of "prefs" objects
			// and iterate over them adding them to opts_list (fewer lines to add upon adding new setting).

			// Instantiate class for Menu Min/Max Note Count setting
			MenuMinMaxNoteCountPreference menuNoteCountPref = new MenuMinMaxNoteCountPreference();
			// Add the widget for this setting to the Advanced tab
			opts_list.PackStart(menuNoteCountPref.Widget, false, false, 0);

			//Instantiate class for Enable Startup Notes setting
			EnableStartupNotesPreference enableStartupNotesPref = new EnableStartupNotesPreference();
			// Add the widget to the Advanced tab
			opts_list.PackStart (enableStartupNotesPref.Widget, false, false, 0);

			if (opts_list != null) {
				preferenceWidget = opts_list;
				return true;
			} else {
				preferenceWidget = null;
				return false;
			}
		}
	}
}
