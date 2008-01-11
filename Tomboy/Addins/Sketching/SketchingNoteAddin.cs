using System;
using System.Collections;
using VirtualPaper;

using Mono.Unix;

using Tomboy;

namespace Tomboy.Sketching
{
	public class SketchingNoteAddin : NoteAddin
	{
		Gtk.ImageMenuItem menu_item;
		
		public override void Initialize ()
		{
            // Translators: a 'sketch' is a quick drawing
			menu_item = new Gtk.ImageMenuItem (
					Catalog.GetString ("Add a sketch"));
			// FIXME: Use a real Sketching icon instead of the Edit icon
			menu_item.Image = new Gtk.Image (Gtk.Stock.Edit, Gtk.IconSize.Menu);
			menu_item.Activated += OnMenuItemActivated;
			menu_item.Show ();
			AddPluginMenuItem (menu_item);

			if (!Note.TagTable.IsDynamicTagRegistered ("sketch"))
				Note.TagTable.RegisterDynamicTag ("sketch", typeof (SketchingTextTag));
		}

		public override void Shutdown ()
		{
			// The following two lines are required to prevent the plugin
			// from leaking references when the plugin is disabled.
			menu_item.Activated -= OnMenuItemActivated;
		}

		public override void OnNoteOpened ()
		{
		}
		
		void OnMenuItemActivated (object sender, EventArgs args)
		{
			// Insert a new Sketching Widget at the current cursor position
			SketchingTextTag tag = (SketchingTextTag)
				Note.TagTable.CreateDynamicTag ("sketch");

			// FIXME: Create a dynamic name for a sketch file that will
			// be stored in the note's attachments directory.
			tag.Uri = "test";
            Handwriting h = new Handwriting(new Paper(new Cairo.Color(1.0,1.0,1.0,1.0)));
            h.SetSizeRequest(500,500);
            tag.Widget = h;

			// Insert the sketch tag at the current cursor position
			Gtk.TextIter cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
			Gtk.TextTag[] tags = {tag};
			Buffer.InsertWithTags (ref cursor, String.Empty, tags);
		}
	}
}
