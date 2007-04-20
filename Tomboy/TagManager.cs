
using System;
using System.Collections.Generic;
using Gtk;

namespace Tomboy
{
	// <summary>
	// The Gtk.TreeIter is valid for the TagManager.Tags (Gtk.ListStore).
	// </summary>
	public delegate void TagAddedEventHandler (Tag tag, Gtk.TreeIter iter);
	public delegate void TagRemovedEventHandler (string tag_name);

	public class TagManager
	{
		static Gtk.ListStore tags;
		static Gtk.TreeModelSort sorted_tags;
		static Dictionary<string, Gtk.TreeIter> tag_map;
		static object locker = new object ();
		
		#region Constructors
		static TagManager ()
		{
			tags = new Gtk.ListStore (typeof (Tag));
			
			sorted_tags = new Gtk.TreeModelSort (tags);
			sorted_tags.SetSortFunc (0, new Gtk.TreeIterCompareFunc (CompareTagsSortFunc));
			sorted_tags.SetSortColumnId (0, Gtk.SortType.Ascending);
			
			// <summary>
			// The key for this dictionary is Tag.Name.ToLower ().
			// </summary>
			tag_map = new Dictionary<string, Gtk.TreeIter> ();
		}
		
		private TagManager ()
		{
		}
		#endregion
		
		#region Private Methods
		static int CompareTagsSortFunc (TreeModel model, TreeIter a, TreeIter b)
		{
			Tag tag_a = model.GetValue (a, 0) as Tag;
			Tag tag_b = model.GetValue (b, 0) as Tag;
			
			if (tag_a == null || tag_b == null)
				return 0;
			
			return string.Compare (tag_a.NormalizedName, tag_b.NormalizedName);
		}
		#endregion
		
		#region Public Static Methods
		// <summary>
		// Return an existing tag for the specified tag name.  If no Tag exists
		// null will be returned.
		// </summary>
		public static Tag GetTag (string tag_name)
		{
			if (tag_name == null)
				throw new ArgumentNullException ("TagManager.GetTag () called with a null tag name.");
			
			string normalized_tag_name = tag_name.Trim ().ToLower ();
			if (normalized_tag_name == String.Empty)
				throw new ArgumentException ("TagManager.GetTag () called with an empty tag name.");
			
			if (tag_map.ContainsKey (normalized_tag_name)) {
				Gtk.TreeIter iter = tag_map [normalized_tag_name];
				return tags.GetValue (iter, 0) as Tag;
			}
			
			return null;
		}
		
		// <summary>
		// Same as GetTag () but will create a new tag if one doesn't already exist.
		// </summary>
		public static Tag GetOrCreateTag (string tag_name)
		{
			if (tag_name == null)
				throw new ArgumentNullException ("TagManager.GetOrCreateTag () called with a null tag name.");
			
			string normalized_tag_name = tag_name.Trim ().ToLower ();
			if (normalized_tag_name == String.Empty)
				throw new ArgumentException ("TagManager.GetOrCreateTag () called with an empty tag name.");
			
			Gtk.TreeIter iter = Gtk.TreeIter.Zero;
			bool tag_added = false;
			Tag tag = GetTag (normalized_tag_name);
			if (tag == null) {
				lock (locker) {
					tag = GetTag (normalized_tag_name);
					if (tag == null) {
						tag = new Tag (tag_name.Trim ());
						iter = tags.Append ();
						tags.SetValue (iter, 0, tag);
						tag_map [tag.NormalizedName] = iter;
						
						tag_added = true;
					}
				}
			}
			
			if (tag_added && TagAdded != null)
				TagAdded (tag, iter);
			
			return tag;
		}
		
		// <summary>
		// This will remove the tag from every note that is currently tagged
		// and from the main list of tags.
		// </summary>
		public static void RemoveTag (Tag tag)
		{
			if (tag == null)
				throw new ArgumentNullException ("TagManager.RemoveTag () called with a null tag");

			bool tag_removed = false;
			if (tag_map.ContainsKey (tag.NormalizedName)) {
				lock (locker) {
					if (tag_map.ContainsKey (tag.NormalizedName)) {
						Gtk.TreeIter iter = tag_map [tag.NormalizedName];
						if (!tags.Remove (ref iter)) {
							Logger.Debug ("TagManager: Removed tag: {0}", tag.NormalizedName);
						} else {
							// FIXME: For some really weird reason, this block actually gets called sometimes!
							Logger.Warn ("TagManager: Call to remove tag from ListStore failed: {0}", tag.NormalizedName);
						}

						tag_map.Remove (tag.NormalizedName);
						Logger.Debug ("Removed TreeIter from tag_map: {0}", tag.NormalizedName);
						tag_removed = true;

						foreach (Note note in tag.Notes) {
							note.RemoveTag (tag);
						}
					}
				}
			}
			
			if (tag_removed && TagRemoved != null) {
				TagRemoved (tag.NormalizedName);
			}
		}
		
		// <summary>
		// </summary>
		#endregion
		
		#region Properties
		public static Gtk.TreeModel Tags
		{
			get { return sorted_tags; }
		}
		#endregion
		
		#region Events
		public static event TagAddedEventHandler TagAdded;
		public static event TagRemovedEventHandler TagRemoved;
		#endregion
	}
}
