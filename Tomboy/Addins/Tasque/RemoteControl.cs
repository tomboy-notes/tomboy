// RemoteControl.cs created with MonoDevelop
// User: boyd at 9:41 PM 2/14/2008

using System;
using System.Collections.Generic;

using DBus;
using org.freedesktop.DBus;

namespace Tasque
{
	[Interface ("org.gnome.Tasque.RemoteControl")]
	public class RemoteControl : MarshalByRefObject
	{
		public RemoteControl()
		{
		}
		
		public string CreateTask (string categoryName, string taskName,
								  bool enterEditMode)
		{
			return null;
		}
		
		public string[] GetCategoryNames ()
		{
			return null;
		}
		
		public void ShowTasks ()
		{
		}
	}
}
