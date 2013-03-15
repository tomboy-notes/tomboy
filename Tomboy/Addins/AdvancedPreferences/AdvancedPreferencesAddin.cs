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
			
			Gtk.Alignment align;

			// Addin's tab caption
			tabLabel = Catalog.GetString ("Advanced");
			
			Gtk.VBox opts_list = new Gtk.VBox (false, 12);
			opts_list.BorderWidth = 12;
			opts_list.Show ();
			
			align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 1.0f);
			align.Show ();
			opts_list.PackStart (align, false, false, 0);

			/*
			If you want to add new settings to the Advanced tab - follow the steps below:
				1) define a class which implements the functionality (see e.g. MenuMinMaxNoteCountPreference.cs);
				2) define property/method for that class that returns the widget you want to place onto the tab;
				3) (similar to the below) instantiate object of your class and add its widget to the "align" widget;
			*/
			// Instantiate class for Menu Min/Max Note Count setting
			MenuMinMaxNoteCountPreference menuNoteCountPref = new MenuMinMaxNoteCountPreference();
			// Add the widget for this setting to the Advanced tab
			align.Add (menuNoteCountPref.Widget);

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
