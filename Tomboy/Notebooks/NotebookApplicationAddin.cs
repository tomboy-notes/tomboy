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
					Catalog.GetString ("Note_books"), null,
					Catalog.GetString ("Create a new note in a notebook"), null),
				
				new Gtk.ActionEntry ("NewNotebookAction", Gtk.Stock.New,
				        Catalog.GetString ("New Note_book..."), null,
				        Catalog.GetString ("Create a new notebook"), null),
					
				new Gtk.ActionEntry ("NewNotebookNoteAction", Gtk.Stock.New,
					Catalog.GetString ("_New Note"), null,
					Catalog.GetString ("Create a new note in this notebook"), null),
					
				new Gtk.ActionEntry ("OpenNotebookTemplateNoteAction", Gtk.Stock.Open,
					Catalog.GetString ("_Open Template Note"), null,
					Catalog.GetString ("Open this notebook's template note"), null),
					
				new Gtk.ActionEntry ("DeleteNotebookAction", Gtk.Stock.Delete,
					Catalog.GetString ("Delete Note_book"), null,
					Catalog.GetString ("Delete the selected notebook"), null),
					
				new Gtk.ActionEntry ("TrayNewNotebookMenuAction", Gtk.Stock.New,
					Catalog.GetString ("Notebooks"), null,
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
							<menu name='EditMenu' action='EditMenuAction'>
								<placeholder name='EditMenuDeletePlaceholder'>
								    <menuitem name='DeleteNotebook' action='DeleteNotebookAction' position='bottom'/>
								</placeholder>
							</menu>
						</menubar>
						<popup name='NotebooksTreeContextMenu' action='NotebooksTreeContextMenuAction'>
							<menuitem name='NewNotebookNote' action='NewNotebookNoteAction' />
							<separator />
							<menuitem name='OpenNotebookTemplateNote' action='OpenNotebookTemplateNoteAction' />
							<menuitem name='DeleteNotebook' action='DeleteNotebookAction' />
						</popup>
						<popup name='NotebooksTreeNoRowContextMenu' action='NotebooksTreeNoRowContextMenuAction'>
							<menuitem name='NewNotebook' action='NewNotebookAction' />
						</popup>
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
			RemoveMenuItems (menu);			

			NotebookNewNoteMenuItem item;

			Gtk.TreeModel model = NotebookManager.Notebooks;
			Gtk.TreeIter iter;
			
			// Add in the "New Notebook..." menu item
			Gtk.ImageMenuItem newNotebookMenuItem =
				new Gtk.ImageMenuItem (Catalog.GetString ("New Note_book..."));
			// TODO: Replace this new stock icon with a tomboy-new-notebook icon
			newNotebookMenuItem.Image = new Gtk.Image (Gtk.Stock.New, Gtk.IconSize.Menu);
			newNotebookMenuItem.Activated += OnNewNotebookMenuItem;
			newNotebookMenuItem.ShowAll ();
			menu.Append (newNotebookMenuItem);
			
			if (model.IterNChildren () > 0) {
				Gtk.SeparatorMenuItem separator = new Gtk.SeparatorMenuItem ();
				separator.ShowAll ();
				menu.Append (separator);
				
				if (model.GetIterFirst (out iter) == true) {
					do {
						Notebook notebook = model.GetValue (iter, 0) as Notebook;
						item = new NotebookNewNoteMenuItem (notebook);
						item.ShowAll ();
						menu.Append (item);
					} while (model.IterNext (ref iter) == true);
				}
			}
		}
		
		private void RemoveMenuItems (Gtk.Menu menu)
		{
			foreach (Gtk.MenuItem child in menu.Children) {
				menu.Remove (child);
			}
		}
		
		private void OnNewNotebookMenuItem (object sender, EventArgs args)
		{
			NotebookManager.PromptCreateNewNotebook (null);
		}
	}
}
