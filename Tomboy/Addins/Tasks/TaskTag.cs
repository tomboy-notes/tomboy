
using System;
using Tomboy;

namespace Tomboy.Tasks
{
	public class TaskTag : DynamicNoteTag
	{
		static Gdk.Pixbuf todo_icon;
		static Gdk.Pixbuf done_icon;
		
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

//			Underline = Pango.Underline.Single;
			Editable = true;
			CanActivate = false;
			CanGrow = true;
			CanSpellCheck = true;
			CanSplit = false;
			
			UpdateStatus ();
		}
		
		public void UpdateStatus ()
		{
			if (Completed) {
				Foreground = "green"; // This is temporary so we can view state
				Strikethrough = true;
			} else {
				Foreground = "red"; // This is temporary so we can view state
				Strikethrough = false;
			}
		}
		
		public string Uri
		{
			get { return (string) Attributes ["uri"]; }
			set { Attributes ["uri"] = value; }
		}
		
		public bool Completed
		{
			get {
				bool completed = false;
				string completed_str = (string) Attributes ["completed"];
				if (completed_str != null)
					completed = Boolean.Parse (completed_str);

				return completed;
			}
			set {
				Attributes ["completed"] = value.ToString ();
				UpdateStatus ();
			}
		}

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
