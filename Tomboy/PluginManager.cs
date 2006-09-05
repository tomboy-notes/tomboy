
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using Mono.Unix.Native;

namespace Tomboy
{
	public abstract class NotePlugin : IDisposable
	{
		Note note;
		ArrayList plugin_menu_items;
		ArrayList text_menu_items;

		public void Initialize (Note note)
		{
			this.note = note;
			this.note.Opened += OnNoteOpenedEvent;

			Initialize ();

			if (note.IsOpened)
				OnNoteOpened ();
		}

		public void Dispose ()
		{
			this.note.Opened -= OnNoteOpenedEvent;

			if (plugin_menu_items != null) {
				foreach (Gtk.Widget item in plugin_menu_items) {
					item.Destroy ();
				}
			}
			if (text_menu_items != null) {
				foreach (Gtk.Widget item in text_menu_items) {
					item.Destroy ();
				}
			}

			Shutdown ();
		}

		protected abstract void Initialize ();
		protected abstract void Shutdown ();
		protected abstract void OnNoteOpened ();

		public Note Note
		{
			get { return note; }
		}

		public NoteBuffer Buffer
		{
			get { return note.Buffer; }
		}

		public NoteWindow Window
		{
			get { return note.Window; }
		}

		public NoteManager Manager
		{
			get { return note.Manager; }
		}

		void OnNoteOpenedEvent (object sender, EventArgs args)
		{
			OnNoteOpened ();

			if (plugin_menu_items != null) {
				foreach (Gtk.Widget item in plugin_menu_items) {
					if (item.Parent == null || 
					    item.Parent != Window.PluginMenu)
						Window.PluginMenu.Add (item);
				}
			}

			if (text_menu_items != null) {
				foreach (Gtk.Widget item in text_menu_items) {
					if (item.Parent == null || 
					    item.Parent != Window.TextMenu) {
						Window.TextMenu.Add (item);
						Window.TextMenu.ReorderChild (item, 7);
					}
				}
			}
		}

		public void AddPluginMenuItem (Gtk.MenuItem item)
		{
			if (plugin_menu_items == null)
				plugin_menu_items = new ArrayList ();

			plugin_menu_items.Add (item);

			if (note.IsOpened)
				Window.PluginMenu.Add (item);
		}

		public void AddTextMenuItem (Gtk.MenuItem item)
		{
			if (text_menu_items == null)
				text_menu_items = new ArrayList ();

			text_menu_items.Add (item);

			if (note.IsOpened) {
				Window.TextMenu.Add (item);
				Window.TextMenu.ReorderChild (item, 7);
			}
		}
	}

	public class PluginManager
	{
		string plugins_dir;
		ArrayList plugin_types;
		Hashtable plugin_hash;
		FileSystemWatcher dir_watcher;
		FileSystemWatcher sys_dir_watcher;

		// Plugins in the tomboy.exe assembly, always loaded.
		static Type [] stock_plugins = 
			new Type [] {
				typeof (NoteRenameWatcher),
#if ENABLE_GTKSPELL
				typeof (NoteSpellChecker),
#endif // ENABLE_GTKSPELL
				typeof (NoteUrlWatcher),
				typeof (NoteLinkWatcher),
				typeof (NoteWikiWatcher),
				typeof (MouseHandWatcher),
				
				// Not ready yet:
				// typeof (NoteRelatedToWatcher),
				// typeof (IndentWatcher),
			};

		public PluginManager (string plugins_dir)
		{
			this.plugins_dir = plugins_dir;

			try {
				dir_watcher = new FileSystemWatcher (plugins_dir, "*.dll");
				dir_watcher.Created += OnPluginCreated;
				dir_watcher.Deleted += OnPluginDeleted;
				dir_watcher.EnableRaisingEvents = true;
			} catch (ArgumentException e) { 
				Logger.Log ("Error creating a FileSystemWatcher on \"{0}\": {1}",
					    plugins_dir, e.Message);
				dir_watcher = null;
			}
			
			try {
				sys_dir_watcher = 
					new FileSystemWatcher (Defines.SYS_PLUGINS_DIR, "*.dll");
				sys_dir_watcher.Created += OnPluginCreated;
				sys_dir_watcher.Deleted += OnPluginDeleted;
				sys_dir_watcher.EnableRaisingEvents = true;
			} catch (ArgumentException e) {
				Logger.Log ("Error creating a FileSystemWatcher on \"{0}\": {1}", 
					    Defines.SYS_PLUGINS_DIR, e.Message);
				sys_dir_watcher = null;
			}
			
			plugin_types = FindPluginTypes ();
			plugin_hash = new Hashtable ();
		}

		// Run file manager for ~/.tomboy/Plugins
		public void ShowPluginsDirectory ()
		{
			string command, args;

			// FIXME: There has to be a better way to check this...
			if (Environment.GetEnvironmentVariable ("GNOME_DESKTOP_SESSION_ID") == null &&
			    (Environment.GetEnvironmentVariable ("KDE_FULL_SESSION") != null ||
			     Environment.GetEnvironmentVariable ("KDEHOME") != null ||
			     Environment.GetEnvironmentVariable ("KDEDIR") != null)) {
				Logger.Log ("Starting Konqueror...");

				command = "konqueror";
				args = plugins_dir;
			} else {
				Logger.Log ("Starting Nautilus...");

				command = "nautilus";
				args = string.Format ("--no-desktop --no-default-window {0}",
						      plugins_dir);
			}

			try {
				Process.Start (command, args);
			} catch (SystemException e) {
				Logger.Log ("Error opening file browser \"{0}\" to \"{1}\": {2}",
					    command,
					    plugins_dir,
					    e.Message);
			}
		}

		public void LoadPluginsForNote (Note note)
		{
			ArrayList note_plugins = new ArrayList ();

			foreach (Type type in plugin_types) {
				NotePlugin plugin = (NotePlugin) Activator.CreateInstance (type);
				if (plugin != null) {
					plugin.Initialize (note);
					note_plugins.Add (plugin);
				}
			}

			// Store the plugins for this note
			plugin_hash [note] = note_plugins;

			// Make sure we remove plugins when a note is deleted
			note.Manager.NoteDeleted += OnNoteDeleted;
		}

		static void CleanupOldPlugins (string plugins_dir)
		{
			// NOTE: These might be symlinks to the system-installed
			// versions, so use unlink() just in case.

			// Remove old version 0.3.[12] "Uninstalled Plugins" symlink
			string uninstalled_dir = Path.Combine (plugins_dir, "Uninstalled Plugins");
			if (Directory.Exists (uninstalled_dir)) {
				Logger.Log ("Removing old \"Uninstalled Plugins\" " +
						   "directory...");
				try {
					Syscall.unlink (uninstalled_dir);
				} catch (Exception e) {
					Logger.Log ("Error removing: {0}", e);
				}
			}

			// Remove old version 0.3.[12] "ExportToHTML.dll" file
			string export_to_html_dll = Path.Combine (plugins_dir, "ExportToHTML.dll");
			if (File.Exists (export_to_html_dll)) {
				Logger.Log ("Removing old \"ExportToHTML.dll\" plugin...");
				try {
					Syscall.unlink (export_to_html_dll);
				} catch (Exception e) {
					Logger.Log ("Error removing: {0}", e);
				}
			}

			// Remove old version 0.3.[12] "PrintNotes.dll" file
			string print_notes_dll = Path.Combine (plugins_dir, "PrintNotes.dll");
			if (File.Exists (print_notes_dll)) {
				Logger.Log ("Removing old \"PrintNotes.dll\" plugin...");
				try {
					Syscall.unlink (print_notes_dll);
				} catch (Exception e) {
					Logger.Log ("Error removing: {0}", e);
				}
			}
		}

		public static void CreatePluginsDir (string plugins_dir)
		{
			// Create Plugins dir
			if (!Directory.Exists (plugins_dir))
				Directory.CreateDirectory (plugins_dir);

			// Clean up old plugin remnants
			CleanupOldPlugins (plugins_dir);

			// Copy Plugins/DefaultPlugins.desktop file from resource
			string default_desktop = "DefaultPlugins.desktop";
			string default_path = Path.Combine (plugins_dir, default_desktop);
			if (!File.Exists (default_path)) {
				Assembly asm = Assembly.GetExecutingAssembly();
				Stream stream = asm.GetManifestResourceStream (default_desktop);
				if (stream != null) {
					Logger.Log ("Writing '{0}'...", default_path);
					try {
						StreamWriter file = File.CreateText (default_path);
						StreamReader reader;
						try {
							reader = new StreamReader (stream);
							file.Write (reader.ReadToEnd ());
						} finally {
							file.Close ();
						}
					} finally {
						stream.Close ();
					}
				}
			}
		}

		void OnNoteDeleted (object sender, Note deleted)
		{
			// Clean out the plugins for this deleted note.
			ArrayList note_plugins = (ArrayList) plugin_hash [deleted];

			if (note_plugins != null) {
				foreach (NotePlugin plugin in note_plugins) {
					plugin.Dispose ();
				}

				note_plugins.Clear ();
			}

			plugin_hash.Remove (deleted);
		}

		void OnPluginCreated (object sender, FileSystemEventArgs args)
		{
			Logger.Log ("Plugin '{0}' Created", 
					   Path.GetFileName (args.FullPath));

			ArrayList asm_plugins = FindPluginTypesInFile (args.FullPath);

			// Add the plugin to the list
			foreach (Type type in asm_plugins) {
				plugin_types.Add (type);
			}

			// Load the added plugin for all existing plugged in notes
			foreach (Type type in asm_plugins) {
				foreach (Note note in plugin_hash.Keys) {
					NotePlugin plugin = (NotePlugin) 
						Activator.CreateInstance (type);
					if (plugin == null)
						continue;

					plugin.Initialize (note);

					ArrayList note_plugins = (ArrayList) plugin_hash [note];
					note_plugins.Add (plugin);
				}
			}

			asm_plugins.Clear ();
		}

		void OnPluginDeleted (object sender, FileSystemEventArgs args)
		{
			Logger.Log ("Plugin '{0}' Deleted", 
					   Path.GetFileName (args.FullPath));

			ArrayList kill_list = new ArrayList ();

			// Find the plugins in the deleted assembly
			foreach (Type type in plugin_types) {
				if (type.Assembly.Location == args.FullPath) {
					kill_list.Add (type);
				}
			}

			foreach (Type type in kill_list) {
				plugin_types.Remove (type);
			}

			foreach (Note note in plugin_hash.Keys) {
				ArrayList note_plugins = (ArrayList) plugin_hash [note];

				for (int i = 0; i < note_plugins.Count; i++) {
					NotePlugin plugin = (NotePlugin) note_plugins [i];

					if (kill_list.Contains (plugin.GetType ())) {
						// Allow the plugin to free resources
						plugin.Dispose ();
						note_plugins.Remove (plugin);
					}
				}
			}

			kill_list.Clear ();
		}

		ArrayList FindPluginTypes ()
		{
			ArrayList all_plugin_types = new ArrayList ();

			// Load the stock plugins
			foreach (Type type in stock_plugins) {
				all_plugin_types.Add (type);
			}

			// Load system default plugins
			ArrayList sys_plugin_types;
			sys_plugin_types = FindPluginTypesInDirectory (Defines.SYS_PLUGINS_DIR);
			foreach (Type type in sys_plugin_types) {
				all_plugin_types.Add (type);
			}

			// Load user's plugins
			ArrayList user_plugin_types = FindPluginTypesInDirectory (plugins_dir);
			foreach (Type type in user_plugin_types) {
				all_plugin_types.Add (type);
			}

			return all_plugin_types;
		}

		static ArrayList FindPluginTypesInDirectory (string dirpath)
		{
			ArrayList dir_plugin_types = new ArrayList ();
			string [] files;
			
			try {
				files = Directory.GetFiles (dirpath, "*.dll");
			} catch (Exception e) {
				Logger.Log ("Error getting plugin types from {0}: {1}", 
					    dirpath, 
					    e.Message);
				return dir_plugin_types;
			}

			foreach (string file in files) {
				Console.Write ("Trying Plugin: {0} ... ", Path.GetFileName (file));
				
				try {
					ArrayList asm_plugins = FindPluginTypesInFile (file);
					foreach (Type type in asm_plugins) {
						dir_plugin_types.Add (type);
					}
				} catch (Exception e) {
					Logger.Log ("Failed.\n{0}", e);
				}
			}

			return dir_plugin_types;
		}

		static ArrayList FindPluginTypesInFile (string filepath)
		{
			Assembly asm = Assembly.LoadFrom (filepath);
			return FindPluginTypesInAssembly (asm);
		}

		static ArrayList FindPluginTypesInAssembly (Assembly asm)
		{
			Type [] types = asm.GetTypes ();
			ArrayList asm_plugins = new ArrayList ();
			bool found_one = false;

			foreach (Type type in types) {
				if (type.IsSubclassOf (typeof (NotePlugin))) {
					Console.Write ("{0}. ", type.FullName);
					asm_plugins.Add (type);
					found_one = true;
				}
			}

			Logger.Log ("{0}", found_one ? "Done." : "Skipping.");

			return asm_plugins;
		}
	}
}
