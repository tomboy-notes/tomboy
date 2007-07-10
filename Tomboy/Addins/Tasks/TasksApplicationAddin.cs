
using System;
using System.IO;
using Mono.Unix;
using Tomboy;
using Gtk;

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
				
				tray_icon_ui = Tomboy.ActionManager.UI.AddUiFromString (@"
					<ui>
						<popup name='TrayIconMenu' action='TrayIconMenuAction'>
							<menuitem name='OpenToDoList' action='OpenToDoListAction' />
						</popup>
					</ui>
				");
				
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
	}
}
