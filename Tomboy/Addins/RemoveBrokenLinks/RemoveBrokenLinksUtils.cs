// Plugin for removing broken links (util functions).
// (c) 2010 Alex Tereschenko <frozenblue@zoho.com>
// LGPL 2.1 or later.


using System;
using System.Text.RegularExpressions;
using Gtk;
using Mono.Unix;
using Tomboy;


namespace Tomboy.RemoveBrokenLinks
{
	/// <summary>
	/// Dummy class containing util functions used by App and Note RemoveBrokenLinks addins
	/// </summary>
	public class RemoveBrokenLinksUtils
	{
		
		public void RemoveBrokenLinkTag (Note note)
		{
			
			NoteTag broken_link_tag = note.TagTable.BrokenLinkTag;
			Gtk.TextIter note_start, note_end;
			
			// We get the whole note as a range
			// and then just remove the "broken link" tag from it
			note.Buffer.GetBounds (out note_start, out note_end);
			
			// Sweep 'em
			note.Buffer.RemoveTag (broken_link_tag,note_start,note_end);

			Logger.Debug ("Removed broken links from a note: " + note.Title);
		}
		
		public void HighlightWikiWords (Note note)
		{
			NoteTag broken_link_tag = note.TagTable.BrokenLinkTag;
			Gtk.TextIter note_start, note_end;
			
			note.Buffer.GetBounds (out note_start, out note_end);
			
			// HACK: The below is copied from Watchers.cs->ApplyWikiwordToBlock()
			// It turns WikiWords back into broken links after sweeping all broken links,
			// but only in case WikiWords are enabled.
			// Most probably there's more elegant way of doing this.
			
			const string WIKIWORD_REGEX = @"\b((\p{Lu}+[\p{Ll}0-9]+){2}([\p{Lu}\p{Ll}0-9])*)\b";

			Regex regex = new Regex (WIKIWORD_REGEX, RegexOptions.Compiled);
			
			NoteBuffer.GetBlockExtents (ref note_start,
			                            ref note_end,
			                            80 /* max wiki name */,
			                            broken_link_tag);
			
			for (Match match = regex.Match (note_start.GetText (note_end));
			     match.Success;
			     match = match.NextMatch ()) {
				System.Text.RegularExpressions.Group group = match.Groups [1];
				
				Logger.Debug ("Highlighting back wikiword: '{0}' at offset {1}",
				              group,
				              group.Index);
				
				Gtk.TextIter start_cpy = note_start;
				start_cpy.ForwardChars (group.Index);
				
				note_end = start_cpy;
				note_end.ForwardChars (group.Length);
				
				if (note.Manager.Find (group.ToString ()) == null) {
					note.Buffer.ApplyTag (broken_link_tag, start_cpy, note_end);
				}
			}
			/// End of hack
		}
		
		
	}
		
}
