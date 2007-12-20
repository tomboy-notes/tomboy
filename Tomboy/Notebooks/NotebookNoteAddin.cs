using System;
using System.Collections.Generic;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookNoteAddin : NoteAddin
	{
		Gtk.ImageMenuItem menuItem;
		Gtk.Menu menu;
		bool submenuBuilt;

		public override void Initialize ()
		{
			submenuBuilt = false;

			menu = new Gtk.Menu ();
			menu.Hidden += OnMenuHidden;
			menu.ShowAll ();
			// Note to translators.  "Place" in the following string is
			// the verb.  When a user opens the submenu here, it will
			// open a list of notebooks they can place the current note
			// into.
			menuItem = new Gtk.ImageMenuItem (
			        Catalog.GetString ("Place in notebook"));
			menuItem.Image = new Gtk.Image (Gtk.Stock.Paste, Gtk.IconSize.Menu);
			menuItem.Submenu = menu;
			menuItem.Activated += OnMenuItemActivated;
			menuItem.Show ();
			AddPluginMenuItem (menuItem);
		}

		public override void Shutdown ()
		{
			// Disconnect the event handlers so
			// there aren't any memory leaks.
			menu.Hidden -= OnMenuHidden;
			menuItem.Activated -= OnMenuItemActivated;
		}

		public override void OnNoteOpened ()
		{
			// Do nothing.
		}

		void OnMenuItemActivated (object sender, EventArgs args)
		{
			if (submenuBuilt == true)
				return; // submenu already built.  do nothing.

			UpdateMenu ();
		}

		void OnMenuHidden (object sender, EventArgs args)
		{
			// FIXME: Figure out how to have this function be called only when
			// the whole Tools menu is collapsed so that if a user keeps
			// toggling over the menu item, it doesn't
			// keep forcing the submenu to rebuild.

			// Force the submenu to rebuild next time it's supposed to show
			submenuBuilt = false;
		}

		void UpdateMenu ()
		{
			//
			// Clear out the old list
			//
			foreach (Gtk.MenuItem oldItem in menu.Children) {
				menu.Remove (oldItem);
			}

			//
			// Build a new menu
			//
			
			// Add the "(no notebook)" item at the top of the list
			NotebookMenuItem noNotebookMenuItem = new NotebookMenuItem (Note, null);
			noNotebookMenuItem.ShowAll ();
			menu.Append (noNotebookMenuItem);
			
			// Add in all the real notebooks
			List<NotebookMenuItem> notebookMenuItems = GetNotebookMenuItems ();
			if (notebookMenuItems.Count > 0) {
				Gtk.SeparatorMenuItem separator = new Gtk.SeparatorMenuItem ();
				separator.ShowAll ();
				menu.Append (separator);
				
				foreach (NotebookMenuItem item in GetNotebookMenuItems ()) {
					item.ShowAll ();
					menu.Append (item);
				}
			}

			submenuBuilt = true;
		}
		
		List<NotebookMenuItem> GetNotebookMenuItems ()
		{
			List<NotebookMenuItem> items = new List<NotebookMenuItem> ();
			
			Gtk.TreeModel model = NotebookManager.Notebooks;
			Gtk.TreeIter iter;
			
			if (model.GetIterFirst (out iter) == true) {
				do {
					Notebook notebook = model.GetValue (iter, 0) as Notebook;
					NotebookMenuItem item = new NotebookMenuItem (Note, notebook);
					items.Add (item);
				} while (model.IterNext (ref iter) == true);
			}
			
			items.Sort ();
			
			return items;
		}
	}
}
