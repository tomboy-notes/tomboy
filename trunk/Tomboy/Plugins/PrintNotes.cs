
using System;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gtk;

using Tomboy;

[PluginInfo(
	"Print Plugin", Defines.VERSION,
	PluginInfoAttribute.OFFICIAL_AUTHOR,
	"Allows you to print a note.",
	WebSite = "http://www.gnome.org/projects/tomboy/"
	)]
public class PrintPlugin : NotePlugin
{
	Gtk.ImageMenuItem item;

	protected override void Initialize ()
	{
		item = new Gtk.ImageMenuItem (Catalog.GetString ("Print"));
		item.Image = new Gtk.Image (Gtk.Stock.Print, Gtk.IconSize.Menu);
		item.Activated += PrintButtonClicked;
		item.Show ();
		AddPluginMenuItem (item);
	}

	protected override void Shutdown ()
	{
		// Disconnect the event handlers so
		// there aren't any memory leaks.
		item.Activated -= PrintButtonClicked;
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
