
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Unix;

namespace Tomboy
{
	public class XKeybinder 
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

		ArrayList      bindings;
		BindkeyHandler key_handler;

		struct Binding {
			internal string       keystring;
			internal EventHandler handler;
		}

		public XKeybinder ()
			: base ()
		{
			bindings = new ArrayList ();
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
	}

	public class GConfXKeybinder : XKeybinder
	{
		GConf.Client client;
		ArrayList bindings;
		
		public GConfXKeybinder ()
		{
			client = new GConf.Client ();
			bindings = new ArrayList ();
		}

		public void Bind (string       gconf_path, 
				  string       default_binding, 
				  EventHandler handler)
		{
			try {
				Binding binding = new Binding (gconf_path, 
							       default_binding,
							       handler,
							       this);
				bindings.Add (binding);
			} catch (Exception e) {
				Console.WriteLine ("Error Adding global keybinding:");
				Console.WriteLine (e);
			}
		}

		public override void UnbindAll ()
		{
			try {
				bindings.Clear ();
				base.UnbindAll ();
			} catch (Exception e) {
				Console.WriteLine ("Error Removing global keybinding:");
				Console.WriteLine (e);
			}
		}

		class Binding 
		{
			public string   gconf_path;
			public string   key_sequence;
			EventHandler    handler;
			GConfXKeybinder parent;

			public Binding (string          gconf_path, 
					string          default_binding,
					EventHandler    handler,
					GConfXKeybinder parent)
			{
				this.gconf_path = gconf_path;
				this.key_sequence = default_binding;
				this.handler = handler;
				this.parent = parent;

				try {
					key_sequence = (string) parent.client.Get (gconf_path);
				} catch (Exception e) {
					Console.WriteLine ("GConf key '{0}' does not exist, using default.", 
							   gconf_path);
				}

				SetBinding ();

				parent.client.AddNotify (
					gconf_path, 
					new GConf.NotifyEventHandler (BindingChanged));
			}

			void BindingChanged (object sender, GConf.NotifyEventArgs args)
			{
				if (args.Key == gconf_path) {
					Console.WriteLine ("Binding for '{0}' changed to '{1}'!",
							   gconf_path,
							   args.Value);

					UnsetBinding ();

					key_sequence = (string) args.Value;
					SetBinding ();
				}
			}

			public void SetBinding ()
			{
				if (key_sequence == null || 
				    key_sequence == String.Empty || 
				    key_sequence == "disabled")
					return;

				Console.WriteLine ("Binding key '{0}' for '{1}'",
						   key_sequence,
						   gconf_path);

				parent.Bind (key_sequence, handler);
			}

			public void UnsetBinding ()
			{
				if (key_sequence == null)
					return;

				Console.WriteLine ("Unbinding key '{0}' for '{1}'",
						   key_sequence,
						   gconf_path);

				parent.Unbind (key_sequence);
			}
		}
	}

	public class TomboyGConfXKeybinder : GConfXKeybinder
	{
		NoteManager manager;
		TomboyTray  tray;

		public TomboyGConfXKeybinder (NoteManager manager, TomboyTray tray)
			: base ()
		{
			this.manager = manager;
			this.tray = tray;

			EnableDisable ((bool) Preferences.Get (Preferences.ENABLE_KEYBINDINGS));

			Preferences.SettingChanged += EnableKeybindingsChanged;
		}

		void EnableKeybindingsChanged (object sender, GConf.NotifyEventArgs args)
		{
			if (args.Key == Preferences.ENABLE_KEYBINDINGS) {
				bool enabled = (bool) args.Value;
				EnableDisable (enabled);
			}
		}

		void EnableDisable (bool enable)
		{
			Console.WriteLine ("EnableDisable Called: enabling... {0}", enable);
			if (enable) {
				BindPreference (Preferences.KEYBINDING_SHOW_NOTE_MENU,
						new EventHandler (KeyShowMenu));

				BindPreference (Preferences.KEYBINDING_OPEN_START_HERE,
						new EventHandler (KeyOpenStartHere));

				BindPreference (Preferences.KEYBINDING_CREATE_NEW_NOTE,
						new EventHandler (KeyCreateNewNote));

				BindPreference (Preferences.KEYBINDING_OPEN_SEARCH,
						new EventHandler (KeyOpenSearch));

				BindPreference (Preferences.KEYBINDING_OPEN_RECENT_CHANGES,
						new EventHandler (KeyOpenRecentChanges));
			} else {
				UnbindAll ();
			}
		}

		void BindPreference (string gconf_path, EventHandler handler)
		{
			Bind (gconf_path,  
			      (string) Preferences.GetDefault (gconf_path),
			      handler);
		}

		void KeyShowMenu (object sender, EventArgs args)
		{
			// Show the notes menu, highlighting the first item.
			// This matches the behavior of GTK for
			// accelerator-shown menus.
			tray.ShowMenu (true);
		}

		void KeyOpenStartHere (object sender, EventArgs args)
		{
			Note note = manager.Find (Catalog.GetString ("Start Here"));
			if (note != null)
				note.Window.Present ();
		}

		void KeyCreateNewNote (object sender, EventArgs args)
		{
			try {
				Note new_note = manager.Create ();
				new_note.Window.Show ();
			} catch (Exception e) {
				// Fail silently.
			}
		}

		void KeyOpenSearch (object sender, EventArgs args)
		{
			NoteFindDialog find_dialog = NoteFindDialog.GetInstance (manager);
			find_dialog.Present ();
		}

		void KeyOpenRecentChanges (object sender, EventArgs args)
		{
			Gtk.Window recent = new NoteRecentChanges (manager);
			recent.Show ();
		}
	}
}
