// Class for Menu Min/Max Note Count setting of Tomboy Advanced preferences tab
// (c) 2013 Alex Tereschenko <frozen.and.blue@gmail.com>
// LGPL 2.1 or later

using System;
using Gtk;
using Mono.Unix;
using Tomboy;

namespace Tomboy.AdvancedPreferences
{
	/// <summary>
	/// Contains a class for Menu Min/Max Note Count setting for Advanced preferences tab
	/// </summary>
	public class MenuMinMaxNoteCountPreference
	{
		private Gtk.Label menuMinNoteCountLabel;
		private Gtk.SpinButton menuMinNoteCountSpinner;
		private int menuMinNoteCount;
		private Gtk.Label menuMaxNoteCountLabel;
		private Gtk.SpinButton menuMaxNoteCountSpinner;
		private int menuMaxNoteCount;
		// This will store both labels and spinbuttons
		private Gtk.Table table;

		public MenuMinMaxNoteCountPreference ()
		{
			table = new Gtk.Table (2, 2, false);
			table.ColumnSpacing = 6;
			table.RowSpacing = 6;
			table.Show ();

			// Menu Min Note Count option
			menuMinNoteCountLabel = new Gtk.Label (Catalog.GetString ("Minimum number of notes to show in Recent list"));

			menuMinNoteCountLabel.UseMarkup = true;
			menuMinNoteCountLabel.Justify = Gtk.Justification.Left;
			menuMinNoteCountLabel.SetAlignment (0.0f, 0.5f);
			menuMinNoteCountLabel.Show ();
			table.Attach (menuMinNoteCountLabel, 0, 1, 0, 1);

			menuMinNoteCount = (int) Preferences.Get (Preferences.MENU_NOTE_COUNT);
			menuMaxNoteCount = (int) Preferences.Get (Preferences.MENU_MAX_NOTE_COUNT);
			// This is to avoid having Max bigger than absolute maximum if someone changed the setting
			// outside of the Tomboy using e.g. gconf
			menuMaxNoteCount = (menuMaxNoteCount <= int.MaxValue ? menuMaxNoteCount : int.MaxValue);
			// This is to avoid having Min bigger than Max if someone changed the setting
			// outside of the Tomboy using e.g. gconf
			menuMinNoteCount = (menuMinNoteCount <= menuMaxNoteCount ? menuMinNoteCount : menuMaxNoteCount);

			menuMinNoteCountSpinner = new Gtk.SpinButton (1, menuMaxNoteCount, 1);
			menuMinNoteCountSpinner.Value = menuMinNoteCount;
			menuMinNoteCountSpinner.Show ();
			table.Attach (menuMinNoteCountSpinner, 1, 2, 0, 1);
			menuMinNoteCountSpinner.ValueChanged += UpdateMenuMinNoteCountPreference;

			// Menu Max Note Count option
			menuMaxNoteCountLabel = new Gtk.Label (Catalog.GetString ("Maximum number of notes to show in Recent list"));

			menuMaxNoteCountLabel.UseMarkup = true;
			menuMaxNoteCountLabel.Justify = Gtk.Justification.Left;
			menuMaxNoteCountLabel.SetAlignment (0.0f, 0.5f);
			menuMaxNoteCountLabel.Show ();
			table.Attach (menuMaxNoteCountLabel, 0, 1, 1, 2);

			menuMaxNoteCountSpinner = new Gtk.SpinButton (menuMinNoteCount, int.MaxValue, 1);
			menuMaxNoteCountSpinner.Value = menuMaxNoteCount;
			menuMaxNoteCountSpinner.Show ();
			table.Attach (menuMaxNoteCountSpinner, 1, 2, 1, 2);
			menuMaxNoteCountSpinner.ValueChanged += UpdateMenuMaxNoteCountPreference;
		}

		// This one is an event handler for a SpinButton, used to set the menu Min note count
		private void UpdateMenuMinNoteCountPreference (object source, EventArgs args)
		{
			Gtk.SpinButton spinner = source as SpinButton;
			Preferences.Set (Preferences.MENU_NOTE_COUNT, spinner.ValueAsInt);
			// We need to update lower limit for menuMaxNoteCountSpinner in view of this change
			double min, max;
			menuMaxNoteCountSpinner.GetRange(out min, out max);
			menuMaxNoteCountSpinner.SetRange(spinner.Value, max);
		}

		// This one is an event handler for a SpinButton, used to set the menu Max note count
		private void UpdateMenuMaxNoteCountPreference (object source, EventArgs args)
		{
			Gtk.SpinButton spinner = source as SpinButton;
			Preferences.Set (Preferences.MENU_MAX_NOTE_COUNT, spinner.ValueAsInt);
			// We need to update upper limit for menuMinNoteCountSpinner in view of this change
			double min, max;
			menuMinNoteCountSpinner.GetRange(out min, out max);
			menuMinNoteCountSpinner.SetRange(min, spinner.Value);
		}

		public Gtk.Table Widget
		{
			get
			{
				return table;
			}
		}
	}
}