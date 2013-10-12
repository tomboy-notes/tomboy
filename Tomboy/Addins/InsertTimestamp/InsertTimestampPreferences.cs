//
// InsertTimestampPreferences.cs: Preferences dialog for InsertTimestamp addin.
// Allows configuration of timestamp format.
//

using System;
using System.Collections.Generic;

using Mono.Unix;

using Tomboy;

namespace Tomboy.InsertTimestamp {
	public class InsertTimestampPreferences : Gtk.VBox {

		static List<string> formats;

		Gtk.RadioButton selected_radio;
		Gtk.RadioButton custom_radio;

		Gtk.ScrolledWindow scroll;
		Gtk.TreeView tv;
		Gtk.ListStore store;
		Gtk.Entry custom_entry;

		static InsertTimestampPreferences ()
		{
			String defaultFormat = Catalog.GetString (
				"dddd, MMMM d, h:mm tt");

			formats = new List<string> ();
			formats.Add (defaultFormat);
			formats.Add ("MM/dd/yyyy hh:mm tt");
			formats.Add ("dd.MM.yyyy HH:mm");
			formats.Add ("MM/dd/yyyy");
			formats.Add ("MM.dd.yyyy");
			formats.Add ("dd/MM/yyyy");
			formats.Add ("dd.MM.yyyy");
			formats.Add ("hh:mm tt");
			formats.Add ("HH:mm");
			formats.Add ("HH:mm:ss");
		}

		public InsertTimestampPreferences () : base (false, 12)
		{	
			// Get current values
			String dateFormat = (string) Preferences.Get (
				Preferences.INSERT_TIMESTAMP_FORMAT);

			DateTime now = DateTime.Now;

			// Label
			Gtk.Label label = new Gtk.Label (Catalog.GetString (
				"Choose one of the predefined formats " +
				"or use your own."));
			label.Wrap = true;
			label.Xalign = 0;
			PackStart (label, false, false, 0);

			// Use Selected Format
			selected_radio = new Gtk.RadioButton (Catalog.GetString (
				"Use _Selected Format"));
			PackStart (selected_radio, false, false, 0);

			// 1st column (visible): formatted date
			// 2nd column (not visible): date format
			store = new Gtk.ListStore (typeof (string),
				typeof (string));
			foreach (String format in formats)
				store.AppendValues (now.ToString (format), format);

			scroll = new Gtk.ScrolledWindow();
			scroll.ShadowType = Gtk.ShadowType.In;
			PackStart (scroll, false, false, 0);

			tv = new Gtk.TreeView (store);
			tv.HeadersVisible = false;
			tv.AppendColumn ("Format", new Gtk.CellRendererText (),
				"text", 0);
			scroll.Add (tv);

			// Use Custom Format
			Gtk.HBox customBox = new Gtk.HBox (false, 12);
			PackStart (customBox, false, false, 0);

			custom_radio = new Gtk.RadioButton (
				selected_radio, Catalog.GetString ("_Use Custom Format"));
			customBox.PackStart (custom_radio, false, false, 0);

			custom_entry = new Gtk.Entry ();
			customBox.PackStart (custom_entry, false, false, 0);

			IPropertyEditor entryEditor = Services.Factory.CreatePropertyEditorEntry (
				Preferences.INSERT_TIMESTAMP_FORMAT, custom_entry);
			entryEditor.Setup ();

			// Activate/deactivate widgets
			bool useCustom = true;
			Gtk.TreeIter iter;
			store.GetIterFirst (out iter);

			foreach (object[] row in store) {
				if (dateFormat.Equals (row[1])) {
					// Found format in list
					useCustom = false;
					break;
				}	
				store.IterNext (ref iter);
			}

			if (useCustom) {
				custom_radio.Active = true;
				scroll.Sensitive = false;
			} else {
				selected_radio.Active = true;
				custom_entry.Sensitive = false;
				tv.Selection.SelectIter (iter);
				Gtk.TreePath path = store.GetPath (iter);				
				tv.ScrollToCell (path, null, false, 0, 0);
			}

			// Register Toggled event for one radio button only
			selected_radio.Toggled += OnSelectedRadioToggled;
			tv.Selection.Changed += OnSelectionChanged;

			ShowAll ();
		}

		/// <summary>
		/// Called when toggling between radio buttons.
		/// Activate/deactivat widgets depending on selection.
		/// </summary>
		void OnSelectedRadioToggled (object sender, EventArgs args)
		{
			if (selected_radio.Active) {
				scroll.Sensitive = true;
				custom_entry.Sensitive = false;
				// select 1st row
				Gtk.TreeIter iter;
				store.GetIterFirst (out iter);
				tv.Selection.SelectIter (iter);
				Gtk.TreePath path = store.GetPath (iter);				
				tv.ScrollToCell (path, null, false, 0, 0);
			} else {
				scroll.Sensitive = false;
				custom_entry.Sensitive = true;
				tv.Selection.UnselectAll ();
			}
		}

		/// <summary>
		/// Called when a different format is selected in the TreeView.
		/// Set the GConf key to selected format.
		/// </summary>
		void OnSelectionChanged (object sender, EventArgs args)
		{
			Gtk.ITreeModel model;
			Gtk.TreeIter iter;

			if (((Gtk.TreeSelection) sender).GetSelected (out model, 
				out iter)) {
				string format = (string) model.GetValue (iter, 1);
				Preferences.Set (Preferences.INSERT_TIMESTAMP_FORMAT,
					format);
			}
		}
	}
}
