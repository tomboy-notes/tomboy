using System;

namespace Tomboy
{
	public interface IKeybinder
	{
		void Bind (string keystring, EventHandler handler);
		void Unbind (string keystring);
		void UnbindAll ();
		bool GetAccelKeys (string prefs_path, out uint keyval, out Gdk.ModifierType mods);
	}

	public class NullKeybinder : IKeybinder
	{
		#region IKeybinder implementation 
		
		public void Bind (string keystring, EventHandler handler)
		{
			// Do nothing
		}
		
		public void Unbind (string keystring)
		{
			// Do nothing
		}
		
		public void UnbindAll ()
		{
			// Do nothing
		}
		
		public bool GetAccelKeys (string prefs_path, out uint keyval, out Gdk.ModifierType mods)
		{
			keyval = 0;
			mods = Gdk.ModifierType.None;
			return false;
		}
		
		#endregion
	}
}
