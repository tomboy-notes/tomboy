using System;
using System.Collections.Generic;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookNoteAddin : NoteAddin
	{
		ToolMenuButton toolButton;
		Gtk.Menu menu;
		static Gdk.Pixbuf notebookIcon;
		static Gdk.Pixbuf newNotebookIcon;
		
		static Gdk.Pixbuf NotebookIcon
		{
			get {
				if (notebookIcon == null)
					notebookIcon = GuiUtils.GetIcon ("notebook", 22);
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

		public override void Initialize ()
		{
		}

		private void InitializeToolButton ()
		{
			toolButton =
					new ToolMenuButton (Note.Window.Toolbar,
										new Gtk.Image (NotebookIcon),
										string.Empty, menu);
			toolButton.IsImportant = true;
			toolButton.Homogeneous = false;
			Gtk.Tooltips toolbarTips = new Gtk.Tooltips ();
			toolbarTips.SetTip (toolButton, Catalog.GetString ("Place this note into a notebook"), null);
			
			// Set the notebook submenu
			menu.Shown += OnMenuShown;
			toolButton.ShowAll ();
			AddToolItem (toolButton, -1);
			UpdateNotebookButtonLabel ();
			
			NotebookManager.NoteAddedToNotebook += OnNoteAddedToNotebook;
			NotebookManager.NoteRemovedFromNotebook += OnNoteRemovedFromNotebook;
		}

		public override void Shutdown ()
		{
			// Disconnect the event handlers so
			// there aren't any memory leaks.
			if (toolButton != null) {
				menu.Shown -= OnMenuShown;
				NotebookManager.NoteAddedToNotebook -=
					OnNoteAddedToNotebook;
				NotebookManager.NoteRemovedFromNotebook -=
					OnNoteRemovedFromNotebook;
			}
		}

		public override void OnNoteOpened ()
		{
			if (menu == null) {
				menu = new Gtk.Menu ();
				menu.ShowAll ();
			}
			
			if (toolButton == null) {
				InitializeToolButton ();

				// Disable the notebook button if this note is a template note
				Tag templateTag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);
				if (Note.ContainsTag (templateTag) == true) {
					toolButton.Sensitive = false;
				
					// Also prevent notebook templates from being deleted
					if (NotebookManager.GetNotebookFromNote (Note) != null)
						Note.Window.DeleteButton.Sensitive = false;
				}
			}
		}
		
		void OnMenuShown (object sender, EventArgs args)
		{
			UpdateMenu ();
		}
		
		void OnNoteAddedToNotebook (Note note, Notebook notebook)
		{
			if (note == Note)
				UpdateNotebookButtonLabel (notebook);
		}
		
		void OnNoteRemovedFromNotebook (Note note, Notebook notebook)
		{
			if (note == Note)
				UpdateNotebookButtonLabel (null);
		}
		
		void OnNewNotebookMenuItem (object sender, EventArgs args)
		{
			List<Note> noteList = new List<Note> ();
			noteList.Add (Note);
			NotebookManager.PromptCreateNewNotebook (Note.Window, noteList);
		}
		
		void UpdateNotebookButtonLabel ()
		{
			Notebook currentNotebook = NotebookManager.GetNotebookFromNote (Note);
			UpdateNotebookButtonLabel (currentNotebook);
		}
		
		void UpdateNotebookButtonLabel (Notebook notebook)
		{
			string labelText =
				notebook == null ?
					Catalog.GetString ("Notebook") :
					notebook.Name;
			
			// Ellipsize names longer than 12 chars in length
			// TODO: Should we hardcode the ellipsized notebook name to 12 chars?
			Gtk.Label l = toolButton.LabelWidget as Gtk.Label;
			if (l != null) {
				l.Text = labelText;
				l.MaxWidthChars = 12;
				l.Ellipsize = Pango.EllipsizeMode.End;
				l.ShowAll ();
			}
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
			
			// Add the "New Notebook..."
			Gtk.ImageMenuItem newNotebookMenuItem =
				new Gtk.ImageMenuItem (Catalog.GetString ("_New notebook..."));
			newNotebookMenuItem.Image = new Gtk.Image (NewNotebookIcon);
			newNotebookMenuItem.Activated += OnNewNotebookMenuItem;
			newNotebookMenuItem.Show ();
			menu.Append (newNotebookMenuItem);
			
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
