
using System;
using System.Collections.Generic;
using GConf.PropertyEditors;
using Mono.Unix;

namespace Tomboy
{
	public class PreferencesDialog : Gtk.Dialog
	{
		readonly AddinManager addin_manager;

		Type current_extension;

		Gtk.Button font_button;
		Gtk.Label font_face;
		Gtk.Label font_size;
		
		Mono.Addins.Gui.AddinTreeWidget addin_tree;
		
		Gtk.Button enable_addin_button;
		Gtk.Button disable_addin_button;
		Gtk.Button addin_prefs_button;
		Gtk.Button addin_info_button;

		/// <summary>
		/// Keep track of the opened addin prefs dialogs so other windows
		/// can be interacted with (as opposed to opening these as modal
		/// dialogs).
		///
		/// Key = Mono.Addins.Addin.Id
		/// </summary>
		Dictionary<string, Gtk.Dialog> addin_prefs_dialogs;

		public PreferencesDialog (AddinManager addin_manager)
			: base ()
		{
			this.addin_manager = addin_manager;

			IconName = "tomboy";
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = true;
			Title = Catalog.GetString ("Tomboy Preferences");

			VBox.Spacing = 5;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;
			
			addin_prefs_dialogs =
				new Dictionary<string, Gtk.Dialog> ();

			// Notebook Tabs (Editing, Hotkeys)...

			Gtk.Notebook notebook = new Gtk.Notebook ();
			notebook.TabPos = Gtk.PositionType.Top;
			notebook.BorderWidth = 5;
			notebook.Show ();

			notebook.AppendPage (MakeEditingPane (), 
				new Gtk.Label (Catalog.GetString ("Editing")));
			notebook.AppendPage (MakeHotkeysPane (), 
				new Gtk.Label (Catalog.GetString ("Hotkeys")));
			notebook.AppendPage (MakeAddinsPane (),
			    new Gtk.Label (Catalog.GetString ("Add-ins")));

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

#if FIXED_GTKSPELL
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
#endif


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
		// Extension Preferences
		public Gtk.Widget MakeAddinsPane ()
		{
			Gtk.VBox vbox = new Gtk.VBox (false, 6);
			vbox.BorderWidth = 6;
			Gtk.Label l = new Gtk.Label (Catalog.GetString (
					"The following add-ins are installed"));
			l.Xalign = 0;
			l.Show ();
			vbox.PackStart (l, false, false, 0);
			
			Gtk.HBox hbox = new Gtk.HBox (false, 6);
			
			// TreeView of Add-ins
			Gtk.TreeView tree = new Gtk.TreeView ();
			addin_tree = new Mono.Addins.Gui.AddinTreeWidget (tree);
			
			tree.Show ();

			Gtk.ScrolledWindow sw = new Gtk.ScrolledWindow ();
			sw.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.ShadowType = Gtk.ShadowType.In;
			sw.Add (tree);
			sw.Show ();
			hbox.PackStart (sw, true, true, 0);

			// Action Buttons (right of TreeView)
			Gtk.VButtonBox button_box = new Gtk.VButtonBox ();
			button_box.Spacing = 4;
			button_box.Layout = Gtk.ButtonBoxStyle.Start;
			
			// TODO: In a future version, add in an "Install Add-ins..." button
			
			// TODO: In a future version, add in a "Repositories..." button
			
			enable_addin_button =
				new Gtk.Button (Catalog.GetString ("_Enable"));
			enable_addin_button.Sensitive = false;
			enable_addin_button.Clicked += OnEnableAddinButton;
			enable_addin_button.Show ();

			disable_addin_button =
				new Gtk.Button (Catalog.GetString ("_Disable"));
			disable_addin_button.Sensitive = false;
			disable_addin_button.Clicked += OnDisableAddinButton;
			disable_addin_button.Show ();

			addin_prefs_button =
				new Gtk.Button (Gtk.Stock.Preferences);
			addin_prefs_button.Sensitive = false;
			addin_prefs_button.Clicked += OnAddinPrefsButton;
			addin_prefs_button.Show ();

			addin_info_button =
				new Gtk.Button (Gtk.Stock.Info);
			addin_info_button.Sensitive = false;
			addin_info_button.Clicked += OnAddinInfoButton;
			addin_info_button.Show ();

			button_box.PackStart (enable_addin_button);
			button_box.PackStart (disable_addin_button);
			button_box.PackStart (addin_prefs_button);
			button_box.PackStart (addin_info_button);
			
			button_box.Show ();
			hbox.PackStart (button_box, false, false, 0);
			
			hbox.Show ();
			vbox.PackStart (hbox, true, true, 0);
			vbox.Show ();
			
			tree.Selection.Changed += OnAddinTreeSelectionChanged;
			LoadAddins ();
			
			return vbox;
		}
		
		void OnAddinTreeSelectionChanged (object sender, EventArgs args)
		{
			UpdateAddinButtons ();
		}
		
		/// <summary>
		/// Set the sensitivity of the buttons based on what is selected
		/// </summary>
		void UpdateAddinButtons ()
		{
			Mono.Addins.Addin sinfo =
					addin_tree.ActiveAddinData as Mono.Addins.Addin;
			
			if (sinfo == null) {
				enable_addin_button.Sensitive = false;
				disable_addin_button.Sensitive = false;
				addin_prefs_button.Sensitive = false;
				addin_info_button.Sensitive = false;
			} else {
				enable_addin_button.Sensitive = !sinfo.Enabled;
				disable_addin_button.Sensitive = sinfo.Enabled;
				addin_prefs_button.Sensitive = addin_manager.IsAddinConfigurable (sinfo);
				addin_info_button.Sensitive = true;
			}
		}
		
		void LoadAddins ()
		{
			object s = addin_tree.SaveStatus ();
			
			addin_tree.Clear ();
			foreach (Mono.Addins.Addin ainfo in addin_manager.GetAllAddins ()) {
				addin_tree.AddAddin (
					Mono.Addins.Setup.SetupService.GetAddinHeader (ainfo),
					ainfo,
					ainfo.Enabled,
					ainfo.IsUserAddin);
			}
			
			addin_tree.RestoreStatus (s);
			UpdateAddinButtons ();
		}
		
		void OnEnableAddinButton (object sender, EventArgs args)
		{
			Mono.Addins.Addin sinfo =
				addin_tree.ActiveAddinData as Mono.Addins.Addin;
			
			if (sinfo == null)
				return;
			
			EnableAddin (sinfo, true);
		}
		
		void OnDisableAddinButton (object sender, EventArgs args)
		{
			Mono.Addins.Addin sinfo =
				addin_tree.ActiveAddinData as Mono.Addins.Addin;
			
			if (sinfo == null)
				return;
				
			EnableAddin (sinfo, false);
		}
		
		void EnableAddin (Mono.Addins.Addin addin, bool enable)
		{
			addin.Enabled = enable;
			LoadAddins ();
		}
		
		void OnAddinPrefsButton (object sender, EventArgs args)
		{
			Gtk.Dialog dialog = null;
			Mono.Addins.Addin addin =
				addin_tree.ActiveAddinData as Mono.Addins.Addin;
			
			if (addin == null)
				return;
			
			if (addin_prefs_dialogs.ContainsKey (addin.Id) == false) {
				// A preference dialog isn't open already so create a new one
				Gtk.Image icon =
					new Gtk.Image (Gtk.Stock.Preferences, Gtk.IconSize.Dialog);
				Gtk.Label caption = new Gtk.Label ();
				caption.Markup = string.Format (
					"<span size='large' weight='bold'>{0} {1}</span>",
					addin.Name, addin.Version);
				caption.Xalign = 0;
				caption.UseMarkup = true;
				caption.UseUnderline = false;
				
				Gtk.Widget pref_widget =
					addin_manager.CreateAddinPreferenceWidget (addin);
				
				if (pref_widget == null)
					pref_widget = new Gtk.Label (Catalog.GetString ("Not Implemented"));
				
				Gtk.HBox hbox = new Gtk.HBox (false, 6);
				Gtk.VBox vbox = new Gtk.VBox (false, 6);
				vbox.BorderWidth = 6;
				
				hbox.PackStart (icon, false, false, 0);
				hbox.PackStart (caption, true, true, 0);
				vbox.PackStart (hbox, false, false, 0);
				
				vbox.PackStart (pref_widget, true, true, 0);
				vbox.ShowAll ();
				
				dialog = new Gtk.Dialog (
					string.Format (Catalog.GetString ("{0} Preferences"),
						addin.Name),
					this,
					Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.NoSeparator,
					Gtk.Stock.Close, Gtk.ResponseType.Close);
				
				dialog.VBox.PackStart (vbox, true, true, 0);
				dialog.DeleteEvent += AddinPrefDialogDeleted;
				dialog.Response += AddinPrefDialogResponse;
				
				// Store this dialog off in the dictionary so it can be
				// presented again if the user clicks on the preferences button
				// again before closing the preferences dialog.
				dialog.Data ["AddinId"] = addin.Id;
				addin_prefs_dialogs [addin.Id] = dialog;
			} else {
				// It's already opened so just present it again
				dialog = addin_prefs_dialogs [addin.Id];
			}
			
			dialog.Present ();
		}
		
		[GLib.ConnectBeforeAttribute]
		void AddinPrefDialogDeleted (object sender, Gtk.DeleteEventArgs args)
		{
			// Remove the addin from the addin_prefs_dialogs Dictionary
			Gtk.Dialog dialog = sender as Gtk.Dialog;
			dialog.Hide ();
			
			if (dialog.Data.ContainsKey ("AddinId")) {
				addin_prefs_dialogs.Remove (dialog.Data ["AddinId"] as String);
			}
			
			dialog.Destroy ();
		}
		
		void AddinPrefDialogResponse (object sender, Gtk.ResponseArgs args)
		{
			AddinPrefDialogDeleted (sender, null);
		}
		
		void OnAddinInfoButton (object sender, EventArgs args)
		{
			// TODO: Implement OnAddinInfoButton so an info dialog pops up
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
