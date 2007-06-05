
using System;
using System.IO;
using Mono.Unix;
using Tomboy;
using Gtk;

using Mono.Addins;

namespace Tomboy.Tasks
{
	public class TasksAddin : ApplicationAddin
	{
		static TaskManager manager;
		static object locker = new object ();
		
		static Gtk.ActionGroup action_group;
		static uint tray_icon_ui = 0;
		
		static TaskListWindow task_list_window = null;

		public static TaskManager DefaultTaskManager
		{
			get { return manager; }
		}

		public TasksAddin ()
		{
			Logger.Debug ("TasksAddin Constructor");
		}
		
		public override void Initialize ()
		{
			Logger.Debug ("TasksAddin.Initialize ()");

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
				
				Tomboy.ActionManager.UI.InsertActionGroup (action_group, 0);
			}
		}

		public override void Shutdown ()
		{
			Logger.Debug ("TasksAddin.Shutdown ()");
			manager.Shutdown ();
			manager = null;
			
			try {
				Tomboy.ActionManager.UI.RemoveActionGroup (action_group);
			} catch {}
			try {
				Tomboy.ActionManager.UI.RemoveUi (tray_icon_ui);
			} catch {}
		}

		private void OnOpenToDoListAction ()
		{
			TaskListWindow task_list_window = TaskListWindow.GetInstance (manager);
			if (task_list_window != null)
				task_list_window.Present ();
		}
	}
}
