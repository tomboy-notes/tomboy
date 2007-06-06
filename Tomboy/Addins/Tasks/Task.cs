
using System;
using System.IO;
using Tomboy;

namespace Tomboy.Tasks
{
	public delegate void TaskRenamedHandler (Task task, string old_title);
	public delegate void TaskSavedHandler (Task task);
	public delegate void TaskStatusChangedHandler (Task task);

	public enum TaskPriority : uint
	{
		Undefined = 0,
		Low,
		Normal,
		High
	};

	/// <summary>
	/// Just as Note is to a *.note file, Task is to a *.task file
	/// </summary>
	public class Task
	{
		#region Fields
		string filepath;
		TaskManager manager;
		InterruptableTimeout save_timeout;
		bool save_needed;
		TaskData data;
		#endregion // Fields

		#region Constructors
		/// <summary>
		/// Construct a task object.
		/// </summary>
		Task (TaskData data, string filepath, TaskManager manager)
		{
			this.data = data;
			this.filepath = filepath;
			this.manager = manager;
			
			save_timeout = new InterruptableTimeout ();
			save_timeout.Timeout += SaveTimeout;

			// If an OriginNoteUri exists, make sure the note
			// actually exists.  If it doesn't, clean it up
			string origin_note_uri = OriginNoteUri;
			if (origin_note_uri != null && origin_note_uri != string.Empty) {
				Note note =
					Tomboy.DefaultNoteManager.FindByUri (origin_note_uri);
				if (note == null)
					OriginNoteUri = String.Empty;
			}
		}
		#endregion // Constructors
		
		#region Public Properties
		public string Uri
		{
			get { return data.Uri; }
		}
		
		public string FilePath
		{
			get { return filepath; }
		}
		
		public string Summary
		{
			get { return data.Summary; }
			set {
				if (data.Summary != value) {
					string old_summary = data.Summary;
					data.Summary = value;
					
					if (Renamed != null)
						Renamed (this, old_summary);
					
					QueueSave (true);
				}
			}
		}
		
		public string Details
		{
			get { return data.Details; }
			set {
				if (data.Details != value) {
					data.Details = value;
					
					QueueSave (true);
				}
			}
		}
		
		public TaskData Data
		{
			get { return data; }
		}

		public DateTime CreateDate 
		{
			get { return data.CreateDate; }
		}

		public DateTime LastChangeDate 
		{
			get { return data.LastChangeDate; }
		}
		
		public DateTime CompletionDate
		{
			get { return data.CompletionDate; }
		}
		
		public DateTime DueDate
		{
			get { return data.DueDate; }
			set {
				if (data.DueDate != value) {
					data.DueDate = value;
					
					QueueSave (true);
				}
			}
		}
		
		public TaskPriority Priority
		{
			get { return data.Priority; }
			set {
				if (data.Priority != value) {
					data.Priority = value;
					
					QueueSave (true);
				}
			}
		}
		
		public string OriginNoteUri
		{
			get { return data.OriginNoteUri; }
			set {
				if (data.OriginNoteUri != value) {
					data.OriginNoteUri = value;
					
					QueueSave (true);
				}
			}
		}

		public TaskManager Manager
		{
			get { return manager; }
			set { manager = value; }
		}
		
		public bool IsComplete
		{
			get { return data.CompletionDate > DateTime.MinValue; }
		}

		#endregion // Public Properties

		#region Public Methods
		public static Task CreateNewTask (string summary,
						  string filepath,
						  TaskManager manager)
		{
			TaskData data = new TaskData (UrlFromPath (filepath));
			data.Summary = summary;
			data.CreateDate = DateTime.Now;
			data.LastChangeDate = data.CreateDate;
			return new Task (data, filepath, manager);
		}

		public static Task CreateExistingTask (TaskData data,
						       string filepath,
						       TaskManager manager)
		{
			if (data.CreateDate == DateTime.MinValue)
				data.CreateDate = File.GetCreationTime (filepath);
			if (data.LastChangeDate == DateTime.MinValue)
				data.LastChangeDate = File.GetLastWriteTime (filepath);
			return new Task (data, filepath, manager);
		}

		/// <summary>
		/// Load from an existing Task...
		/// </summary>
		public static Task Load (string read_file, TaskManager manager)
		{
			TaskData data = TaskArchiver.Read (read_file, UrlFromPath (read_file));
			Task task = CreateExistingTask (data, read_file, manager);

			return task;
		}
		
		/// <summary>
		/// Mark a task as complete
		/// </summary>
		public void Complete ()
		{
			// If it's already marked complete, don't do anything
			if (IsComplete)
				return;
			
			data.CompletionDate = DateTime.Now;

			if (StatusChanged != null)
				StatusChanged (this);
			
			QueueSave (true);
		}
		
		/// <summary>
		/// Mark a task as not complete (re-open it)
		/// </summary>
		public void ReOpen ()
		{
			// If it's already open, don't do anything
			if (IsComplete == false)
				return;
			
			data.CompletionDate = DateTime.MinValue;
			
			if (StatusChanged != null)
				StatusChanged (this);
			
			QueueSave (true);
		}

		public void Delete ()
		{
			save_timeout.Cancel ();
		}

		public void Save () 
		{
			// Do nothing if we don't need to save.  Avoids unneccessary saves
			// e.g on forced quit when we call save for every task.
			if (!save_needed)
				return;

			Logger.Log ("Saving task '{0}'...", data.Summary);

			TaskArchiver.Write (filepath, data);
			
			if (Saved != null)
				Saved (this);
		}
		
		/// <summary>
		/// Set a 4 second timeout to execute the save.  Possibly
		/// invalidate the text, which causes a re-serialize when the
		/// timeout is called...
		/// </summary>
		public void QueueSave (bool content_changed)
		{
			DebugSave ("Got QueueSave");

			// Replace the existing save timeout.  Wait 4 seconds
			// before saving...
			save_timeout.Reset (4000);
			save_needed = true;

			if (content_changed) {
				data.LastChangeDate = DateTime.Now;
			}
		}

		#endregion // Public Methods

		#region Events
		public event TaskRenamedHandler Renamed;
		public event TaskSavedHandler Saved;
		
		/// <summary>
		/// Indicated when a task is marked as completed
		/// or when a task is re-opened.
		/// </summary>
		public event TaskStatusChangedHandler StatusChanged;
		#endregion // Events

		#region Private Methods
		[System.Diagnostics.Conditional ("DEBUG_SAVE")]
		static void DebugSave (string format, params object[] args)
		{
			Console.WriteLine (format, args);
		}

		static string UrlFromPath (string filepath)
		{
			return "task://tomboy/" +
				Path.GetFileNameWithoutExtension (filepath);
		}

		// Save timeout to avoid constanly resaving.  Called every 4 seconds.
		void SaveTimeout (object sender, EventArgs args)
		{
			try {
				Save ();
				save_needed = false;
			} catch (Exception e) {
				// FIXME: Present a nice dialog here that interprets the
				// error message correctly.
				Logger.Log ("Error while saving task: {0}", e);
			}
		}
		#endregion // Private Methods

		#region Event Handlers
		#endregion // Event Handlers
	}
}
