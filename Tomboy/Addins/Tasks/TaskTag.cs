
using System;
using Tomboy;

namespace Tomboy.Tasks
{
	public class TaskTag : DynamicNoteTag
	{
		static Gdk.Pixbuf todo_icon;
		static Gdk.Pixbuf done_icon;
		
		const string PROP_URI = "uri";
		const string PROP_CREATION_DATE = "creation-date";
		const string PROP_LAST_CHANGE_DATE = "last-change-date";
		const string PROP_DUE_DATE = "due-date";
		const string PROP_COMPLETION_DATE = "completion-date";
		const string PROP_PRIORITY = "priority";

		static TaskTag ()
		{
			// Load the pixbufs directly from the assembly's resources
			done_icon = new Gdk.Pixbuf (null, "checkbox-done.png");
			todo_icon = new Gdk.Pixbuf (null, "checkbox-todo.png");
		}

		public TaskTag () : base ()
		{
		}

		public override void Initialize (string element_name)
		{
			base.Initialize (element_name);

//   Underline = Pango.Underline.Single;
			Editable = true;
			CanActivate = false;
			CanGrow = true;
			CanSpellCheck = true;
			CanSplit = false;

			UpdateStatus ();
		}

		public void UpdateStatus ()
		{
			if (CompletionDate != DateTime.MinValue) {
				Foreground = "green"; // This is temporary so we can view state
				Strikethrough = true;
			} else {
				Foreground = "red"; // This is temporary so we can view state
				Strikethrough = false;
			}
		}

		public string Uri
		{
			get {
				return (string) Attributes [PROP_URI];
			}
			set {
				Attributes [PROP_URI] = value;
			}
		}
		
		public DateTime CreationDate
		{
			get {
				string date_str =  (string) Attributes [PROP_CREATION_DATE];
				if (date_str == null)
					return DateTime.MinValue;
				else
					return DateTime.Parse (date_str);
			}
			set {
				Attributes [PROP_CREATION_DATE] = ((DateTime)value).ToString ();
			}
		}
		
		public DateTime LastChangeDate
		{
			get {
				string date_str =  (string) Attributes [PROP_LAST_CHANGE_DATE];
				if (date_str == null)
					return DateTime.MinValue;
				else
					return DateTime.Parse (date_str);
			}
			set {
				Attributes [PROP_LAST_CHANGE_DATE] = ((DateTime)value).ToString ();
			}
		}
		
		public DateTime DueDate
		{
			get {
				string date_str =  (string) Attributes [PROP_DUE_DATE];
				if (date_str == null)
					return DateTime.MinValue;
				else
					return DateTime.Parse (date_str);
			}
			set {
				Attributes [PROP_DUE_DATE] = ((DateTime)value).ToString ();
			}
		}

		public DateTime CompletionDate
		{
			get {
				string date_str = (string) Attributes [PROP_COMPLETION_DATE];
				if (date_str == null)
					return DateTime.MinValue;
				else
					return DateTime.Parse (date_str);
			}
			set {
				Attributes [PROP_COMPLETION_DATE] = ((DateTime)value).ToString ();
			}
		}

		public TaskPriority TaskPriority
		{
			get {
				TaskPriority priority;
				string prop = (string) Attributes [PROP_COMPLETION_DATE];
				switch (prop) {
					case "low":
						priority = TaskPriority.Low;
						break;
					case "normal":
						priority = TaskPriority.Normal;
						break;
					case "high":
						priority = TaskPriority.High;
						break;
					default:
						priority = TaskPriority.Undefined;
						break;
				}
				
				return priority;
			}
			set {
				string prop;
				switch (value) {
					case TaskPriority.Low:
						prop = "low";
						break;
					case TaskPriority.Normal:
						prop = "normal";
						break;
					case TaskPriority.High:
						prop = "high";
						break;
					default:
						prop = "undefined";
						break;
				}
				
				Attributes [PROP_PRIORITY] = prop;
			}
		}

//		public bool Complete
//		{
//			get {
//				bool completed = false;
//				string completed_str = (string) Attributes ["completed"];
//				if (completed_str != null)
//					completed = Boolean.Parse (completed_str);
//
//				return completed;
//			}
//			set {
//				Attributes ["completed"] = value.ToString ();
//				UpdateStatus ();
//			}
//		}
		
		/*
		  public override Gdk.Pixbuf Image
		  {
		   get
		   {
		    if (Uri == null)
		     return todo_icon;

		    TaskManager task_mgr = TasksApplicationAddin.DefaultTaskManager;
		    if (task_mgr != null) {
		     Task task = task_mgr.FindByUri (Uri);
		     if (task != null && task.IsComplete)
		      return done_icon;
		    }

		    return todo_icon;
		   }
		  }
		*/
	}
}
