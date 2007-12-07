// Add a 'fixed width' item to the font styles menu.
// (C) 2006 Ryan Lortie <desrt@desrt.ca>, LGPL 2.1 or later.
// vim:set sw=8 noet:

using Mono.Unix;
using System;
using Gtk;

using Tomboy;

namespace Tomboy.FixedWidth
{
	public class FixedWidthNoteAddin : NoteAddin
	{
		TextTag tag;

		public override void Initialize ()
		{
			// If a tag of this name already exists, don't install.
			if (Note.TagTable.Lookup ("monospace") == null) {
				tag = new FixedWidthTag ();
				Note.TagTable.Add (tag);
			}
		}

		public override void Shutdown ()
		{
			// Remove the tag only if we installed it.
			if (tag != null)
				Note.TagTable.Remove (tag);
		}

		public override void OnNoteOpened ()
		{
			// Add here instead of in Initialize to avoid creating unopened
			// notes' windows/buffers.
			AddTextMenuItem (new FixedWidthMenuItem (this));
		}
	}
}
