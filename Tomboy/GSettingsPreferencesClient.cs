using System;
using GLib;
using System.Collections.Generic;

namespace Tomboy
{
	public class GSettingsPreferencesClient : IPreferencesClient
	{
		const string schema_name_core = "org.gnome.tomboy";
		const string schema_name_keybindings = "org.gnome.tomboy.global-keybindings";
		const string schema_name_exporthtml = "org.gnome.tomboy.export-html";
		const string schema_name_stickynoteimport = "org.gnome.tomboy.sticky-note-importer";
		const string schema_name_sync = "org.gnome.tomboy.sync";
		const string schema_name_inserttimestamp = "org.gnome.tomboy.insert-timestamp";
		readonly Settings settings_core = new Settings (schema_name_core);		
		readonly Settings settings_keybindings = new Settings (schema_name_keybindings);	
		readonly Settings settings_exporthtml = new Settings (schema_name_exporthtml);	
		readonly Settings settings_stickynoteimport = new Settings (schema_name_stickynoteimport);	
		readonly Settings settings_sync = new Settings (schema_name_sync);	
		readonly Settings settings_inserttimestamp = new Settings (schema_name_inserttimestamp);	
		//		readonly SettingsSchema schema = new SettingsSchema (schema_name);
		Dictionary<string, NotifyEventHandler> events;

		/*Because Tomboy's internal API for settings is built around the earlier gConf system where one 
		  could assign arbitrary values (and was designed before generics) we need to jump through some hoops.
		  Every key we get in is compared to the known keys in order to determine the type, essentially doing some
		  DuplicateNameException work here so that we don't have to redesign Tomboy's internals. The knowledge of
		  which types belong to which keys comes from reading the schema in data/org.gnome.gschema.xml(.in) a priori.
		  (Perhaps it would have been more ideal to read the XML and parse it ourselves, but then we might as well
		  have redesigned the internal API. */
		public GSettingsPreferencesClient ()
		{
			events = new Dictionary<string, NotifyEventHandler> ();
			settings_core.Changed += handleChanges;
		}

		private void handleChanges (object o, ChangedArgs args)
		{
			if (events.ContainsKey ("/apps/tomboy")) {
				NotifyEventHandler handler = events ["/apps/tomboy"] as NotifyEventHandler;
				NotifyEventArgs notify_args = new NotifyEventArgs (args.Key, Get (args.Key));
				handler (this, notify_args);
			}
		}

		private static string stripKey (string key)
		{
			return key.Substring (key.LastIndexOf ('/') + 1).Replace ('_', '-');
		}

		#region IPreferencesClient implementation

		public void Set (string key, object val)
		{
			switch (key)
			{
					//Booleans from core Tomboy
					case Preferences.ENABLE_SPELLCHECKING:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_WIKIWORDS:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_CUSTOM_FONT:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_KEYBINDINGS:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_STARTUP_NOTES:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_AUTO_BULLETED_LISTS:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_ICON_PASTE:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_CLOSE_NOTE_ON_ESCAPE:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_TRAY_ICON:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.ENABLE_DELETE_CONFIRM:
						settings_core.SetBoolean (stripKey (key), (bool) val);
						break;
					//Other types from core
					case Preferences.START_NOTE_URI:
						settings_core.SetString (stripKey (key), (string) val);
						break;
					case Preferences.CUSTOM_FONT_FACE:
						settings_core.SetString (stripKey (key), (string) val);
						break;
					case Preferences.MENU_NOTE_COUNT:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.MENU_MAX_NOTE_COUNT:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.MENU_PINNED_NOTES:
						settings_core.SetString (stripKey (key), (string) val);
						break;
					case Preferences.MENU_ITEM_MAX_LENGTH:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.NOTE_RENAME_BEHAVIOR:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					//Window position for search window (are these still respected on GNOME3?)
					case Preferences.SEARCH_WINDOW_X_POS:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.SEARCH_WINDOW_Y_POS:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.SEARCH_WINDOW_WIDTH:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.SEARCH_WINDOW_HEIGHT:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.SEARCH_WINDOW_SPLITTER_POS:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					case Preferences.SEARCH_WINDOW_MONITOR_NUM:
						settings_core.SetInt (stripKey (key), (int) val);
						break;
					//Keybindings
					case Preferences.KEYBINDING_SHOW_NOTE_MENU:
						settings_keybindings.SetString (stripKey (key), (string) val);
						break;
					case Preferences.KEYBINDING_OPEN_START_HERE:
						settings_keybindings.SetString (stripKey (key), (string) val);
						break;
					case Preferences.KEYBINDING_CREATE_NEW_NOTE:
						settings_keybindings.SetString (stripKey (key), (string) val);
						break;
					case Preferences.KEYBINDING_OPEN_SEARCH:
						settings_keybindings.SetString (stripKey (key), (string) val);
						break;
					case Preferences.KEYBINDING_OPEN_RECENT_CHANGES:
						settings_keybindings.SetString (stripKey (key), (string) val);
						break;
					//Export to HTML
					case Preferences.EXPORTHTML_LAST_DIRECTORY:
						settings_exporthtml.SetString (stripKey (key), (string) val);
						break;
					case Preferences.EXPORTHTML_EXPORT_LINKED:
						settings_exporthtml.SetBoolean (stripKey (key), (bool) val);
						break;
					case Preferences.EXPORTHTML_EXPORT_LINKED_ALL:
						settings_exporthtml.SetBoolean (stripKey (key), (bool) val);
						break;
					//Sticky note importer
					case Preferences.STICKYNOTEIMPORTER_FIRST_RUN:
						settings_stickynoteimport.SetBoolean (stripKey (key), (bool) val);
						break;
					//Sync
					case Preferences.SYNC_CLIENT_ID:
						settings_sync.SetString (stripKey (key), (string) val);
						break;
					case Preferences.SYNC_LOCAL_PATH:
						settings_sync.SetString (stripKey (key), (string) val);
						break;
					case Preferences.SYNC_SELECTED_SERVICE_ADDIN:
						settings_sync.SetString (stripKey (key), (string) val);
						break;
					case Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR:
						settings_sync.SetInt(stripKey (key), (int) val);
						break;
					case Preferences.SYNC_AUTOSYNC_TIMEOUT:
						settings_sync.SetInt(stripKey (key), (int) val);
						break;
					//Insert timestamp
					case Preferences.INSERT_TIMESTAMP_FORMAT:
						settings_inserttimestamp.SetString (stripKey (key), (string) val);
						break;

					default:
						throw new NoSuchKeyException (key);
			}

		}

		public object Get (string key)
		{
			key = stripKey (key);
				//Booleans from core Tomboy
				if (key == stripKey (Preferences.ENABLE_SPELLCHECKING))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_WIKIWORDS))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_CUSTOM_FONT))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_KEYBINDINGS))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_STARTUP_NOTES))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_AUTO_BULLETED_LISTS))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_ICON_PASTE))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_CLOSE_NOTE_ON_ESCAPE))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_TRAY_ICON))
					return settings_core.GetBoolean (key);
				if (key == stripKey (Preferences.ENABLE_DELETE_CONFIRM))
					return settings_core.GetBoolean (key);
				//Other types from core
				if (key == stripKey (Preferences.START_NOTE_URI))
					return settings_core.GetString (key);
				if (key == stripKey (Preferences.CUSTOM_FONT_FACE))
					return settings_core.GetString (key);
				if (key == stripKey (Preferences.MENU_NOTE_COUNT))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.MENU_MAX_NOTE_COUNT))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.MENU_PINNED_NOTES))
					return settings_core.GetString (key);
				if (key == stripKey (Preferences.MENU_ITEM_MAX_LENGTH))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.NOTE_RENAME_BEHAVIOR))
					return settings_core.GetInt (key);
				//Window position for search window (are these still respected on GNOME3?)
				if (key == stripKey (Preferences.SEARCH_WINDOW_X_POS))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.SEARCH_WINDOW_Y_POS))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.SEARCH_WINDOW_WIDTH))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.SEARCH_WINDOW_HEIGHT))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.SEARCH_WINDOW_SPLITTER_POS))
					return settings_core.GetInt (key);
				if (key == stripKey (Preferences.SEARCH_WINDOW_MONITOR_NUM))
					return settings_core.GetInt (key);
				//Keybindings
				if (key == stripKey (Preferences.KEYBINDING_SHOW_NOTE_MENU))
					return settings_keybindings.GetString (key);
				if (key == stripKey (Preferences.KEYBINDING_OPEN_START_HERE))
					return settings_keybindings.GetString (key);
				if (key == stripKey (Preferences.KEYBINDING_CREATE_NEW_NOTE))
					return settings_keybindings.GetString (key);
				if (key == stripKey (Preferences.KEYBINDING_OPEN_SEARCH))
					return settings_keybindings.GetString (key);
				if (key == stripKey (Preferences.KEYBINDING_OPEN_RECENT_CHANGES))
					return settings_keybindings.GetString (key);
				//Export to HTML
				if (key == stripKey (Preferences.EXPORTHTML_LAST_DIRECTORY))
					return settings_exporthtml.GetString (key);
				if (key == stripKey (Preferences.EXPORTHTML_EXPORT_LINKED))
					return settings_exporthtml.GetBoolean (key);
				if (key == stripKey (Preferences.EXPORTHTML_EXPORT_LINKED_ALL))
					return settings_exporthtml.GetBoolean (key);
				//Sticky note importer
				if (key == stripKey (Preferences.STICKYNOTEIMPORTER_FIRST_RUN))
					return settings_stickynoteimport.GetBoolean (key);
					//Sync
				if (key == stripKey (Preferences.SYNC_CLIENT_ID))
					return settings_sync.GetString (key);
				if (key == stripKey (Preferences.SYNC_LOCAL_PATH))
					return settings_sync.GetString (key);
				if (key == stripKey (Preferences.SYNC_SELECTED_SERVICE_ADDIN))
					return settings_sync.GetString (key);
				if (key == stripKey (Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR))
					return settings_sync.GetInt(key);
				if (key == stripKey (Preferences.SYNC_AUTOSYNC_TIMEOUT))
					return settings_sync.GetInt(key);
				//Insert timestamp
				if (key == stripKey (Preferences.INSERT_TIMESTAMP_FORMAT))
					return settings_inserttimestamp.GetString (key);
				
				throw new NoSuchKeyException (key);
			
		}

		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				if (!events.ContainsKey (dir))
					events [dir] = notify;
				else
					events [dir] += notify;
			}
		}

		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				if (events.ContainsKey (dir))
					events [dir] -= notify;
			}
		}

		public void SuggestSync ()
		{
			//Do nothing, gSettings handles this. We think.
		}

		#endregion
	}
}

