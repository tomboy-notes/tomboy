// Plugin for removing broken links (NoteAddin).
// (c) 2010 Alex Tereschenko <frozenblue@zoho.com>
// LGPL 2.1 or later.


using System;
using Gtk;
using Mono.Unix;
using Tomboy;

namespace Tomboy.RemoveBrokenLinks
{
	
	public class RemoveBrokenLinksAddin : NoteAddin
	{
		Gtk.ImageMenuItem item;
		
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
			// Adding menu item when note is opened and window created
			item = new Gtk.ImageMenuItem (Catalog.GetString ("Remove broken links"));
			item.Image = new Gtk.Image (Gtk.Stock.Clear, Gtk.IconSize.Menu);
			item.AddAccelerator ("activate", Window.AccelGroup,
				(uint) Gdk.Key.r, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			item.Activated += OnMenuItemActivated;
			item.Show ();
			AddPluginMenuItem (item);
		}
			

		void OnMenuItemActivated (object sender, EventArgs args)
		{
			RemoveBrokenLinksUtils utils = new RemoveBrokenLinksUtils ();
			
			utils.RemoveBrokenLinkTag (Note);
			if ((bool) Preferences.Get (Preferences.ENABLE_WIKIWORDS))
				utils.HighlightWikiWords (Note);
			
		}
		                                      
	}
		
}