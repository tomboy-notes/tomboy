
using System;
using Tomboy;
using Mono.Posix;

public class PrintPlugin : NotePlugin
{
	Gtk.Widget toolbar_item;

	protected override void Initialize ()
	{
		// Do nothing.
	}

	public override void Dispose ()
	{
		if (toolbar_item != null) {
			Window.Toolbar.Remove (toolbar_item);
			toolbar_item = null;
		}
	}

	protected override void OnNoteOpened () {
		toolbar_item = 
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
