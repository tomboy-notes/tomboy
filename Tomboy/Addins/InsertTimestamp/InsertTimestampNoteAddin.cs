//
// InsertTimestampNoteAddin.cs: Inserts a timestamp at the cursor position.
//

using System;

using Mono.Unix;

namespace Tomboy.InsertTimestamp {
	public class InsertTimestampNoteAddin : NoteAddin {
		
		Gtk.MenuItem item;
		
		public override void Initialize ()
		{
			item = new Gtk.MenuItem (
					Catalog.GetString ("Insert Timestamp"));
			item.Activated += OnMenuItemActivated;
			item.Show ();
			AddPluginMenuItem (item);
		}
		
		public override void Shutdown ()
		{
			item.Activated -= OnMenuItemActivated;
		}
		
		public override void OnNoteOpened ()
		{
		}
		
		void OnMenuItemActivated (object sender, EventArgs args)
		{
			string format = Catalog.GetString ("dddd, MMMM d, h:mm tt");
			string text = DateTime.Now.ToString (format);
			
			Gtk.TextIter cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
			Buffer.InsertWithTagsByName (ref cursor, text, "datetime");
		}
	}
}
