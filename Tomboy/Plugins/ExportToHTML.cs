
using System;
using Tomboy;
using Mono.Posix;

public class ExportToHTMLPlugin : NotePlugin
{
	protected override void Initialize ()
	{
		// Do nothing.
	}

	protected override void OnNoteOpened () {
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
