
using System;

namespace Tomboy
{
	public class Preferences
	{
		static readonly string[] DefaultEnabledPlugins = {
			"BacklinksPlugin", "EvolutionPlugin", "ExportToHTMLPlugin",
			"FixedWidthPlugin", "PrintPlugin", "StickyNoteImporter"
		};

		public const string ENABLE_SPELLCHECKING = "/apps/tomboy/enable_spellchecking";
		public const string ENABLE_WIKIWORDS = "/apps/tomboy/enable_wikiwords";
		public const string ENABLE_CUSTOM_FONT = "/apps/tomboy/enable_custom_font";
		public const string ENABLE_KEYBINDINGS = "/apps/tomboy/enable_keybindings";
		public const string ENABLE_STARTUP_NOTES = "/apps/tomboy/enable_startup_notes";

		public const string START_NOTE_URI = "/apps/tomboy/start_note";
		public const string CUSTOM_FONT_FACE = "/apps/tomboy/custom_font_face";
		public const string MENU_NOTE_COUNT = "/apps/tomboy/menu_note_count";
		public const string MENU_PINNED_NOTES = "/apps/tomboy/menu_pinned_notes";
		public const string ENABLED_PLUGINS = "/apps/tomboy/enabled_plugins";

		public const string KEYBINDING_SHOW_NOTE_MENU = "/apps/tomboy/global_keybindings/show_note_menu";
		public const string KEYBINDING_OPEN_START_HERE = "/apps/tomboy/global_keybindings/open_start_here";
		public const string KEYBINDING_CREATE_NEW_NOTE = "/apps/tomboy/global_keybindings/create_new_note";
		public const string KEYBINDING_OPEN_SEARCH = "/apps/tomboy/global_keybindings/open_search";
		public const string KEYBINDING_OPEN_RECENT_CHANGES = "/apps/tomboy/global_keybindings/open_recent_changes";

		public const string EXPORTHTML_LAST_DIRECTORY = "/apps/tomboy/export_html/last_directory";
		public const string EXPORTHTML_EXPORT_LINKED = "/apps/tomboy/export_html/export_linked";
		public const string EXPORTHTML_EXPORT_LINKED_ALL = "/apps/tomboy/export_html/export_linked_all";

		public const string STICKYNOTEIMPORTER_FIRST_RUN = "/apps/tomboy/sticky_note_importer/sticky_importer_first_run";

		static GConf.Client client;
		static GConf.NotifyEventHandler changed_handler;

		public static GConf.Client Client 
		{
			get {
				if (client == null) {
					client = new GConf.Client ();

					changed_handler = new GConf.NotifyEventHandler (OnSettingChanged);
					client.AddNotify ("/apps/tomboy", changed_handler);
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
			
			case START_NOTE_URI:
				return String.Empty;

			case CUSTOM_FONT_FACE:
				return "Serif 11";

			case MENU_NOTE_COUNT:
				return 10;

			case MENU_PINNED_NOTES:
				return "";

			case ENABLED_PLUGINS:
				return DefaultEnabledPlugins;

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
				return "";

			case STICKYNOTEIMPORTER_FIRST_RUN:
				return true;
			
			case ENABLE_STARTUP_NOTES:
				return true;
			}

			return null;
		}

		public static object Get (string key)
		{
			try {
				return Client.Get (key);
			} catch (GConf.NoSuchKeyException) {
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

		public static event GConf.NotifyEventHandler SettingChanged;

		static void OnSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			if (SettingChanged != null) {
				SettingChanged (sender, args);
			}
		}
	}
}

