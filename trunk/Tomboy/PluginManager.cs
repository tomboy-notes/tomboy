
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;

using Mono.Unix;
using Mono.Unix.Native;

namespace Tomboy
{
	using PluginTable = IDictionary<Type, NotePlugin>;

	class PluginReference : WeakReference
	{
		readonly string description;

		public PluginReference (object plugin, string description) : 
			base (plugin)
		{
			this.description = description;
		}

		public string Description
		{
			get { return description; }
		}
	}

	[AttributeUsage(
		AttributeTargets.Class,
		AllowMultiple = false, Inherited = false)]
	public class PluginInfoAttribute : Attribute
	{
		string name;
		string version;
		string description;
		string website;
		string author;

		Type preferencesWidget;

		public const string OFFICIAL_AUTHOR = "official";
		
		// The default constructor is, for some reason, needed or Tomboy will
		// crash when attempting to read the plugin attributes.
		public PluginInfoAttribute ()
		{
			// Intentionally blank
		}

		public PluginInfoAttribute (string name, string version,
		                            string author, string description)
		{
			this.name = Catalog.GetString (name);
			this.description = Catalog.GetString (description);
			this.version = version;
			if (author == OFFICIAL_AUTHOR)
				this.author = Catalog.GetString ("Tomboy Project");
			else
				this.author = author;
		}

		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		public string Version
		{
			get { return version; }
			set { version = value; }
		}

		public string Description
		{
			get { return description; }
			set { description = value; }
		}

		public string WebSite
		{
			get { return website; }
			set { website = value; }
		}

		public string Author
		{
			get { return author; }
			set { author = value; }
		}

		public Type PreferencesWidget
		{
			get { return preferencesWidget; }
			set { preferencesWidget = value; }
		}
	}

	[AttributeUsage(
		AttributeTargets.Class,
		AllowMultiple = false, Inherited = true)]
	public class RequiredPlugins: Attribute
	{
		readonly string[] pluginNames;

		public RequiredPlugins(params string[] pluginNames)
		{
			this.pluginNames = pluginNames;
		}

		public string[] PluginNames
		{
			get { return pluginNames; }
		}
	}

	[AttributeUsage(
		AttributeTargets.Class,
		AllowMultiple = false, Inherited = true)]
	public class SuggestedPlugins: Attribute
	{
		readonly string[] pluginNames;

		public SuggestedPlugins(params string[] pluginNames)
		{
			this.pluginNames = pluginNames;
		}

		public string[] PluginNames
		{
			get { return pluginNames; }
		}
	}

	public interface IPlugin : IDisposable
	{
	}

	public abstract class AbstractPlugin : IPlugin
	{
		bool disposing = false;

		~AbstractPlugin ()
		{
			Dispose (false);
		}

		public void Dispose ()
		{
			disposing = true;
			Dispose (true);

			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		public bool IsDisposing
		{
			get { return disposing; }
		}
	}

	public abstract class NotePlugin : AbstractPlugin
	{
		Note note;

		List<Gtk.MenuItem> plugin_menu_items;
		List<Gtk.MenuItem> text_menu_items;

		public void Initialize (Note note)
		{
			this.note = note;
			this.note.Opened += OnNoteOpenedEvent;

			Initialize ();

			if (note.IsOpened)
				OnNoteOpened ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (plugin_menu_items != null) {
					foreach (Gtk.Widget item in plugin_menu_items)
						item.Destroy ();
				}

				if (text_menu_items != null) {
					foreach (Gtk.Widget item in text_menu_items)
						item.Destroy ();
				}

				Shutdown ();
			}

			note.Opened -= OnNoteOpenedEvent;
		}

		protected abstract void Initialize ();
		protected abstract void Shutdown ();
		protected abstract void OnNoteOpened ();

		public Note Note
		{
			get { return note; }
		}

		public bool HasBuffer
		{
			get { return note.HasBuffer; }
		}

		public NoteBuffer Buffer
		{
			get
			{
				if (IsDisposing && !HasBuffer)
					throw new InvalidOperationException ("Plugin is disposing already");

				return note.Buffer; 
			}
		}

		public bool HasWindow
		{
			get { return note.HasWindow; }
		}

		public NoteWindow Window
		{
			get
			{
				if (IsDisposing && !HasWindow)
					throw new InvalidOperationException ("Plugin is disposing already");

				return note.Window; 
			}
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
			if (IsDisposing)
				throw new InvalidOperationException ("Plugin is disposing already");

			if (plugin_menu_items == null)
				plugin_menu_items = new List<Gtk.MenuItem> ();

			plugin_menu_items.Add (item);

			if (note.IsOpened)
				Window.PluginMenu.Add (item);
		}

		public void AddTextMenuItem (Gtk.MenuItem item)
		{
			if (IsDisposing)
				throw new InvalidOperationException ("Plugin is disposing already");

			if (text_menu_items == null)
				text_menu_items = new List<Gtk.MenuItem> ();

			text_menu_items.Add (item);

			if (note.IsOpened) {
				Window.TextMenu.Add (item);
				Window.TextMenu.ReorderChild (item, 7);
			}
		}
	}

	public class PluginManager
	{
		readonly string plugins_dir;

		readonly IList<Type> plugin_types;
		readonly IDictionary<Note, PluginTable> attached_plugins;

		readonly FileSystemWatcher dir_watcher;
		readonly FileSystemWatcher sys_dir_watcher;

		static bool check_plugin_unloading;

		// Plugins in the tomboy.exe assembly, always loaded.
		static Type[] stock_plugins = {
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
			attached_plugins = new Dictionary<Note, PluginTable> ();

		}

		public static bool CheckPluginUnloading
		{
			get { return check_plugin_unloading; }
			set { check_plugin_unloading = value; }
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
			} catch (Exception e) {
				Logger.Log ("Error opening file browser \"{0}\" to \"{1}\": {2}",
					    command,
					    plugins_dir,
					    e.Message);
			}
		}

		public void LoadPluginsForNote (Note note)
		{
			foreach (Type type in plugin_types as 
					System.Collections.Generic.IEnumerable<Type>) {
				if (IsPluginEnabled (type))
					AttachPlugin (type, note);
			}

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
			if (File.Exists (default_path)) {
				// Check the existing file to make sure it has the correct path.
				// If it doesn't, delete it so a correct one will installed.
				StreamReader reader = null;
				try {
					reader = new StreamReader (File.OpenRead (default_path));
					string contents = reader.ReadToEnd ();
					if (contents.IndexOf ("file://" + Defines.SYS_PLUGINS_DIR) < 0)
						File.Delete (default_path);
				} catch (Exception e) {
					Logger.Warn ("Could not update DefaultPlugins.desktop file: {0}",
						e.Message);
				} finally {
					if (reader != null)
						reader.Close ();
				}
			}

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
			PluginTable note_plugins = null;
			
			if (attached_plugins.TryGetValue (deleted, out note_plugins)) {
				foreach (NotePlugin plugin in note_plugins.Values as
						System.Collections.Generic.IEnumerable<NotePlugin>) {
					try {
						plugin.Dispose ();
					} catch (Exception e) {
						Logger.Fatal (
							"Cannot dispose {0}: {1}", 
							plugin.GetType(), e);
					}
				}

				note_plugins.Clear ();
				attached_plugins.Remove (deleted);
			}
		}

		void AttachPlugin (Type type, Note note)
		{
			PluginTable note_plugins;

			if (!attached_plugins.TryGetValue (note, out note_plugins)) {
				note_plugins = new Dictionary<Type, NotePlugin> ();
				attached_plugins [note] = note_plugins;
			}

			if (typeof (NotePlugin).IsAssignableFrom (type)) {
				NotePlugin plugin;

				try {
					plugin = (NotePlugin)Activator.CreateInstance (type);
					plugin.Initialize (note);
				} catch (Exception e) {
					Logger.Fatal (
							"Cannot initialize {0} for note '{1}': {2}", 
							type, note.Title, e);
					plugin = null;
				}

				if (null != plugin)
					note_plugins[type] = plugin;
			}
		}

		void AttachPlugin (Type type)
		{
			if (typeof (NotePlugin).IsAssignableFrom (type)) {
				// A plugin may add or remove notes when being
				// created. Therefore, it is best to iterate
				// through a copy of the notes list to avoid
				// "System.InvalidOperationException: out of sync"
				List<Note> notes_copy =
					new List<Note> (attached_plugins.Keys);				
				foreach (Note note in notes_copy)
					AttachPlugin (type, note);
			}
		}

		void DetachPlugin (Type type)
		{
			List<PluginReference> references;
			
			if (CheckPluginUnloading) {
				references = new List<PluginReference> ();
			} else {
				references = null;
			}

			DetachPlugin (type, references);

			Logger.Debug ("Starting garbage collection...");

			GC.Collect();
			GC.WaitForPendingFinalizers ();
			
			Logger.Debug ("Garbage collection complete.");

			if (!CheckPluginUnloading) 
				return;

			Logger.Debug ("Checking plugin references...");

			int finalized = 0, leaking = 0;

			foreach (PluginReference r in references) {
				object plugin = r.Target;

				if (null != plugin) {
					Logger.Fatal (
						"Leaking reference on {0}: '{1}'",
						plugin, r.Description);

					++leaking;
				} else {
					++finalized;
				}
			}

			if (leaking > 0) {
				string heapshot =
					"http://svn.myrealbox.com/source/trunk/heap-shot";
				string title = String.Format (
					Catalog.GetString ("Cannot fully disable {0}."),
					type);
				string message = String.Format (Catalog.GetString (
					"Cannot fully disable {0} as there still are " +
					"at least {1} reference to this plugin. This " +
					"indicates a programming error. Contact the " +
					"plugin's author and report this problem.\n\n" +
					"<b>Developer Information:</b> This problem " +
					"usually occurs when the plugin's Dispose " +
					"method fails to disconnect all event handlers. " +
					"The heap-shot profiler ({2}) can help to " +
					"identify leaking references."),
					type, leaking, heapshot);

				HIGMessageDialog dialog = new HIGMessageDialog (
					null, 0, Gtk.MessageType.Error, Gtk.ButtonsType.Ok,
					title, message);

				dialog.Run ();
				dialog.Destroy ();
			}

			Logger.Debug ("finalized: {0}, leaking: {1}", finalized, leaking);
		}

		// Dispose loop has to happen on separate stack frame when debugging,
		// as otherwise the local variable "plugin" will cause at least one
		// plugin instance to not be garbage collected. Just assigning
		// null to the variable will not help, as the JIT optimizes this
		// assignment away.
		void DetachPlugin (Type type, List<PluginReference> references)
		{
			if (typeof (NotePlugin).IsAssignableFrom (type)) {
				foreach (KeyValuePair<Note, PluginTable> pair 
						in attached_plugins as
							System.Collections.Generic.IEnumerable<
								System.Collections.Generic.KeyValuePair<
									Note, PluginTable>>) {
					NotePlugin plugin = null;

					if (pair.Value.TryGetValue (type, out plugin)) {
						pair.Value[type] = null;
						pair.Value.Remove (type);

						try {
							plugin.Dispose();
						} catch (Exception e) {
							Logger.Fatal (
								"Cannot dispose {0} for note '{1}': {2}", 
								type, pair.Key.Title, e);
						}

						if (null != references)
							references.Add (new PluginReference (
									plugin, pair.Key.Title));
					}
				}
			}
		}

		void OnPluginCreated (object sender, FileSystemEventArgs args)
		{
			Logger.Log ("Plugin '{0}' Created", 
					   Path.GetFileName (args.FullPath));

			IList<Type> asm_plugins = FindPluginTypesInFile (args.FullPath);

			// Add the plugin to the list
			// and load the added plugin for all existing plugged in notes
			foreach (Type type in asm_plugins as
					System.Collections.Generic.IEnumerable<Type>) {
				if (IsPluginEnabled (type)) {
					plugin_types.Add (type);
					AttachPlugin (type);
				}
			}

			asm_plugins.Clear ();
		}

		void OnPluginDeleted (object sender, FileSystemEventArgs args)
		{
			Logger.Log ("Plugin '{0}' Deleted", 
					   Path.GetFileName (args.FullPath));

			List<Type> kill_list = new List<Type> ();

			// Find the plugins in the deleted assembly
			foreach (Type type in plugin_types as
					System.Collections.Generic.IEnumerable<Type>) {
				if (type.Assembly.Location == args.FullPath)
					kill_list.Add (type);
			}

			foreach (Type type in kill_list) {
				plugin_types.Remove (type);
				DetachPlugin (type);
			}

			kill_list.Clear ();
		}

		public Type[] Plugins
		{
			get
			{
				List<Type> plugins = new List<Type> (plugin_types.Count);

				foreach (Type type in plugin_types as
						System.Collections.Generic.IEnumerable<Type>) {
					if (!IsBuiltin (type))
						plugins.Add (type);
				}

				return plugins.ToArray ();
			}
		}

		static string[] GetActivePlugins ()
		{
			return (string[]) Preferences.Get (Preferences.ENABLED_PLUGINS);
		}

		public static bool IsBuiltin (Type plugin)
		{
			return plugin.Assembly == typeof (PluginManager).Assembly;
		}

		public bool IsPluginEnabled (Type plugin)
		{
			return IsBuiltin (plugin) || 
					Array.IndexOf (GetActivePlugins (), plugin.Name) >= 0;
		}

		public void SetPluginEnabled (Type plugin, bool enabled)
		{
			List<string> active_plugins = new List<string> (GetActivePlugins ());
			int index = active_plugins.IndexOf (plugin.Name);

			if (enabled != (index >= 0)) {
				if (enabled) {
					active_plugins.Add (plugin.Name);
					AttachPlugin (plugin);
				} else {
					active_plugins.RemoveAt (index);
					DetachPlugin (plugin);
				}

				Preferences.Set (Preferences.ENABLED_PLUGINS,
						active_plugins.ToArray ());
			}
		}

		public static PluginInfoAttribute GetPluginInfo(Type plugin)
		{
			return Attribute.GetCustomAttribute (plugin, 
					typeof (PluginInfoAttribute)) as PluginInfoAttribute;
		}

		public static string GetPluginName (Type type, PluginInfoAttribute info)
		{
			string name = null;
			
			if (null != info)
				name = info.Name;
			if (null == name)
				name = type.Name;

			return name;
		}

		public static string GetPluginVersion (Type type, PluginInfoAttribute info)
		{
			string version = null;
			
			if (null != info)
				version = info.Version;

			if (null == version) {
				foreach (Attribute attribute
						in type.Assembly.GetCustomAttributes (true)) {
					AssemblyInformationalVersionAttribute productVersion =
						attribute as AssemblyInformationalVersionAttribute;

					if (null != productVersion) {
						version = productVersion.InformationalVersion;
						break;
					}

					AssemblyFileVersionAttribute fileVersion = 
						attribute as AssemblyFileVersionAttribute;

					if (null != fileVersion) {
						version = fileVersion.Version;
						break;
					}
				}
			}

			if (null == version)
				version = type.Assembly.GetName().Version.ToString();

			return version;
		}

		public static string GetPluginName (Type type)
		{
			return GetPluginName (type, GetPluginInfo (type));
		}

		public static Gtk.Widget CreatePreferencesWidget (PluginInfoAttribute info)
		{
			if (null != info && null != info.PreferencesWidget)
				return (Gtk.Widget)Activator.CreateInstance (info.PreferencesWidget);

			return null;
		}

		IList<Type> FindPluginTypes ()
		{
			List<Type> all_plugin_types = new List<Type> ();

			all_plugin_types.AddRange (stock_plugins);
			all_plugin_types.AddRange (
					FindPluginTypesInDirectory (Defines.SYS_PLUGINS_DIR));
			all_plugin_types.AddRange (FindPluginTypesInDirectory (plugins_dir));

			return all_plugin_types;
		}

		static IList<Type> FindPluginTypesInDirectory (string dirpath)
		{
			IList<Type> dir_plugin_types = new List<Type> ();
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
					IList<Type> asm_plugins = FindPluginTypesInFile (file);
					foreach (Type type in asm_plugins as
							System.Collections.Generic.IEnumerable<Type>) {
						dir_plugin_types.Add (type);
					}
				} catch (Exception e) {
					Logger.Log ("Failed.\n{0}", e);
				}
			}

			return dir_plugin_types;
		}

		static IList<Type> FindPluginTypesInFile (string filepath)
		{
			Assembly asm = Assembly.LoadFrom (filepath);
			return FindPluginTypesInAssembly (asm);
		}

		static IList<Type> FindPluginTypesInAssembly (Assembly asm)
		{
			IList<Type> asm_plugins = new List<Type> ();
			Type [] types = asm.GetTypes ();
			bool found_one = false;

			foreach (Type type in types) {
				if (typeof (IPlugin).IsAssignableFrom (type)) {
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
