
using System;
using Gtk;
using Mono.Unix;
using Tomboy;

public class TaskListWindow : Tomboy.ForcedPresentWindow
{
	TaskManager manager;
	
	Gtk.ActionGroup action_group;
	uint menubar_ui;
	
	Gtk.MenuBar menu_bar;
	Gtk.Label task_count;
	Gtk.ScrolledWindow tasks_sw;
	Gtk.VBox content_vbox;

	Gtk.TreeView tree;
	Gtk.TreeModelSort store_sort;
	
	// Use this to select the task that was created inside
	// this window.
	bool expecting_newly_created_task;

	static TaskListWindow instance;

	public static TaskListWindow GetInstance (TaskManager manager)
	{
		if (instance == null)
			instance = new TaskListWindow (manager);
		System.Diagnostics.Debug.Assert (
			instance.manager == manager, 
			"Multiple TaskManagers not supported");
		return instance;
	}

	protected TaskListWindow (TaskManager manager)
		: base (Catalog.GetString ("To Do List"))
	{
		this.manager = manager;
		this.IconName = "tomboy";
		this.SetDefaultSize (500, 300);
		
		expecting_newly_created_task = false;
		
		AddAccelGroup (Tomboy.Tomboy.ActionManager.UI.AccelGroup);

		action_group = new Gtk.ActionGroup ("TaskList");
		action_group.Add (new Gtk.ActionEntry [] {
			new Gtk.ActionEntry ("TaskListFileMenuAction", null,
				Catalog.GetString ("_File"), null, null, null),

			new Gtk.ActionEntry ("NewTaskAction", Gtk.Stock.New,
				Catalog.GetString ("New _Task"), "<Control>T",
				Catalog.GetString ("Create a new task"), null),
			
			new Gtk.ActionEntry ("OpenTaskAction", String.Empty,
				Catalog.GetString ("_Open..."), "<Control>O",
				Catalog.GetString ("Open the selected task"), null),
			
			new Gtk.ActionEntry ("CloseTaskListWindowAction", Gtk.Stock.Close,
				Catalog.GetString ("_Close"), "<Control>W",
				Catalog.GetString ("Close this window"), null),
			
			new Gtk.ActionEntry ("TaskListEditMenuAction", null,
				Catalog.GetString ("_Edit"), null, null, null),
				
			new Gtk.ActionEntry ("DeleteTaskAction", Gtk.Stock.Preferences,
				Catalog.GetString ("_Delete"), "Delete",
				Catalog.GetString ("Delete the selected task"), null),

			new Gtk.ActionEntry ("TaskListHelpMenuAction", null,
				Catalog.GetString ("_Help"), null, null, null),
				
			new Gtk.ActionEntry ("ShowTaskHelpAction", Gtk.Stock.Help,
				Catalog.GetString ("_Contents"), "F1",
				Catalog.GetString ("Tasks Help"), null)
		});
		
		Tomboy.Tomboy.ActionManager.UI.InsertActionGroup (action_group, 0);

		menu_bar = CreateMenuBar ();
		
		MakeTasksTree ();
		tree.Show ();

		// Update on changes to tasks
		manager.TaskAdded += OnTaskAdded;
		manager.TaskDeleted += OnTaskDeleted;
		manager.TaskStatusChanged += OnTaskStatusChanged;

		tasks_sw = new Gtk.ScrolledWindow ();
		tasks_sw.ShadowType = Gtk.ShadowType.In;
		tasks_sw.VscrollbarPolicy = Gtk.PolicyType.Automatic;
		tasks_sw.HscrollbarPolicy = Gtk.PolicyType.Automatic;

		// Reign in the window size if there are notes with long
		// names, or a lot of notes...

		Gtk.Requisition tree_req = tree.SizeRequest ();
		if (tree_req.Height > 420)
			tasks_sw.HeightRequest = 420;

		if (tree_req.Width > 480)
			tasks_sw.WidthRequest = 480;

		tasks_sw.Add (tree);
		tasks_sw.Show ();
		
		task_count = new Gtk.Label ();
		task_count.Xalign = 0;
		task_count.Show ();

		Gtk.HBox status_box = new Gtk.HBox (false, 8);
		status_box.PackStart (task_count, true, true, 0);
		status_box.Show ();
		
		Gtk.VBox vbox = new Gtk.VBox (false, 8);
		vbox.BorderWidth = 6;
		vbox.PackStart (tasks_sw, true, true, 0);
		vbox.PackStart (status_box, false, false, 0);
		vbox.Show ();

		// Use another VBox to place the MenuBar
		// right at thetop of the window.
		content_vbox = new Gtk.VBox (false, 0);
		content_vbox.PackStart (menu_bar, false, false, 0);
		content_vbox.PackStart (vbox, true, true, 0);
		content_vbox.Show ();

		this.Add (content_vbox);
		this.DeleteEvent += OnDelete;
		this.KeyPressEvent += OnKeyPressed; // For Escape

		SetUpTreeModel ();
	}

	Gtk.MenuBar CreateMenuBar ()
	{
		ActionManager am = Tomboy.Tomboy.ActionManager;
		menubar_ui = Tomboy.Tomboy.ActionManager.UI.AddUiFromResource (
				"TasksUIManagerLayout.xml");
		
		Gtk.MenuBar menubar =
			Tomboy.Tomboy.ActionManager.GetWidget ("/TaskListWindowMenubar") as Gtk.MenuBar;
		
		am ["NewTaskAction"].Activated += OnNewTask;
		am ["OpenTaskAction"].Activated += OnOpenTask;
		am ["CloseTaskListWindowAction"].Activated += OnCloseWindow;
		am ["DeleteTaskAction"].Activated += OnDeleteTask;
		am ["ShowTaskHelpAction"].Activated += OnShowHelp;
		
		return menubar;
	}
	
	void MakeTasksTree ()
	{
		tree = new Gtk.TreeView ();
		tree.HeadersVisible = true;
		tree.RulesHint = true;
		tree.RowActivated += OnRowActivated;
		tree.Selection.Changed += OnSelectionChanged;
		tree.ButtonPressEvent += OnButtonPressed;
		
		// Columns: Summary, Due Date (No Date/Date), Completed (No Date/Date), Priority

		Gtk.CellRenderer renderer;
		
		///
		/// Summary
		///
		Gtk.TreeViewColumn summary = new Gtk.TreeViewColumn ();
		summary.Title = Catalog.GetString ("Summary");
		summary.Sizing = Gtk.TreeViewColumnSizing.Autosize;
		summary.Resizable = true;
		
		renderer = new Gtk.CellRendererToggle ();
		(renderer as Gtk.CellRendererToggle).Toggled += OnTaskToggled;
		summary.PackStart (renderer, false);
		summary.SetCellDataFunc (renderer,
				new Gtk.TreeCellDataFunc (ToggleCellDataFunc));
		
		renderer = new Gtk.CellRendererText ();
		(renderer as CellRendererText).Editable = true;
		(renderer as CellRendererText).Edited += OnTaskSummaryEdited;
		renderer.Xalign = 0.0f;
		summary.PackStart (renderer, true);
		summary.SetCellDataFunc (renderer,
				new Gtk.TreeCellDataFunc (SummaryCellDataFunc));

		tree.AppendColumn (summary);
		
		///
		/// Due Date
		///
		Gtk.TreeViewColumn due_date = new Gtk.TreeViewColumn ();
		due_date.Title = Catalog.GetString ("Due Date");
		due_date.Sizing = Gtk.TreeViewColumnSizing.Autosize;
		due_date.Resizable = false;
		
		renderer = new Gtk.Extras.CellRendererDate ();
		(renderer as Gtk.Extras.CellRendererDate).Editable = true;
		(renderer as Gtk.Extras.CellRendererDate).Edited += OnDueDateEdited;
		renderer.Xalign = 0.0f;
		due_date.PackStart (renderer, true);
		due_date.SetCellDataFunc (renderer,
				new Gtk.TreeCellDataFunc (DueDateCellDataFunc));
		tree.AppendColumn (due_date);
		
		///
		/// Completion Date
		///
		Gtk.TreeViewColumn completion_date = new Gtk.TreeViewColumn ();
		completion_date.Title = Catalog.GetString ("Completion Date");
		completion_date.Sizing = Gtk.TreeViewColumnSizing.Autosize;
		completion_date.Resizable = false;
		
		renderer = new Gtk.Extras.CellRendererDate ();
		(renderer as Gtk.Extras.CellRendererDate).Editable = false;
		renderer.Xalign = 0.0f;
		completion_date.PackStart (renderer, true);
		completion_date.SetCellDataFunc (renderer,
				new Gtk.TreeCellDataFunc (CompletionDateCellDataFunc));
		tree.AppendColumn (completion_date);
		
		///
		/// Priority
		///
		Gtk.TreeViewColumn priority = new Gtk.TreeViewColumn ();
		priority.Title = Catalog.GetString ("Priority");
		priority.Sizing = Gtk.TreeViewColumnSizing.Autosize;
		priority.Resizable = false;
		
		renderer = new Gtk.CellRendererCombo ();
		(renderer as Gtk.CellRendererCombo).Editable = true;
		(renderer as Gtk.CellRendererCombo).HasEntry = false;
		(renderer as Gtk.CellRendererCombo).Edited += OnTaskPriorityEdited;
		Gtk.ListStore priority_store = new Gtk.ListStore (typeof (string));
		priority_store.AppendValues (Catalog.GetString ("Low"));
		priority_store.AppendValues (Catalog.GetString ("Normal"));
		priority_store.AppendValues (Catalog.GetString ("High"));
		(renderer as Gtk.CellRendererCombo).Model = priority_store;
		(renderer as Gtk.CellRendererCombo).TextColumn = 0;
		renderer.Xalign = 0.0f;
		priority.PackStart (renderer, true);
		priority.SetCellDataFunc (renderer,
				new Gtk.TreeCellDataFunc (PriorityCellDataFunc));
		tree.AppendColumn (priority);
	}

	void ToggleCellDataFunc (Gtk.TreeViewColumn tree_column,
			Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
			Gtk.TreeIter iter)
	{
		Gtk.CellRendererToggle crt = cell as Gtk.CellRendererToggle;
		Task task = tree_model.GetValue (iter, 0) as Task;
		if (task == null)
			crt.Active = false;
		else {
			crt.Active = task.IsComplete;
		}
	}
	
	void SummaryCellDataFunc (Gtk.TreeViewColumn tree_column,
			Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
			Gtk.TreeIter iter)
	{
		Gtk.CellRendererText crt = cell as Gtk.CellRendererText;
		Task task = tree_model.GetValue (iter, 0) as Task;
		if (task == null)
			crt.Text = String.Empty;
		else
			crt.Text = task.Summary;
	}
	
	void DueDateCellDataFunc (Gtk.TreeViewColumn tree_column,
			Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
			Gtk.TreeIter iter)
	{
		Gtk.Extras.CellRendererDate crd = cell as Gtk.Extras.CellRendererDate;
		Task task = tree_model.GetValue (iter, 0) as Task;
		if (task == null)
			crd.Date = DateTime.MinValue;
		else
			crd.Date = task.DueDate;
	}
	
	void CompletionDateCellDataFunc (Gtk.TreeViewColumn tree_column,
			Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
			Gtk.TreeIter iter)
	{
		Gtk.Extras.CellRendererDate crd = cell as Gtk.Extras.CellRendererDate;
		Task task = tree_model.GetValue (iter, 0) as Task;
		if (task == null)
			crd.Date = DateTime.MinValue;
		else
			crd.Date = task.CompletionDate;
	}
	
	void PriorityCellDataFunc (Gtk.TreeViewColumn tree_column,
			Gtk.CellRenderer cell, Gtk.TreeModel tree_model,
			Gtk.TreeIter iter)
	{
		Gtk.CellRendererCombo crc = cell as Gtk.CellRendererCombo;
		Task task = tree_model.GetValue (iter, 0) as Task;
		switch (task.Priority) {
		case TaskPriority.Low:
			crc.Text = Catalog.GetString ("Low");
			break;
		case TaskPriority.High:
			crc.Text = Catalog.GetString ("High");
			break;
		default:
			crc.Text = Catalog.GetString ("Normal");
			break;
		}
	}

	void SetUpTreeModel ()
	{
		store_sort = new Gtk.TreeModelSort (manager.Tasks);
		store_sort.SetSortFunc (0 /* summary */,
			new Gtk.TreeIterCompareFunc (CompareSummaries));
			
		tree.Model = store_sort;
		
		int cnt = tree.Model.IterNChildren ();
		
		task_count.Text = string.Format (
			Catalog.GetPluralString("Total: {0} task",
						"Total: {0} tasks",
						cnt),
			cnt);
	}
	
	void UpdateTaskCount (int total)
	{
		string cnt = string.Format (
			Catalog.GetPluralString("Total: {0} task",
						"Total: {0} tasks",
						total), 
			total);

		task_count.Text = cnt;
	}
	
	void OnSelectionChanged (object sender, EventArgs args)
	{
		Task task = GetSelectedTask ();
		if (task != null) {
			Tomboy.Tomboy.ActionManager ["OpenTaskAction"].Sensitive = true;
			Tomboy.Tomboy.ActionManager ["DeleteTaskAction"].Sensitive = true;
		} else {
			Tomboy.Tomboy.ActionManager ["OpenTaskAction"].Sensitive = false;
			Tomboy.Tomboy.ActionManager ["DeleteTaskAction"].Sensitive = false;
		}
	}
	
	[GLib.ConnectBefore]
	void OnButtonPressed (object sender, Gtk.ButtonPressEventArgs args)
	{
		switch (args.Event.Button) {
		case 3: // third mouse button (right-click)
			Gtk.TreePath path = null;
			Gtk.TreeViewColumn column = null;
			
			if (tree.GetPathAtPos ((int) args.Event.X,
					(int) args.Event.Y,
					out path,
					out column) == false)
				break;
			
			Gtk.TreeSelection selection = tree.Selection;
			if (selection.CountSelectedRows () == 0)
				break;
			
			PopupContextMenuAtLocation ((int) args.Event.X,
					(int) args.Event.Y);

			break;
		}
	}
	
	void PopupContextMenuAtLocation (int x, int y)
	{
		Gtk.Menu menu = Tomboy.Tomboy.ActionManager.GetWidget (
				"/TaskListWindowContextMenu") as Gtk.Menu;
		menu.ShowAll ();
		Gtk.MenuPositionFunc pos_menu_func = null;
		
		// Set up the funtion to position the context menu
		// if we were called by the keyboard Gdk.Key.Menu.
		if (x == 0 && y == 0)
			pos_menu_func = PositionContextMenu;
			
		menu.Popup (null, null,
				pos_menu_func,
				0,
				Gtk.Global.CurrentEventTime);
	}
	
	// This is needed for when the user opens
	// the context menu with the keyboard.
	void PositionContextMenu (Gtk.Menu menu,
			out int x, out int y, out bool push_in)
	{
		Gtk.TreeIter iter;
		Gtk.TreePath path;
		Gtk.TreeSelection selection;
		
		// Set default "return" values
		push_in = false; // not used
		x = 0;
		y = 0;
		
		selection = tree.Selection;
		if (!selection.GetSelected (out iter))
			return;
		
		path = store_sort.GetPath (iter);
		
		int pos_x = 0;
		int pos_y = 0;
		
		GetWidgetScreenPos (tree, ref pos_x, ref pos_y);
		Gdk.Rectangle cell_rect = tree.GetCellArea (path, tree.Columns [0]);
		
		// Add 100 to x so it's not be at the far left
		x = pos_x + cell_rect.X + 100;
		y = pos_y + cell_rect.Y;
	}
	
	// Walk the widget hiearchy to figure out
	// where to position the context menu.
	void GetWidgetScreenPos (Gtk.Widget widget, ref int x, ref int y)
	{
		int widget_x;
		int widget_y;
		
		if (widget is Gtk.Window) {
			((Gtk.Window) widget).GetPosition (out widget_x, out widget_y);
		} else {
			GetWidgetScreenPos (widget.Parent, ref x, ref y);
			
			// Special case the TreeView because it adds
			// too much since it's in a scrolled window.
			if (widget == tree) {
				widget_x = 2;
				widget_y = 2;
			} else {
				Gdk.Rectangle widget_rect = widget.Allocation;
				widget_x = widget_rect.X;
				widget_y = widget_rect.Y;
			}
		}
		
		x += widget_x;
		y += widget_y;
	}

	Task GetSelectedTask ()
	{
		Gtk.TreeModel model;
		Gtk.TreeIter iter;

		if (!tree.Selection.GetSelected (out model, out iter))
			return null;

		return (Task) model.GetValue (iter, 0);
	}

	string PrettyPrintDate (DateTime date)
	{
		DateTime now = DateTime.Now;
		string short_time = date.ToShortTimeString ();

		if (date.Year == now.Year) {
			if (date.DayOfYear == now.DayOfYear)
				return String.Format (Catalog.GetString ("Today, {0}"), 
						      short_time);
			else if (date.DayOfYear == now.DayOfYear - 1)
				return String.Format (Catalog.GetString ("Yesterday, {0}"),
						      short_time);
			else if (date.DayOfYear > now.DayOfYear - 6)
				return String.Format (
					Catalog.GetString ("{0} days ago, {1}"), 
					now.DayOfYear - date.DayOfYear,
					short_time);
			else
				return date.ToString (
					Catalog.GetString ("MMMM d, h:mm tt"));
		} else
			return date.ToString (Catalog.GetString ("MMMM d yyyy, h:mm tt"));
	}
	
	/// <summary>
	/// Create a new task using "New Task NNN" format
	/// </summary>
	void OnNewTask (object sender, EventArgs args)
	{
		int new_num = manager.Tasks.IterNChildren ();
		string summary;
		
		while (true) {
			summary = String.Format (Catalog.GetString ("New Task {0}"), 
						    ++new_num);
			if (manager.Find (summary) == null)
				break;
		}
		
		try {
			expecting_newly_created_task = true;
			manager.Create (summary);
		} catch (Exception e) {
			expecting_newly_created_task = false;
			Logger.Error ("Could not create a new task with summary: {0}:{1}", summary, e.Message);
		}
	}
	
	void OnOpenTask (object sender, EventArgs args)
	{
		Task task = GetSelectedTask ();
		if (task == null)
			return;
		
		Logger.Debug ("FIXME: Implement TaskListWindow.OnOpenTask");
	}
	
	void OnDeleteTask (object sender, EventArgs args)
	{
		Task task = GetSelectedTask ();
		if (task == null)
			return;
		
		ShowDeletionDialog (task);
	}
	
	void OnShowHelp (object sender, EventArgs args)
	{
		GuiUtils.ShowHelp ("tomboy.xml", "tasks", Screen, this);
	}
	
	void OnCloseWindow (object sender, EventArgs args)
	{
		// Disconnect external signal handlers to prevent bloweup
		manager.TaskAdded -= OnTaskAdded;
		manager.TaskDeleted -= OnTaskDeleted;
		manager.TaskStatusChanged -= OnTaskStatusChanged;
		
		// The following code has to be done for the MenuBar to
		// appear properly the next time this window is opened.
		if (menu_bar != null) {
			content_vbox.Remove (menu_bar);
			ActionManager am = Tomboy.Tomboy.ActionManager;
			am ["NewTaskAction"].Activated -= OnNewTask;
			am ["OpenTaskAction"].Activated -= OnOpenTask;
			am ["CloseTaskListWindowAction"].Activated -= OnCloseWindow;
			am ["DeleteTaskAction"].Activated -= OnDeleteTask;
			am ["ShowTaskHelpAction"].Activated -= OnShowHelp;
		}
		
		Tomboy.Tomboy.ActionManager.UI.RemoveActionGroup (action_group);
		Tomboy.Tomboy.ActionManager.UI.RemoveUi (menubar_ui);
		
		Hide ();
		Destroy ();
		instance = null;
	}
	
	void OnDelete (object sender, Gtk.DeleteEventArgs args)
	{
		OnCloseWindow (sender, EventArgs.Empty);
		args.RetVal = true;
	}
	
	void OnKeyPressed (object sender, Gtk.KeyPressEventArgs args)
	{
		switch (args.Event.Key) {
		case Gdk.Key.Escape:
			// Allow Escape to close the window
			OnCloseWindow (this, EventArgs.Empty);
			break;
		case Gdk.Key.Menu:
			// Pop up the context menu if a note is selected
			Task task = GetSelectedTask ();
			if (task != null)
				PopupContextMenuAtLocation (0, 0);

			break;
		}
	}
	
//	protected override void OnShown ()
//	{
//		base.OnShown ();
//	}
	
	int CompareSummaries (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
	{
		Task task_a = model.GetValue (a, 0) as Task;
		Task task_b = model.GetValue (b, 0) as Task;
		
		if (task_a == null || task_b == null)
			return -1;
		
		return task_a.Summary.CompareTo (task_b.Summary);
	}
	
	void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
	{
		Gtk.TreeIter iter;
		if (!store_sort.GetIter (out iter, args.Path)) 
			return;

		Task task = store_sort.GetValue (iter, 0) as Task;
		
		Logger.Debug ("FIXME: Implement TaskListWindow.OnRowActivated: {0}", task.Summary);
	}
	
	void ShowDeletionDialog (Task task)
	{
		HIGMessageDialog dialog = 
			new HIGMessageDialog (
				this,
				Gtk.DialogFlags.DestroyWithParent,
				Gtk.MessageType.Question,
				Gtk.ButtonsType.None,
				Catalog.GetString ("Really delete this task?"),
				Catalog.GetString ("If you delete a task it is " +
						   "permanently lost."));

		Gtk.Button button;

		button = new Gtk.Button (Gtk.Stock.Cancel);
		button.CanDefault = true;
		button.Show ();
		dialog.AddActionWidget (button, Gtk.ResponseType.Cancel);
		dialog.DefaultResponse = Gtk.ResponseType.Cancel;

		button = new Gtk.Button (Gtk.Stock.Delete);
		button.CanDefault = true;
		button.Show ();
		dialog.AddActionWidget (button, 666);

		int result = dialog.Run ();
		if (result == 666) {
			task.Manager.Delete (task);
		}

		dialog.Destroy();
	}

	void OnTaskToggled (object sender, Gtk.ToggledArgs args)
	{
		Gtk.TreePath path = new Gtk.TreePath (args.Path);
		Gtk.TreeIter iter;
		if (store_sort.GetIter (out iter, path) == false)
			return;
		
		Task task = store_sort.GetValue (iter, 0) as Task;
		if (task == null)
			return;
		
		if (task.IsComplete)
			task.ReOpen ();
		else
			task.Complete ();
	}
	
	void OnTaskSummaryEdited (object sender, Gtk.EditedArgs args)
	{
Logger.Debug ("TaskListWindow.OnTaskSummaryEdited");
		
		Gtk.TreeIter iter;
		Gtk.TreePath path = new TreePath (args.Path);
		if (store_sort.GetIter (out iter, path) == false)
			return;
		
		Task task = store_sort.GetValue (iter, 0) as Task;
		task.Summary = args.NewText;
	}
	
	void OnDueDateEdited (Gtk.Extras.CellRendererDate renderer, string path)
	{
		Logger.Debug ("OnDueDateEdited");
		
		Gtk.TreeIter iter;
		Gtk.TreePath tree_path = new TreePath (path);
		if (store_sort.GetIter (out iter, tree_path) == false)
			return;
		
		Task task = store_sort.GetValue (iter, 0) as Task;
		task.DueDate = renderer.Date;
	}
	
	void OnTaskPriorityEdited (object sender, Gtk.EditedArgs args)
	{
		Gtk.TreeIter iter;
		Gtk.TreePath path = new TreePath (args.Path);
		if (store_sort.GetIter (out iter, path) == false)
			return;
		
		Task task = store_sort.GetValue (iter, 0) as Task;
		if (args.NewText.CompareTo (Catalog.GetString ("Low")) == 0)
			task.Priority = TaskPriority.Low;
		else if (args.NewText.CompareTo (Catalog.GetString ("High")) == 0)
			task.Priority = TaskPriority.High;
		else
			task.Priority = TaskPriority.Normal;
	}
	
	void OnTaskAdded (TaskManager manager, Task task)
	{
		if (expecting_newly_created_task) {
			// A user just created this task inside this window
			expecting_newly_created_task = false;
			
			SelectTask (task);
		}
		
		int cnt = manager.Tasks.IterNChildren ();
		UpdateTaskCount (cnt);
	}
	
	void OnTaskDeleted (TaskManager manager, Task task)
	{
		int cnt = manager.Tasks.IterNChildren ();
		UpdateTaskCount (cnt);
	}
	
	void OnTaskStatusChanged (Task task)
	{
		// FIXME: Eventually update the status bar to include the number of completed notes
	}
	
	void SelectTask (Task task)
	{
		// FIXME: YUCK!  TaskListWindow.SelectTask is pretty ugly (brute force).  Is there a better way to do this?
		Gtk.TreeIter iter;
		
		if (store_sort.IterChildren (out iter) == false)
			return;
		
		do {
			Task iter_task = store_sort.GetValue (iter, 0) as Task;
			if (iter_task == task) {
				// Found it!
				tree.Selection.SelectIter (iter);
				break;
			}
		} while (store_sort.IterNext (ref iter));
	}
}

