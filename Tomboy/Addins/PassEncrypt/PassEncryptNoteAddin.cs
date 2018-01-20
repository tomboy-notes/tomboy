// Add an PassEncrypt item to the font styles menu.
// (C) 2009 Mark Wakim <markwakim@gmail.com>, LGPL 2.1 or later.

using Mono.Unix;
using System;
using Gtk;
using Tomboy;

namespace Tomboy.PassEncrypt
{
	public class PassEncryptNoteAddin : NoteAddin
	{

		public override void Initialize ()
		{
            if (!Note.TagTable.IsDynamicTagRegistered(PassEncryptTag.TagName))
            {
                Note.TagTable.RegisterDynamicTag(PassEncryptTag.TagName, typeof(PassEncryptTag));
            }
            //Note.TagTable.RegisterDynamicTag(PassEncryptTag.TagName, typeof(PassEncryptTag));
        }

		public override void Shutdown ()
		{
		}

		public override void OnNoteOpened ()
		{
			// Add here instead of in Initialize to avoid creating unopened
			// notes' windows/buffers.
			AddTextMenuItem (new PassEncryptMenuItem (this));
            // get all note xml and search for passencrypt tags
            string xml = Note.TextContent;
            
		}
	}
}
