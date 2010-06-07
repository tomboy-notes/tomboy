
using System;

using Mono.Unix;

namespace Tomboy
{
	public class Preferences
	{
		public const string ENABLE_SPELLCHECKING = "/apps/tomboy/enable_spellchecking";
		public const string ENABLE_WIKIWORDS = "/apps/tomboy/enable_wikiwords";
		public const string ENABLE_CUSTOM_FONT = "/apps/tomboy/enable_custom_font";
		public const string ENABLE_KEYBINDINGS = "/apps/tomboy/enable_keybindings";
		public const string ENABLE_STARTUP_NOTES = "/apps/tomboy/enable_startup_notes";
		public const string ENABLE_AUTO_BULLETED_LISTS = "/apps/tomboy/enable_bulleted_lists";
		public const string ENABLE_ICON_PASTE = "/apps/tomboy/enable_icon_paste";
		public const string ENABLE_CLOSE_NOTE_ON_ESCAPE = "/apps/tomboy/enable_close_note_on_escape";
		public const string ENABLE_TRAY_ICON = "/apps/tomboy/enable_tray_icon";
		public const string ENABLE_DELETE_CONFIRM = "/apps/tomboy/enable_delete_confirm";

		public const string START_NOTE_URI = "/apps/tomboy/start_note";
		public const string CUSTOM_FONT_FACE = "/apps/tomboy/custom_font_face";
		public const string MENU_NOTE_COUNT = "/apps/tomboy/menu_note_count";
		public const string MENU_PINNED_NOTES = "/apps/tomboy/menu_pinned_notes";
		public const string MENU_ITEM_MAX_LENGTH = "/apps/tomboy/tray_menu_item_max_length";

		public const string KEYBINDING_SHOW_NOTE_MENU = "/apps/tomboy/global_keybindings/show_note_menu";
		public const string KEYBINDING_OPEN_START_HERE = "/apps/tomboy/global_keybindings/open_start_here";
		public const string KEYBINDING_CREATE_NEW_NOTE = "/apps/tomboy/global_keybindings/create_new_note";
		public const string KEYBINDING_OPEN_SEARCH = "/apps/tomboy/global_keybindings/open_search";
		public const string KEYBINDING_OPEN_RECENT_CHANGES = "/apps/tomboy/global_keybindings/open_recent_changes";

		public const string EXPORTHTML_LAST_DIRECTORY = "/apps/tomboy/export_html/last_directory";
		public const string EXPORTHTML_EXPORT_LINKED = "/apps/tomboy/export_html/export_linked";
		public const string EXPORTHTML_EXPORT_LINKED_ALL = "/apps/tomboy/export_html/export_linked_all";

		public const string STICKYNOTEIMPORTER_FIRST_RUN = "/apps/tomboy/sticky_note_importer/sticky_importer_first_run";

		public const string SYNC_CLIENT_ID = "/apps/tomboy/sync/sync_guid";
		public const string SYNC_LOCAL_PATH = "/apps/tomboy/sync/sync_local_path";
		public const string SYNC_SELECTED_SERVICE_ADDIN = "/apps/tomboy/sync/sync_selected_service_addin";
		public const string SYNC_CONFIGURED_CONFLICT_BEHAVIOR = "/apps/tomboy/sync/sync_conflict_behavior";
		public const string SYNC_AUTOSYNC_TIMEOUT = "/apps/tomboy/sync/autosync_timeout";

		public const string NOTE_RENAME_BEHAVIOR = "/apps/tomboy/note_rename_behavior";

		public const string INSERT_TIMESTAMP_FORMAT = "/apps/tomboy/insert_timestamp/format";
		
		public const string SEARCH_WINDOW_X_POS = "/apps/tomboy/search_window_x_pos";
		public const string SEARCH_WINDOW_Y_POS = "/apps/tomboy/search_window_y_pos";
		public const string SEARCH_WINDOW_WIDTH = "/apps/tomboy/search_window_width";
		public const string SEARCH_WINDOW_HEIGHT = "/apps/tomboy/search_window_height";
		public const string SEARCH_WINDOW_SPLITTER_POS = "/apps/tomboy/search_window_splitter_pos";

		static IPreferencesClient client;

		public static IPreferencesClient Client
		{
			get {
				if (client == null) {
					client = Services.Preferences;
					client.AddNotify ("/apps/tomboy", OnSettingChanged);
				}
				return client;
			}
		}

		// NOTE: Keep synced with tomboy.schemas.in
		public static object GetDefault (string key)
		{
			switch (key) {
			case ENABLE_SPELLCHECKING:
			case ENABLE_KEYBINDINGS:
				return true;

			case ENABLE_CUSTOM_FONT:
				return false;

			case ENABLE_WIKIWORDS:
				return false;

			case ENABLE_AUTO_BULLETED_LISTS:
				return true;

			case ENABLE_ICON_PASTE:
				return false;

			case ENABLE_CLOSE_NOTE_ON_ESCAPE:
				return true;

			case ENABLE_TRAY_ICON:
				return true;

			case ENABLE_DELETE_CONFIRM:
				return true;

			case START_NOTE_URI:
				return String.Empty;

			case CUSTOM_FONT_FACE:
				return "Serif 11";

			case MENU_NOTE_COUNT:
				return 10;

			case MENU_PINNED_NOTES:
				return string.Empty;

			case MENU_ITEM_MAX_LENGTH:
				return 100;

			case KEYBINDING_SHOW_NOTE_MENU:
				return "<Alt>F12";

			case KEYBINDING_OPEN_START_HERE:
				return "<Alt>F11";

			case KEYBINDING_CREATE_NEW_NOTE:
			case KEYBINDING_OPEN_SEARCH:
			case KEYBINDING_OPEN_RECENT_CHANGES:
				return "disabled";

			case EXPORTHTML_EXPORT_LINKED:
				return true;

			case EXPORTHTML_EXPORT_LINKED_ALL:
				return false;

			case EXPORTHTML_LAST_DIRECTORY:
				return string.Empty;

			case STICKYNOTEIMPORTER_FIRST_RUN:
				return true;

			case ENABLE_STARTUP_NOTES:
				return true;

			case SYNC_CLIENT_ID:
				return System.Guid.NewGuid ().ToString ();

			case SYNC_LOCAL_PATH:
				return string.Empty;

			case SYNC_CONFIGURED_CONFLICT_BEHAVIOR:
				return 0;

			case SYNC_AUTOSYNC_TIMEOUT:
				return -1;

			case NOTE_RENAME_BEHAVIOR:
				return 0;

			case INSERT_TIMESTAMP_FORMAT:
				return Catalog.GetString ("dddd, MMMM d, h:mm tt");
			}

			return null;
		}

		public static object Get (string key)
		{
			try {
				return Client.Get (key);
			} catch (NoSuchKeyException) {
				object default_val = GetDefault (key);

				if (default_val != null)
					Client.Set (key, default_val);

				return default_val;
			}
		}

		public static void Set (string key, object value)
		{
			Client.Set (key, value);
		}

		public static event NotifyEventHandler SettingChanged;

		static void OnSettingChanged (object sender, NotifyEventArgs args)
		{
			if (SettingChanged != null) {
				SettingChanged (sender, args);
			}
		}
	}
}

