
using System;
using Tomboy;
using Mono.Posix;

public class ExportToHTMLPlugin : NotePlugin
{
	Gtk.Widget toolbar_item;

	protected override void Initialize ()
	{
		// Do nothing.
	}

	protected override void Shutdown ()
	{
		if (toolbar_item != null) {
			Window.Toolbar.Remove (toolbar_item);
			toolbar_item = null;
		}
	}

	protected override void OnNoteOpened () {
		toolbar_item = 
			Window.Toolbar.AppendItem (Catalog.GetString ("Export"), 
						   Catalog.GetString ("Export this note to HTML"), 
						   null, 
						   new Gtk.Image (Gtk.Stock.SaveAs, 
								  Gtk.IconSize.LargeToolbar),
						   new Gtk.SignalFunc (ExportButtonClicked));
	}

	void ExportButtonClicked ()
	{
		Console.WriteLine ("Exporting Note '{0}'...", Note.Title);
	}
}
