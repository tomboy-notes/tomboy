using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using ManagedWinapi;

namespace Tomboy
{
	public class WindowsKeybinder : IKeybinder
	{
		#region Private Members

		private Dictionary<string, Hotkey> bindings;

		#endregion

		#region Constructor

		public WindowsKeybinder ()
		{
			bindings = new Dictionary<string, Hotkey> ();
		}

		#endregion

		#region IKeybinder Members

		public void Bind (string keystring, EventHandler handler)
		{
			Hotkey hotkey = ParseHotkey (keystring);
			if (hotkey == null)
				return;

			hotkey.HotkeyPressed += handler;
			hotkey.Enabled = true;

			bindings [keystring] = hotkey;
		}

		public void Unbind (string keystring)
		{
			Hotkey hotkey;
			if (bindings.TryGetValue (keystring, out hotkey)) {
				hotkey.Enabled = false;
				bindings.Remove (keystring);
			}
		}

		public void UnbindAll ()
		{
			foreach (string keystring in bindings.Keys)
				Unbind (keystring);
		}

		public bool GetAccelKeys (string prefs_path, out uint keyval, out Gdk.ModifierType mods)
		{
			keyval = 0;
			mods = 0;

			try {
				string keystring = (string) Preferences.Get (prefs_path);

				if (string.IsNullOrEmpty (keystring) ||
					keystring == "disabled")
					return false;

				Hotkey hotkey = ParseHotkey (keystring);
				if (hotkey == null)
					return false;

				keyval = (uint) (Gdk.Key) Enum.Parse (
					typeof (Gdk.Key),
					hotkey.KeyCode.ToString ());
				if (hotkey.Alt)
					mods |= Gdk.ModifierType.MetaMask;
				if (hotkey.Ctrl)
					mods |= Gdk.ModifierType.ControlMask;
				if (hotkey.WindowsKey)
					mods |= Gdk.ModifierType.SuperMask;
				if (hotkey.Shift)
					mods |= Gdk.ModifierType.ShiftMask;

				return true;
			} catch {
				return false;
			}
		}

		#endregion

		#region Private Methods

		private Hotkey ParseHotkey (string keystring)
		{
			keystring = keystring.ToUpper ();

			// TODO: Really, is this the same behavior as XKeybinder?
			if (string.IsNullOrEmpty (keystring) ||
				bindings.ContainsKey (keystring))
				return null;

			Regex bindingExp = new Regex (
				"^(<((ALT)|(CTRL)|(SUPER)|(WIN)|(SHIFT))>)+((F(([1-9])|(1[0-2])))|[A-Z])$");

			if (bindingExp.Matches (keystring).Count == 0)
				return null;

			Hotkey hotkey = new Hotkey ();
			hotkey.KeyCode = (Keys) Enum.Parse (
				typeof (Keys),
				keystring.Substring (keystring.LastIndexOf (">") + 1));
			hotkey.Alt = keystring.Contains ("<ALT>");
			hotkey.Ctrl = keystring.Contains ("<CTRL>");
			hotkey.Shift = keystring.Contains ("<SHIFT>");
			hotkey.WindowsKey = keystring.Contains ("<SUPER>") ||
				keystring.Contains ("<WIN>");

			return hotkey;
		}

		#endregion
	}
}
