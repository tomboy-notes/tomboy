
using System;
using System.Collections;

namespace Tomboy
{
	public class TomboyTray 
	{
		NoteManager manager;
		Egg.TrayIcon icon;

		static Gdk.Pixbuf tintin;
		static Gdk.Pixbuf stock_notes;

		static TomboyTray ()
		{
			tintin = GuiUtils.GetMiniIcon ("tintin.png");
			stock_notes = GuiUtils.GetMiniIcon ("stock_notes.png");
		}

		public TomboyTray (NoteManager manager) 
		{
			this.manager = manager;

			Gtk.EventBox ev = new Gtk.EventBox ();
			ev.CanFocus = true;
			ev.ButtonPressEvent += new Gtk.ButtonPressEventHandler (ButtonPress);
			ev.Add (new Gtk.Image (tintin));

			icon = new Egg.TrayIcon ("Tomboy");
			icon.Add (ev);
			icon.ShowAll ();
		}

		void ButtonPress (object sender, Gtk.ButtonPressEventArgs args) 
		{
			Gtk.Widget parent = (Gtk.Widget) sender;
			Gtk.Menu recent_menu = MakeRecentNotesMenu (parent);
			GuiUtils.PopupMenu (recent_menu, args.Event);
		}

		Gtk.Menu MakeRecentNotesMenu (Gtk.Widget parent) 
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AttachToWidget (parent, null);

			Gtk.ImageMenuItem item = new Gtk.ImageMenuItem ("Create _New Note");
			item.Image = new Gtk.Image (Gtk.Stock.New, Gtk.IconSize.Menu);
			item.Activated += new EventHandler (AddNote);
			menu.Append (item);

			int i = 5; // Number of recent entries to list

			foreach (Note note in manager.Notes) {
				if (note.IsSpecial)
					continue;

				if (i-- == 0)
					break;

				item = MakeNoteMenuItem (note);
				menu.Append (item);
			}

			Note start = manager.Find ("Start Here");
			if (start != null) {
				item = MakeNoteMenuItem (start);
				menu.Append (item);
			}

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem ("_Recent Changes");
			item.Image = new Gtk.Image (Gtk.Stock.SortAscending, Gtk.IconSize.Menu);
			item.Activated += new EventHandler (ViewRecentChanges);
			menu.Append (item);

			item = new Gtk.ImageMenuItem ("_Search Notes...");
			item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			item.Activated += new EventHandler (SearchNotes);
			menu.Append (item);

			menu.Append (new Gtk.SeparatorMenuItem ());

			item = new Gtk.ImageMenuItem ("_Quit");
			item.Image = new Gtk.Image (Gtk.Stock.Quit, Gtk.IconSize.Menu);
			item.Activated += new EventHandler (Quit);
			menu.Append (item);

			menu.ShowAll ();
			return menu;
		}

		Gtk.ImageMenuItem MakeNoteMenuItem (Note note)
		{
			string display_name = note.Title;
			if (note.IsNew)
				display_name += " (new)";

			Gtk.ImageMenuItem item = new Gtk.ImageMenuItem (display_name);
			item.Image = new Gtk.Image (stock_notes);
			item.Data ["Note"] = note;
			item.Activated += new EventHandler (ShowNote);

			return item;
		}

		void ShowNote (object sender, EventArgs args) 
		{
			Note note = (Note) ((Gtk.Widget) sender).Data ["Note"];
			if (note != null)
				note.Window.Present ();
		}

		void AddNote (object sender, EventArgs args) 
		{
			int new_num = manager.Notes.Count;
			string temp_title;

			while (true) {
				temp_title = String.Format ("New Note {0}", new_num);
				if (manager.Find (temp_title) != null)
					new_num++;
				else
					break;
			}
					
			Note new_note = manager.Create (temp_title);
			new_note.Window.Show ();
		}

		void SearchNotes (object sender, EventArgs args) 
		{
			NoteFindDialog find_dialog = NoteFindDialog.GetInstance (manager);
			find_dialog.Present ();
		}

		void ViewRecentChanges (object sender, EventArgs args)
		{
			Gtk.Window recent = new NoteRecentChanges (manager);
			recent.Show ();
		}

		void Quit (object sender, EventArgs args)
		{
			Console.WriteLine ("Quitting Tomboy.  Ciao!");
			Environment.Exit (0);
		}

		// FIXME: If receiving a drag, pop up last window used, or a new
		//        window, or the recent list?  I think recent list
	}
}
