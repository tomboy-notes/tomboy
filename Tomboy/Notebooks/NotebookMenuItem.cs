using System;
using System.Collections.Generic;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookMenuItem : Gtk.RadioMenuItem, IComparable<NotebookMenuItem>
	{
		Note note;
		Notebook notebook;

		public NotebookMenuItem (Note note, Notebook notebook) : base (notebook.Name)
		{
			this.note = note;
			this.notebook = notebook;
			
			if (notebook.ContainsNote (note))
				Active = true;
			
			Activated += OnActivated;
		}
		
		protected void OnActivated (object sender, EventArgs args)
		{
			if (note == null || notebook == null)
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
			
			note.AddTag (notebook.Tag);
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
			return notebook.Name.CompareTo (other.Notebook.Name);
		}
	}
}
