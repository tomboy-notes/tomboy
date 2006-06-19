
using System;
using Mono.Unix;
using GConf.PropertyEditors;

namespace Tomboy
{
	public class Preferences
	{
		public const string ENABLE_SPELLCHECKING = "/apps/tomboy/enable_spellchecking";
		public const string ENABLE_WIKIWORDS = "/apps/tomboy/enable_wikiwords";
		public const string ENABLE_CUSTOM_FONT = "/apps/tomboy/enable_custom_font";
		public const string ENABLE_KEYBINDINGS = "/apps/tomboy/enable_keybindings";

		public const string CUSTOM_FONT_FACE = "/apps/tomboy/custom_font_face";

		public const string KEYBINDING_SHOW_NOTE_MENU = "/apps/tomboy/global_keybindings/show_note_menu";
		public const string KEYBINDING_OPEN_START_HERE = "/apps/tomboy/global_keybindings/open_start_here";
		public const string KEYBINDING_CREATE_NEW_NOTE = "/apps/tomboy/global_keybindings/create_new_note";
		public const string KEYBINDING_OPEN_SEARCH = "/apps/tomboy/global_keybindings/open_search";
		public const string KEYBINDING_OPEN_RECENT_CHANGES = "/apps/tomboy/global_keybindings/open_recent_changes";

		public const string EXPORTHTML_LAST_DIRECTORY = "/apps/tomboy/export_html/last_directory";
		public const string EXPORTHTML_EXPORT_LINKED = "/apps/tomboy/export_html/export_linked";

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
			case ENABLE_CUSTOM_FONT:
			case ENABLE_KEYBINDINGS:
				return true;

			case ENABLE_WIKIWORDS:
				return false;

			case CUSTOM_FONT_FACE:
				return "Serif 11";

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

			case EXPORTHTML_LAST_DIRECTORY:
				return "";
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
		Gtk.Button font_button;
		Gtk.Label font_face;
		Gtk.Label font_size;

		static Gdk.Pixbuf tintin;

		static PreferencesDialog ()
		{
			tintin = new Gdk.Pixbuf (null, "tintin.png");
		}

		public PreferencesDialog ()
			: base ()
		{
			Icon = tintin;
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
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


			// Spellchecking...

			check = MakeCheckButton (Catalog.GetString ("_Spellcheck While Typing"));
			options_list.PackStart (check, false, false, 0);

			peditor = new PropertyEditorToggleButton (Preferences.ENABLE_SPELLCHECKING,
								  check);
			SetupPropertyEditor (peditor);

			label = MakeTipLabel (Catalog.GetString ("Misspellings will be underlined " +
								 "in red, and correct spelling " +
								 "suggestions shown in the right-click " +
								 "menu."));
			options_list.PackStart (label, false, false, 0);


			// WikiWords...

			check = MakeCheckButton (Catalog.GetString ("Highlight _WikiWords"));
			options_list.PackStart (check, false, false, 0);

			peditor = new PropertyEditorToggleButton (Preferences.ENABLE_WIKIWORDS, 
								  check);
			SetupPropertyEditor (peditor);

			label = MakeTipLabel (Catalog.GetString ("Enable this option to highlight " +
								 "words <b>ThatLookLikeThis</b>. " +
								 "Clicking the word will create a " +
								 "note with that name."));
			options_list.PackStart (label, false, false, 0);


			// Custom font...

			check = MakeCheckButton (Catalog.GetString ("Use Custom _Font"));
			options_list.PackStart (check, false, false, 0);

			font_peditor = 
				new PropertyEditorToggleButton (Preferences.ENABLE_CUSTOM_FONT, 
								check);
			SetupPropertyEditor (font_peditor);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.4f, 1.0f);
			align.Show ();
			options_list.PackStart (align, true, true, 0);

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

			label = MakeTipLabel (Catalog.GetString ("Hotkeys allow you to quickly access " +
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
			entry.Show ();
			table.Attach (entry, 1, 2, 2, 3);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_CREATE_NEW_NOTE, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Search dialog keybinding...

			label = MakeLabel (Catalog.GetString ("S_earch notes"));
			table.Attach (label, 0, 1, 3, 4);

			entry = new Gtk.Entry ();
			entry.Show ();
			table.Attach (entry, 1, 2, 3, 4);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_OPEN_SEARCH, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			return hotkeys_list;
		}

		void SetupPropertyEditor (PropertyEditor peditor)
		{
			// Ensure the key exists
			Preferences.Get (peditor.Key);
			peditor.Setup ();
		}

		// Utilities...

		Gtk.Label MakeLabel (string label_text)
		{
			Gtk.Label label = new Gtk.Label (label_text);
			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();

			return label;
		}

		Gtk.CheckButton MakeCheckButton (string label_text)
		{
			Gtk.Label label = MakeLabel (label_text);

			Gtk.CheckButton check = new Gtk.CheckButton ();
			check.Add (label);
			check.Show ();

			return check;
		}

		Gtk.Label MakeTipLabel (string label_text)
		{
			Gtk.Label label =  MakeLabel (String.Format ("<small><i>{0}</i></small>", 
								     label_text));
			label.LineWrap = true;
			label.Xpad = 20;
			return label;
		}

		// Font Change handler

		void OnFontButtonClicked (object sender, EventArgs args)
		{
			Gtk.FontSelectionDialog font_dialog = 
				new Gtk.FontSelectionDialog (Catalog.GetString ("Choose Note Font"));

			string font_name = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
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
			Pango.FontDescription desc = Pango.FontDescription.FromString (font_desc);

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
