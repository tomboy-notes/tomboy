// Add a 'fixed width' item to the font styles menu.
// © 2006 Ryan Lortie <desrt@desrt.ca>, LGPL 2.1 or later.
// vim:set sw=8 noet:

using Mono.Unix;
using System;
using Gtk;

using Tomboy;

class FixedWidthTag : NoteTag
{
	public FixedWidthTag () : base ("monospace") {}

	public override void Initialize (string element_name)
	{
		base.Initialize (element_name);
		Family = "monospace";
	}
}

class FixedWidthMenuItem : CheckMenuItem
{
	NotePlugin Plugin;
	bool event_freeze;

	public FixedWidthMenuItem (NotePlugin plugin) : base (
				"<span font_family='monospace'>" +
				Catalog.GetString ("_Fixed Width") +
				"</span>")
	{
		((Label) Child).UseUnderline = true;
		((Label) Child).UseMarkup = true;

		Plugin = plugin;
		Plugin.Window.TextMenu.Shown += MenuShown;

		ShowAll();
	}

	~FixedWidthMenuItem ()
	{
		Plugin.Window.TextMenu.Shown -= MenuShown;
	}

	protected void MenuShown (object sender, EventArgs e)
	{
		event_freeze = true;
		Active = Plugin.Buffer.IsActiveTag ("monospace");
		event_freeze = false;
	}

	protected override void OnActivated ()
	{
		if (!event_freeze)
			Plugin.Buffer.ToggleActiveTag ("monospace");

		base.OnActivated();
	}
}

public class FixedWidthPlugin : NotePlugin
{
	Widget item;
	TextTag tag;

	protected override void Initialize ()
	{
		// if a tag of this name already exists, don't install
		if (Note.TagTable.Lookup ("monospace") != null)
			return;

		tag = new FixedWidthTag ();
		Note.TagTable.Add (tag);
	}

	protected override void Shutdown ()
	{
		if (item != null)
			Window.TextMenu.Remove (item);

		// remove the tag only if we installed it
		if (tag != null)
			Note.TagTable.Remove (tag);
	}

	protected override void OnNoteOpened () 
	{
		item = new FixedWidthMenuItem (this);
		Window.TextMenu.Add (item);
		Window.TextMenu.ReorderChild (item, 7);
	}
}
