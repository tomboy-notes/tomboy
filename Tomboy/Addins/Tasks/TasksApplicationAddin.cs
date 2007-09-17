
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using Mono.Unix;
using Tomboy;
using Gtk;
using Gtk.Extras;

using Mono.Addins;

namespace Tomboy.Tasks
{
	public class TasksApplicationAddin : ApplicationAddin
	{
		static TaskManager manager;
		static object locker = new object ();
		
		static Gtk.ActionGroup action_group;
		static uint tray_icon_ui = 0;
		static uint tools_menu_ui = 0;
		
		static TaskListWindow task_list_window = null;
		
		bool initialized;
		
		Gtk.Menu tomboy_tray_menu;
		List<Gtk.MenuItem> top_tasks = new List<Gtk.MenuItem> ();

		public static TaskManager DefaultTaskManager
		{
			get { return manager; }
		}
		
		public TasksApplicationAddin ()
		{
			initialized = false;
		}

		public override void Initialize ()
		{
			Logger.Debug ("TasksApplicationAddin.Initialize ()");

			if (manager == null) {
				lock (locker) {
					if (manager == null) {
						manager = new TaskManager (
							Path.Combine (Tomboy.DefaultNoteManager.NoteDirectoryPath, "Tasks"));
					}
				}
				
				///
				/// Add a "To Do List" to Tomboy's Tray Icon Menu
				///
				action_group = new Gtk.ActionGroup ("Tasks");
				action_group.Add (new Gtk.ActionEntry [] {
					new Gtk.ActionEntry ("ToolsMenuAction", null,
						Catalog.GetString ("_Tools"), null, null, null),
					new Gtk.ActionEntry ("OpenToDoListAction", null,
						Catalog.GetString ("To Do List"), null, null,
						delegate { OnOpenToDoListAction (); })
				});
				
//				tray_icon_ui = Tomboy.ActionManager.UI.AddUiFromString (@"
//					<ui>
//						<popup name='TrayIconMenu' action='TrayIconMenuAction'>
//							<menuitem name='OpenToDoList' action='OpenToDoListAction' />
//						</popup>
//					</ui>
//				");
				
				tools_menu_ui = Tomboy.ActionManager.UI.AddUiFromString (@"
					<ui>
					    <menubar name='MainWindowMenubar'>
					    	<placeholder name='MainWindowMenuPlaceholder'>
						    	<menu name='ToolsMenu' action='ToolsMenuAction'>
						    		<menuitem name='OpenToDoList' action='OpenToDoListAction' />
						    	</menu>
						    </placeholder>
					    </menubar>
					</ui>
				");
				
				Tomboy.ActionManager.UI.InsertActionGroup (action_group, 0);
				
				Tomboy.DefaultNoteManager.NoteDeleted += OnNoteDeleted;
				
				tomboy_tray_menu = GetTomboyTrayMenu ();
				tomboy_tray_menu.Shown += OnTomboyTrayMenuShown;
				tomboy_tray_menu.Hidden += OnTomboyTrayMenuHidden;
				
				initialized = true;
			}
		}

		public override void Shutdown ()
		{
			Logger.Debug ("TasksApplicationAddin.Shutdown ()");
			manager.Shutdown ();
			manager = null;
			
			try {
				Tomboy.ActionManager.UI.RemoveActionGroup (action_group);
			} catch {}
			try {
				Tomboy.ActionManager.UI.RemoveUi (tray_icon_ui);
				Tomboy.ActionManager.UI.RemoveUi (tools_menu_ui);
			} catch {}
			
			initialized = false;
		}
		
		private void OnTomboyTrayMenuShown (object sender, EventArgs args)
		{
			// Add in the top tasks
			// TODO: Read the number of todo items to show from Preferences
			int max_size = 5;
			int list_size = 0;
			Gtk.MenuItem item;
			
			// Filter the tasks to the ones that are incomplete
			Gtk.TreeModelFilter store_filter =
				new Gtk.TreeModelFilter (TasksApplicationAddin.DefaultTaskManager.Tasks, null);
			store_filter.VisibleFunc = FilterTasks;

			// TODO: Sort the tasks to order by due date and priority
//			store_sort = new Gtk.TreeModelSort (store_filter);
//			store_sort.DefaultSortFunc = 
//				new Gtk.TreeIterCompareFunc (TaskSortFunc);
				
//			tree.Model = store_sort;
			
//			int cnt = tree.Model.IterNChildren ();
			
//			task_count.Text = string.Format (
//				Catalog.GetPluralString("Total: {0} task",
//							"Total: {0} tasks",
//							cnt),
//				cnt);

			
			// List the top "max_size" tasks
			Gtk.TreeIter iter;
			Gtk.SeparatorMenuItem separator;
			
			// Determine whether the icon is near the top/bottom of the screen
			// TODO: Do this better!
			int x, y;
			Tomboy.Tray.GdkWindow.GetOrigin (out x, out y);
			int position;
			if (y < 24)
				position = 2;
			else
				position = tomboy_tray_menu.Children.Length - 7;

			separator = new Gtk.SeparatorMenuItem ();
			tomboy_tray_menu.Insert (separator, position++);
			separator.Show ();
			top_tasks.Add (separator);

			item = new Gtk.MenuItem (Catalog.GetString ("To Do List"));
			tomboy_tray_menu.Insert (item, position++);
			item.ShowAll ();
			top_tasks.Add (item);
			item.Activated += OnOpenTodoList;
			
			if (store_filter.GetIterFirst (out iter)) {
				do {
					Task task = store_filter.GetValue (iter, 0) as Task;
					item = new TomboyTaskMenuItem (task);
					tomboy_tray_menu.Insert (item, list_size + position);
					item.ShowAll ();
					top_tasks.Add (item);
					list_size++;
				} while (store_filter.IterNext (ref iter) && list_size < max_size);
			}
		}
		
		private void OnTomboyTrayMenuHidden (object sender, EventArgs args)
		{
			// Remove the tasks
			foreach (Gtk.Widget item in top_tasks) {
				tomboy_tray_menu.Remove (item);
			}
			
			top_tasks.Clear ();
		}


		private void OnOpenToDoListAction ()
		{
			TaskListWindow task_list_window = TaskListWindow.GetInstance (manager);
			if (task_list_window != null)
				task_list_window.Present ();
		}
		
		public override bool Initialized
		{
			get { return initialized; }
		}
		
		private void OnNoteDeleted (object sender, Note note)
		{
			// Delete all the tasks associated with this note
			foreach (Task task in DefaultTaskManager.GetTasksForNote (note)) {
				Logger.Info ("Deleting task: {0}", task.Summary);
				DefaultTaskManager.Delete (task);
			}
		}
		
		private Gtk.Menu GetTomboyTrayMenu ()
		{
			Gtk.Menu menu =
				Tomboy.ActionManager.GetWidget ("/TrayIconMenu") as Gtk.Menu;
			
			return menu;
		}

		/// <summary>
		/// Only allow incomplete tasks to pass this filter
		/// </summary>
		bool FilterTasks (Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			Task task = model.GetValue (iter, 0) as Task;
			if (task == null)
				return false;
			
			if (task.CompletionDate == DateTime.MinValue)
				return true; // No completion date set
			
			return false;
		}
		
		void OnOpenTodoList (object sender, EventArgs args)
		{
			Tomboy.ActionManager ["OpenToDoListAction"].Activate ();
		}
	}
	
	public class TomboyTaskMenuItem : Gtk.ImageMenuItem
//	public class TomboyTaskMenuItem : ComplexMenuItem
	{
		Task task;
		BetterCheckButton check_button;
		Gtk.Label summary;
		bool inhibit_activate;
		Gtk.RcStyle old_style;

		static Dictionary<Task, TaskOptionsDialog> options_dialogs =
			new Dictionary<Task, TaskOptionsDialog> ();
		
		public TomboyTaskMenuItem (Task task)
			: base ()
		{
			this.task = task;
			
			summary = new Label ();
			summary.Markup = task.Summary;
			summary.UseUnderline = false;
			summary.UseMarkup = true;
			summary.Xalign = 0;
			
			summary.Show ();
			Add (summary);

			check_button = new BetterCheckButton ();
			check_button.Toggled += OnCheckButtonToggled;
			Image = check_button;
		}

		public Task Task
		{
			get { return task; }
		}
		
		static string FormatForLabel (string summary)
		{
			// Replace underscores ("_") with double-underscores ("__")
			// so Note menuitems are not created with mnemonics.
			return summary.Replace ("_", "__");
		}

		static string GetDisplayText (string summary)
		{
			// TODO: use ellipsed summary if it's too long
			return FormatForLabel (summary);
		}

		protected override bool OnButtonPressEvent (Gdk.EventButton ev)
		{
			if (ev.X >= check_button.Allocation.X &&
				ev.X < check_button.Allocation.X + check_button.Allocation.Width) {
				inhibit_activate = true;
				check_button.Active = !check_button.Active;
				return true;
			}
			
			return base.OnButtonPressEvent (ev);
		}

		protected override bool OnButtonReleaseEvent (Gdk.EventButton ev)
		{
			if (inhibit_activate) {
				inhibit_activate = false;
				return true;
			}
			return base.OnButtonReleaseEvent (ev);
		}

		/// <summary>
		/// If the user has just clicked on the checkbutton, the standard
		/// behavior of the OnActivated should be suppressed.
		/// </summary>
		protected override void OnActivated () 
		{
			// Open the option dialog
			if (!inhibit_activate) {
				TaskOptionsDialog dialog;
				if (options_dialogs.ContainsKey (task)) {
					dialog = options_dialogs [task];
				} else {
					dialog =
						new TaskOptionsDialog (
							null,
							Gtk.DialogFlags.DestroyWithParent,
							task);
					dialog.WindowPosition = Gtk.WindowPosition.CenterOnParent;
					dialog.DeleteEvent += OnOptionsDialogDeleted;
					options_dialogs [task] = dialog;
				}
				
				dialog.Present ();
				dialog.GrabFocus ();
			}
		}
		
		protected override bool OnEnterNotifyEvent (Gdk.EventCrossing evnt)
		{
			Rc.ParseString ("class \"*<GtkMenuItem>.GtkCheckButton\" style \"theme-menu-item\"");
			return base.OnEnterNotifyEvent (evnt);
		}
		
		protected override bool OnLeaveNotifyEvent (Gdk.EventCrossing evnt)
		{
			Rc.ReparseAll ();
			return base.OnLeaveNotifyEvent (evnt);
		}
		
		private void OnCheckButtonToggled (object sender, EventArgs args)
		{
			if (check_button.Active) {
				task.Complete ();
				summary.Markup =
					String.Format (
						"<span strikethrough='true'>{0}</span>",
						GetDisplayText (task.Summary));
				summary.Sensitive = false;
			} else {
				task.ReOpen ();
				summary.Markup =
					String.Format (
						"<span strikethrough='false'>{0}</span>",
						GetDisplayText (task.Summary));
				summary.Sensitive = true;
			}
		}
		
		void OnOptionsDialogDeleted (object sender, Gtk.DeleteEventArgs args)
		{
			TaskOptionsDialog dialog = sender as TaskOptionsDialog;
			if (dialog == null)
				return;
			
			if (options_dialogs.ContainsKey (dialog.Task))
				options_dialogs.Remove (dialog.Task);
		}
	}

	public class BetterCheckButton : Gtk.MenuItem
	{
		bool active;
		static int indicator_size = 13;
		static int indicator_spacing = 2;
		
		#region Constructors
		public BetterCheckButton () : base ()
		{
			active = false;
			SetSizeRequest (indicator_size + indicator_spacing,
				indicator_size + indicator_spacing);
		}
		#endregion // Constructors
		
		#region Properties
		public bool Active
		{
			get { return active; }
			set { this.Toggle (); }
		}
		#endregion // Properties
		
		#region Private Methods
		void Paint (Gdk.Rectangle area)
		{
			int x;
			int y;
			Widget child;
			Gtk.ShadowType shadow_type;
			Gtk.StateType state_type;
			
			x = Allocation.X + indicator_spacing + (int) BorderWidth;
			y = Allocation.Y + (Allocation.Height - indicator_size) / 2;
			
			child = this.Child;
			
			if (active) {
				shadow_type = Gtk.ShadowType.In;
				state_type = Gtk.StateType.Insensitive;
			} else {
				shadow_type = Gtk.ShadowType.Out;
				state_type = Gtk.StateType.Normal;
			}
			
			Gtk.Style.PaintCheck (Style, GdkWindow,
								  state_type, shadow_type,
								  area, this, "checkbutton",
								  x, y, indicator_size, indicator_size);
		}
		
		protected override bool OnExposeEvent (Gdk.EventExpose evnt)
		{
			Paint (evnt.Area);
			
			return false;
		}
		
		protected override void OnToggled ()
		{
			active = !active;
		}

		#endregion // Private Methods
	}
}
