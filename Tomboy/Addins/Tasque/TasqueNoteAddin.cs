
using System;

using Mono.Unix;

using Tomboy;

using DBus;
using org.freedesktop.DBus;

namespace Tomboy.TasqueAddin
{
	public class TasqueNoteAddin : NoteAddin
	{
		static ObjectPath TasquePath =
			new ObjectPath ("/org/gnome/Tasque/RemoteControl");
		static string TasqueNamespace = "org.gnome.Tasque";
		
		static Gdk.Pixbuf tasqueIcon = null;

		static Gdk.Pixbuf TasqueIcon {
			get {
				if (tasqueIcon == null)
					tasqueIcon =
						GuiUtils.GetIcon (System.Reflection.Assembly.GetExecutingAssembly (),
						"tasque", 22);
				return tasqueIcon;
			}
		}
		
		Gtk.MenuToolButton menuToolButton;
		Gtk.Menu menu;
		bool submenuBuilt;
		InterruptableTimeout markSetTimeout;
		
		public override void Initialize ()
		{
			submenuBuilt = false;
		}

		public override void Shutdown ()
		{
			// The following two lines are required to prevent the plugin
			// from leaking references when the plugin is disabled.
			if (menu != null)
				menu.Hidden -= OnMenuHidden;
			if (menuToolButton != null) {
				menuToolButton.Clicked -= OnMenuToolButtonClicked;
				menuToolButton.ShowMenu -= OnMenuItemActivated;
			}
		}

		public override void OnNoteOpened ()
		{
			menu = new Gtk.Menu ();
			menu.Hidden += OnMenuHidden;
			menu.ShowAll ();
			
			Gtk.Image tasqueImage = new Gtk.Image (TasqueIcon);
			tasqueImage.Show ();
			menuToolButton =
				new Gtk.MenuToolButton (tasqueImage, Catalog.GetString ("Tasque"));
			menuToolButton.Menu = menu;
			menuToolButton.Clicked += OnMenuToolButtonClicked;
			menuToolButton.ShowMenu += OnMenuItemActivated;
			menuToolButton.Sensitive = false;
			menuToolButton.Show ();
			AddToolItem (menuToolButton, -1);
			
			// Sensitize the Task button on text selection
			markSetTimeout = new InterruptableTimeout();
			markSetTimeout.Timeout += UpdateTaskButtonSensitivity;
			Note.Buffer.MarkSet += OnSelectionMarkSet;
		}
		
		void OnMenuToolButtonClicked (object sender, EventArgs args)
		{
			string taskName = GetTaskName ();
			if (taskName == null)
				return;
			
			// Note to translators: "All" here must match up with the "All"
			// category translation in Tasque for this to work properly.  "All"
			// is used here to allow Tasque to decide which default category
			// will be used to create the new task.
			CreateTask (Catalog.GetString ("All"), taskName);
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
			// toggling over the "What links here?" menu item, it doesn't
			// keep forcing the submenu to rebuild.

			// Force the submenu to rebuild next time it's supposed to show
			submenuBuilt = false;
		}

		void UpdateMenu ()
		{
			//
			// Clear out the old list
			//
			foreach (Gtk.MenuItem old_item in menu.Children) {
				menu.Remove (old_item);
			}
			
			Tasque.RemoteControl tasque = GetTasqueRemoteControl ();
			
			string [] taskCategories = null;
			
			if (tasque != null) {
				try {
					taskCategories = tasque.GetCategoryNames ();
				} catch (Exception e) {
					Logger.Debug ("Exception calling Tasque.GetCategoryNames (): {0}",
								  e.Message);
				}
			}
			
			if (taskCategories != null) {
				//
				// Build a new list
				//
				foreach (string category in taskCategories) {
					CategoryMenuItem item = new CategoryMenuItem (category);
					item.Activated += OnCategoryActivated;
					item.ShowAll ();
					menu.Append (item);
				}
			}

			// If nothing was found, add in a "dummy" item
			if (menu.Children.Length == 0) {
				Gtk.MenuItem blankItem =
					new Gtk.MenuItem (Catalog.GetString ("--- Tasque is not running ---"));
				blankItem.Sensitive = false;
				blankItem.ShowAll ();
				menu.Append (blankItem);
			}

			submenuBuilt = true;
		}
		
		void OnCategoryActivated (object sender, EventArgs args)
		{
			CategoryMenuItem item = sender as CategoryMenuItem;
			if (item == null) {
				return;
			}
			
			string categoryName = item.CategoryName;
			
			Tasque.RemoteControl tasque = GetTasqueRemoteControl ();
			if (tasque == null)
				return;
			
			string taskName = GetTaskName ();
			if (taskName == null)
				return;
			
			CreateTask (categoryName, taskName);
		}
		
		private void CreateTask (string categoryName, string taskName)
		{
			Tasque.RemoteControl tasque = GetTasqueRemoteControl ();
			if (tasque == null)
				return;
			
			// Trim and truncate to 100 chars max (this is a
			// "number out of a hat")...a4gpa protection
			taskName = taskName.Trim ();
			if (taskName.Length > 100)
				taskName = taskName.Substring (0, 100);
			
			if (taskName == string.Empty)
				return;
			
			try {
				tasque.CreateTask (categoryName, taskName, false);
			} catch (Exception e) {
				Logger.Debug ("Exception calling Tasque.CreateTask ()", e.Message);
			}
		}
		
		Tasque.RemoteControl GetTasqueRemoteControl ()
		{
			Tasque.RemoteControl remoteControl = null;
			
			try {
				if (Bus.Session.NameHasOwner (TasqueNamespace) == false)
					return null;
				
				remoteControl =
					Bus.Session.GetObject<Tasque.RemoteControl> (
						TasqueNamespace,
						TasquePath);
			} catch (Exception e) {
				Logger.Error ("Exception when getting Tasque.RemoteControl: {0}",
							  e.Message);
			}
			
			return remoteControl;
		}
		
		string GetTaskName ()
		{
			Gtk.TextIter selectionStart;
			Gtk.TextIter selectionEnd;
			
			if (Note.Buffer.GetSelectionBounds (out selectionStart,
												out selectionEnd) == false)
				return null;
			
			return Note.Buffer.GetText (selectionStart, selectionEnd, false);
		}
		
		void OnSelectionMarkSet (object sender, Gtk.MarkSetArgs args)
		{
			// FIXME: Process in a timeout due to GTK+ bug #172050.
			markSetTimeout.Reset (0);
		}
		
		void UpdateTaskButtonSensitivity (object sender, EventArgs args)
		{
			menuToolButton.Sensitive = (Note.Buffer.Selection != null);
		}
		
		class CategoryMenuItem : Gtk.MenuItem
		{
			string name;
			
			public CategoryMenuItem (string categoryName) : base (categoryName)
			{
				this.name = categoryName;
			}
			
			public string CategoryName
			{
				get { return name; }
			}
		}
	}
}
