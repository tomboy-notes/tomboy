using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Tomboy
{
	public class XKeybinder : IKeybinder
	{
		[DllImport("libtomboy")]
		static extern void tomboy_keybinder_init ();

		[DllImport("libtomboy")]
		static extern void tomboy_keybinder_bind (string keystring,
			                BindkeyHandler handler);

		[DllImport("libtomboy")]
		static extern void tomboy_keybinder_unbind (string keystring,
			                BindkeyHandler handler);

		public delegate void BindkeyHandler (string key, IntPtr user_data);

		List<Binding> bindings;
		BindkeyHandler key_handler;

		struct Binding {
			internal string       keystring;
			internal EventHandler handler;
		}

		public XKeybinder ()
		{
			bindings = new List<Binding> ();
			key_handler = new BindkeyHandler (KeybindingPressed);

			tomboy_keybinder_init ();
		}

		void KeybindingPressed (string keystring, IntPtr user_data)
		{
			foreach (Binding bind in bindings) {
				if (bind.keystring == keystring) {
					bind.handler (this, new EventArgs ());
				}
			}
		}

		public void Bind (string       keystring,
		                  EventHandler handler)
		{
			Binding bind = new Binding ();
			bind.keystring = keystring;
			bind.handler = handler;
			bindings.Add (bind);

			tomboy_keybinder_bind (bind.keystring, key_handler);
		}

		public void Unbind (string keystring)
		{
			foreach (Binding bind in bindings) {
				if (bind.keystring == keystring) {
					tomboy_keybinder_unbind (bind.keystring,
					                         key_handler);

					bindings.Remove (bind);
					break;
				}
			}
		}

		public virtual void UnbindAll ()
		{
			foreach (Binding bind in bindings) {
				tomboy_keybinder_unbind (bind.keystring, key_handler);
			}

			bindings.Clear ();
		}

	[DllImport("libtomboy")]
		static extern bool egg_accelerator_parse_virtual (string keystring,
			                out uint keysym,
			                out uint virtual_mods);

		[DllImport("libtomboy")]
		static extern void egg_keymap_resolve_virtual_modifiers (
			        IntPtr keymap,
			        uint virtual_mods,
			        out Gdk.ModifierType real_mods);

		public bool GetAccelKeys (string               gconf_path,
		                                 out uint             keyval,
		                                 out Gdk.ModifierType mods)
		{
			keyval = 0;
			mods = 0;

			try {
				string binding = (string) Preferences.Get (gconf_path);
				if (binding == null ||
				                binding == String.Empty ||
				                binding == "disabled")
					return false;

				uint virtual_mods = 0;
				if (!egg_accelerator_parse_virtual (binding,
				                                    out keyval,
				                                    out virtual_mods))
					return false;

				Gdk.Keymap keymap = Gdk.Keymap.Default;
				egg_keymap_resolve_virtual_modifiers (keymap.Handle,
				                                      virtual_mods,
				                                      out mods);

				return true;
			} catch {
			return false;
		}
	}
	}
}
