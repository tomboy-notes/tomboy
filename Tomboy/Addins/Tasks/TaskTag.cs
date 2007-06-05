
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

	//		Underline = Pango.Underline.Single;
			Foreground = "green"; // This is temporary just so during debugging we can visualize the tag
			Editable = true;
			CanActivate = true;
			CanGrow = true;
			CanSpellCheck = true;
			CanSplit = false;
		}

		protected override bool OnActivate (NoteEditor editor, Gtk.TextIter start, Gtk.TextIter end)
		{
			string uri = Attributes ["uri"] as string;
			if (uri == null)
				return false;
			
			Logger.Debug ("FIXME: Implement Tasks.OnActivate: {0}", uri);
			return true;
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
			}
		}
	}
}
