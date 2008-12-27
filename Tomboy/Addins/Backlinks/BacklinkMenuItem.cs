
using System;
using Tomboy;

namespace Tomboy.Backlinks
{
	public class BacklinkMenuItem : Gtk.ImageMenuItem, System.IComparable
	{
		Note note;
		string title_search;

		static Gdk.Pixbuf note_icon;

		static Gdk.Pixbuf NoteIcon
		{
			get {
				if (note_icon == null)
					note_icon = GuiUtils.GetIcon ("note", 16);
				return note_icon;
			}
		}

		public BacklinkMenuItem (Note note, string title_search) :
			base (note.Title)
		{
			this.note = note;
			this.title_search = title_search;
			this.Image = new Gtk.Image (NoteIcon);
		}

		protected override void OnActivated ()
		{
			if (note == null)
				return;

			// Show the title of the note
			// where the user just came from.
			NoteFindBar find = note.Window.Find;
			find.ShowAll ();
			find.Visible = true;
			find.SearchText = title_search;

			note.Window.Present ();
		}

		public Note Note
		{
			get {
				return note;
			}
		}

		// IComparable interface
		public int CompareTo (object obj)
		{
			BacklinkMenuItem other_item = obj as BacklinkMenuItem;
			return note.Title.CompareTo (other_item.Note.Title);
		}
	}
}
