// Plugin for removing broken links.
// (c) 2009 Alex Tereschenko <frozenblue@zoho.com>
// LGPL 2.1 or later.


using System;
using System.Text.RegularExpressions;
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
			
			NoteTag broken_link_tag = Note.TagTable.BrokenLinkTag;
			Gtk.TextIter note_start, note_end;
			
			// We get the whole note as a range
			// and then just remove the "broken link" tag from it
			Note.Buffer.GetBounds (out note_start, out note_end);
			
			// Sweep 'em & recreate WikiWord broken links (depending on Preferences),
			Buffer.RemoveTag (broken_link_tag,note_start,note_end);
		
			// HACK: The below is copied from Watchers.cs->ApplyWikiwordToBlock()
			// It turns WikiWords back into broken links after sweeping all broken links,
			// but only in case WikiWords are enabled.
			// Most probably there's more elegant way of doing this.
			
			if ((bool) Preferences.Get (Preferences.ENABLE_WIKIWORDS)) {
			
				const string WIKIWORD_REGEX = @"\b((\p{Lu}+[\p{Ll}0-9]+){2}([\p{Lu}\p{Ll}0-9])*)\b";

				Regex regex = new Regex (WIKIWORD_REGEX, RegexOptions.Compiled);
			
				NoteBuffer.GetBlockExtents (ref note_start,
				                            ref note_end,
				                            80 /* max wiki name */,
				                            broken_link_tag);

				//Buffer.RemoveTag (broken_link_tag, start, end);
	
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
	
					if (Manager.Find (group.ToString ()) == null) {
						Buffer.ApplyTag (broken_link_tag, start_cpy, note_end);
					}
				}
			}
			/// End of hack 
		}
		                                      
	}
		
}