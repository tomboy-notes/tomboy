// Add an Underline item to the font styles menu.
// (C) 2009 Mark Wakim <markwakim@gmail.com>, LGPL 2.1 or later.

using Mono.Unix;
using System;
using Gtk;
using Tomboy;

namespace Tomboy.Underline
{
	public class UnderlineNoteAddin : NoteAddin
	{
		TextTag tag;

		public override void Initialize ()
		{
			// Only install Underline addin if it does not currently exist
			if (Note.TagTable.Lookup ("underline") == null) {
				tag = new UnderlineTag ();
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
			AddTextMenuItem (new UnderlineMenuItem (this));
		}
	}
}
