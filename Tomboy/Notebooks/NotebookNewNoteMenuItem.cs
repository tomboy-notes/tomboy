using System;
using System.Collections.Generic;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookNewNoteMenuItem : Gtk.ImageMenuItem, IComparable<NotebookMenuItem>
	{
		Notebook notebook;
		
		// TODO: Uncomment this code when we have an
		// icon for creating a new note inside of a notebook
		//static Gdk.Pixbuf noteIcon;
		//
		//static NotebookNewNoteMenuItem ()
		//{
		//	noteIcon = GuiUtils.GetIcon ("note", 22);
		//}
		
		public NotebookNewNoteMenuItem(Notebook notebook)
			: base (
				// Translators should preserve the "{0}" in the following
				// string.  After being formatted for a notebook named,
				// "Meetings", for example, the resultant string would be:
				//		New "Meetings" Note
				String.Format (Catalog.GetString ("New \"{0}\" Note"),
							   notebook.Name))
		{
			this.notebook = notebook;
			this.Image = new Gtk.Image (GuiUtils.GetIcon ("note-new", 16));
			//this.Image = new Gtk.Image (Gtk.Stock.New, Gtk.IconSize.Menu);
			
			Activated += OnActivated;
		}

		protected void OnActivated (object sender, EventArgs args)
		{
			if (notebook == null)
				return;
			
			// Look for the template note and create a new note
			Note templateNote = notebook.GetTemplateNote ();
			Note note;
			
			NoteManager noteManager = Tomboy.DefaultNoteManager;
			note = noteManager.Create ();
			if (templateNote != null) {
				// Use the body from the template note
				string xmlContent = templateNote.XmlContent.Replace (XmlEncoder.Encode (templateNote.Title),
					XmlEncoder.Encode (note.Title));
				xmlContent = NoteManager.SanitizeXmlContent (xmlContent);
				note.XmlContent = xmlContent;
			}
			
			note.AddTag (notebook.Tag);
			note.Window.Show ();
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
