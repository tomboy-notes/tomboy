using System;
using System.Collections.Generic;

namespace Tomboy
{
	public class PrefsKeybinder
	{
		List<Binding> bindings;
		IKeybinder native_keybinder;

		public PrefsKeybinder ()
		{
			bindings = new List<Binding> ();
			native_keybinder = Services.Keybinder;
		}

		public void Bind (string       pref_path,
		                  string       default_binding,
		                  EventHandler handler)
		{
			try {
				Binding binding = new Binding (pref_path,
				                               default_binding,
				                               handler,
				                               native_keybinder);
				bindings.Add (binding);
			} catch (Exception e) {
				Logger.Error ("Error Adding global keybinding:");
				Logger.Error (e.ToString ());
			}
		}

		public void UnbindAll ()
		{
			try {
				foreach (Binding binding in bindings)
					binding.RemoveNotify ();
				bindings.Clear ();
				native_keybinder.UnbindAll ();
			} catch (Exception e) {
				Logger.Error ("Error Removing global keybinding:");
				Logger.Error (e.ToString ());
			}
		}

		class Binding
		{
			public string   pref_path;
			public string   key_sequence;
			EventHandler    handler;
			IKeybinder native_keybinder;

			public Binding (string          pref_path,
			                string          default_binding,
			                EventHandler    handler,
			                IKeybinder native_keybinder)
			{
				this.pref_path = pref_path;
				this.key_sequence = default_binding;
				this.handler = handler;
				this.native_keybinder = native_keybinder;

				try {
					key_sequence = (string) Preferences.Client.Get (pref_path);
				} catch {
				        Logger.Warn ("Preference key '{0}' does not exist, using default.",
				                    pref_path);
				}

				SetBinding ();

				Preferences.Client.AddNotify (
				        pref_path,
				        BindingChanged);
			}

			public void RemoveNotify ()
			{
				Preferences.Client.RemoveNotify (
				        pref_path,
				        BindingChanged);
			}

			void BindingChanged (object sender, NotifyEventArgs args)
			{
				if (args.Key == pref_path) {
					Logger.Debug ("Binding for '{0}' changed to '{1}'!",
					            pref_path,
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

				Logger.Debug ("Binding key '{0}' for '{1}'",
				            key_sequence,
				            pref_path);

				native_keybinder.Bind (key_sequence, handler);
			}

			public void UnsetBinding ()
			{
				if (key_sequence == null)
					return;

				Logger.Debug ("Unbinding key '{0}' for '{1}'",
				            key_sequence,
				            pref_path);

				native_keybinder.Unbind (key_sequence);
			}
		}
	}

	public class TomboyPrefsKeybinder : PrefsKeybinder
	{
		NoteManager manager;
		ITomboyTray tray;

		public TomboyPrefsKeybinder (NoteManager manager, ITomboyTray tray)
				: base ()
		{
			this.manager = manager;
			this.tray = tray;

			EnableDisable ((bool) Preferences.Get (Preferences.ENABLE_KEYBINDINGS));

			Preferences.SettingChanged += EnableKeybindingsChanged;
		}

		void EnableKeybindingsChanged (object sender, NotifyEventArgs args)
		{
			if (args.Key == Preferences.ENABLE_KEYBINDINGS) {
				bool enabled = (bool) args.Value;
				EnableDisable (enabled);
			}
		}

		void EnableDisable (bool enable)
		{
			Logger.Debug ("EnableDisable Called: enabling... {0}", enable);
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

		void BindPreference (string pref_path, EventHandler handler)
		{
			Bind (pref_path,
			      (string) Preferences.GetDefault (pref_path),
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
			manager.GtkInvoke (() => {
				Note note = manager.FindByUri (NoteManager.StartNoteUri);
				if (note != null)
					note.Window.Present ();
			});
		}

		void KeyCreateNewNote (object sender, EventArgs args)
		{
			try {
				manager.GtkInvoke (() => {
					Note new_note = manager.Create ();
					new_note.Window.Show ();
				});
			} catch {
				// Fail silently.
			}
		}

		void KeyOpenSearch (object sender, EventArgs args)
		{
			/* Find dialog is deprecated in favor of searcable ToC */
			/*
			NoteFindDialog find_dialog = NoteFindDialog.GetInstance (manager);
			find_dialog.Present ();
			*/
			KeyOpenRecentChanges (sender, args);
		}

		void KeyOpenRecentChanges (object sender, EventArgs args)
		{
			manager.GtkInvoke (() => {
				NoteRecentChanges recent = NoteRecentChanges.GetInstance (manager);
				recent.Present ();
			});
		}
	}
}
