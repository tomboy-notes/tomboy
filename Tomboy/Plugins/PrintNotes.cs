
using System;
using Tomboy;
using Mono.Posix;

public class PrintPlugin : NotePlugin
{
	protected override void Initialize ()
	{
		// Do nothing.
	}

	protected override void OnNoteOpened () {
		Window.Toolbar.AppendItem (Catalog.GetString ("Print"), 
					   Catalog.GetString ("Print this note"), 
					   null, 
					   new Gtk.Image (Gtk.Stock.Print, 
							  Gtk.IconSize.LargeToolbar),
					   new Gtk.SignalFunc (PrintButtonClicked));
	}

	void PrintButtonClicked ()
	{
		Console.WriteLine ("Printing Note '{0}'...", Note.Title);
	}
}
