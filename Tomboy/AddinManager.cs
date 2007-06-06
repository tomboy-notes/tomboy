
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO;

using Mono.Unix;
using Mono.Unix.Native;

namespace Tomboy
{
	public class AddinManager
	{
		readonly string tomboy_conf_dir;
		
		Dictionary<Note,List<NoteAddin>> note_addins;
		
		public AddinManager (string tomboy_conf_dir)
		{
			this.tomboy_conf_dir = tomboy_conf_dir;
			note_addins = new Dictionary<Note,List<NoteAddin>> ();
			
			InitializeMonoAddins ();
		}
		
		void InitializeMonoAddins ()
		{
			Logger.Info ("Initializing Mono.Addins");
			
			string addins_dir = Path.Combine (tomboy_conf_dir, "addins");
			if (!Directory.Exists (addins_dir))
				Directory.CreateDirectory (addins_dir);
			
			// Make sure a Tomboy.addins file exists
			string addins_file = Path.Combine (addins_dir, "Tomboy.addins");
			
			if (!File.Exists (addins_file)) {
				StreamWriter sw = File.CreateText (addins_file);
				string addins_file_contents = String.Format (
						"<!--\n" +
						"     This file was automatically generated.  Editing this\n" +
						"     file by hand is strongly discouraged.\n" +
						"-->\n\n" +
						"<Addins>\n" +
						"\t<Directory>{0}</Directory>\n" +
						"</Addins>\n",
						Defines.SYS_ADDINS_DIR);
				sw.Write (addins_file_contents);
				sw.Close ();
			}
			
			Mono.Addins.AddinManager.AddinLoaded += OnAddinLoaded;
			Mono.Addins.AddinManager.AddinUnloaded += OnAddinUnloaded;
			Mono.Addins.AddinManager.Initialize (tomboy_conf_dir);
			Mono.Addins.AddinManager.Registry.Rebuild (null);
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
			DetachAddin (args.AddinId);
		}

		public void LoadAddinsForNote (Note note)
		{
			NoteAddin [] addins = CreateNoteAddins ();
			
			foreach (NoteAddin addin in addins) {
				AttachAddin (addin, note);
			}

			// Make sure we remove plugins when a note is deleted
			note.Manager.NoteDeleted += OnNoteDeleted;
		}
		
		/// <summary>
		/// Returns an array of ApplicationAddin objects
		/// </summary>
		public ApplicationAddin [] GetApplicationAddins ()
		{
			ApplicationAddin [] addins;
			
			try {
				addins = (ApplicationAddin [])
						Mono.Addins.AddinManager.GetExtensionObjects (
							"/Tomboy/ApplicationAddins",
							typeof (ApplicationAddin));
			} catch (Exception e) {
				Logger.Debug ("No ApplicationAddins found: {0}", e.Message);
				addins = new ApplicationAddin [0];
			}
			
			return addins;
		}
		
		/// <summary>
		/// Create new instances of any NoteAddin that is installed/enabled
		/// and return them.
		/// </summary>
		private NoteAddin [] CreateNoteAddins ()
		{
			NoteAddin [] addins;
			
			try {
				addins = (NoteAddin [])
						Mono.Addins.AddinManager.GetExtensionObjects (
							"/Tomboy/NoteAddins",
							typeof (NoteAddin),
							false); // Don't reuse objects from cache, create new ones
			} catch (Exception e) {
				Logger.Debug ("No NoteAddins found: {0}", e.Message);
				addins = new NoteAddin [0];
			}
			
			return addins;
		}
		
		/// <summary>
		/// Add the addin to the note and save off a reference to the addin that
		/// will be used when the note is deleted.
		/// </summary>
		void AttachAddin (NoteAddin addin, Note note)
		{
			List<NoteAddin> addins;
			if (note_addins.ContainsKey (note) == false) {
				addins = new List<NoteAddin> ();
				note_addins [note] = addins;
			} else {
				addins = note_addins [note];
			}
			
			// Add the addin to the list
			try {
				addin.Initialize (note);
				addins.Add (addin);
			} catch (Exception e) {
				Logger.Warn ("Error initializing addin: {0}: {1}",
						addin.GetType ().ToString (), e.Message);
				// FIXME: Would be nice to figure out how to just disable
				// the addin altogether if it's failing to initialize so
				// it doesn't keep causing problems.
			}
		}
		
		/// <summary>
		/// Call NoteAddin.Shutdown () and NoteAddin.Dispose () on every
		/// NoteAddin that's attached to the deleted Note.
		/// </summary>
		void OnNoteDeleted (object sender, Note deleted)
		{
			if (note_addins.ContainsKey (deleted) == false)
				return;
			
			List<NoteAddin> addins = note_addins [deleted];
			foreach (NoteAddin addin in addins) {
				try {
					addin.Dispose ();
				} catch (Exception e) {
					Logger.Fatal (
						"Cannot dispose {0}: {1}",
						addin.GetType (), e);
				}
			}

			addins.Clear ();
			note_addins.Remove (deleted);
		}
		
		// FIXME: Warning, the DetachAddin code has never been tested since we don't have a way to disable/uninstall addins yet
		void DetachAddin (string addin_id)
		{
			List<AddinReference> references = null;
			
			// FIXME: Eventually prevent reference checks and enable from command-line
//			if (CheckAddinUnloading)
				references = new List<AddinReference> ();
			
			DetachAddin (addin_id, references);
			
			Logger.Debug ("Starting garbage collection...");
			GC.Collect ();
			GC.WaitForPendingFinalizers ();

			Logger.Debug ("Garbage collection complete.");

//			if (!CheckPluginUnloading) 
//				return;

			Logger.Debug ("Checking addin references...");

			int finalized = 0, leaking = 0;

			foreach (AddinReference r in references) {
				object addin = r.Target;

				if (null != addin) {
					Logger.Fatal (
						"Leaking reference on {0}: '{1}'",
						addin, r.Description);

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
					addin_id);
				string message = String.Format (Catalog.GetString (
					"Cannot fully disable {0} as there still are " +
					"at least {1} reference to this addin. This " +
					"indicates a programming error. Contact the " +
					"addin's author and report this problem.\n\n" +
					"<b>Developer Information:</b> This problem " +
					"usually occurs when the addin's Dispose " +
					"method fails to disconnect all event handlers. " +
					"The heap-shot profiler ({2}) can help to " +
					"identify leaking references."),
					addin_id, leaking, heapshot);

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
		void DetachAddin (string addin_id, List<AddinReference> references)
		{
			Mono.Addins.Addin addin =
				Mono.Addins.AddinManager.Registry.GetAddin (addin_id);
			if (addin == null)
				return;
			
			Mono.Addins.Description.AddinDescription description =
				addin.Description;
			
			foreach (Mono.Addins.Description.ExtensionPoint point in 
							description.ExtensionPoints) {
				if (point.Path != "/Tomboy/NoteAddins")
					continue;
				
				Mono.Addins.Description.ExtensionNodeSet node_set =
						point.NodeSet;
				
				foreach (object o in node_set.NodeTypes) {
					// FIXME: Get the System.Type of the NoteAddins so we can
					// look them up and remove them from note_addins.
					Logger.Debug ("NodeType: {0}", o.GetType ().ToString ());
				}
			}
		}
		
		void DetachAddin (Type type, List<AddinReference> references)
		{
			if (typeof (NoteAddin).IsAssignableFrom (type)) {
				foreach (List<NoteAddin> addin_list in note_addins.Values) {
					foreach (NoteAddin addin in addin_list) {
						try {
							addin.Dispose ();
						} catch (Exception e) {
							Logger.Fatal (
								"Cannot dispose {0}: {1}",
								type, e.Message);
						}
						
						if (null != references)
							references.Add (new AddinReference (
								addin, type.ToString ()));
					}
				}
			}
		}
	}

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
			get { return description; }
		}
	}
}
