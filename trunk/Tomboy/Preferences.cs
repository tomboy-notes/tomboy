
using System;
using Mono.Unix;
using GConf.PropertyEditors;

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

	public class PreferencesDialog : Gtk.Dialog
	{
		readonly PluginManager plugin_manager;

		Type current_plugin;
		PluginInfoAttribute plugin_info;

		Gtk.Button font_button;
		Gtk.Label font_face;
		Gtk.Label font_size;

		Gtk.ListStore plugin_store;
		Gtk.Label plugin_name;

		Gtk.TextView plugin_description;
		Gtk.TextView plugin_author;
		Gtk.TextView plugin_website;
		Gtk.TextView plugin_filename;

		Gtk.Button plugin_preferences;

		public PreferencesDialog (PluginManager plugin_manager)
			: base ()
		{
			this.plugin_manager = plugin_manager;

			IconName = "tomboy";
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = true;
			Title = Catalog.GetString ("Tomboy Preferences");

			VBox.Spacing = 5;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;


			// Notebook Tabs (Editing, Hotkeys)...

			Gtk.Notebook notebook = new Gtk.Notebook ();
			notebook.TabPos = Gtk.PositionType.Top;
			notebook.BorderWidth = 5;
			notebook.Show ();

			notebook.AppendPage (MakeEditingPane (), 
				new Gtk.Label (Catalog.GetString ("Editing")));
			notebook.AppendPage (MakeHotkeysPane (), 
				new Gtk.Label (Catalog.GetString ("Hotkeys")));
			notebook.AppendPage (MakePluginsPane (), 
				new Gtk.Label (Catalog.GetString ("Plugins")));

			VBox.PackStart (notebook, true, true, 0);


			// Ok button...
			
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Close);
			button.CanDefault = true;
			button.Show ();

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			button.AddAccelerator ("activate",
					       accel_group,
					       (uint) Gdk.Key.Escape, 
					       0,
					       0);

			AddActionWidget (button, Gtk.ResponseType.Close);
			DefaultResponse = Gtk.ResponseType.Close;
		}

		// Page 1
		// List of editing options
		public Gtk.Widget MakeEditingPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;
			PropertyEditorBool peditor, font_peditor;
			
			Gtk.VBox options_list = new Gtk.VBox (false, 12);
			options_list.BorderWidth = 12;
			options_list.Show ();


			// Spell checking...

			if (NoteSpellChecker.GtkSpellAvailable) {
				check = MakeCheckButton (
					Catalog.GetString ("_Spell check while typing"));
				options_list.PackStart (check, false, false, 0);
				
				peditor = new PropertyEditorToggleButton (
					Preferences.ENABLE_SPELLCHECKING,
					check);
				SetupPropertyEditor (peditor);

				label = MakeTipLabel (
					Catalog.GetString ("Misspellings will be underlined " +
							   "in red, with correct spelling " +
							   "suggestions shown in the context " +
							   "menu."));
				options_list.PackStart (label, false, false, 0);
			}


			// WikiWords...

			check = MakeCheckButton (Catalog.GetString ("Highlight _WikiWords"));
			options_list.PackStart (check, false, false, 0);

			peditor = new PropertyEditorToggleButton (Preferences.ENABLE_WIKIWORDS, 
								  check);
			SetupPropertyEditor (peditor);

			label = MakeTipLabel (
				Catalog.GetString ("Enable this option to highlight " +
						   "words <b>ThatLookLikeThis</b>. " +
						   "Clicking the word will create a " +
						   "note with that name."));
			options_list.PackStart (label, false, false, 0);


			// Custom font...

			check = MakeCheckButton (Catalog.GetString ("Use custom _font"));
			options_list.PackStart (check, false, false, 0);

			font_peditor = 
				new PropertyEditorToggleButton (Preferences.ENABLE_CUSTOM_FONT, 
								check);
			SetupPropertyEditor (font_peditor);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.4f, 1.0f);
			align.Show ();
			options_list.PackStart (align, false, false, 0);

			font_button = MakeFontButton ();
			font_button.Sensitive = check.Active;
			align.Add (font_button);
			
			font_peditor.AddGuard (font_button);


			return options_list;
		}

		Gtk.Button MakeFontButton ()
		{
			Gtk.HBox font_box = new Gtk.HBox (false, 0);
			font_box.Show ();
			
			font_face = new Gtk.Label (null);
			font_face.UseMarkup = true;
			font_face.Show ();
			font_box.PackStart (font_face, true, true, 0);

			Gtk.VSeparator sep = new Gtk.VSeparator ();
			sep.Show ();
			font_box.PackStart (sep, false, false, 0);

			font_size = new Gtk.Label (null);
			font_size.Xpad = 4;
			font_size.Show ();
			font_box.PackStart (font_size, false, false, 0);
			
			Gtk.Button button = new Gtk.Button ();
			button.Clicked += OnFontButtonClicked;
			button.Add (font_box);
			button.Show ();

			string font_desc = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
			UpdateFontButton (font_desc);

			return button;
		}

		// Page 2
		// List of Hotkey options
		public Gtk.Widget MakeHotkeysPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;
			Gtk.Entry entry;
			PropertyEditorBool keybind_peditor;
			PropertyEditor peditor;
			
			Gtk.VBox hotkeys_list = new Gtk.VBox (false, 12);
			hotkeys_list.BorderWidth = 12;
			hotkeys_list.Show ();


			// Hotkeys...

			check = MakeCheckButton (Catalog.GetString ("Listen for _Hotkeys"));
			hotkeys_list.PackStart (check, false, false, 0);

			keybind_peditor = 
				new PropertyEditorToggleButton (Preferences.ENABLE_KEYBINDINGS, 
								check);
			SetupPropertyEditor (keybind_peditor);

			label = MakeTipLabel (
				Catalog.GetString ("Hotkeys allow you to quickly access " +
						   "your notes from anywhere with a keypress. " +
						   "Example Hotkeys: " +
						   "<b>&lt;Control&gt;&lt;Shift&gt;F11</b>, " +
						   "<b>&lt;Alt&gt;N</b>"));
			hotkeys_list.PackStart (label, false, false, 0);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 1.0f);
			align.Show ();
			hotkeys_list.PackStart (align, false, false, 0);

			Gtk.Table table = new Gtk.Table (4, 2, false);
			table.ColumnSpacing = 6;
			table.RowSpacing = 6;
			table.Show ();
			align.Add (table);


			// Show notes menu keybinding...

			label = MakeLabel (Catalog.GetString ("Show notes _menu"));
			table.Attach (label, 0, 1, 0, 1);

			entry = new Gtk.Entry ();
			label.MnemonicWidget = entry;	
			entry.Show ();
			table.Attach (entry, 1, 2, 0, 1);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_SHOW_NOTE_MENU, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Open Start Here keybinding...

			label = MakeLabel (Catalog.GetString ("Open \"_Start Here\""));
			table.Attach (label, 0, 1, 1, 2);

			entry = new Gtk.Entry ();
			label.MnemonicWidget = entry;	
			entry.Show ();
			table.Attach (entry, 1, 2, 1, 2);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_OPEN_START_HERE, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Create new note keybinding...

			label = MakeLabel (Catalog.GetString ("Create _new note"));
			table.Attach (label, 0, 1, 2, 3);

			entry = new Gtk.Entry ();
			label.MnemonicWidget = entry;	
			entry.Show ();
			table.Attach (entry, 1, 2, 2, 3);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_CREATE_NEW_NOTE, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Open Search All Notes window keybinding...

			label = MakeLabel (Catalog.GetString ("Open \"Search _All Notes\""));
			table.Attach (label, 0, 1, 3, 4);

			entry = new Gtk.Entry ();
			label.MnemonicWidget = entry;	
			entry.Show ();
			table.Attach (entry, 1, 2, 3, 4);

			peditor = new PropertyEditorEntry (
				Preferences.KEYBINDING_OPEN_RECENT_CHANGES, 
				entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			return hotkeys_list;
		}

		// Page 3
		// Plugin Preferences
		public Gtk.Widget MakePluginsPane ()
		{
			Gtk.HPaned pane;

			pane = new Gtk.HPaned ();
			pane.Pack2 (MakePluginDetails (), true, false);
			pane.Pack1 (MakePluginList (), false, false);
			pane.BorderWidth = 6;
			pane.ShowAll ();

			return pane;
		}

		Gtk.Widget MakePluginList ()
		{
			Gtk.TreeView tree_view;
			Gtk.CellRendererText caption;
			Gtk.CellRendererToggle enabled;
			Gtk.ScrolledWindow scroller;

			Gtk.TreeIter first_plugin;

			plugin_store = new Gtk.ListStore (typeof (Type), typeof (string));
			plugin_store.SetSortColumnId (1, Gtk.SortType.Ascending);
			
			foreach (Type type in plugin_manager.Plugins) {
				string name = PluginManager.GetPluginName (type);
				plugin_store.AppendValues (type, name);
			}

			tree_view = new Gtk.TreeView (plugin_store);
			tree_view.HeadersVisible = false;
			tree_view.SetSizeRequest (160, 0);
			tree_view.Selection.Mode = Gtk.SelectionMode.Browse;
			tree_view.Selection.Changed += SelectedPluginChanged;

			enabled = new Gtk.CellRendererToggle ();
			enabled.Toggled += PluginStateToggled;
			tree_view.AppendColumn (null, 
						enabled, 
						(Gtk.TreeCellDataFunc) GetPluginState);
			
			caption = new Gtk.CellRendererText ();
			caption.Ellipsize = Pango.EllipsizeMode.End;
			tree_view.AppendColumn (null, caption, "text", 1);

			scroller = new Gtk.ScrolledWindow ();
			scroller.BorderWidth = 6;
			scroller.ShadowType = Gtk.ShadowType.In;
			scroller.SetPolicy (Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
			scroller.Add (tree_view);

			if (plugin_store.GetIterFirst (out first_plugin))
				tree_view.Selection.SelectIter (first_plugin);

			return scroller;
		}

		void GetPluginState (
			Gtk.TreeViewColumn column, Gtk.CellRenderer cell,
			Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			Type plugin = (Type) model.GetValue (iter, 0);

			((Gtk.CellRendererToggle) cell).Active =
				plugin_manager.IsPluginEnabled (plugin); 
		}

		void PluginStateToggled (object sender, Gtk.ToggledArgs e)
		{
			Gtk.TreeIter iter;

			if (plugin_store.GetIter (out iter, new Gtk.TreePath (e.Path))) {
				Type plugin = (Type) plugin_store.GetValue (iter, 0);
				bool enabled = plugin_manager.IsPluginEnabled (plugin);
				plugin_manager.SetPluginEnabled (plugin, !enabled);
			}
		}

		string MakePluginTitle ()
		{
			return String.Format (
				"<span size='large' weight='bold'>{0} {1}</span>",
				PluginManager.GetPluginName (current_plugin, plugin_info),
				PluginManager.GetPluginVersion (current_plugin, plugin_info));
		}

		void SelectedPluginChanged (object sender, EventArgs e)
		{
			Gtk.TreeSelection selection;
			Gtk.TreeModel model;
			Gtk.TreeIter iter;
			
			selection = (Gtk.TreeSelection)sender;

			if (selection.GetSelected (out model, out iter)) {
				current_plugin = (Type) model.GetValue (iter, 0);
				plugin_info = PluginManager.GetPluginInfo (current_plugin);
				string description = null, author = null, website = null;

				if (null != plugin_info) {
					description = plugin_info.Description;
					website = plugin_info.WebSite;
					author = plugin_info.Author;
				}

				plugin_name.Markup = MakePluginTitle ();

				SetPluginDetail (plugin_filename, current_plugin.Assembly.Location);
				SetPluginDetail (plugin_description, description);
				SetPluginDetail (plugin_website, website);
				SetPluginDetail (plugin_author, author);

				plugin_preferences.Sensitive = 
					null != plugin_info && 
					null != plugin_info.PreferencesWidget;
			} else {
				current_plugin = null;
				plugin_info = null;
			}
		}

		static void SetPluginDetail (Gtk.TextView detail, string text)
		{
			if (null == text || 0 == text.Length)
				text = Catalog.GetString ("Not available");

			detail.Buffer.Text = text;
		}

		static Gtk.TextView MakePluginDetail (
				Gtk.Table table, uint row, string caption)
		{
			Gtk.Label label;
			Gtk.TextView detail;

			label = MakeLabel ("<b>{0}</b>", Catalog.GetString (caption));
			label.Yalign = 0.0f;

			detail = new Gtk.TextView ();
			detail.WrapMode = Gtk.WrapMode.WordChar;
			detail.Editable = false;

			table.Attach (label, 
				      0, 1, row, row + 1,
				      Gtk.AttachOptions.Fill,
				      Gtk.AttachOptions.Fill, 
				      0, 0);
			table.Attach (detail, 
				      1, 2, row, row + 1, 
				      Gtk.AttachOptions.Fill | Gtk.AttachOptions.Expand, 
				      Gtk.AttachOptions.Fill, 
				      0, 0);

			return detail;
		}

		Gtk.Widget MakePluginDetails ()
		{
			Gtk.Table table;
			Gtk.ScrolledWindow scroller;
			Gdk.Color base_color;
			Gtk.VBox vbox;

			Gtk.HBox action_area;
			Gtk.Button open_plugin_folder;

			// Name...

			plugin_name = new Gtk.Label ();
			plugin_name.UseMarkup = true;
			plugin_name.UseUnderline = false;
			plugin_name.ModifyBase (Gtk.StateType.Normal,
					new Gdk.Color (255, 0, 0));

			// Details...

			table = new Gtk.Table (4, 2, false);
			table.BorderWidth = 6;
			table.ColumnSpacing = 6;
			table.RowSpacing = 6;

			plugin_description = MakePluginDetail (
				table, 0, Catalog.GetString ("Description:"));
			plugin_author = MakePluginDetail (
				table, 1, Catalog.GetString ("Written by:"));
			plugin_website = MakePluginDetail (
				table, 2, Catalog.GetString ("Web site:"));
			plugin_filename = MakePluginDetail (
				table, 3, Catalog.GetString ("File name:"));

			scroller = new Gtk.ScrolledWindow ();
			scroller.SetPolicy (Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
			scroller.AddWithViewport (table);

			plugin_description.EnsureStyle ();
			base_color = plugin_description.Style.Base (Gtk.StateType.Normal);
			scroller.Child.ModifyBg (Gtk.StateType.Normal, base_color);

			// Action area...

			plugin_preferences = 
				GuiUtils.MakeImageButton (Gtk.Stock.Preferences,
							  Catalog.GetString ("Settings"));
			plugin_preferences.Clicked += ShowPluginSettings;
			plugin_preferences.Sensitive = false;

			open_plugin_folder = 
				GuiUtils.MakeImageButton (
					Gtk.Stock.Directory,
					Catalog.GetString ("_Open Plugins Folder"));
			open_plugin_folder.Clicked += 
				delegate (object sender, EventArgs e) 
				{
					plugin_manager.ShowPluginsDirectory ();
				};

			action_area = new Gtk.HBox (false, 12);
			action_area.PackStart (plugin_preferences, false, false, 0);
			action_area.PackEnd (open_plugin_folder, false, false, 0);
			action_area.ModifyBase (Gtk.StateType.Normal,
					new Gdk.Color (0, 255, 0));

			// VBox

			vbox = new Gtk.VBox (false, 6);
			vbox.PackStart (plugin_name, false, true, 0);
			vbox.PackStart (scroller, true, true, 0);
			vbox.PackStart (action_area, false, true, 0);
			vbox.BorderWidth = 6;
			vbox.ShowAll ();

			return vbox;
		}

		void ShowPluginSettings (object sender, EventArgs e)
		{
			if (current_plugin == null)
				return;
			
			Gtk.Image icon = 
				new Gtk.Image (Gtk.Stock.Preferences, Gtk.IconSize.Dialog);

			Gtk.Label caption = new Gtk.Label();
			caption.Markup = MakePluginTitle ();
			caption.Xalign = 0;

			Gtk.Widget preferences =
				PluginManager.CreatePreferencesWidget (plugin_info);
			
			Gtk.HBox hbox = new Gtk.HBox (false, 6);
			Gtk.VBox vbox = new Gtk.VBox (false, 6);
			vbox.BorderWidth = 6;
			
			hbox.PackStart (icon, false, false, 0);
			hbox.PackStart (caption, true, true, 0);
			vbox.PackStart (hbox, false, false, 0);
			
			vbox.PackStart (preferences, true, true, 0);			
			vbox.ShowAll ();
			
			Gtk.Dialog dialog = new Gtk.Dialog (
				string.Format (Catalog.GetString ("{0} Settings"),
					       plugin_info.Name),
				this,
				Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.NoSeparator,
				Gtk.Stock.Close, Gtk.ResponseType.Close);

			dialog.VBox.PackStart (vbox, true, true, 0);

			dialog.Run ();
			dialog.Destroy();
		}

		void SetupPropertyEditor (PropertyEditor peditor)
		{
			// Ensure the key exists
			Preferences.Get (peditor.Key);
			peditor.Setup ();
		}

		// Utilities...

		static Gtk.Label MakeLabel (string label_text, params object[] args)
		{
			if (args.Length > 0)
				label_text = String.Format (label_text, args);

			Gtk.Label label = new Gtk.Label (label_text);

			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();

			return label;
		}

		static Gtk.CheckButton MakeCheckButton (string label_text)
		{
			Gtk.Label label = MakeLabel (label_text);

			Gtk.CheckButton check = new Gtk.CheckButton ();
			check.Add (label);
			check.Show ();

			return check;
		}

		static Gtk.Label MakeTipLabel (string label_text)
		{
			Gtk.Label label =  MakeLabel ("<small>{0}</small>", label_text);
			label.LineWrap = true;
			label.Xpad = 20;
			return label;
		}

		// Font Change handler

		void OnFontButtonClicked (object sender, EventArgs args)
		{
			Gtk.FontSelectionDialog font_dialog = 
				new Gtk.FontSelectionDialog (
					Catalog.GetString ("Choose Note Font"));

			string font_name = (string)
				Preferences.Get (Preferences.CUSTOM_FONT_FACE);
			font_dialog.SetFontName (font_name);

			if ((int) Gtk.ResponseType.Ok == font_dialog.Run ()) {
				if (font_dialog.FontName != font_name) {
					Preferences.Set (Preferences.CUSTOM_FONT_FACE, 
							 font_dialog.FontName);

					UpdateFontButton (font_dialog.FontName);
				}
			}

			font_dialog.Destroy ();
		}

		void UpdateFontButton (string font_desc)
		{
			Pango.FontDescription desc =
				Pango.FontDescription.FromString (font_desc);

			// Set the size label
			font_size.Text = (desc.Size / Pango.Scale.PangoScale).ToString ();

			desc.UnsetFields (Pango.FontMask.Size);

			// Set the font name label
			font_face.Markup = String.Format ("<span font_desc='{0}'>{1}</span>",
							  font_desc,
							  desc.ToString ());
		}
	}
}

