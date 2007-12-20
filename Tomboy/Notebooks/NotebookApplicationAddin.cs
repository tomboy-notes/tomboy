using System;
using System.Collections.Generic;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookApplicationAddin : ApplicationAddin
	{
		static Gtk.ActionGroup actionGroup;
		static uint notebookUi = 0;

		bool initialized;

		Gtk.Menu trayNotebookMenu;
		Gtk.Menu mainWindowNotebookMenu;
		
		public NotebookApplicationAddin ()
		{
			initialized = false;
		}

		public override void Initialize ()
		{
			actionGroup = new Gtk.ActionGroup ("Notebooks");
			actionGroup.Add (new Gtk.ActionEntry [] {
				new Gtk.ActionEntry ("NewNotebookMenuAction", Gtk.Stock.New,
					Catalog.GetString ("New Note_book Note"), null,
					Catalog.GetString ("Create a new note in a notebook"), null),
					
				new Gtk.ActionEntry ("TrayNewNotebookMenuAction", Gtk.Stock.New,
					Catalog.GetString ("New Notebook Note"), null,
					Catalog.GetString ("Create a new note in a notebook"), null)
			});
			
			notebookUi = Tomboy.ActionManager.UI.AddUiFromString (@"
					<ui>
						<menubar name='MainWindowMenubar'>
							<menu name='FileMenu' action='FileMenuAction'>
								<placeholder name='FileMenuNewNotePlaceholder'>
									<menuitem name='NewNotebookMenu' action='NewNotebookMenuAction' />
								</placeholder>
							</menu>
						</menubar>
						<popup name='TrayIconMenu' action='TrayIconMenuAction'>
							<placeholder name='TrayNewNotePlaceholder'>
								<menuitem name='TrayNewNotebookMenu' action='TrayNewNotebookMenuAction' position='top' />
							</placeholder>
						</popup>
					</ui>
				");
			
			Tomboy.ActionManager.UI.InsertActionGroup (actionGroup, 0);
			
			Gtk.MenuItem item = Tomboy.ActionManager.GetWidget (
				"/TrayIconMenu/TrayNewNotePlaceholder/TrayNewNotebookMenu") as Gtk.MenuItem;
			if (item != null) {
				trayNotebookMenu = new Gtk.Menu ();
				item.Submenu = trayNotebookMenu;
				trayNotebookMenu.Shown += OnTrayNotebookMenuShown;
				trayNotebookMenu.Hidden += OnTrayNotebookMenuHidden;
			}
			
			item = Tomboy.ActionManager.GetWidget (
				"/MainWindowMenubar/FileMenu/FileMenuNewNotePlaceholder/NewNotebookMenu") as Gtk.MenuItem;
			if (item != null) {
				mainWindowNotebookMenu = new Gtk.Menu ();
				item.Submenu = mainWindowNotebookMenu;
				mainWindowNotebookMenu.Shown += OnNewNotebookMenuShown;
				mainWindowNotebookMenu.Hidden += OnNewNotebookMenuHidden;
			}
			
			initialized = true;
		}

		public override void Shutdown ()
		{
			try {
				Tomboy.ActionManager.UI.RemoveActionGroup (actionGroup);
			} catch {}
			try {
				Tomboy.ActionManager.UI.RemoveUi (notebookUi);
			} catch {}
			
			if (trayNotebookMenu != null) {
				trayNotebookMenu.Shown -= OnTrayNotebookMenuShown;
				trayNotebookMenu.Hidden -= OnTrayNotebookMenuHidden;
			}
			
			if (mainWindowNotebookMenu != null) {
				mainWindowNotebookMenu.Shown -= OnNewNotebookMenuShown;
				mainWindowNotebookMenu.Hidden -= OnNewNotebookMenuHidden;
			}

			initialized = false;
		}

		public override bool Initialized
		{
			get {
				return initialized;
			}
		}
		
		private void OnTrayNotebookMenuShown (object sender, EventArgs args)
		{
			AddMenuItems (trayNotebookMenu);
		}

		private void OnTrayNotebookMenuHidden (object sender, EventArgs args)
		{
			RemoveMenuItems (trayNotebookMenu);
		}
		
		private void OnNewNotebookMenuShown (object sender, EventArgs args)
		{
			AddMenuItems (mainWindowNotebookMenu);
		}
		
		private void OnNewNotebookMenuHidden (object sender, EventArgs args)
		{
			RemoveMenuItems (mainWindowNotebookMenu);
		}
		
		private void AddMenuItems (Gtk.Menu menu)
		{
			NotebookNewNoteMenuItem item;

			Gtk.TreeModel model = NotebookManager.Notebooks;
			Gtk.TreeIter iter;
			
			if (model.GetIterFirst (out iter) == true) {
				do {
					Notebook notebook = model.GetValue (iter, 0) as Notebook;
					item = new NotebookNewNoteMenuItem (notebook);
					item.ShowAll ();
					menu.Append (item);
				} while (model.IterNext (ref iter) == true);
			}
		}
		
		private void RemoveMenuItems (Gtk.Menu menu)
		{
			foreach (Gtk.MenuItem child in menu.Children) {
				menu.Remove (child);
			}
		}
	}
}
