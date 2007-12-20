using System;
using System.Collections.Generic;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookMenuItem : Gtk.RadioMenuItem, IComparable<NotebookMenuItem>
	{
		Note note;
		Notebook notebook;

		public NotebookMenuItem (Note note, Notebook notebook) :
			base (notebook == null ? Catalog.GetString ("No notebook") : notebook.Name)
		{
			this.note = note;
			this.notebook = notebook;
			
			if (notebook == null) {
				// This is for the "No notebook" menu item
				
				// Check to see if the specified note belongs
				// to a notebook.  If so, don't activate the
				// radio button.
				if (NotebookManager.GetNotebookFromNote (note) == null)
					Active = true;
			} else if (notebook.ContainsNote (note)) {
				Active = true;
			}
			
			Activated += OnActivated;
		}
		
		protected void OnActivated (object sender, EventArgs args)
		{
			if (note == null)
				return;
			
			// TODO: In the future we may want to allow notes
			// to exist in multiple notebooks.  For now, to
			// alleviate the confusion, only allow a note to
			// exist in one notebook at a time.
			List<Tag> tagsToRemove = new List<Tag> ();
			foreach (Tag tag in note.Tags) {
				if (tag.NormalizedName.StartsWith (Tag.SYSTEM_TAG_PREFIX + Notebooks.Notebook.NotebookTagPrefix))
					tagsToRemove.Add (tag);
			}
			
			foreach (Tag tag in tagsToRemove) {
				note.RemoveTag (tag);
			}
			
			// Only attempt to add the notebook tag when this
			// menu item is not the "No notebook" menu item.
			if (notebook != null) {
				note.AddTag (notebook.Tag);
			}
		}

		public Note Note
		{
			get { return note; }
		}
		
		public Notebook Notebook
		{
			get { return notebook; }
		}

		// IComparable interface
		public int CompareTo (NotebookMenuItem other)
		{
			// Always have the "No notebook" item at the top
			// of the list if it ever gets compared to another
			// item.
			if (notebook == null)
				return -1;
			
			return notebook.Name.CompareTo (other.Notebook.Name);
		}
	}
}
