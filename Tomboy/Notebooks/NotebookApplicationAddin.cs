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
		static Gdk.Pixbuf notebookIcon;
		static Gdk.Pixbuf newNotebookIcon;
		
		static Gdk.Pixbuf NotebookIcon
		{
			get {
				if (notebookIcon == null)
					notebookIcon = GuiUtils.GetIcon ("notebook", 16);
				return notebookIcon;
			}
		}

		static Gdk.Pixbuf NewNotebookIcon
		{
			get {
				if (newNotebookIcon == null)
					newNotebookIcon = GuiUtils.GetIcon ("notebook-new", 16);
				return newNotebookIcon;
			}
		}

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
			
			Gtk.ImageMenuItem item = Tomboy.ActionManager.GetWidget (
				"/TrayIconMenu/TrayNewNotePlaceholder/TrayNewNotebookMenu") as Gtk.ImageMenuItem;
			if (item != null) {
				if (item is Gtk.ImageMenuItem) {
					Gtk.ImageMenuItem imageItem = item as Gtk.ImageMenuItem;
					(imageItem.Image as Gtk.Image).Pixbuf = NotebookIcon;
				}
				trayNotebookMenu = new Gtk.Menu ();
				item.Submenu = trayNotebookMenu;
				
				trayNotebookMenu.Shown += OnTrayNotebookMenuShown;
				trayNotebookMenu.Hidden += OnTrayNotebookMenuHidden;
			}
			
			Gtk.ImageMenuItem imageitem = Tomboy.ActionManager.GetWidget (
				"/MainWindowMenubar/FileMenu/FileMenuNewNotePlaceholder/NewNotebookMenu") as Gtk.ImageMenuItem;
			if (imageitem != null) {
				if (imageitem is Gtk.ImageMenuItem) {
					Gtk.ImageMenuItem imageItem = imageitem as Gtk.ImageMenuItem;
					(imageItem.Image as Gtk.Image).Pixbuf = NotebookIcon;
				}
				mainWindowNotebookMenu = new Gtk.Menu ();
				imageitem.Submenu = mainWindowNotebookMenu;
				
#if MAC
				// Make the menu once, since Shown events aren't working in Mac Menu
				AddMenuItems (mainWindowNotebookMenu);
				// Listen to these events to maintain list of Notebooks; watching the TreeStore
				// takes more work because it's a pre-notify.
				NotebookManager.NoteAddedToNotebook += delegate (Note o, Notebook args) {
					AddMenuItems (mainWindowNotebookMenu); };
				NotebookManager.NoteRemovedFromNotebook += delegate (Note o, Notebook args) {
					AddMenuItems (mainWindowNotebookMenu); };
#endif

				mainWindowNotebookMenu.Shown += OnNewNotebookMenuShown;
				mainWindowNotebookMenu.Hidden += OnNewNotebookMenuHidden;
			}
			 imageitem = Tomboy.ActionManager.GetWidget (
				"/NotebooksTreeContextMenu/NewNotebookNote") as Gtk.ImageMenuItem;
			if (imageitem != null) {
				if (imageitem is Gtk.ImageMenuItem) {
					Gtk.ImageMenuItem imageItem = imageitem as Gtk.ImageMenuItem;
					(imageItem.Image as Gtk.Image).Pixbuf = ActionManager.newNote;
				}
			}
					
			foreach (Note note in Tomboy.DefaultNoteManager.Notes) {
				note.TagAdded += OnTagAdded;
				note.TagRemoved += OnTagRemoved;
			}
					
			Tomboy.DefaultNoteManager.NoteAdded += OnNoteAdded;
			Tomboy.DefaultNoteManager.NoteDeleted += OnNoteDeleted;
				
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

			Gtk.ITreeModel model = NotebookManager.Notebooks;
			Gtk.TreeIter iter;
			
			// Add in the "New Notebook..." menu item
			Gtk.ImageMenuItem newNotebookMenuItem =
				new Gtk.ImageMenuItem (Catalog.GetString ("New Note_book..."));
			newNotebookMenuItem.Image = new Gtk.Image (NewNotebookIcon);
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
#if MAC
			menu.ShowAll ();
#endif
		}
		
		private void RemoveMenuItems (Gtk.Menu menu)
		{
#if MAC
			menu.HideAll ();
#endif
			foreach (Gtk.MenuItem child in menu.Children) {
				menu.Remove (child);
			}
		}
		
		private void OnNewNotebookMenuItem (object sender, EventArgs args)
		{
			NotebookManager.PromptCreateNewNotebook (null);
		}
		
		/// <summary>
		/// Handle the addition of note tags through programmatic means,
		/// such as note sync or the dbus remote control.
		/// </summary>
		private void OnTagAdded (Note note, Tag tag)
		{
			if (NotebookManager.AddingNotebook)
				return;
			
			string megaPrefix =
				Tag.SYSTEM_TAG_PREFIX + Notebook.NotebookTagPrefix;
			if (tag.IsSystem == false
			    || tag.Name.StartsWith (megaPrefix) == false) {
				return;
			}
			
			string notebookName =
				tag.Name.Substring (megaPrefix.Length);
			
			Notebook notebook =
				NotebookManager.GetOrCreateNotebook (notebookName);
				
			NotebookManager.FireNoteAddedToNoteBook (note, notebook);
		}
			
		private void OnTagRemoved (Note note, string normalizedTagName)
		{
			string megaPrefix =
				Tag.SYSTEM_TAG_PREFIX + Notebook.NotebookTagPrefix;
			if (normalizedTagName.StartsWith (megaPrefix) == false)
				return;
			
			string normalizedNotebookName =
				normalizedTagName.Substring (megaPrefix.Length);
			
			Notebook notebook =
				NotebookManager.GetNotebook (normalizedNotebookName);
			if (notebook == null)
				return;
			
			NotebookManager.FireNoteRemovedFromNoteBook (note, notebook);
		}
		
		private void OnNoteAdded (object sender, Note note)
		{
			note.TagAdded += OnTagAdded;
			note.TagRemoved += OnTagRemoved;
		}
		
		private void OnNoteDeleted (object sender, Note note)
		{
			note.TagAdded -= OnTagAdded;
			note.TagRemoved -= OnTagRemoved;
		}
	}
}
