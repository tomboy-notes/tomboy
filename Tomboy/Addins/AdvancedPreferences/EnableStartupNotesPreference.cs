// Class for Enable Startup Notes setting of Tomboy Advanced preferences tab
// (c) 2013 Alex Tereschenko <frozen.and.blue@gmail.com>
// LGPL 2.1 or later

using System;
using Gtk;
using Mono.Unix;
using Tomboy;

namespace Tomboy.AdvancedPreferences
{
	/// <summary>
	/// Contains a class for Enable Startup Notes setting for Advanced preferences tab
	/// </summary>
	public class EnableStartupNotesPreference
	{
		// This will store all widgets
		private Gtk.Alignment align;

		public EnableStartupNotesPreference ()
		{
			IPropertyEditorBool enableStartupNotes_peditor;
			Gtk.CheckButton enableStartupNotesCheckbox;
			Gtk.Label enableStartupNotesLabel;

			// Enable Startup Notes option
			enableStartupNotesLabel = new Gtk.Label (Catalog.GetString ("Enable startup notes"));
			enableStartupNotesLabel.UseMarkup = true;
			enableStartupNotesLabel.Justify = Gtk.Justification.Left;
			enableStartupNotesLabel.SetAlignment (0.0f, 0.5f);
			enableStartupNotesLabel.Show ();

			enableStartupNotesCheckbox = new Gtk.CheckButton ();
			enableStartupNotesCheckbox.Add (enableStartupNotesLabel);
			enableStartupNotesCheckbox.Show ();

			enableStartupNotes_peditor =
				Services.Factory.CreatePropertyEditorToggleButton (Preferences.ENABLE_STARTUP_NOTES, enableStartupNotesCheckbox);
			Preferences.Get (enableStartupNotes_peditor.Key);
			enableStartupNotes_peditor.Setup ();

			align = new Gtk.Alignment (0.0f, 0.0f, 0.0f, 1.0f);
			align.Show ();
			align.Add (enableStartupNotesCheckbox);
		}

		public Gtk.Widget Widget
		{
			get
			{
				return align;
			}
		}
	}
}