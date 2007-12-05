
using System;
using System.Collections.Generic;
using System.IO;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Tasks
{
	public delegate void TasksChangedHandler (TaskManager manager, Task task);

	public class TaskManager 
	{
	#region Private Members
		string tasks_dir;
		string archive_dir;
		
		Gtk.ListStore tasks;
		Dictionary<string, Gtk.TreeIter> task_iters;
	#endregion // Private Members

	#region Constructors
		public TaskManager (string directory) : 
			this (directory, Path.Combine (directory, "Archive")) 
		{
		}

		public TaskManager (string directory, string archive_directory) 
		{
			Logger.Log ("TaskManager created with path \"{0}\".", directory);

			tasks_dir = directory;
			archive_dir = archive_directory;
			
			tasks = new Gtk.ListStore (typeof (Task));
			task_iters = new Dictionary<string,Gtk.TreeIter> ();

			bool first_run = FirstRun ();
			CreateTasksDir ();

			if (first_run) {
				// First run. Create "Learn About Tomboy" task.
				CreateStartTasks ();
			} else {
				LoadTasks ();
			}
		}
	#endregion // Constructors

	#region Public Properties
		public Gtk.ListStore Tasks
		{
			get { return tasks; }
		}
	#endregion // Public Properties

	#region Public Methods

		/// <summary>
		/// Gets a Gtk.TreePath for the passed task.
		/// </summary>
		public Gtk.TreePath GetTreePathFromTask(Task task)
		{
			if (!task_iters.ContainsKey(task.Uri))
				throw new Exception("Cannot find task in tree");

			return tasks.GetPath(task_iters[task.Uri]);
		}

		/// <summary>
		/// Delete the specified task from the system.
		/// </summary>
		public void Delete (Task task) 
		{
			if (File.Exists (task.FilePath)) {
				if (archive_dir != null) {
					// FIXME: Instead of moving deleted tasks into the archive dir, move them into a backup dir 
					if (!Directory.Exists (archive_dir))
						Directory.CreateDirectory (archive_dir);

					string archive_path = 
						Path.Combine (archive_dir, 
							      Path.GetFileName (task.FilePath));
					if (File.Exists (archive_path))
						File.Delete (archive_path);

					File.Move (task.FilePath, archive_path);
				} else 
					File.Delete (task.FilePath);
			}

			string uri = task.Uri;
			if (task_iters.ContainsKey (uri)) {
				Gtk.TreeIter iter = task_iters [uri];
				tasks.Remove (ref iter);
				task_iters.Remove (uri);
				task.Delete ();
			}

			Logger.Log ("Deleting task '{0}'.", task.Summary);

			if (TaskDeleted != null)
				TaskDeleted (this, task);
		}

		/// <summary>
		/// Create a new Task with the specified summary.
		/// <param name="summary">The summary of the new task.  This may be an
		/// empty string but should not be null.</param>
		/// </summary>
		public Task Create (string summary) 
		{
			if (summary == null)
				throw new ArgumentNullException ("summary", "You cannot create the a new task with a null summary.  Use String.Empty instead.");

//			if (summary.Length > 0 && Find (summary) != null)
//				throw new Exception ("A task with this summary already exists");

			string filename = MakeNewFileName ();
			
			Task new_task = Task.CreateNewTask (summary, filename, this);
			new_task.Renamed += OnTaskRenamed;
			new_task.Saved += OnTaskSaved;
			new_task.StatusChanged += OnTaskStatusChanged;

			Gtk.TreeIter iter = tasks.Append ();
			tasks.SetValue (iter, 0, new_task);
			task_iters [new_task.Uri] = iter;
			
			if (TaskAdded != null)
				TaskAdded (this, new_task);
			
			return new_task;
		}

		/// <summary>
		/// Find an existing task with the specified summary.
		/// <param name="summary">The summary string to search for.</param>
		/// </summary>
		public Task Find (string summary) 
		{
			Gtk.TreeIter iter;
			if (tasks.GetIterFirst (out iter)) {
				do {
					Task task = tasks.GetValue (iter, 0) as Task;
					if (task.Summary.ToLower () == summary.ToLower ())
						return task;
				} while (tasks.IterNext (ref iter));
			}
			
			return null;
		}

		public Task FindByUri (string uri)
		{
			if (task_iters.ContainsKey (uri)) {
				Gtk.TreeIter iter = task_iters [uri];
				Task task = tasks.GetValue (iter, 0) as Task;
				return task;
			}
			
			return null;
		}
		
		/// <summary>
		/// Save any task that hasn't been saved already.
		/// This should only be called by TasksApplicationAddin.Shutdown ().
		/// </summary>
		public void Shutdown ()
		{
			Logger.Log ("Saving unsaved tasks...");
			
			Gtk.TreeIter iter;
			if (tasks.GetIterFirst (out iter)) {
				do {
					Task task = tasks.GetValue (iter, 0) as Task;
					task.Save ();
				} while (tasks.IterNext (ref iter));
			}
		}
		
		/// <summary>
		/// Return a list of tasks whose origin note is the note specified.
		/// </summary>
		public List<Task> GetTasksForNote (Note note)
		{
			List<Task> list = new List<Task> ();
			
			Gtk.TreeIter iter;
			if (tasks.GetIterFirst (out iter)) {
				do {
					Task task = tasks.GetValue (iter, 0) as Task;
					if (task.OriginNoteUri != null &&
							task.OriginNoteUri.CompareTo (note.Uri) == 0)
						list.Add (task);
				} while (tasks.IterNext (ref iter));
			}
			
			return list;
		}
	#endregion // Public Methods

	#region Events
		public static event TasksChangedHandler TaskDeleted;
		public static event TasksChangedHandler TaskAdded;
		public static event TaskRenamedHandler TaskRenamed;
		public static event TaskSavedHandler TaskSaved;
		public static event TaskStatusChangedHandler TaskStatusChanged;
	#endregion // Events

	#region Private Methods
		/// <summary>
		/// Create the notes directory if it doesn't exist yet.
		/// </summary>
		void CreateTasksDir ()
		{
			if (!DirectoryExists (tasks_dir)) {
				// First run. Create storage directory.
				CreateDirectory (tasks_dir);
			}
		}

		// For overriding in test methods.
		protected virtual bool DirectoryExists (string directory)
		{
			return Directory.Exists (directory);
		}

		// For overriding in test methods.
		protected virtual DirectoryInfo CreateDirectory (string directory)
		{
			return Directory.CreateDirectory (directory);
		}

		protected virtual bool FirstRun ()
		{
			return !DirectoryExists (tasks_dir);
		}
		
		/// <summary>
		/// Create a "Learn About Tomboy" task
		/// </summary>
		protected virtual void CreateStartTasks () 
		{
			try {
				Task first_task = Create (Catalog.GetString ("Learn About Tomboy"));
				first_task.Details =
					Catalog.GetString (
						"Click on the Tomboy icon in your panel and select " +
						"\"Start Here\".  You'll see more instructions on how " +
						"to use Tomboy inside the \"Start Here\" note.\n\n" +
						"When you've opened the \"Start Here\" note, mark this " +
						"task as being complete.");
				first_task.QueueSave (true);
			} catch (Exception e) {
				Logger.Warn ("Error creating the \"Learn About Tomboy\" task: {0}\n{1}",
						e.Message, e.StackTrace);
			}
		}

		protected virtual void LoadTasks ()
		{
			string [] files = Directory.GetFiles (tasks_dir, "*.task");

			foreach (string file_path in files) {
				try {
					Task task = Task.Load (file_path, this);
					if (task != null) {
						task.Renamed += OnTaskRenamed;
						task.Saved += OnTaskSaved;
						task.StatusChanged += OnTaskStatusChanged;
						
						Gtk.TreeIter iter = tasks.Append ();
						tasks.SetValue (iter, 0, task);
						task_iters [task.Uri] = iter;
					}
				} catch (System.Xml.XmlException e) {
					Logger.Log ("Error parsing task XML, skipping \"{0}\": {1}",
						    file_path,
						    e.Message);
				}
			}
			
			Logger.Debug ("{0} tasks loaded", task_iters.Count);
		}

		string MakeNewFileName ()
		{
			Guid guid = Guid.NewGuid ();
			return Path.Combine (tasks_dir, guid.ToString () + ".task");
		}

		void EmitRowChangedForTask (Task task)
		{
			if (task_iters.ContainsKey (task.Uri)) {
				Gtk.TreeIter iter = task_iters [task.Uri];
				
				tasks.EmitRowChanged (tasks.GetPath (iter), iter);
			}
		}

	#endregion // Private Methods

	#region Event Handlers
		void OnTaskRenamed (Task task, string old_summary)
		{
			EmitRowChangedForTask (task);

			if (TaskRenamed != null)
				TaskRenamed (task, old_summary);
		}
		
		void OnTaskSaved (Task task)
		{
			EmitRowChangedForTask (task);

			if (TaskSaved != null)
				TaskSaved (task);
		}
		
		void OnTaskStatusChanged (Task task)
		{
			EmitRowChangedForTask (task);
			
			if (TaskStatusChanged != null)
				TaskStatusChanged (task);
		}

	#endregion // Event Handlers
	}
}
