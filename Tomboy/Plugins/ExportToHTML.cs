
using System;
using Tomboy;
using Mono.Posix;

public class ExportToHTMLPlugin : NotePlugin
{
	public override void Initialize ()
	{
		Note.Opened += OnNoteOpened;
	}

	void OnNoteOpened (object sender, EventArgs args) {
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
