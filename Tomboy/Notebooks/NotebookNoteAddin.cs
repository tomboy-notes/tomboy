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
		static Tag templateTag;

		static Tag TemplateTag
		{
			get {
				if (templateTag == null)
					templateTag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);
				return templateTag;
			}
		}
		
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
			toolButton.TooltipText = Catalog.GetString ("Place this note into a notebook");
			
			// Set the notebook submenu
			menu.Shown += OnMenuShown;
			toolButton.ShowAll ();
			AddToolItem (toolButton, -1);
			UpdateNotebookButtonLabel ();
			
			NotebookManager.NoteAddedToNotebook += OnNoteAddedToNotebook;
			NotebookManager.NoteRemovedFromNotebook += OnNoteRemovedFromNotebook;


			Note.TagAdded += delegate (Note taggedNote, Tag tag) {
				if (taggedNote == Note && tag == TemplateTag)
					UpdateButtonSensitivity (true);
			};

			// TODO: Make sure this is handled in NotebookNoteAddin, too
			Note.TagRemoved += delegate (Note taggedNote, string tag) {
				if (taggedNote == Note && tag == TemplateTag.NormalizedName)
					UpdateButtonSensitivity (false);
			};
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
				UpdateButtonSensitivity (Note.ContainsTag (TemplateTag));
			}
		}

		private void UpdateButtonSensitivity (bool isTemplate)
		{
			if (toolButton != null)
				toolButton.Sensitive = !isTemplate;
			if (Note.HasWindow)
				Note.Window.DeleteButton.Sensitive = !isTemplate || NotebookManager.GetNotebookFromNote (Note) == null;
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

			Gtk.ITreeModel model = NotebookManager.Notebooks;
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
