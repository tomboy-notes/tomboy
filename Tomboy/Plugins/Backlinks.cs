
using System;
using System.Collections;

using Mono.Unix;

using Tomboy;


public class BacklinkMenuItem : Gtk.ImageMenuItem, System.IComparable
{
	Note note;
	
	static Gdk.Pixbuf note_icon;
	
	static BacklinkMenuItem ()
	{
		note_icon = GuiUtils.GetIcon ("tomboy-note", 22);
	}

	public BacklinkMenuItem (Note note) : 
			base (note.Title)
	{
		this.note = note;
		this.Image = new Gtk.Image (note_icon);
	}
	
	protected override void OnActivated ()
	{
		if (note == null)
			return;
		
		note.Window.Present ();
	}
	
	public Note Note
	{
		get { return note; }
	}
	
	// IComparable interface
	public int CompareTo (object obj)
	{
		BacklinkMenuItem other_item = obj as BacklinkMenuItem;
		return note.Title.CompareTo (other_item.Note.Title);
	}
}

[PluginInfo(
	Name = "Backlinks Plugin",
	Version = Defines.VERSION,
	Author = "Boyd Timothy <btimothy@gmail.com>",
	Description = 
		"See which notes link to the one you're currently " +
		"viewing.",
	PreferencesWidget = null
	)]

public class BacklinksPlugin : NotePlugin
{
	Gtk.ImageMenuItem menu_item;
	Gtk.Menu menu;
	bool submenu_built;
	
	protected override void Initialize ()
	{
		submenu_built = false;

		menu = new Gtk.Menu ();
		menu.Hidden += OnMenuHidden;
		menu.ShowAll ();
		menu_item = new Gtk.ImageMenuItem (
				Catalog.GetString ("What links here?"));
		menu_item.Image = new Gtk.Image (Gtk.Stock.JumpTo, Gtk.IconSize.Menu);
		menu_item.Submenu = menu;
		menu_item.Activated += OnMenuItemActivated;
		menu_item.Show ();
		AddPluginMenuItem (menu_item);
	}

	protected override void Shutdown ()
	{
		// The following two lines are required to prevent the plugin
		// from leaking references when the plugin is disabled.
		menu.Hidden -= OnMenuHidden;
		menu_item.Activated -= OnMenuItemActivated;
	}

	protected override void OnNoteOpened ()
	{
	}
	
	void OnMenuItemActivated (object sender, EventArgs args)
	{
		if (submenu_built == true)
			return; // submenu already built.  do nothing.
		
		UpdateMenu ();
	}
	
	void OnMenuHidden (object sender, EventArgs args)
	{
		// FIXME: Figure out how to have this function be called only when
		// the whole Tools menu is collapsed so that if a user keeps
		// toggling over the "What links here?" menu item, it doesn't
		// keep forcing the submenu to rebuild.

		// Force the submenu to rebuild next time it's supposed to show
		submenu_built = false;
	}
	
	void UpdateMenu ()
	{
		//
		// Clear out the old list
		//
		foreach (Gtk.MenuItem old_item in menu.Children) {
			menu.Remove (old_item);
		}
		
		//
		// Build a new list
		//
		foreach (BacklinkMenuItem item in GetBacklinkMenuItems ()) {
			item.ShowAll ();
			menu.Append (item);
		}
		
		// If nothing was found, add in a "dummy" item
		if (menu.Children.Length == 0) {
			Gtk.MenuItem blank_item = new Gtk.MenuItem (Catalog.GetString ("(none)"));
			blank_item.Sensitive = false;
			blank_item.ShowAll ();
			menu.Append (blank_item);
		}

		submenu_built = true;
	}
	
	BacklinkMenuItem [] GetBacklinkMenuItems ()
	{
		ArrayList items = new ArrayList ();
		
		string encoded_title = XmlEncoder.Encode (Note.Title.ToLower ());
		
		// Go through each note looking for
		// notes that link to this one.
		foreach (Note note in Note.Manager.Notes) {
			if (note != Note // don't match ourself
						&& CheckNoteHasMatch (note, encoded_title)) {
				BacklinkMenuItem item = new BacklinkMenuItem (note);

				items.Add (item);
			}
		}
		
		items.Sort ();
		
		return items.ToArray (typeof (BacklinkMenuItem)) as BacklinkMenuItem [];
	}
	
	bool CheckNoteHasMatch (Note note, string encoded_title)
	{
		string note_text = note.XmlContent.ToLower ();
		if (note_text.IndexOf (encoded_title) < 0)
			return false;
		
		return true;
	}
	
	private static int CompareNoteTitles (Note a, Note b)
	{
		return a.Title.CompareTo (b.Title);
	}
}
