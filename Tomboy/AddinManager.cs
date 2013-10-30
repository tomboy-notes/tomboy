
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;

using Mono.Unix;

using Tomboy.Sync;

namespace Tomboy
{
	public class AddinManager
	{
		readonly string tomboy_conf_dir;

		/// <summary>
		/// Key = TypeExtensionNode.Id
		/// </summary>
		Dictionary<string, ApplicationAddin> app_addins;

		/// <summary>
		/// </summary>
		Dictionary<Note, List<NoteAddinInfo>> note_addins;

		/// <summary>
		/// Key = TypeExtensionNode.Id
		/// </summary>
		Dictionary<string, List<NoteAddinInfo>> note_addin_infos;

		public event System.EventHandler ApplicationAddinListChanged;

		public AddinManager (string tomboy_conf_dir) : this (tomboy_conf_dir, null)
		{
		}

		public AddinManager (string tomboy_conf_dir, string old_tomboy_conf_dir)
		{
			this.tomboy_conf_dir = tomboy_conf_dir;
			app_addins = new Dictionary<string, ApplicationAddin> ();
			note_addins = new Dictionary<Note, List<NoteAddinInfo>> ();
			note_addin_infos = new Dictionary<string, List<NoteAddinInfo>> ();

			InitializeMonoAddins (old_tomboy_conf_dir);
		}

		void InitializeMonoAddins (string old_conf_dir)
		{
			Logger.Info ("Initializing Mono.Addins");

			// Perform migration if necessary
			if (!String.IsNullOrEmpty (old_conf_dir)) {
				foreach (string dir_path in Directory.GetDirectories (old_conf_dir, "addin*")) {
					string new_dir_path =
						Path.Combine (tomboy_conf_dir, Path.GetFileName (dir_path));
					if (!Directory.Exists (new_dir_path))
						IOUtils.CopyDirectory (dir_path, new_dir_path);
				}
			}

			string addins_dir = Tomboy.Uninstalled ? ".":
				Path.Combine (tomboy_conf_dir, "addins");
			if (!Directory.Exists (addins_dir))
				Directory.CreateDirectory (addins_dir);

			// Make sure a Tomboy.addins file exists
			string addins_file = Path.Combine (addins_dir, "Tomboy.addins");

			// Always recreate this file.  This means it's
			// completely hands-off to the user.  This is done to
			// support upgrades and parallel install scenarios,
			// ensuring that Tomboy does its best not to load old
			// versions of addins (which may no longer be compatible
			// with the new version of Tomboy).
			// See bug #514931 for details.
			using (StreamWriter sw = File.CreateText (addins_file)) {
				string addins_file_contents = String.Format (
				                                      "<!--\n" +
				                                      "     This file was automatically generated.  Editing this\n" +
				                                      "     file by hand is strongly discouraged and such changes\n" +
			                                              "     may be reverted at any time.\n" +
				                                      "-->\n\n" +
				                                      "<Addins>\n" +
				                                      "\t<Directory>{0}</Directory>\n" +
				                                      "</Addins>\n",
				                                      Tomboy.Uninstalled ? "./addins" : Defines.SYS_ADDINS_DIR);
				sw.Write (addins_file_contents);
			}

			Mono.Addins.AddinManager.AddinLoaded += OnAddinLoaded;
			Mono.Addins.AddinManager.AddinUnloaded += OnAddinUnloaded;

                        /* Hopefully adding the try / catch block will fix an exception when the Addin Manager cannot read the Addin description.
                         * bgo #681542
                         * jjenings Aug 22, 2012
                         */
                        try {
                                Mono.Addins.AddinManager.Initialize (Tomboy.Uninstalled ? "." : tomboy_conf_dir);
                        } catch (System.InvalidOperationException e) {
                                Logger.Error ("Failed to load add-ins into AddinManager", e);
                        }

			UpgradeOldAddinConfig ();
			if (Tomboy.Debugging) {
				Mono.Addins.AddinManager.Registry.Rebuild (null);
			} else {
				Mono.Addins.AddinManager.Registry.Update (null);
			}
			Mono.Addins.AddinManager.AddExtensionNodeHandler ("/Tomboy/ApplicationAddins", OnApplicationAddinExtensionChanged);
			// NOTE: A SyncServiceAddin is a specialization of an ApplicationAddin
			Mono.Addins.AddinManager.AddExtensionNodeHandler ("/Tomboy/SyncServiceAddins", OnApplicationAddinExtensionChanged);
			Mono.Addins.AddinManager.AddExtensionNodeHandler ("/Tomboy/NoteAddins", OnNoteAddinExtensionChanged);
		}

		void OnAddinLoaded (object sender, Mono.Addins.AddinEventArgs args)
		{
			Logger.Debug ("AddinManager.OnAddinLoaded: {0}", args.AddinId);
			Mono.Addins.Addin addin = Mono.Addins.AddinManager.Registry.GetAddin (args.AddinId);
			Logger.Debug ("\t       Name: {0}", addin.Name);
			Logger.Debug ("\tDescription: {0}", addin.Description.Description);
			Logger.Debug ("\t  Namespace: {0}", addin.Namespace);
			Logger.Debug ("\t    Enabled: {0}", addin.Enabled);
			Logger.Debug ("\t       File: {0}", addin.AddinFile);
		}

		void OnAddinUnloaded (object sender, Mono.Addins.AddinEventArgs args)
		{
			Logger.Debug ("AddinManager.OnAddinUnloaded: {0}", args.AddinId);
		}

		void OnApplicationAddinExtensionChanged (object sender, Mono.Addins.ExtensionNodeEventArgs args)
		{
			Mono.Addins.TypeExtensionNode type_node =
			        args.ExtensionNode as Mono.Addins.TypeExtensionNode;

			ApplicationAddin addin;
			if (args.Change == Mono.Addins.ExtensionChange.Add) {
				// Load NoteAddins
				if (Tomboy.DefaultNoteManager == null) {
					return; // too early -- YUCK!  Bad hack
				}

				addin = type_node.GetInstance (
				                typeof (ApplicationAddin)) as ApplicationAddin;
				if (addin != null) {
					if (addin.Initialized == false) {
						try {
							addin.Initialize ();
							app_addins [type_node.Id] = addin;
						} catch (Exception e) {
							Logger.Debug ("Error initializing app addin {0}: {1}\n{2}",
							              addin.GetType ().ToString (),
							              e.Message,
							              e.StackTrace);
						}
					}
				}
			} else {
				if (app_addins.ContainsKey (type_node.Id)) {
					addin = app_addins [type_node.Id];
					try {
						addin.Shutdown ();
					} catch (Exception e1) {
						Logger.Warn ("Error shutting down app addin {0}: {1}\n{2}",
						             addin.GetType ().ToString (),
						             e1.Message,
						             e1.StackTrace);
					} finally {
						app_addins.Remove (type_node.Id);
					}

					try {
						addin.Dispose ();
					} catch (Exception e1) {
						Logger.Warn ("Error disposing app addin: {0} - {1}",
						             addin.GetType ().ToString (), e1.Message);
					}
				}
			}

			if (ApplicationAddinListChanged != null)
				ApplicationAddinListChanged (sender, args);
		}

		void OnNoteAddinExtensionChanged (object sender, Mono.Addins.ExtensionNodeEventArgs args)
		{
			if (args.Change == Mono.Addins.ExtensionChange.Add)
				OnNoteAddinEnabled (args);
			else
				OnNoteAddinDisabled (args);
		}

		void OnNoteAddinEnabled (Mono.Addins.ExtensionNodeEventArgs args)
		{
			// Load NoteAddins
			if (Tomboy.DefaultNoteManager == null) {
				return; // too early -- YUCK!  Bad hack
			}

			foreach (Note note in Tomboy.DefaultNoteManager.Notes) {
				// Create a new NoteAddin
				Mono.Addins.TypeExtensionNode type_node =
				        args.ExtensionNode as Mono.Addins.TypeExtensionNode;

				try {
					NoteAddin n_addin = type_node.CreateInstance () as NoteAddin;

					// Keep track of the addins added to each note
					AttachAddin (type_node.Id, note, n_addin);
				} catch (Exception e) {
					Logger.Debug ("Couldn't create a NoteAddin instance: {0}", e.Message);
				}
			}
		}

		void OnNoteAddinDisabled (Mono.Addins.ExtensionNodeEventArgs args)
		{
			Mono.Addins.TypeExtensionNode type_node =
			        args.ExtensionNode as Mono.Addins.TypeExtensionNode;

			try {
				OnDisabledAddin (type_node.Id);
			} catch (Exception e) {
				Logger.Debug ("Error unloading add-in: " + e.Message);
			}
		}

		public void LoadAddinsForNote (Note note)
		{
			Mono.Addins.ExtensionNodeList list = Mono.Addins.AddinManager.GetExtensionNodes ("/Tomboy/NoteAddins");
			foreach (Mono.Addins.ExtensionNode node in list) {
				Mono.Addins.TypeExtensionNode type_node =
				        node as Mono.Addins.TypeExtensionNode;

				try {
					NoteAddin n_addin = type_node.CreateInstance () as NoteAddin;

					// Keep track of the addins added to each note
					AttachAddin (type_node.Id, note, n_addin);
				} catch (Exception e) {
					Logger.Warn ("Couldn't create a NoteAddin instance: {0}", e.Message);
				}
			}

			// Make sure we remove addins when a note is deleted
			note.Manager.NoteDeleted += OnNoteDeleted;
		}

		/// <summary>
		/// Returns an array of ApplicationAddin objects
		/// </summary>
		public ApplicationAddin [] GetApplicationAddins ()
		{
			ApplicationAddin [] app_addins;

			try {
				app_addins = (ApplicationAddin [])
				             Mono.Addins.AddinManager.GetExtensionObjects (
				                     "/Tomboy/ApplicationAddins",
				                     typeof (ApplicationAddin),
				                     true);
			} catch (Exception e) {
				Logger.Warn ("No ApplicationAddins found: {0}", e.Message);
				app_addins = new ApplicationAddin [0];
			}

			return app_addins;
		}

		/// <summary>
		/// Returns an array of NoteAddin objects that tomboy
		/// currently knows about.
		/// </summary>
		public NoteAddin [] GetNoteAddins ()
		{
			NoteAddin [] addins;

			try {
				addins = (NoteAddin [])
				         Mono.Addins.AddinManager.GetExtensionObjects (
				                 "/Tomboy/NoteAddins",
				                 typeof (NoteAddin));
			} catch (Exception e) {
				Logger.Warn ("No NoteAddins found: {0}", e.Message);
				addins = new NoteAddin [0];
			}

			return addins;
		}
		
		/// <summary>
		/// Returns an array of PreferenceTabAddin objects.
		/// </summary>
		/// <returns>
		/// A <see cref="PreferenceTabAddin"/>
		/// </returns>
		public PreferenceTabAddin [] GetPreferenceTabAddins ()
		{
			PreferenceTabAddin [] addins;
			
			try {
				addins = (PreferenceTabAddin [])
							Mono.Addins.AddinManager.GetExtensionObjects (
								"/Tomboy/PreferenceTabAddins",
								typeof (PreferenceTabAddin));
			} catch (Exception e) {
				Logger.Warn ("No PreferenceTabAddins found: {0}", e.Message);
				addins = new PreferenceTabAddin [0];
			}
			
			return addins;
		}

		/// <summary>
		/// Returns an array of SyncServiceAddin objects
		/// </summary>
		public SyncServiceAddin [] GetSyncServiceAddins ()
		{
			SyncServiceAddin [] addins;

			try {
				addins = (SyncServiceAddin [])
				         Mono.Addins.AddinManager.GetExtensionObjects (
				                 "/Tomboy/SyncServiceAddins",
				                 typeof (SyncServiceAddin));
			} catch (Exception e) {
				Logger.Debug ("No SyncServiceAddins found: {0}", e.Message);
				addins = new SyncServiceAddin [0];
			}

			return addins;
		}

		/// <summary>
		/// Add the addin to the note and save off a reference to the addin that
		/// will be used when the note is deleted.
		/// </summary>
		public List<Mono.Addins.Addin> GetAllAddins ()
		{
			List<Mono.Addins.Addin> addins = new List<Mono.Addins.Addin> ();

			Mono.Addins.Addin [] addinsArray =
			        Mono.Addins.AddinManager.Registry.GetAddins ();

			if (addinsArray != null) {
				// It just so happens that the NoteAddins that are part of
				// Tomboy.exe (from Watchers.cs) are not returned by the
				// above GetAddins () call, so we don't have to do anything
				// to exclude them here.
				addins = new List<Mono.Addins.Addin> (addinsArray);
			}

			return addins;
		}

		/// <summary>
		/// Call NoteAddin.Shutdown () and NoteAddin.Dispose () on every
		/// NoteAddin that's attached to the deleted Note.
		/// </summary>
		void OnNoteDeleted (object sender, Note deleted)
		{
			if (note_addins.ContainsKey (deleted) == false)
				return;

			OnDeletedNote (deleted);
		}

		void AttachAddin (string ext_node_id, Note note, NoteAddin addin)
		{
			if (ext_node_id == null || note == null || addin == null)
				throw new ArgumentNullException ("Cannot pass in a null parameter to AttachAddin");

			// Loading the addin to the note
			try {
				addin.Initialize (note);
			} catch (Exception e) {
				Logger.Warn ("Error initializing addin: {0}: {1}",
				             addin.GetType ().ToString (), e.Message);
				// TODO: Would be nice to figure out how to just disable
				// the addin altogether if it's failing to initialize so
				// it doesn't keep causing problems.
				return;
			}

			NoteAddinInfo info = new NoteAddinInfo (ext_node_id, note, addin);
			List<NoteAddinInfo> note_addin_list;
			List<NoteAddinInfo> ext_node_addin_list;

			if (note_addins.ContainsKey (note))
				note_addin_list = note_addins [note];
			else {
				note_addin_list = new List<NoteAddinInfo> ();
				note_addins [note] = note_addin_list;
			}

			if (note_addin_infos.ContainsKey (ext_node_id))
				ext_node_addin_list = note_addin_infos [ext_node_id];
			else {
				ext_node_addin_list = new List<NoteAddinInfo> ();
				note_addin_infos [ext_node_id] = ext_node_addin_list;
			}

			note_addin_list.Add (info);
			ext_node_addin_list.Add (info);
		}

		void OnDisabledAddin (string ext_node_id)
		{
			if (ext_node_id == null)
				throw new ArgumentNullException (
				        "Cannot call OnDisabledAddin with null parameters");

			Logger.Debug ("OnDisabledAddin: {0}", ext_node_id);

			// Remove and shut down all the addins
			if (note_addin_infos.ContainsKey (ext_node_id) == false)
				throw new ArgumentException (
				        "Cannot call OnDisabledAddin with an invalid Mono.Addins.ExtensionNode.Id");

			List<NoteAddinInfo> addin_info_list = note_addin_infos [ext_node_id];
			foreach (NoteAddinInfo info in addin_info_list) {
				try {
					info.Addin.Shutdown ();
				} catch (Exception e) {
					Logger.Warn ("Error shutting down addin: {0} - {1}",
					             info.Addin.GetType ().ToString (), e.Message);
				}

				try {
					info.Addin.Dispose ();
				} catch (Exception e1) {
					Logger.Warn ("Error disposing addin: {0} - {1}",
					             info.Addin.GetType ().ToString (), e1.Message);
				}

				// Remove the addin from the Note
				if (note_addins.ContainsKey (info.Note)) {
					List<NoteAddinInfo> note_addin_list = note_addins [info.Note];
					note_addin_list.Remove (info);
				}
			}
			note_addin_infos.Remove (ext_node_id);
		}

		void OnDeletedNote (Note note)
		{
			if (note == null)
				throw new ArgumentNullException (
				        "Cannot call OnDeletedNote with null parameters");

			if (note_addins.ContainsKey (note) == false)
				throw new ArgumentException (
				        "Cannot call OnDeletedNote with an invalid Note");

			List<NoteAddinInfo> note_addin_list = note_addins [note];
			foreach (NoteAddinInfo info in note_addin_list) {
				try {
					info.Addin.Shutdown ();
				} catch (Exception e) {
					Logger.Warn ("Error shutting down addin: {0} - {1}",
					             info.Addin.GetType ().ToString (), e.Message);
				}

				try {
					info.Addin.Dispose ();
				} catch (Exception e1) {
					Logger.Warn ("Error disposing addin: {0} - {1}",
					             info.Addin.GetType ().ToString (), e1.Message);
				}

				if (note_addin_infos.ContainsKey (info.ExtensionNodeId)) {
					List<NoteAddinInfo> addin_info_list =
					        note_addin_infos [info.ExtensionNodeId];
					addin_info_list.Remove (info);
				}
			}

			note_addins.Remove (note);
		}

		public bool IsAddinConfigurable (Mono.Addins.Addin addin)
		{
			object o = GetAddinPrefFactory (addin);

			if (o == null)
				return false;

			return true;
		}

		public Gtk.Widget CreateAddinPreferenceWidget (Mono.Addins.Addin addin)
		{
			AddinPreferenceFactory factory = GetAddinPrefFactory (addin);
			if (factory == null)
				return null;

			return factory.CreatePreferenceWidget ();
		}

		AddinPreferenceFactory GetAddinPrefFactory (Mono.Addins.Addin addin)
		{
			Mono.Addins.ExtensionNode node =
			        Mono.Addins.AddinManager.GetExtensionNode ("/Tomboy/AddinPreferences");

			if (node != null) {
				Mono.Addins.ExtensionNodeList child_nodes = node.ChildNodes;
				if (child_nodes != null) {
					foreach (Mono.Addins.ExtensionNode child_node in child_nodes) {
						if (addin.Id.StartsWith (child_node.Addin.Id)) {
							AddinPreferenceFactory factory =
							        ((Mono.Addins.TypeExtensionNode)child_node).GetInstance () as AddinPreferenceFactory;
							return factory;
						}
					}
				}
			}

			return null;
		}
		
		/// <summary>
		/// The purpose of this method is to check for an older config.xml file
		/// in an older addin-db-* directory.  If a config.xml is found in an
		/// older directory but not in the new addin-db-* directory, config.xml
		/// will be copied into the new one.  This addresses a problem found
		/// in bug #514931.  While running an older version of Tomboy, if a user
		/// enables an addin that's disabled by deafult and then upgrades to a
		/// newer Tomboy, the addin could be disabled again (which is what this
		/// method attempts to fix).
		/// </summary>
		private void UpgradeOldAddinConfig ()
		{
			string registryPath =
				Mono.Addins.AddinManager.Registry.RegistryPath;
			
			// Get the list of addin-db-* directories
			string[] dirs =
				Directory.GetDirectories (registryPath, "addin-db-*");
			if (dirs == null || dirs.Length < 2) {
				// If there are less than two, this is not an upgrade case
				return;
			}
			
			string oldAddinsDbPath = dirs [dirs.Length - 2];
			string newAddinsDbPath = dirs [dirs.Length - 1];
			// Check the last directory to see if it has a "config.xml" file.
			// If it does, we can assume that the upgrade has already happened.
			string oldConfigFile = Path.Combine (oldAddinsDbPath, "config.xml");
			string newConfigFile = Path.Combine (newAddinsDbPath, "config.xml");
			if (File.Exists (newConfigFile) == true)
				return;
			
			// If there's no config.xml file in the old directory, there's no
			// need to do an upgrade because the user must have never changed
			// anything from the default.
			if (File.Exists (oldConfigFile) == false)
				return;
			
			Logger.Info ("Upgrading Mono.Addins config.xml: {0}", newConfigFile);
			try {
				File.Copy (oldConfigFile, newConfigFile);
			} catch (Exception e) {
				Logger.Warn ("Exception when upgrading Mono.Addins config.xml: {0}",
							 e.Message);
			}
		}
	}

	// TODO: Add this back in so that we can know when Addins leak memory at
	// disable/shutdown.
	class AddinReference : WeakReference
	{
		readonly string description;

public AddinReference (object addin, string description) :
		base (addin)
		{
			this.description = description;
		}

		public string Description
		{
			get {
				return description;
			}
		}
	}

	class NoteAddinInfo
	{
		readonly string extension_node_id;
		readonly Note note;
		readonly NoteAddin note_addin;

		public NoteAddinInfo (string node_id, Note note, NoteAddin addin)
		{
			this.extension_node_id = node_id;
			this.note = note;
			this.note_addin = addin;
		}

		public string ExtensionNodeId
		{
			get {
				return extension_node_id;
			}
		}

		public Note Note
		{
			get {
				return note;
			}
		}

		public NoteAddin Addin
		{
			get {
				return note_addin;
			}
		}
	}
}
