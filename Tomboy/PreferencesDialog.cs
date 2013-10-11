
using System;
using System.Collections.Generic;
using System.Text;
using Mono.Unix;
using Gtk;
using Tomboy.Sync;

namespace Tomboy
{
	public class PreferencesDialog : Gtk.Window
	{
		Gtk.ListStore syncAddinStore;
		Dictionary<string, Gtk.TreeIter> syncAddinIters;
		Gtk.ComboBox syncAddinCombo;
		SyncServiceAddin selectedSyncAddin;
		Gtk.VBox syncAddinPrefsContainer;
		Gtk.Widget syncAddinPrefsWidget;
		Gtk.Button resetSyncAddinButton;
		Gtk.Button saveSyncAddinButton;
		Gtk.CheckButton autosyncCheck;
		Gtk.SpinButton autosyncSpinner;
		Gtk.ComboBox rename_behavior_combo;
		readonly AddinManager addin_manager;
		
		Gtk.Button font_button;
		Gtk.Label font_face;
		Gtk.Label font_size;

//		Mono.Addins.Gui.AddinTreeWidget addin_tree;

		Gtk.Button enable_addin_button;
		Gtk.Button disable_addin_button;
		Gtk.Button addin_prefs_button;
		Gtk.Button addin_info_button;

		private Gtk.RadioButton promptOnConflictRadio;
		private Gtk.RadioButton renameOnConflictRadio;
		private Gtk.RadioButton overwriteOnConflictRadio;

		/// <summary>
		/// Keep track of the opened addin prefs dialogs so other windows
		/// can be interacted with (as opposed to opening these as modal
		/// dialogs).
		///
		/// Key = Mono.Addins.Addin.Id
		/// </summary>
		Dictionary<string, Gtk.Dialog> addin_prefs_dialogs;

		/// <summary>
		/// Used to keep track of open AddinInfoDialogs.
		/// Key = Mono.Addins.Addin.Id
		/// </summary>
		Dictionary<string, Gtk.Dialog> addin_info_dialogs;

		public PreferencesDialog (NoteManager manager) : base(Gtk.WindowType.Toplevel)
		{
			this.addin_manager = manager.AddinManager;
			
			IconName = "tomboy";
			BorderWidth = 5;
			Resizable = true;
			Title = Catalog.GetString ("Tomboy Preferences");
			WindowPosition = WindowPosition.Center;
			
			addin_prefs_dialogs = new Dictionary<string, Gtk.Dialog> ();
			addin_info_dialogs = new Dictionary<string, Gtk.Dialog> ();
			
			// Notebook Tabs (Editing, Hotkeys)...
			
			Gtk.Notebook notebook = new Gtk.Notebook ();
			notebook.TabPos = Gtk.PositionType.Top;
			notebook.Show ();
			
			notebook.AppendPage (MakeEditingPane (), new Gtk.Label (Catalog.GetString ("Editing")));
			
			if (!(Services.Keybinder is NullKeybinder))
				notebook.AppendPage (MakeHotkeysPane (), new Gtk.Label (Catalog.GetString ("Hotkeys")));
			
			notebook.AppendPage (MakeSyncPane (), new Gtk.Label (Catalog.GetString ("Synchronization")));
			notebook.AppendPage (MakeAddinsPane (), new Gtk.Label (Catalog.GetString ("Add-ins")));
			
			// TODO: Figure out a way to have these be placed in a specific order
			foreach (PreferenceTabAddin tabAddin in addin_manager.GetPreferenceTabAddins ()) {
				Logger.Debug ("Adding preference tab addin: {0}", tabAddin.GetType ().Name);
				try {
					string tabName;
					Gtk.Widget tabWidget;
					if (tabAddin.GetPreferenceTabWidget (this, out tabName, out tabWidget) == true) {
						notebook.AppendPage (tabWidget, new Gtk.Label (tabName));
					}
				} catch (Exception e) {
					Logger.Warn ("Problems adding preferences tab addin: {0}", tabAddin.GetType ().Name);
					Logger.Debug ("{0}:\n{1}", e.Message, e.StackTrace);
				}
			}
			Gtk.VBox VBox = new Gtk.VBox ();
			VBox.PackStart (notebook, true, true, 0);
			
			addin_manager.ApplicationAddinListChanged += OnAppAddinListChanged;
			
			// Close Button
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Close);
			button.CanDefault = true;
			button.Label = "Close";
			button.Clicked += OnClickedClose;
			VBox.Add (button);
			button.Show ();
			
			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);
			
			button.AddAccelerator ("activate", accel_group, (uint)Gdk.Key.Escape, 0, 0);
			
			this.Add (VBox);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.Show ();
			Preferences.SettingChanged += HandlePreferencesSettingChanged;
		}
		
		/// <summary>
		/// Close the Preferences Window
		/// </summary>
		/// <param name="sender">
		/// A <see cref="System.Object"/>
		/// </param>
		/// <param name="args">
		/// A <see cref="EventArgs"/>
		/// </param>
		void OnClickedClose (object sender, EventArgs args)
		{
			Hide ();
			Destroy ();
		}		

		void HandlePreferencesSettingChanged (object sender, NotifyEventArgs args)
		{
			if (args.Key == Preferences.NOTE_RENAME_BEHAVIOR) {
				int rename_behavior = (int) args.Value;
				if (rename_behavior < 0 || rename_behavior > 2) {
					rename_behavior = 0;
					Preferences.Set (Preferences.NOTE_RENAME_BEHAVIOR, rename_behavior);
				}
				if (rename_behavior_combo.Active != rename_behavior)
					rename_behavior_combo.Active = rename_behavior;
			} else if (args.Key == Preferences.SYNC_AUTOSYNC_TIMEOUT) {
				int timeout = (int) args.Value;
				if (timeout <= 0 && autosyncCheck.Active)
					autosyncCheck.Active = false;
				else if (timeout > 0) {
					timeout = (timeout >= 5 && timeout < 1000) ? timeout : 5;
					if (!autosyncCheck.Active)
						autosyncCheck.Active = true;
					if ((int) autosyncSpinner.Value != timeout)
						autosyncSpinner.Value = timeout;
				}
			}
		}
		
		// Page 1
		// List of editing options
		public Gtk.Widget MakeEditingPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;
			IPropertyEditorBool peditor, font_peditor, bullet_peditor;

			Gtk.VBox options_list = new Gtk.VBox (false, 12);
			options_list.BorderWidth = 12;
			options_list.Show ();


			// Spell checking...

			#if FIXED_GTKSPELL
			if (NoteSpellChecker.GtkSpellAvailable) {
				check = MakeCheckButton (
				                Catalog.GetString ("_Spell check while typing"));
				options_list.PackStart (check, false, false, 0);

				peditor = Services.Factory.CreatePropertyEditorToggleButton (
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

			peditor = Services.Factory.CreatePropertyEditorToggleButton (Preferences.ENABLE_WIKIWORDS,
			                check);
			SetupPropertyEditor (peditor);

			label = MakeTipLabel (
			                Catalog.GetString ("Enable this option to highlight " +
			                                   "words <b>ThatLookLikeThis</b>. " +
			                                   "Clicking the word will create a " +
			                                   "note with that name."));
			options_list.PackStart (label, false, false, 0);

			// Auto bulleted list
			check = MakeCheckButton (Catalog.GetString ("Enable auto-_bulleted lists"));
			options_list.PackStart (check, false, false, 0);
			bullet_peditor =
			        Services.Factory.CreatePropertyEditorToggleButton (Preferences.ENABLE_AUTO_BULLETED_LISTS,
			                                        check);
			SetupPropertyEditor (bullet_peditor);

			// Custom font...
			Gtk.HBox font_box = new Gtk.HBox (false, 0);
			check = MakeCheckButton (Catalog.GetString ("Use custom _font"));
			font_box.PackStart (check);

			font_peditor =
			        Services.Factory.CreatePropertyEditorToggleButton (Preferences.ENABLE_CUSTOM_FONT,
			                                        check);
			SetupPropertyEditor (font_peditor);

			font_button = MakeFontButton ();
			font_button.Sensitive = check.Active;
			font_box.PackStart (font_button);
			font_box.ShowAll ();
			options_list.PackStart (font_box, false, false, 0);

			font_peditor.AddGuard (font_button);

			// Note renaming bahvior
			Gtk.HBox rename_behavior_box = new Gtk.HBox (false, 0);
			label = MakeLabel (Catalog.GetString ("When renaming a linked note: "));
			rename_behavior_box.PackStart (label);
			rename_behavior_combo = new Gtk.ComboBox (new string [] {
				Catalog.GetString ("Ask me what to do"),
				Catalog.GetString ("Never rename links"),
				Catalog.GetString ("Always rename links")});
			int rename_behavior = (int) Preferences.Get (Preferences.NOTE_RENAME_BEHAVIOR);
			if (rename_behavior < 0 || rename_behavior > 2) {
				rename_behavior = 0;
				Preferences.Set (Preferences.NOTE_RENAME_BEHAVIOR, rename_behavior);
			}
			rename_behavior_combo.Active = rename_behavior;
			rename_behavior_combo.Changed += (o, e) =>
				Preferences.Set (Preferences.NOTE_RENAME_BEHAVIOR,
				                 rename_behavior_combo.Active);
			rename_behavior_box.PackStart (rename_behavior_combo);
			rename_behavior_box.ShowAll ();
			options_list.PackStart (rename_behavior_box, false, false, 0);
			
			// New Note Template
			// Translators: This is 'New Note' Template, not New 'Note Template'
			label = MakeLabel (Catalog.GetString ("New Note Template"));
			options_list.PackStart (label, false, false, 0);

			label = MakeTipLabel (
				Catalog.GetString ("Use the new note template to specify the text " +
								   "that should be used when creating a new note."));
			options_list.PackStart (label, false, false, 0);
			
			align = new Gtk.Alignment (0.5f, 0.5f, 0.4f, 1.0f);
			align.Show ();
			options_list.PackStart (align, false, false, 0);
			
			Gtk.Button open_template_button = new Gtk.Button ();
			open_template_button.Label = Catalog.GetString ("Open New Note Template");

			open_template_button.Clicked += OpenTemplateButtonClicked;
			open_template_button.Show ();
			align.Add (open_template_button);

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
			IPropertyEditorBool keybind_peditor;
			IPropertyEditor peditor;

			Gtk.VBox hotkeys_list = new Gtk.VBox (false, 12);
			hotkeys_list.BorderWidth = 12;
			hotkeys_list.Show ();


			// Hotkeys...

			check = MakeCheckButton (Catalog.GetString ("Listen for _Hotkeys"));
			hotkeys_list.PackStart (check, false, false, 0);

			keybind_peditor =
			        Services.Factory.CreatePropertyEditorToggleButton (Preferences.ENABLE_KEYBINDINGS,
			                                        check);
			SetupPropertyEditor (keybind_peditor);

			label = MakeTipLabel (
			                Catalog.GetString ("Hotkeys allow you to quickly access " +
			                                   "your notes from anywhere with a keypress. " +
			                                   "Example Hotkeys: " +
			                                   "<b>&lt;ALT&gt;F11</b>, " +
			                                   "<b>&lt;ALT&gt;N</b>"));
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

			peditor = Services.Factory.CreatePropertyEditorEntry (Preferences.KEYBINDING_SHOW_NOTE_MENU,
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

			peditor = Services.Factory.CreatePropertyEditorEntry (Preferences.KEYBINDING_OPEN_START_HERE,
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

			peditor = Services.Factory.CreatePropertyEditorEntry (Preferences.KEYBINDING_CREATE_NEW_NOTE,
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

			peditor = Services.Factory.CreatePropertyEditorEntry (
			        Preferences.KEYBINDING_OPEN_RECENT_CHANGES,
			        entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			return hotkeys_list;
		}

		public Gtk.Widget MakeSyncPane ()
		{
			Gtk.VBox vbox = new Gtk.VBox (false, 0);
			vbox.Spacing = 4;
			vbox.BorderWidth = 8;

			Gtk.HBox hbox = new Gtk.HBox (false, 4);

			Gtk.Label label = new Gtk.Label (Catalog.GetString ("Ser_vice:"));
			label.Xalign = 0;
			label.Show ();
			hbox.PackStart (label, false, false, 0);

			// Populate the store with all the available SyncServiceAddins
			syncAddinStore = new Gtk.ListStore (typeof (SyncServiceAddin));
			syncAddinIters = new Dictionary<string,Gtk.TreeIter> ();
			SyncServiceAddin [] addins = Tomboy.DefaultNoteManager.AddinManager.GetSyncServiceAddins ();
			Array.Sort (addins, CompareSyncAddinsByName);
			foreach (SyncServiceAddin addin in addins) {
				Gtk.TreeIter iter = syncAddinStore.Append ();
				syncAddinStore.SetValue (iter, 0, addin);
				syncAddinIters [addin.Id] = iter;
			}

			syncAddinCombo = new Gtk.ComboBox (syncAddinStore);
			label.MnemonicWidget = syncAddinCombo;
			Gtk.CellRendererText crt = new Gtk.CellRendererText ();
			syncAddinCombo.PackStart (crt, true);
			syncAddinCombo.SetCellDataFunc (crt,
			                                new Gtk.CellLayoutDataFunc (ComboBoxTextDataFunc));

			// Read from Preferences which service is configured and select it
			// by default.  Otherwise, just select the first one in the list.
			string addin_id = Preferences.Get (
			                          Preferences.SYNC_SELECTED_SERVICE_ADDIN) as String;

			Gtk.TreeIter active_iter;
			if (addin_id != null && syncAddinIters.ContainsKey (addin_id)) {
				active_iter = syncAddinIters [addin_id];
				syncAddinCombo.SetActiveIter (active_iter);
			} else {
				if (syncAddinStore.GetIterFirst (out active_iter) == true) {
					syncAddinCombo.SetActiveIter (active_iter);
				}
			}

			syncAddinCombo.Changed += OnSyncAddinComboChanged;

			syncAddinCombo.Show ();
			hbox.PackStart (syncAddinCombo, true, true, 0);

			hbox.Show ();
			vbox.PackStart (hbox, false, false, 0);

			// Get the preferences GUI for the Sync Addin
			if (active_iter.Stamp != Gtk.TreeIter.Zero.Stamp)
				selectedSyncAddin = syncAddinStore.GetValue (active_iter, 0) as SyncServiceAddin;

			if (selectedSyncAddin != null)
				syncAddinPrefsWidget = selectedSyncAddin.CreatePreferencesControl (OnSyncAddinPrefsChanged);
			if (syncAddinPrefsWidget == null) {
				Gtk.Label l = new Gtk.Label (Catalog.GetString ("Not configurable"));
				l.Yalign = 0.5f;
				l.Yalign = 0.5f;
				syncAddinPrefsWidget = l;
			}
			if (syncAddinPrefsWidget != null && addin_id != null &&
			                syncAddinIters.ContainsKey (addin_id) && selectedSyncAddin.IsConfigured)
				syncAddinPrefsWidget.Sensitive = false;

			syncAddinPrefsWidget.Show ();
			syncAddinPrefsContainer = new Gtk.VBox (false, 0);
			syncAddinPrefsContainer.PackStart (syncAddinPrefsWidget, false, false, 0);
			syncAddinPrefsContainer.Show ();
			vbox.PackStart (syncAddinPrefsContainer, true, true, 10);

			// Autosync preference
			int timeout = (int) Preferences.Get (Preferences.SYNC_AUTOSYNC_TIMEOUT);
			if (timeout > 0 && timeout < 5) {
				timeout = 5;
				Preferences.Set (Preferences.SYNC_AUTOSYNC_TIMEOUT, 5);
			}
			Gtk.HBox autosyncBox = new Gtk.HBox (false, 5);
			// Translators: This is and the next string go together.
			// Together they look like "Automatically Sync in Background Every [_] Minutes",
			// where "[_]" is a GtkSpinButton.
			autosyncCheck =
				new Gtk.CheckButton (Catalog.GetString ("Automaticall_y Sync in Background Every"));
			autosyncSpinner = new Gtk.SpinButton (5, 1000, 1);
			autosyncSpinner.Value = timeout >= 5 ? timeout : 10;
			Gtk.Label autosyncExtraText =
				// Translators: See above comment for details on
				// this string.
				new Gtk.Label (Catalog.GetString ("Minutes"));
			autosyncCheck.Active = autosyncSpinner.Sensitive = timeout >= 5;
			EventHandler updateTimeoutPref = (o, e) => {
				Preferences.Set (Preferences.SYNC_AUTOSYNC_TIMEOUT,
				                 autosyncCheck.Active ? (int) autosyncSpinner.Value : -1);
			};
			autosyncCheck.Toggled += (o, e) => {
				autosyncSpinner.Sensitive = autosyncCheck.Active;
				updateTimeoutPref (o, e);
			};
			autosyncSpinner.ValueChanged += updateTimeoutPref;

			autosyncBox.PackStart (autosyncCheck);
			autosyncBox.PackStart (autosyncSpinner);
			autosyncBox.PackStart (autosyncExtraText);
			vbox.PackStart (autosyncBox, false, true, 0);

			Gtk.HButtonBox bbox = new Gtk.HButtonBox ();
			bbox.Spacing = 4;
			bbox.LayoutStyle = Gtk.ButtonBoxStyle.End;

			// "Advanced..." button to bring up extra sync config dialog
			Gtk.Button advancedConfigButton = new Gtk.Button (Catalog.GetString ("_Advanced..."));
			advancedConfigButton.Clicked += OnAdvancedSyncConfigButton;
			advancedConfigButton.Show ();
			bbox.PackStart (advancedConfigButton, false, false, 0);
			bbox.SetChildSecondary (advancedConfigButton, true);

			resetSyncAddinButton = new Gtk.Button (Gtk.Stock.Clear);
			resetSyncAddinButton.Clicked += OnResetSyncAddinButton;
			resetSyncAddinButton.Sensitive =
			        (selectedSyncAddin != null &&
			         addin_id == selectedSyncAddin.Id &&
			         selectedSyncAddin.IsConfigured);
			resetSyncAddinButton.Show ();
			bbox.PackStart (resetSyncAddinButton, false, false, 0);

			// TODO: Tabbing should go directly from sync prefs widget to here
			// TODO: Consider connecting to "Enter" pressed in sync prefs widget
			saveSyncAddinButton = new Gtk.Button (Gtk.Stock.Save);
			saveSyncAddinButton.Clicked += OnSaveSyncAddinButton;
			saveSyncAddinButton.Sensitive =
			        (selectedSyncAddin != null &&
			         (addin_id != selectedSyncAddin.Id || !selectedSyncAddin.IsConfigured));
			saveSyncAddinButton.Show ();
			bbox.PackStart (saveSyncAddinButton, false, false, 0);

			syncAddinCombo.Sensitive =
			        (selectedSyncAddin == null ||
			         addin_id != selectedSyncAddin.Id ||
			         !selectedSyncAddin.IsConfigured);

			bbox.Show ();
			vbox.PackStart (bbox, false, false, 0);

			vbox.ShowAll ();
			return vbox;
		}

		private int CompareSyncAddinsByName (SyncServiceAddin addin1, SyncServiceAddin addin2)
		{
			return addin1.Name.CompareTo (addin2.Name);
		}

		private void ComboBoxTextDataFunc (Gtk.ICellLayout cell_layout, Gtk.CellRenderer cell,
		                                   Gtk.ITreeModel tree_model, Gtk.TreeIter iter)
		{
			Gtk.CellRendererText crt = cell as Gtk.CellRendererText;
			SyncServiceAddin addin = tree_model.GetValue (iter, 0) as SyncServiceAddin;
			if (addin == null) {
				crt.Text = string.Empty;
			} else {
				crt.Text = addin.Name;
			}
		}

		// Page 3
		// Extension Preferences
//		public Gtk.Widget MakeAddinsPane ()
//		{
//			Gtk.VBox vbox = new Gtk.VBox (false, 6);
//			vbox.BorderWidth = 6;
//			Gtk.Label l = new Gtk.Label (Catalog.GetString (
//			                                     "The following add-ins are installed"));
//			l.Xalign = 0;
//			l.Show ();
//			vbox.PackStart (l, false, false, 0);
//
//			Gtk.HBox hbox = new Gtk.HBox (false, 6);
//
//			// TreeView of Add-ins
//			Gtk.TreeView tree = new Gtk.TreeView ();
//			addin_tree = new Mono.Addins.Gui.AddinTreeWidget (tree);
//
//			tree.Show ();
//
//			Gtk.ScrolledWindow sw = new Gtk.ScrolledWindow ();
//			sw.HscrollbarPolicy = Gtk.PolicyType.Automatic;
//			sw.VscrollbarPolicy = Gtk.PolicyType.Automatic;
//			sw.ShadowType = Gtk.ShadowType.In;
//			sw.Add (tree);
//			sw.Show ();
//			Gtk.LinkButton get_more_link =
//				new Gtk.LinkButton ("http://live.gnome.org/Tomboy/PluginList",
//				                    Catalog.GetString ("Get More Add-Ins..."));
//			get_more_link.Show ();
//			Gtk.VBox tree_box = new Gtk.VBox (false, 0);
//			tree_box.Add (sw);
//			tree_box.PackEnd (get_more_link, false, false, 5);
//			tree_box.Show ();
//			hbox.PackStart (tree_box, true, true, 0);
//
//			// Action Buttons (right of TreeView)
//			Gtk.VButtonBox button_box = new Gtk.VButtonBox ();
//			button_box.Spacing = 4;
//			button_box.Layout = Gtk.ButtonBoxStyle.Start;
//
//			// TODO: In a future version, add in an "Install Add-ins..." button
//
//			// TODO: In a future version, add in a "Repositories..." button
//
//			enable_addin_button =
//			        new Gtk.Button (Catalog.GetString ("_Enable"));
//			enable_addin_button.Sensitive = false;
//			enable_addin_button.Clicked += OnEnableAddinButton;
//			enable_addin_button.Show ();
//
//			disable_addin_button =
//			        new Gtk.Button (Catalog.GetString ("_Disable"));
//			disable_addin_button.Sensitive = false;
//			disable_addin_button.Clicked += OnDisableAddinButton;
//			disable_addin_button.Show ();
//
//			addin_prefs_button =
//			        new Gtk.Button (Gtk.Stock.Preferences);
//			addin_prefs_button.Sensitive = false;
//			addin_prefs_button.Clicked += OnAddinPrefsButton;
//			addin_prefs_button.Show ();
//
//			addin_info_button =
//			        new Gtk.Button (Gtk.Stock.Info);
//			addin_info_button.Sensitive = false;
//			addin_info_button.Clicked += OnAddinInfoButton;
//			addin_info_button.Show ();
//
//			button_box.PackStart (enable_addin_button);
//			button_box.PackStart (disable_addin_button);
//			button_box.PackStart (addin_prefs_button);
//			button_box.PackStart (addin_info_button);
//
//			button_box.Show ();
//			hbox.PackStart (button_box, false, false, 0);
//
//			hbox.Show ();
//			vbox.PackStart (hbox, true, true, 0);
//			vbox.Show ();
//
//			tree.Selection.Changed += OnAddinTreeSelectionChanged;
//			LoadAddins ();
//
//			return vbox;
////		}
//
//		void OnAddinTreeSelectionChanged (object sender, EventArgs args)
//		{
//			UpdateAddinButtons ();
//		}
//
//		/// <summary>
//		/// Set the sensitivity of the buttons based on what is selected
//		/// </summary>
//		void UpdateAddinButtons ()
//		{
//			Mono.Addins.Addin sinfo =
//			        addin_tree.ActiveAddinData as Mono.Addins.Addin;
//
//			if (sinfo == null) {
//				enable_addin_button.Sensitive = false;
//				disable_addin_button.Sensitive = false;
//				addin_prefs_button.Sensitive = false;
//				addin_info_button.Sensitive = false;
//			} else {
//				enable_addin_button.Sensitive = !sinfo.Enabled;
//				disable_addin_button.Sensitive = sinfo.Enabled;
//				addin_prefs_button.Sensitive = addin_manager.IsAddinConfigurable (sinfo);
//				addin_info_button.Sensitive = true;
//			}
//		}
//
//		void LoadAddins ()
//		{
//			object s = addin_tree.SaveStatus ();
//
//			addin_tree.Clear ();
//			foreach (Mono.Addins.Addin ainfo in addin_manager.GetAllAddins ()) {
//				addin_tree.AddAddin (
//				        Mono.Addins.Setup.SetupService.GetAddinHeader (ainfo),
//				        ainfo,
//				        ainfo.Enabled,
//				        ainfo.IsUserAddin);
//			}
//
//			addin_tree.RestoreStatus (s);
//			UpdateAddinButtons ();
//		}
//
//		void OnEnableAddinButton (object sender, EventArgs args)
//		{
//			Mono.Addins.Addin sinfo =
//			        addin_tree.ActiveAddinData as Mono.Addins.Addin;
//
//			if (sinfo == null)
//				return;
//
//			EnableAddin (sinfo, true);
//		}
//
//		void OnDisableAddinButton (object sender, EventArgs args)
//		{
//			Mono.Addins.Addin sinfo =
//			        addin_tree.ActiveAddinData as Mono.Addins.Addin;
//
//			if (sinfo == null)
//				return;
//
//			EnableAddin (sinfo, false);
//		}
//
//		void EnableAddin (Mono.Addins.Addin addin, bool enable)
//		{
//			addin.Enabled = enable;
//			LoadAddins ();
//		}
//
//		void OnAddinPrefsButton (object sender, EventArgs args)
//		{
//			Gtk.Dialog dialog = null;
//			Mono.Addins.Addin addin =
//			        addin_tree.ActiveAddinData as Mono.Addins.Addin;
//
//			if (addin == null)
//				return;
//
//			if (addin_prefs_dialogs.ContainsKey (addin.Id) == false) {
//				// A preference dialog isn't open already so create a new one
//				Gtk.Image icon =
//				        new Gtk.Image (Gtk.Stock.Preferences, Gtk.IconSize.Dialog);
//				Gtk.Label caption = new Gtk.Label ();
//				caption.Markup = string.Format (
//				                         "<span size='large' weight='bold'>{0} {1}</span>",
//				                         addin.Name, addin.Version);
//				caption.Xalign = 0;
//				caption.UseMarkup = true;
//				caption.UseUnderline = false;
//
//				Gtk.Widget pref_widget =
//				        addin_manager.CreateAddinPreferenceWidget (addin);
//
//				if (pref_widget == null)
//					pref_widget = new Gtk.Label (Catalog.GetString ("Not Implemented"));
//
//				Gtk.HBox hbox = new Gtk.HBox (false, 6);
//				Gtk.VBox vbox = new Gtk.VBox (false, 6);
//				vbox.BorderWidth = 6;
//
//				hbox.PackStart (icon, false, false, 0);
//				hbox.PackStart (caption, true, true, 0);
//				vbox.PackStart (hbox, false, false, 0);
//
//				vbox.PackStart (pref_widget, true, true, 0);
//				vbox.ShowAll ();
//
//				dialog = new Gtk.Dialog (
//				        string.Format (Catalog.GetString ("{0} Preferences"),
//				                       addin.Name),
//				        this,
//				        Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.NoSeparator,
//				        Gtk.Stock.Close, Gtk.ResponseType.Close);
//
//				dialog.VBox.PackStart (vbox, true, true, 0);
//				dialog.DeleteEvent += AddinPrefDialogDeleted;
//				dialog.Response += AddinPrefDialogResponse;
//
//				// Store this dialog off in the dictionary so it can be
//				// presented again if the user clicks on the preferences button
//				// again before closing the preferences dialog.
//				dialog.Data ["AddinId"] = addin.Id;
//				addin_prefs_dialogs [addin.Id] = dialog;
//			} else {
//				// It's already opened so just present it again
//				dialog = addin_prefs_dialogs [addin.Id];
//			}
//
//			dialog.Present ();
//		}
//
//		[GLib.ConnectBeforeAttribute]
//		void AddinPrefDialogDeleted (object sender, Gtk.DeleteEventArgs args)
//		{
//			// Remove the addin from the addin_prefs_dialogs Dictionary
//			Gtk.Dialog dialog = sender as Gtk.Dialog;
//			dialog.Hide ();
//
//			if (dialog.Data.ContainsKey ("AddinId")) {
//				addin_prefs_dialogs.Remove (dialog.Data ["AddinId"] as String);
//			}
//
//			dialog.Destroy ();
//		}
//
//		void AddinPrefDialogResponse (object sender, Gtk.ResponseArgs args)
//		{
//			AddinPrefDialogDeleted (sender, null);
//		}
//
//		void OnAddinInfoButton (object sender, EventArgs args)
//		{
//			Mono.Addins.Addin addin =
//			        addin_tree.ActiveAddinData as Mono.Addins.Addin;
//
//			if (addin == null)
//				return;
//
//			Gtk.Dialog dialog = null;
//			if (addin_info_dialogs.ContainsKey (addin.Id) == false) {
//				dialog = new AddinInfoDialog (
//				        Mono.Addins.Setup.SetupService.GetAddinHeader (addin),
//				        this);
//				dialog.DeleteEvent += AddinInfoDialogDeleted;
//				dialog.Response += AddinInfoDialogResponse;
//
//				// Store this dialog off in a dictionary so it can be presented
//				// again if the user clicks on the Info button before closing
//				// the original dialog.
//				dialog.Data ["AddinId"] = addin.Id;
//				addin_info_dialogs [addin.Id] = dialog;
//			} else {
//				// It's already opened so just present it again
//				dialog = addin_info_dialogs [addin.Id];
//			}
//
//			dialog.Present ();
//		}
//
//		[GLib.ConnectBeforeAttribute]
//		void AddinInfoDialogDeleted (object sender, Gtk.DeleteEventArgs args)
//		{
//			// Remove the addin from the addin_prefs_dialogs Dictionary
//			Gtk.Dialog dialog = sender as Gtk.Dialog;
//			dialog.Hide ();
//
//			if (dialog.Data.ContainsKey ("AddinId")) {
//				addin_info_dialogs.Remove (dialog.Data ["AddinId"] as String);
//			}
//
//			dialog.Destroy ();
//		}

		void AddinInfoDialogResponse (object sender, Gtk.ResponseArgs args)
		{
			AddinInfoDialogDeleted (sender, null);
		}

		void SetupPropertyEditor (IPropertyEditor peditor)
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

		private void OnAdvancedSyncConfigButton (object sender, EventArgs args)
		{
			// Get saved behavior
			SyncTitleConflictResolution savedBehavior = SyncTitleConflictResolution.Cancel;
			object dlgBehaviorPref = Preferences.Get (Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR);
			if (dlgBehaviorPref != null && dlgBehaviorPref is int) // TODO: Check range of this int
				savedBehavior = (SyncTitleConflictResolution)dlgBehaviorPref;

			// Create dialog
			Gtk.Dialog advancedDlg =
			        new Gtk.Dialog (Catalog.GetString ("Other Synchronization Options"),
			                        this,
			                        Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.Modal | Gtk.DialogFlags.NoSeparator,
			                        Gtk.Stock.Close, Gtk.ResponseType.Close);
			// Populate dialog
			Gtk.Label label =
			        new Gtk.Label (Catalog.GetString ("When a conflict is detected between " +
			                                          "a local note and a note on the configured " +
			                                          "synchronization server:"));
			label.Wrap = true;
			label.Xalign = 0;

			promptOnConflictRadio =
			        new Gtk.RadioButton (Catalog.GetString ("Always ask me what to do."));
			promptOnConflictRadio.Toggled += OnConflictOptionToggle;

			renameOnConflictRadio =
			        new Gtk.RadioButton (promptOnConflictRadio, Catalog.GetString ("Rename my local note."));
			renameOnConflictRadio.Toggled += OnConflictOptionToggle;

			overwriteOnConflictRadio =
			        new Gtk.RadioButton (promptOnConflictRadio, Catalog.GetString ("Replace my local note with the server's update."));
			overwriteOnConflictRadio.Toggled += OnConflictOptionToggle;

			switch (savedBehavior) {
			case SyncTitleConflictResolution.RenameExistingNoUpdate:
				renameOnConflictRadio.Active = true;
				break;
			case SyncTitleConflictResolution.OverwriteExisting:
				overwriteOnConflictRadio.Active = true;
				break;
			default:
				promptOnConflictRadio.Active = true;
				break;
			}

			Gtk.VBox vbox = new Gtk.VBox ();
			vbox.BorderWidth = 18;

			vbox.PackStart (promptOnConflictRadio);
			vbox.PackStart (renameOnConflictRadio);
			vbox.PackStart (overwriteOnConflictRadio);

			advancedDlg.VBox.PackStart (label, false, false, 6);
			advancedDlg.VBox.PackStart (vbox, false, false, 0);

			advancedDlg.ShowAll ();

			// Run dialog
			advancedDlg.Run ();
			advancedDlg.Destroy ();
		}

		private void OnConflictOptionToggle (object sender, EventArgs args)
		{
			SyncTitleConflictResolution newBehavior = SyncTitleConflictResolution.Cancel;

			if (renameOnConflictRadio.Active)
				newBehavior = SyncTitleConflictResolution.RenameExistingNoUpdate;
			else if (overwriteOnConflictRadio.Active)
				newBehavior = SyncTitleConflictResolution.OverwriteExisting;

			Preferences.Set (Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR,
			                 (int) newBehavior);
		}

		private void OnAppAddinListChanged (object sender, EventArgs args)
		{
			SyncServiceAddin [] newAddinsArray = Tomboy.DefaultNoteManager.AddinManager.GetSyncServiceAddins ();
			Array.Sort (newAddinsArray, CompareSyncAddinsByName);
			List<SyncServiceAddin> newAddins = new List<SyncServiceAddin> (newAddinsArray);

			// Build easier-to-navigate list if addins currently in the combo
			List<SyncServiceAddin> currentAddins = new List<SyncServiceAddin> ();
			foreach (object [] currentRow in syncAddinStore) {
				SyncServiceAddin currentAddin = currentRow [0] as SyncServiceAddin;
				if (currentAddin != null)
					currentAddins.Add (currentAddin);
			}

			// Add new addins
			// TODO: Would be nice to insert these alphabetically instead
			foreach (SyncServiceAddin newAddin in newAddins) {
				if (!currentAddins.Contains (newAddin)) {
					Gtk.TreeIter iter = syncAddinStore.Append ();
					syncAddinStore.SetValue (iter, 0, newAddin);
					syncAddinIters [newAddin.Id] = iter;
				}
			}

			// Remove deleted addins
			foreach (SyncServiceAddin currentAddin in currentAddins) {
				if (!newAddins.Contains (currentAddin)) {
					Gtk.TreeIter iter = syncAddinIters [currentAddin.Id];
					syncAddinStore.Remove (ref iter);
					syncAddinIters.Remove (currentAddin.Id);

					// FIXME: Lots of hacky stuff in here...rushing before freeze
					if (currentAddin == selectedSyncAddin) {
						if (syncAddinPrefsWidget != null &&
						                !syncAddinPrefsWidget.Sensitive)
							OnResetSyncAddinButton (null, null);

						Gtk.TreeIter active_iter;
						if (syncAddinStore.GetIterFirst (out active_iter) == true) {
							syncAddinCombo.SetActiveIter (active_iter);
						} else {
							OnSyncAddinComboChanged (null, null);
						}
					}
				}
			}
		}

		void OnSyncAddinComboChanged (object sender, EventArgs args)
		{
			if (syncAddinPrefsWidget != null) {
				syncAddinPrefsContainer.Remove (syncAddinPrefsWidget);
				syncAddinPrefsWidget.Hide ();
				syncAddinPrefsWidget.Destroy ();
				syncAddinPrefsWidget = null;
			}

			Gtk.TreeIter iter;
			if (syncAddinCombo.GetActiveIter (out iter)) {
				SyncServiceAddin newAddin =
				        syncAddinStore.GetValue (iter, 0) as SyncServiceAddin;
				if (newAddin != null) {
					selectedSyncAddin = newAddin;
					syncAddinPrefsWidget = selectedSyncAddin.CreatePreferencesControl (OnSyncAddinPrefsChanged);
					if (syncAddinPrefsWidget == null) {
						Gtk.Label l = new Gtk.Label (Catalog.GetString ("Not configurable"));
						l.Yalign = 0.5f;
						l.Yalign = 0.5f;
						syncAddinPrefsWidget = l;
					}

					syncAddinPrefsWidget.Show ();
					syncAddinPrefsContainer.PackStart (syncAddinPrefsWidget, false, false, 0);

					resetSyncAddinButton.Sensitive = false;
					saveSyncAddinButton.Sensitive = false;
				}
			} else {
				selectedSyncAddin = null;
				resetSyncAddinButton.Sensitive = false;
				saveSyncAddinButton.Sensitive = false;
			}

		}

		void OnResetSyncAddinButton (object sender, EventArgs args)
		{
			if (selectedSyncAddin == null)
				return;

			// User doesn't get a choice if this is invoked by disabling the addin
			// FIXME: null sender check is lame!
			if (sender != null) {
				// Prompt the user about what they're about to do since
				// it's not really recommended to switch back and forth
				// between sync services.
				HIGMessageDialog dialog =
				        new HIGMessageDialog (null,
				                              Gtk.DialogFlags.Modal,
				                              Gtk.MessageType.Question,
				                              Gtk.ButtonsType.YesNo,
				                              Catalog.GetString ("Are you sure?"),
				                              Catalog.GetString (
				                                      "Clearing your synchronization settings is not recommended.  " +
				                                      "You may be forced to synchronize all of your notes again " +
				                                      "when you save new settings."));
				int response = dialog.Run ();
				dialog.Destroy ();
				if (response != (int) Gtk.ResponseType.Yes)
					return;
			} else { // FIXME: Weird place for this to go.  User should be able to cancel disabling of addin, anyway
				HIGMessageDialog dialog =
				        new HIGMessageDialog (null,
				                              Gtk.DialogFlags.Modal,
				                              Gtk.MessageType.Info,
				                              Gtk.ButtonsType.Ok,
				                              Catalog.GetString ("Resetting Synchronization Settings"),
				                              Catalog.GetString (
				                                      "You have disabled the configured synchronization service.  " +
				                                      "Your synchronization settings will now be cleared.  " +
				                                      "You may be forced to synchronize all of your notes again " +
				                                      "when you save new settings."));
				dialog.Run ();
				dialog.Destroy ();
			}

			try {
				selectedSyncAddin.ResetConfiguration ();
			} catch (Exception e) {
				Logger.Debug ("Error calling {0}.ResetConfiguration: {1}\n{2}",
				              selectedSyncAddin.Id, e.Message, e.StackTrace);
			}

			Preferences.Set (
			        Preferences.SYNC_SELECTED_SERVICE_ADDIN,
			        String.Empty);

			// Reset conflict handling behavior
			Preferences.Set (
			        Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR,
			        Preferences.GetDefault (Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR));

			SyncManager.ResetClient ();

			syncAddinCombo.Sensitive = true;
			resetSyncAddinButton.Sensitive = false;
			saveSyncAddinButton.Sensitive = true;
			if (syncAddinPrefsWidget != null)
				syncAddinPrefsWidget.Sensitive = true;
		}

		/// <summary>
		/// Attempt to save/test the connection to the sync addin.
		/// </summary>
		void OnSaveSyncAddinButton (object sender, EventArgs args)
		{
			if (selectedSyncAddin == null)
				return;

			bool saved = false;
			string errorMsg = null;
			try {
				GdkWindow.Cursor = new Gdk.Cursor (Gdk.CursorType.Watch);
				GdkWindow.Display.Flush ();
				saved = selectedSyncAddin.SaveConfiguration ();
			} catch (TomboySyncException syncEx) {
				errorMsg = syncEx.Message;
			} catch (Exception e) {
				Logger.Debug ("Unexpected error calling {0}.SaveConfiguration: {1}\n{2}",
				              selectedSyncAddin.Id, e.Message, e.StackTrace);
			} finally {
				GdkWindow.Cursor = null;
				GdkWindow.Display.Flush ();
			}

			HIGMessageDialog dialog;
			if (saved) {
				Preferences.Set (
				        Preferences.SYNC_SELECTED_SERVICE_ADDIN,
				        selectedSyncAddin.Id);

				syncAddinCombo.Sensitive = false;
				syncAddinPrefsWidget.Sensitive = false;
				resetSyncAddinButton.Sensitive = true;
				saveSyncAddinButton.Sensitive = false;

				SyncManager.ResetClient ();

				// Give the user a visual letting them know that connecting
				// was successful.
				// TODO: Replace Yes/No with Sync/Close
				dialog =
				        new HIGMessageDialog (this,
				                              Gtk.DialogFlags.Modal,
				                              Gtk.MessageType.Info,
				                              Gtk.ButtonsType.YesNo,
				                              Catalog.GetString ("Connection successful"),
				                              Catalog.GetString (
				                                      "Tomboy is ready to synchronize your notes. Would you like to synchronize them now?"));
				int response = dialog.Run ();
				dialog.Destroy ();

				if (response == (int) Gtk.ResponseType.Yes)
					// TODO: Put this voodoo in a method somewhere
					Tomboy.ActionManager ["NoteSynchronizationAction"].Activate ();
			} else {
				// TODO: Change the SyncServiceAddin API so the call to
				// SaveConfiguration has a way of passing back an exception
				// or other text so it can be displayed to the user.
				Preferences.Set (
				        Preferences.SYNC_SELECTED_SERVICE_ADDIN,
				        String.Empty);

				syncAddinCombo.Sensitive = true;
				syncAddinPrefsWidget.Sensitive = true;
				resetSyncAddinButton.Sensitive = false;
				saveSyncAddinButton.Sensitive = true;

				// Give the user a visual letting them know that connecting
				// was successful.
				if (errorMsg == null) {
					errorMsg = Catalog.GetString ("Please check your information and " +
					                              "try again.  The log file {0} may " +
					                              "contain more information about the error.");
					string logPath = System.IO.Path.Combine (Services.NativeApplication.LogDirectory,
					                                         "tomboy.log");
					errorMsg = String.Format (errorMsg, logPath);
				}
				dialog =
				        new HIGMessageDialog (this,
				                              Gtk.DialogFlags.Modal,
				                              Gtk.MessageType.Warning,
				                              Gtk.ButtonsType.Close,
				                              Catalog.GetString ("Error connecting"),
				                              errorMsg);
				dialog.Run ();
				dialog.Destroy ();
			}
		}
		
		void OnSyncAddinPrefsChanged (object sender, EventArgs args)
		{
			// Enable/disable the save button based on required fields
			if (selectedSyncAddin != null)
				saveSyncAddinButton.Sensitive = selectedSyncAddin.AreSettingsValid;
		}

		void OpenTemplateButtonClicked (object sender, EventArgs args)
		{
			NoteManager manager = Tomboy.DefaultNoteManager;
			Note template_note = manager.GetOrCreateTemplateNote ();
			// Template Window should be top-most. bgo #527177
			template_note.Window.KeepAbove = true;
			// Open the template note
			template_note.Window.Show ();
		}
	}

	// TODO: Figure out how to use Mono.Addins.Gui.AddinInfoDialog here instead.
	// The class here is adapted directly from Mono.Addins.Gui.AddinInfoDialog.
	class AddinInfoDialog : Gtk.Dialog
	{
		Mono.Addins.Setup.AddinHeader info;
		Gtk.Label info_label;

		public AddinInfoDialog (
		        Mono.Addins.Setup.AddinHeader info,
		        Gtk.Window parent)
: base (info.Name,
		        parent,
		        Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.NoSeparator,
		        Gtk.Stock.Close, Gtk.ResponseType.Close)
		{
			this.info = info;

			// TODO: Change this icon to be an addin/package icon
			Gtk.Image icon =
			        new Gtk.Image (Gtk.Stock.DialogInfo, Gtk.IconSize.Dialog);
			icon.Yalign = 0;

			info_label = new Gtk.Label ();
			info_label.Xalign = 0;
			info_label.Yalign = 0;
			info_label.UseMarkup = true;
			info_label.UseUnderline = false;
			info_label.Wrap = true;

			Gtk.HBox hbox = new Gtk.HBox (false, 6);
			Gtk.VBox vbox = new Gtk.VBox (false, 12);
			hbox.BorderWidth = 12;
			vbox.BorderWidth = 6;

			hbox.PackStart (icon, false, false, 0);
			hbox.PackStart (vbox, true, true, 0);

			vbox.PackStart (info_label, true, true, 0);

			hbox.ShowAll ();

			VBox.PackStart (hbox, true, true, 0);

			Fill ();
		}

		void Fill ()
		{
			StringBuilder sb = new StringBuilder ();
			sb.Append ("<b><big>" + info.Name + "</big></b>\n\n");

			if (info.Description != string.Empty)
				sb.Append (info.Description + "\n\n");

			sb.Append ("<small>");

			sb.Append (string.Format (
			                   "<b>{0}</b>\n" +
			                   "{1}\n\n",
			                   Catalog.GetString ("Version:"),
			                   info.Version));

			if (info.Author != string.Empty)
				sb.Append (string.Format (
				                   "<b>{0}</b>\n" +
				                   "{1}\n\n",
				                   Catalog.GetString ("Author:"),
				                   info.Author));

			if (info.Copyright != string.Empty)
				sb.Append (string.Format (
				                   "<b>{0}</b>\n" +
				                   "{1}\n\n",
				                   Catalog.GetString ("Copyright:"),
				                   info.Copyright));

			if (info.Dependencies.Count > 0) {
				sb.Append (string.Format (
				                   "<b>{0}</b>\n",
				                   Catalog.GetString ("Add-in Dependencies:")));
				foreach (Mono.Addins.Description.Dependency dep in info.Dependencies) {
					sb.Append (dep.Name + "\n");
				}
			}

			sb.Append ("</small>");

			info_label.Markup = sb.ToString ();
		}
	}
}
