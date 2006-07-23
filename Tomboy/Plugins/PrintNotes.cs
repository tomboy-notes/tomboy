
using System;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gtk;

using Tomboy;

public class PrintPlugin : NotePlugin
{
	protected override void Initialize ()
	{
		Gtk.ImageMenuItem item = 
			new Gtk.ImageMenuItem (Catalog.GetString ("Print"));
		item.Image = new Gtk.Image (Gtk.Stock.Print, Gtk.IconSize.Menu);
		item.Activated += PrintButtonClicked;
		item.Show ();
		AddPluginMenuItem (item);
	}

	protected override void Shutdown ()
	{
		// Do nothing.
	}

	protected override void OnNoteOpened () 
	{
		// Do nothing.
	}

	[DllImport("libtomboy")]
	static extern void gedit_print (IntPtr text_view_handle);

	//
	// Handle Print menu item Click
	//

	void PrintButtonClicked (object sender, EventArgs args)
	{
		gedit_print (Note.Window.Editor.Handle);
	}
}
