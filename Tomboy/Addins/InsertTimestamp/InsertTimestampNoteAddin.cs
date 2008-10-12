//
// InsertTimestampNoteAddin.cs: Inserts a timestamp at the cursor position.
//

using System;

using Mono.Unix;

namespace Tomboy.InsertTimestamp {
	public class InsertTimestampNoteAddin : NoteAddin {

		String date_format;
		Gtk.MenuItem item;

		public override void Initialize ()
		{
		}

		public override void Shutdown ()
		{
			if (item != null)
				item.Activated -= OnMenuItemActivated;
		}

		public override void OnNoteOpened ()
		{
			// Add the menu item when the window is created
			item = new Gtk.MenuItem (
				Catalog.GetString ("Insert Timestamp"));
			item.Activated += OnMenuItemActivated;
			item.AddAccelerator ("activate", Window.AccelGroup,
				(uint) Gdk.Key.d, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			item.Show ();
			AddPluginMenuItem (item);

			// Get the format from GConf and subscribe to changes
			date_format = (string) Preferences.Get (
				Preferences.INSERT_TIMESTAMP_FORMAT);
			Preferences.SettingChanged += OnFormatSettingChanged;
		}

		void OnMenuItemActivated (object sender, EventArgs args)
		{
			string text = DateTime.Now.ToString (date_format);
			Gtk.TextIter cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
			Buffer.InsertWithTagsByName (ref cursor, text, "datetime");
		}

		void OnFormatSettingChanged (object sender, NotifyEventArgs args)
		{
			if (args.Key == Preferences.INSERT_TIMESTAMP_FORMAT)
				date_format = (string) args.Value;
		}
	}
}
