
using System;
using Mono.Posix;

namespace Tomboy
{
	public class PreferencesDialog : Gtk.Dialog
	{
		Gtk.Button font_button;
		Gtk.Label font_face;
		Gtk.Label font_size;

		Gtk.Entry show_menu_entry;
		Gtk.Entry open_start_here_entry;
		Gtk.Entry create_new_note_entry;
		Gtk.Entry open_search_entry;

		GConf.Client client;

		public PreferencesDialog ()
			: base ()
		{
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = Catalog.GetString ("Tomboy Preferences");

			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;

			// Tintin icon and "Tomboy Preferences" label

			/*
			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			hbox.BorderWidth = 5;
			hbox.Show ();
			VBox.PackStart (hbox, false, false, 0);

			Gtk.Image image = new Gtk.Image (new Gdk.Pixbuf (null, "tintin.png"));
			image.Show ();
			hbox.PackStart (image, false, false, 0);

			label = MakeLabel (String.Format ("<span weight='bold' size='xx-large'>{0}" +
							  "</span>",
							  Catalog.GetString ("Tomboy Preferences")));
			hbox.PackStart (label, false, false, 0);
			*/

			// Notebook Tabs (Editing, Hotkeys)...

			Gtk.Notebook notebook = new Gtk.Notebook ();
			notebook.TabPos = Gtk.PositionType.Top;
			notebook.Show ();

			notebook.AppendPage (MakeEditingPane (), 
					     new Gtk.Label (Catalog.GetString ("Editing")));

			notebook.AppendPage (MakeHotkeysPane (), 
					     new Gtk.Label (Catalog.GetString ("Hotkeys")));

			VBox.PackStart (notebook, true, true, 0);

			// Ok button...
			
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Ok);
			button.CanDefault = true;
			button.Show ();

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			button.AddAccelerator ("activate",
					       accel_group,
					       (uint) Gdk.Key.Escape, 
					       0,
					       0);

			AddActionWidget (button, Gtk.ResponseType.Ok);
			DefaultResponse = Gtk.ResponseType.Ok;
		}

		public Gtk.Widget MakeEditingPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;
			GConf.PropertyEditors.PropertyEditor peditor;

			// Page 1
			// List of editing options
			
			Gtk.VBox options_list = new Gtk.VBox (false, 12);
			options_list.BorderWidth = 12;
			options_list.Show ();

			// Spellchecking...

			check = MakeCheckButton (Catalog.GetString ("Spellcheck While Typing"));
			check.Toggled += OnSpellcheckToggled;
			options_list.PackStart (check, false, false, 0);

			label = MakeTipLabel (Catalog.GetString ("Misspellings will be underlined " +
								 "in red, and correct spelling " +
								 "suggestions shown in the right-click " +
								 "menu."));
			options_list.PackStart (label, false, false, 0);

			// WikiWords...

			check = MakeCheckButton (Catalog.GetString ("Highlight WikiWords"));
			check.Toggled += OnHighlightWikiWordsToggled;
			options_list.PackStart (check, false, false, 0);

			label = MakeTipLabel (Catalog.GetString ("Enable this option to highlight " +
								 "words <b>ThatLookLikeThis</b>.  " +
								 "Clicking the word will create a " +
								 "note with that name."));
			options_list.PackStart (label, false, false, 0);

			// Custom font...

			check = MakeCheckButton (Catalog.GetString ("Use Custom Font"));
			check.Toggled += OnUseCustomFontToggled;
			options_list.PackStart (check, false, false, 0);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.4f, 1.0f);
			align.Show ();
			options_list.PackStart (align, true, true, 0);

			Gtk.HBox font_box = new Gtk.HBox (false, 0);
			font_box.Show ();
			
			font_face = new Gtk.Label ("<span font_desc='Serif 11'>Serif</span>");
			font_face.UseMarkup = true;
			font_face.Show ();
			font_box.PackStart (font_face, true, true, 0);

			Gtk.VSeparator sep = new Gtk.VSeparator ();
			sep.Show ();
			font_box.PackStart (sep, false, false, 0);

			font_size = new Gtk.Label ("11");
			font_size.Xpad = 4;
			font_size.Show ();
			font_box.PackStart (font_size, false, false, 0);
			
			font_button = new Gtk.Button ();
			font_button.Clicked += OnFontButtonClicked;
			font_button.Add (font_box);
			font_button.Show ();
			align.Add (font_button);

			return options_list;
		}

		public Gtk.Widget MakeHotkeysPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;

			// Page 2
			// List of Hotkey options
			
			Gtk.VBox hotkeys_list = new Gtk.VBox (false, 12);
			hotkeys_list.BorderWidth = 12;
			hotkeys_list.Show ();

			// Hotkeys...

			check = MakeCheckButton (Catalog.GetString ("Listen for Hotkeys"));
			check.Toggled += OnListenForHotkeysToggled;
			hotkeys_list.PackStart (check, false, false, 0);

			label = MakeTipLabel (Catalog.GetString ("Hotkeys allow you to access " +
								 "your notes from any application. " +
								 "Example Hotkeys: " +
								 "<b>&lt;Control&gt;&lt;Shift&gt;F11</b>, " +
								 "<b>&lt;Alt&gt;N</b>"));
			hotkeys_list.PackStart (label, false, false, 0);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.4f, 1.0f);
			align.Show ();
			hotkeys_list.PackStart (align, false, false, 0);

			Gtk.Table table = new Gtk.Table (4, 2, false);
			table.ColumnSpacing = 6;
			table.RowSpacing = 6;
			table.Show ();
			align.Add (table);

			label = MakeLabel (Catalog.GetString ("<b>Show notes menu</b>"));
			table.Attach (label, 0, 1, 0, 1);

			show_menu_entry = new Gtk.Entry ();
			show_menu_entry.Changed += OnShowNotesMenuHotkeyChaged;
			show_menu_entry.Show ();
			table.Attach (show_menu_entry, 1, 2, 0, 1);

			label = MakeLabel (Catalog.GetString ("<b>Open \"Start Here\"</b>"));
			table.Attach (label, 0, 1, 1, 2);

			open_start_here_entry = new Gtk.Entry ();
			open_start_here_entry.Changed += OnOpenStartHereHotkeyChaged;
			open_start_here_entry.Show ();
			table.Attach (open_start_here_entry, 1, 2, 1, 2);

			label = MakeLabel (Catalog.GetString ("<b>Create new note</b>"));
			table.Attach (label, 0, 1, 2, 3);

			create_new_note_entry = new Gtk.Entry ();
			create_new_note_entry.Changed += OnCreateNewNoteHotkeyChaged;
			create_new_note_entry.Show ();
			table.Attach (create_new_note_entry, 1, 2, 2, 3);

			label = MakeLabel (Catalog.GetString ("<b>Search notes</b>"));
			table.Attach (label, 0, 1, 3, 4);

			open_search_entry = new Gtk.Entry ();
			open_search_entry.Changed += OnSearchNotesHotkeyChaged;
			open_search_entry.Show ();
			table.Attach (open_search_entry, 1, 2, 3, 4);

			return hotkeys_list;
		}

		// Utilities...

		GConf.Client Client 
		{
			get {
				if (client == null)
					client = new GConf.Client ();

				return client;
			}
		}

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
			label_text = String.Format ("<span weight='bold'>{0}" +
						    "</span>",
						    label_text);

			Gtk.Label label = MakeLabel (label_text);

			Gtk.CheckButton check = new Gtk.CheckButton ();
			check.Add (label);
			check.Show ();

			return check;
		}

		Gtk.Label MakeTipLabel (string label_text)
		{
			Gtk.Label label =  MakeLabel (String.Format ("<i>{0}</i>", label_text));
			label.LineWrap = true;
			label.Xpad = 20;
			return label;
		}

		// Change handlers

		void OnSpellcheckToggled (object sender, EventArgs args)
		{
			Gtk.CheckButton check = (Gtk.CheckButton) sender;

			Client.Set ("/apps/tomboy/enable_spellchecking",
				    check.Active);
		}

		void OnHighlightWikiWordsToggled (object sender, EventArgs args)
		{
			Gtk.CheckButton check = (Gtk.CheckButton) sender;

			Client.Set ("/apps/tomboy/enable_wikiwords",
				    check.Active);
		}

		void OnUseCustomFontToggled (object sender, EventArgs args)
		{
			Gtk.CheckButton check = (Gtk.CheckButton) sender;

			Client.Set ("/apps/tomboy/enable_custom_font",
				    check.Active);

			font_button.Sensitive = check.Active;
		}

		void OnFontButtonClicked (object sender, EventArgs args)
		{
			Gtk.FontSelectionDialog font_dialog = 
				new Gtk.FontSelectionDialog (Catalog.GetString ("Choose Note Font"));

			string font_name;

			try {
				font_name = (string) Client.Get ("/apps/tomboy/custom_font_face");
			} catch (Exception e) {
				font_name = "Serif 11";
			}

			font_dialog.SetFontName (font_name);

			if ((int) Gtk.ResponseType.Ok == font_dialog.Run ()) {
				if (font_name != font_dialog.FontName) {
					Client.Set ("/apps/tomboy/custom_font_face",
						    font_dialog.FontName);

					font_face.Markup = 
						String.Format ("<span font_desc='{0}'>{0}</span>",
							       font_dialog.FontName);
				}
			}

			font_dialog.Destroy ();
		}

		void OnListenForHotkeysToggled (object sender, EventArgs args)
		{
			Gtk.CheckButton check = (Gtk.CheckButton) sender;

			Client.Set ("/apps/tomboy/enable_keybindings",
				    check.Active);

			show_menu_entry.Sensitive = check.Active;
			open_start_here_entry.Sensitive = check.Active;
			create_new_note_entry.Sensitive = check.Active;
			open_search_entry.Sensitive = check.Active;
		}

		// Want to listen for unfocus/exit instead of change for these guys...

		void OnShowNotesMenuHotkeyChaged (object sender, EventArgs args)
		{
			Client.Set ("/apps/tomboy/global_keybindings/show_note_menu",
				    show_menu_entry.Text);
		}

		void OnOpenStartHereHotkeyChaged (object sender, EventArgs args)
		{
			Client.Set ("/apps/tomboy/global_keybindings/open_start_here",
				    open_start_here_entry.Text);
		}

		void OnCreateNewNoteHotkeyChaged (object sender, EventArgs args)
		{
			Client.Set ("/apps/tomboy/global_keybindings/create_new_note",
				    create_new_note_entry.Text);
		}

		void OnSearchNotesHotkeyChaged (object sender, EventArgs args)
		{
			Client.Set ("/apps/tomboy/global_keybindings/open_search",
				    open_search_entry.Text);
		}
	}
}
