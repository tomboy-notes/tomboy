// Plugin for Tomboy Advanced preferences tab
// (c) 2011 Alex Tereschenko <frozenblue@zoho.com>
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
		
		// This one is an event handler for a SpinButton, used to set the menu note count
		private void UpdateMenuNoteCountPreference (object source, EventArgs args)
		{
			Gtk.SpinButton spinner = source as SpinButton;
			Preferences.Set (Preferences.MENU_NOTE_COUNT, spinner.ValueAsInt);
			
		}
		
		
		public override bool GetPreferenceTabWidget (	PreferencesDialog parent,
								out string tabLabel,
								out Gtk.Widget preferenceWidget)
		{
			
			Gtk.Label label;
			Gtk.SpinButton menuNoteCountSpinner;
			Gtk.Alignment align;
			int menuNoteCount;

			// Addin's tab caption
			tabLabel = Catalog.GetString ("Advanced");
			
			Gtk.VBox opts_list = new Gtk.VBox (false, 12);
			opts_list.BorderWidth = 12;
			opts_list.Show ();
			
			align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 1.0f);
			align.Show ();
			opts_list.PackStart (align, false, false, 0);

			Gtk.Table table = new Gtk.Table (1, 2, false);
			table.ColumnSpacing = 6;
			table.RowSpacing = 6;
			table.Show ();
			align.Add (table);


			// Menu Note Count option
			label = new Gtk.Label (Catalog.GetString ("Minimum number of notes to show in Recent list (maximum 18)"));

			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			
			table.Attach (label, 0, 1, 0, 1);
		
			menuNoteCount = (int) Preferences.Get (Preferences.MENU_NOTE_COUNT);
			// we have a hard limit of 18 set, thus not allowing anything bigger than 18
			menuNoteCountSpinner = new Gtk.SpinButton (1, 18, 1);
			menuNoteCountSpinner.Value = menuNoteCount <= 18 ? menuNoteCount : 18;
			
			menuNoteCountSpinner.Show ();
			table.Attach (menuNoteCountSpinner, 1, 2, 0, 1);
			
			menuNoteCountSpinner.ValueChanged += UpdateMenuNoteCountPreference;
			
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
