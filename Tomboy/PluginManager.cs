
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using Mono.Posix;

namespace Tomboy
{
	public abstract class NotePlugin : IDisposable
	{
		Note note;

		public void Initialize (Note note)
		{
			this.note = note;
			this.note.Opened += OnNoteOpenedEvent;

			Initialize ();

			if (note.IsOpened)
				OnNoteOpened ();
		}

		protected abstract void Initialize ();
		public abstract void Dispose ();
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

		void OnNoteOpenedTimeout (object sender, EventArgs args)
		{
			OnNoteOpened ();
		}

		void OnNoteOpenedEvent (object sender, EventArgs args)
		{
			// Call OnNoteOpened in a timeout so we don't confuse
			// Gtk by rendering inside Window.Realize

			InterruptableTimeout timeout = new InterruptableTimeout ();
			timeout.Timeout += OnNoteOpenedTimeout;
			timeout.Reset (0);
		}
	}

	public class PluginManager
	{
		string plugins_dir;
		ArrayList plugin_types;
		Hashtable plugin_hash;
		FileSystemWatcher dir_watcher;

		// Plugins in the tomboy.exe assembly, always loaded.
		static Type [] stock_plugins = 
			new Type [] {
				typeof (NoteRenameWatcher),
				typeof (NoteSpellChecker),
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
			CreatePluginsDir ();

			dir_watcher = new FileSystemWatcher (plugins_dir, "*.dll");
			dir_watcher.Created += OnPluginCreated;
			dir_watcher.Deleted += OnPluginDeleted;
			dir_watcher.EnableRaisingEvents = true;

			plugin_types = FindPluginTypes ();
			plugin_hash = new Hashtable ();
		}

		public void ShowPluginsDirectory ()
		{
			// Run file manager for ~/.tomboy/Plugins
			// FIXME: Decide between nautilus and konqueror somehow

			Console.WriteLine ("Starting Nautilus...");

			string args = string.Format ("--no-desktop --no-default-window {0}",
						     plugins_dir);
			Process.Start ("nautilus", args);
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

		void OnNoteDeleted (object sender, Note deleted)
		{
			// Clean out the plugins for this deleted note.
			ArrayList note_plugins = (ArrayList) plugin_hash [deleted];
			plugin_hash [deleted] = null;

			if (note_plugins != null) {
				foreach (NotePlugin plugin in note_plugins)
					plugin.Dispose ();

				note_plugins.Clear ();
			}
		}

		void OnPluginCreated (object sender, FileSystemEventArgs args)
		{
			Console.WriteLine ("Plugin '{0}' Created", 
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
		}

		void OnPluginDeleted (object sender, FileSystemEventArgs args)
		{
			Console.WriteLine ("Plugin '{0}' Deleted", 
					   Path.GetFileName (args.FullPath));

			ArrayList kill_list = new ArrayList ();

			// Find the plugins in the deleted assembly
			foreach (Type type in plugin_types) {
				if (type.Assembly.Location == args.FullPath) {
					kill_list.Add (type);
				}
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
		}

		void CreatePluginsDir ()
		{
			// Plugins dir
			if (!Directory.Exists (plugins_dir))
				Directory.CreateDirectory (plugins_dir);

			// Plugins/.directory
			string plugins_dot_directory = Path.Combine (plugins_dir, ".directory");
			if (!File.Exists (plugins_dot_directory)) {
				// copy from resource file
			}

			// Plugins/Uninstalled Plugins dir
			string uninstalled_dir = Path.Combine (plugins_dir, "Uninstalled Plugins");
			if (!File.Exists (uninstalled_dir)) {
				// FIXME: Handle the error.
				int retval = Syscall.symlink (Defines.SYS_PLUGINS_DIR, 
							      uninstalled_dir);
 
				if (retval == 0) {
					// Plugins/Uninstalled Plugins/.directory
					string uninstalled_dot_directory = 
						Path.Combine (uninstalled_dir, ".directory");
					if (!File.Exists (uninstalled_dot_directory)) {
						// copy from resource file
					}
				}
			}
		}

		ArrayList FindPluginTypes ()
		{
			ArrayList all_plugin_types = new ArrayList ();

			// Load the stock plugins
			foreach (Type type in stock_plugins) {
				all_plugin_types.Add (type);
			}

			string [] files = Directory.GetFiles (plugins_dir, "*.dll");

			foreach (string file in files) {
				Console.Write ("Trying Plugin: {0} ... ", 
					       Path.GetFileName (file));

				try {
					ArrayList asm_plugins = FindPluginTypesInFile (file);
					foreach (Type type in asm_plugins) {
						all_plugin_types.Add (type);
					}
                                } catch (Exception e) {
                                        Console.WriteLine ("Failed.\n{0}", e);
                                }
			}

			return all_plugin_types;
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
				if (type.BaseType == typeof (NotePlugin)) {
					Console.Write ("{0}. ", type.FullName);
					asm_plugins.Add (type);
					found_one = true;
				}
			}

			Console.WriteLine ("{0}", found_one ? "Done." : "Skipping.");

			return asm_plugins;
		}
	}
}
