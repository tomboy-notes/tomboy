using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using System.Threading;

using Mono.Unix;

namespace Tomboy.Sync
{
	public enum SyncState {
		/// <summary>
		/// The synchronization thread is not running
		/// </summary>
		Idle,

		/// <summary>
		/// Indicates that no sync service has been configured
		/// </summary>
		NoConfiguredSyncService,

		/// <summary>
		/// Indicates that SyncServiceAddin.CreateSyncServer () failed
		/// </summary>
		SyncServerCreationFailed,

		/// <summary>
		/// Connecting to the server
		/// </summary>
		Connecting,

		/// <summary>
		/// Acquiring the right to be the exclusive sync client
		/// </summary>
		AcquiringLock,

		/// <summary>
		/// Another client is currently synchronizing
		/// </summary>
		Locked,

		/// <summary>
		/// Preparing to download new/updated notes from the server.  This also
		/// includes checking for note title name conflicts.
		/// </summary>
		PrepareDownload,

		/// <summary>
		/// Downloading notes from the server
		/// </summary>
		Downloading,

		/// <summary>
		/// Checking for files to send to the server
		/// </summary>
		PrepareUpload,

		/// <summary>
		/// Uploading new/changed notes from the client
		/// </summary>
		Uploading,

		/// <summary>
		/// Deleting notes from the server
		/// </summary>
		DeleteServerNotes,

		/// <summary>
		/// Committing Changes to the server
		/// </summary>
		CommittingChanges,

		/// <summary>
		/// SyncSuccess
		/// </summary>
		Succeeded,

		/// <summary>
		/// The synchronization failed
		/// </summary>
		Failed,

		/// <summary>
		/// The synchronization was cancelled by the user
		/// </summary>
		UserCancelled
	};

	public enum NoteSyncType {
		UploadNew,
		UploadModified,
		DownloadNew,
		DownloadModified,
		DeleteFromServer,
		DeleteFromClient
	};

	/// <summary>
	/// Handle state SyncManager state changes
	/// </summary>
	public delegate void SyncStateChangedHandler (SyncState state);

	/// <summary>
	/// Handle when notes are uploaded, downloaded, or deleted
	/// </summary>
	public delegate void NoteSyncHandler (string noteTitle, NoteSyncType type);

	/// <summary>
	/// Handle a note conflict
	/// </summary>
	public delegate void NoteConflictHandler (NoteManager manager,
	                Note localConflictNote,
	                NoteUpdate remoteNote,
	                IList<string> noteUpdateTitles);

	public class SyncManager
	{
		private static ISyncUI syncUI;
		private static SyncClient client;
		private static SyncState state = SyncState.Idle;
		private static Thread syncThread = null;
		// TODO: Expose the next enum more publicly
		private static SyncTitleConflictResolution conflictResolution;

		// TODO: Are these needed in the era of ISyncUI? Probably,
		//       but leaving them out is good for testing right now
//		/// <summary>
//		/// Emitted when the state of the synchronization changes
//		/// </summary>
//		public static event SyncStateChangedHandler StateChanged;
//
//		/// <summary>
//		/// Emmitted when a file is uploaded, downloaded, or deleted.
//		/// </summary>
//		public static event NoteSyncHandler NoteSynchronized;
//
//		/// <summary>
//		///
//		/// </summary>
//		public static event NoteConflictHandler NoteConflictDetected;

		static SyncManager ()
		{
			client = new TomboySyncClient ();
			//server = new FileSystemSyncServer ();
		}

		public static void Initialize ()
		{
			// NOTE: static constructor should get called if this
			// is the first reference to SyncManager

			///
			/// Add a "Synchronize Notes" to Tomboy's Main Menu
			///
			Gtk.ActionGroup action_group = new Gtk.ActionGroup ("Sync");
			action_group.Add (new Gtk.ActionEntry [] {
				new Gtk.ActionEntry ("ToolsMenuAction", null,
				Catalog.GetString ("_Tools"), null, null, null),
				new Gtk.ActionEntry ("SyncNotesAction", null,
				Catalog.GetString ("Synchronize Notes"), null, null,
				delegate {
					Tomboy.ActionManager ["NoteSynchronizationAction"].Activate ();
				})
			});

			Tomboy.ActionManager.UI.AddUiFromString (@"
			                <ui>
			                <menubar name='MainWindowMenubar'>
			                <placeholder name='MainWindowMenuPlaceholder'>
			                <menu name='ToolsMenu' action='ToolsMenuAction'>
			                <menuitem name='SyncNotes' action='SyncNotesAction' />
			                </menu>
			                </placeholder>
			                </menubar>
			                </ui>
			                ");

			Tomboy.ActionManager.UI.InsertActionGroup (action_group, 0);

			// Initialize all the SyncServiceAddins
			SyncServiceAddin [] addins = Tomboy.DefaultNoteManager.AddinManager.GetSyncServiceAddins ();
			foreach (SyncServiceAddin addin in addins) {
				try {
					addin.Initialize ();
				} catch (Exception e) {
					Logger.Debug ("Error calling {0}.Initialize (): {1}\n{2}",
					              addin.Id, e.Message, e.StackTrace);

					// TODO: Call something like AddinManager.Disable (addin)
				}
			}

			Preferences.SettingChanged += Preferences_SettingChanged;
			NoteMgr.NoteSaved += (n) => HandleNoteSavedOrDeleted ();
			NoteMgr.NoteDeleted += (o, n) => HandleNoteSavedOrDeleted ();

			// Update sync item based on configuration.
			UpdateSyncAction ();
		}

		static void HandleNoteSavedOrDeleted ()
		{
			if (syncThread == null && autosyncTimer != null && autosyncTimeoutPrefMinutes > 0) {
				TimeSpan timeSinceLastCheck =
					DateTime.Now - lastBackgroundCheck;
				TimeSpan timeUntilNextCheck =
					new TimeSpan (0, currentAutosyncTimeoutMinutes, 0) - timeSinceLastCheck;
				if (timeUntilNextCheck.TotalMinutes < 1) {
					Logger.Debug ("Note saved or deleted within a minute of next autosync...resetting sync timer");
					currentAutosyncTimeoutMinutes = 1;
					autosyncTimer.Change (currentAutosyncTimeoutMinutes * 60000,
					                      autosyncTimeoutPrefMinutes * 60000);
					NoteMgr.NoteBufferChanged -= HandleNoteBufferChanged;
					NoteMgr.NoteBufferChanged += HandleNoteBufferChanged;
				}
			} else if (syncThread == null && autosyncTimer == null && autosyncTimeoutPrefMinutes > 0) {
				Logger.Debug ("Note saved or deleted...restarting sync timer");
				lastBackgroundCheck = DateTime.Now;
				 // Perform a sync one minute after setting change
				currentAutosyncTimeoutMinutes = 1;
				autosyncTimer = new Timer ((o) => BackgroundSyncChecker (),
				                           null,
				                           currentAutosyncTimeoutMinutes * 60000,
				                           autosyncTimeoutPrefMinutes * 60000);
				NoteMgr.NoteBufferChanged -= HandleNoteBufferChanged;
				NoteMgr.NoteBufferChanged += HandleNoteBufferChanged;
			}
		}

		static void HandleNoteBufferChanged (Note note)
		{
			// Note changed, iff a sync is coming up we kill the
			// timer to avoid interupting the user (we want to
			// make sure not to sync more often than the user's pref)
			if (syncThread == null && autosyncTimer != null) {
				TimeSpan timeSinceLastCheck =
					DateTime.Now - lastBackgroundCheck;
				if (timeSinceLastCheck.TotalMinutes > autosyncTimeoutPrefMinutes - 1) {
					Logger.Debug ("Note edited...killing autosync timer until next save or delete event");
					autosyncTimer.Dispose ();
					autosyncTimer = null;
					NoteMgr.NoteBufferChanged -= HandleNoteBufferChanged;
				}
			}
		}

		static void Preferences_SettingChanged (object sender, EventArgs args)
		{
			// Update sync item based on configuration.
			UpdateSyncAction ();
		}

		private static Timer autosyncTimer;
		private static int autosyncTimeoutPrefMinutes = -1;
		// This may differ from the pref, if some logic has determined
		// that the next background check should occur in, say, 1 minute
		private static int currentAutosyncTimeoutMinutes = -1;
		private static DateTime lastBackgroundCheck;

		static void UpdateSyncAction ()
		{
			string sync_addin_id = Preferences.Get (Preferences.SYNC_SELECTED_SERVICE_ADDIN) as string;
			Tomboy.ActionManager["SyncNotesAction"].Sensitive = !string.IsNullOrEmpty (sync_addin_id);

			int timeoutPref = (int) Preferences.Get (Preferences.SYNC_AUTOSYNC_TIMEOUT);
			if (timeoutPref != autosyncTimeoutPrefMinutes) {
				autosyncTimeoutPrefMinutes = timeoutPref;
				if (autosyncTimer != null) {
					autosyncTimer.Dispose ();
					autosyncTimer = null;
				}
				if (autosyncTimeoutPrefMinutes > 0) {
					Logger.Debug ("Autosync pref changed...restarting sync timer");
					autosyncTimeoutPrefMinutes = autosyncTimeoutPrefMinutes >= 5 ? autosyncTimeoutPrefMinutes : 5;
					lastBackgroundCheck = DateTime.Now;
					 // Perform a sync no sooner than user specified
					currentAutosyncTimeoutMinutes = autosyncTimeoutPrefMinutes;
					autosyncTimer = new Timer ((o) => BackgroundSyncChecker (),
					                           null,
					                           currentAutosyncTimeoutMinutes * 60000,
					                           autosyncTimeoutPrefMinutes * 60000);
					NoteMgr.NoteBufferChanged -= HandleNoteBufferChanged;
					NoteMgr.NoteBufferChanged += HandleNoteBufferChanged;
				}
			}
		}

		static void BackgroundSyncChecker ()
		{
			lastBackgroundCheck = DateTime.Now;
			currentAutosyncTimeoutMinutes = autosyncTimeoutPrefMinutes;
			if (syncThread != null)
				return;
			var addin = GetConfiguredSyncService ();
			if (addin != null) {
				// TODO: block sync while checking
				SyncServer server = null;
				try {
					server = addin.CreateSyncServer ();
					if (server == null)
						throw new Exception ("addin.CreateSyncServer () returned null");
				} catch (Exception e) {
					Logger.Debug ("BackgroundSyncChecker: Exception while creating SyncServer: {0}\n{1}", e.Message, e.StackTrace);
					addin.PostSyncCleanup ();// TODO: Needed?
					return;
					// TODO: Figure out a clever way to get the specific error up to the GUI
				}
				bool serverHasUpdates = false;
				bool clientHasUpdates = client.DeletedNoteTitles.Count > 0;
				if (!clientHasUpdates) {
					foreach (Note note in new List<Note> (NoteMgr.Notes)) {
						if (client.GetRevision (note) == -1 ||
						    note.MetadataChangeDate > client.LastSyncDate) {
							clientHasUpdates = true;
							break;
						}
					}
				}

				// NOTE: Important to check, at least to verify
				//       that server is available
				try {
					Logger.Debug ("BackgroundSyncChecker: Checking server for updates");
					serverHasUpdates = server.UpdatesAvailableSince (client.LastSynchronizedRevision);
				} catch {
					// TODO: A libnotify bubble might be nice
					Logger.Debug ("BackgroundSyncChecker: Error connecting to server");
					addin.PostSyncCleanup ();
					return;
				}

				addin.PostSyncCleanup (); // Let FUSE unmount, etc

				if (clientHasUpdates || serverHasUpdates) {
					Logger.Debug ("BackgroundSyncChecker: Detected that sync would be a good idea now");
					// TODO: Check that it's safe to sync, block other sync UIs
					PerformSynchronization (new SilentUI (NoteMgr));
				}
			}
		}

		public static void ResetClient ()
		{
			try {
				client.Reset ();
			} catch (Exception e) {
				Logger.Debug ("Error deleting client manifest during reset: {1}",
				              e.Message);
			}
		}

		public static void PerformSynchronization (ISyncUI syncUI)
		{
			if (syncThread != null) {
				// A synchronization thread is already running
				// TODO: Start new sync if existing dlg is for finished sync
				// TODO: ISyncUI-ize this somehow
				if (SyncManager.syncUI == Tomboy.SyncDialog)
					Tomboy.SyncDialog.Present ();
				return;
			}

			SyncManager.syncUI = syncUI;
			syncThread = new Thread (new ThreadStart (SynchronizationThread));
			syncThread.IsBackground = true;
			syncThread.Start ();
		}

		/// <summary>
		/// The function that does all of the work
		/// TODO: Factor some nice methods out of here; this is just garbage to read right now
		/// </summary>
		public static void SynchronizationThread ()
		{
			SyncServiceAddin addin = null;
			SyncServer server = null;
			try {

				addin = GetConfiguredSyncService ();
				if (addin == null) {
					SetState (SyncState.NoConfiguredSyncService);
					Logger.Debug ("GetConfiguredSyncService is null");
					SetState (SyncState.Idle);
					syncThread = null;
					return;
				}

				Logger.Debug ("SyncThread using SyncServiceAddin: {0}", addin.Name);

				SetState (SyncState.Connecting);
				try {
					server = addin.CreateSyncServer ();
					if (server == null)
						throw new Exception ("addin.CreateSyncServer () returned null");
				} catch (Exception e) {
					SetState (SyncState.SyncServerCreationFailed);
					Logger.Error ("Exception while creating SyncServer: {0}\n{1}", e.Message, e.StackTrace);
					SetState (SyncState.Idle);
					syncThread = null;
					addin.PostSyncCleanup ();// TODO: Needed?
					return;
					// TODO: Figure out a clever way to get the specific error up to the GUI
				}

				// TODO: Call something that processes all queued note saves!
				//       For now, only saving before uploading (not sufficient for note conflict handling)

				SetState (SyncState.AcquiringLock);
				// TODO: We should really throw exceptions from BeginSyncTransaction ()
				if (!server.BeginSyncTransaction ()) {
					SetState (SyncState.Locked);
					Logger.Warn ("PerformSynchronization: Server locked, try again later");
					SetState (SyncState.Idle);
					syncThread = null;
					addin.PostSyncCleanup ();
					return;
				}
				Logger.Debug ("8");
				int latestServerRevision = server.LatestRevision;
				int newRevision = latestServerRevision + 1;

				// If the server has been wiped or reinitialized by another client
				// for some reason, our local manifest is inaccurate and could misguide
				// sync into erroneously deleting local notes, etc.  We reset the client
				// to prevent this situation.
				string serverId = server.Id;
				if (client.AssociatedServerId != serverId) {
					client.Reset ();
					client.AssociatedServerId = serverId;
				}

				SetState (SyncState.PrepareDownload);

				// Handle notes modified or added on server
				Logger.Debug ("Sync: GetNoteUpdatesSince rev " + client.LastSynchronizedRevision.ToString ());
				IDictionary<string, NoteUpdate> noteUpdates =
				        server.GetNoteUpdatesSince (client.LastSynchronizedRevision);
				Logger.Debug ("Sync: " + noteUpdates.Count + " updates since rev " + client.LastSynchronizedRevision.ToString ());

				// Gather list of new/updated note titles
				// for title conflict handling purposes.
				List<string> noteUpdateTitles = new List<string> ();
				foreach (NoteUpdate noteUpdate in noteUpdates.Values)
					if (!string.IsNullOrEmpty (noteUpdate.Title))
						noteUpdateTitles.Add (noteUpdate.Title);

				// First, check for new local notes that might have title conflicts
				// with the updates coming from the server.  Prompt the user if necessary.
				// TODO: Lots of searching here and in the next foreach...
				//       Want this stuff to happen all at once first, but
				//       maybe there's a way to store this info and pass it on?
				foreach (NoteUpdate noteUpdate in noteUpdates.Values)
				{
					if (FindNoteByUUID (noteUpdate.UUID) == null) {
						Note existingNote = NoteMgr.Find (noteUpdate.Title);
						if (existingNote != null && !noteUpdate.BasicallyEqualTo (existingNote)) {
//							Logger.Debug ("Sync: Early conflict detection for '{0}'", noteUpdate.Title);
							if (syncUI != null) {
								syncUI.NoteConflictDetected (NoteMgr, existingNote, noteUpdate, noteUpdateTitles);

								// Suspend this thread while the GUI is presented to
								// the user.
								syncThread.Suspend ();
							}
						}
					}
				}

				if (noteUpdates.Count > 0)
					SetState (SyncState.Downloading);

				// TODO: Figure out why GUI doesn't always update smoothly

				// Process updates from the server; the bread and butter of sync!
				foreach (NoteUpdate noteUpdate in noteUpdates.Values) {
					Note existingNote = FindNoteByUUID (noteUpdate.UUID);

					if (existingNote == null) {
						// Actually, it's possible to have a conflict here
						// because of automatically-created notes like
						// template notes (if a note with a new tag syncs
						// before its associated template). So check by
						// title and delete if necessary.
						existingNote = NoteMgr.Find (noteUpdate.Title);
						if (existingNote != null) {
							Logger.Debug ("SyncManager: Deleting auto-generated note: " + noteUpdate.Title);
							DeleteNoteInMainThread (existingNote);
						}
						CreateNoteInMainThread (noteUpdate);
					} else if (existingNote.MetadataChangeDate.CompareTo (client.LastSyncDate) <= 0 ||
					           noteUpdate.BasicallyEqualTo (existingNote)) {
						// Existing note hasn't been modified since last sync; simply update it from server
						UpdateNoteInMainThread (existingNote, noteUpdate);
					} else {
//						Logger.Debug ("Sync: Late conflict detection for '{0}'", noteUpdate.Title);
						Logger.Debug (string.Format (
						                      "SyncManager: Content conflict in note update for note '{0}'",
						                      noteUpdate.Title));
						// Note already exists locally, but has been modified since last sync; prompt user
						if (syncUI != null) {
							syncUI.NoteConflictDetected (NoteMgr, existingNote, noteUpdate, noteUpdateTitles);

							// Suspend this thread while the GUI is presented to
							// the user.
							syncThread.Suspend ();
						}

						// Note has been deleted or okay'd for overwrite
						existingNote = FindNoteByUUID (noteUpdate.UUID);
						if (existingNote == null)
							CreateNoteInMainThread (noteUpdate);
						else
							UpdateNoteInMainThread (existingNote, noteUpdate);
					}
				}

				// Note deletion may affect the GUI, so we have to use the
				// delegate to run in the main gtk thread.
				// To be consistent, any exceptions in the delgate will be caught
				// and then rethrown in the synchronization thread.
				GuiUtils.GtkInvokeAndWait (() => {
					// Make list of all local notes
					List<Note> localNotes = new List<Note> (NoteMgr.Notes);

					// Get all notes currently on server
					IList<string> serverNotes = server.GetAllNoteUUIDs ();

					// Delete notes locally that have been deleted on the server
					foreach (Note note in localNotes) {
						if (client.GetRevision (note) != -1 &&
						!serverNotes.Contains (note.Id)) {
							if (syncUI != null)
								syncUI.NoteSynchronized (note.Title, NoteSyncType.DeleteFromClient);
							NoteMgr.Delete (note);
						}
					}
				});

				// TODO: Add following updates to syncDialog treeview

				SetState (SyncState.PrepareUpload);
				// Look through all the notes modified on the client
				// and upload new or modified ones to the server
				List<Note> newOrModifiedNotes = new List<Note> ();
				foreach (Note note in new List<Note> (NoteMgr.Notes)) {
					if (client.GetRevision (note) == -1) {
						// This is a new note that has never been synchronized to the server
						// TODO: *OR* this is a note that we lost revision info for!!!
						// TODO: Do the above NOW!!! (don't commit this dummy)
						note.Save ();
						newOrModifiedNotes.Add (note);
						if (syncUI != null)
							syncUI.NoteSynchronized (note.Title, NoteSyncType.UploadNew);
					} else if (client.GetRevision (note) <= client.LastSynchronizedRevision &&
					                note.MetadataChangeDate > client.LastSyncDate) {
						note.Save ();
						newOrModifiedNotes.Add (note);
						if (syncUI != null)
							syncUI.NoteSynchronized (note.Title, NoteSyncType.UploadModified);
					}
				}

				Logger.Debug ("Sync: Uploading " + newOrModifiedNotes.Count.ToString () + " note updates");
				if (newOrModifiedNotes.Count > 0) {
					SetState (SyncState.Uploading);
					server.UploadNotes (newOrModifiedNotes); // TODO: Callbacks to update GUI as upload progresses
				}

				// Handle notes deleted on client
				List<string> locallyDeletedUUIDs = new List<string> ();
				foreach (string noteUUID in server.GetAllNoteUUIDs ()) {
					if (FindNoteByUUID (noteUUID) == null) {
						locallyDeletedUUIDs.Add (noteUUID);
						if (syncUI != null) {
							string deletedTitle = noteUUID;
							if (client.DeletedNoteTitles.ContainsKey (noteUUID))
								deletedTitle = client.DeletedNoteTitles [noteUUID];
							syncUI.NoteSynchronized (deletedTitle, NoteSyncType.DeleteFromServer);
						}
					}
				}
				if (locallyDeletedUUIDs.Count > 0) {
					SetState (SyncState.DeleteServerNotes);
					server.DeleteNotes (locallyDeletedUUIDs);
				}

				SetState (SyncState.CommittingChanges);
				bool commitResult = server.CommitSyncTransaction ();
				if (commitResult) {
					// Apply this revision number to all new/modified notes since last sync
					// TODO: Is this the best place to do this (after successful server commit)
					foreach (Note note in newOrModifiedNotes) {
						client.SetRevision (note, newRevision);
					}
					SetState (SyncState.Succeeded);
				} else {
					SetState (SyncState.Failed);
					// TODO: Figure out a way to let the GUI know what exactly failed
				}

				// This should be equivalent to newRevision
				client.LastSynchronizedRevision = server.LatestRevision;

				client.LastSyncDate = DateTime.Now;

				Logger.Debug ("Sync: New revision: {0}", client.LastSynchronizedRevision);

				SetState (SyncState.Idle);

			} catch (Exception e) { // top-level try
				Logger.Error ("Synchronization failed with the following exception: " +
				              e.Message + "\n" +
				              e.StackTrace);
				// TODO: Report graphically to user
				try {
					SetState (SyncState.Idle); // stop progress
					SetState (SyncState.Failed);
					SetState (SyncState.Idle); // required to allow user to sync again
					if (server != null)
						// TODO: All I really want to do here is cancel
						//       the update lock timeout, but in most cases
						//       this will delete lock files, too.  Do better!
						server.CancelSyncTransaction ();
				} catch {}
			} finally {
				syncThread = null;
				try {
					addin.PostSyncCleanup ();
				} catch (Exception e) {
					Logger.Error ("Error cleaning up addin after sync: " +
					              e.Message + "\n" +
					              e.StackTrace);
				}
			}
		}


		/// <summary>
		/// The GUI should call this after having the user resolve a conflict
		/// so the synchronization thread can continue.
		/// </summary>
		public static void ResolveConflict (/*Note conflictNote,*/
		        SyncTitleConflictResolution resolution)
		{
			if (syncThread != null) {
				conflictResolution = resolution;
				syncThread.Resume ();
			}
		}

		private static void CreateNoteInMainThread (NoteUpdate noteUpdate)
		{
			// Note creation may affect the GUI, so we have to use the
			// delegate to run in the main gtk thread.
			// To be consistent, any exceptions in the delgate will be caught
			// and then rethrown in the synchronization thread.
			GuiUtils.GtkInvokeAndWait (() => {
				Note existingNote = NoteMgr.CreateWithGuid (noteUpdate.Title, noteUpdate.UUID);
				UpdateLocalNote (existingNote, noteUpdate, NoteSyncType.DownloadNew);
			});
		}

		private static void UpdateNoteInMainThread (Note existingNote, NoteUpdate noteUpdate)
		{
			// Note update may affect the GUI, so we have to use the
			// delegate to run in the main gtk thread.
			// To be consistent, any exceptions in the delgate will be caught
			// and then rethrown in the synchronization thread.
			GuiUtils.GtkInvokeAndWait (() => {
				UpdateLocalNote (existingNote, noteUpdate, NoteSyncType.DownloadModified);
			});
		}

		private static void DeleteNoteInMainThread (Note existingNote)
		{
			// Note deletion may affect the GUI, so we have to use the
			// delegate to run in the main gtk thread.
			// To be consistent, any exceptions in the delgate will be caught
			// and then rethrown in the synchronization thread.
			GuiUtils.GtkInvokeAndWait (() => {
				NoteMgr.Delete (existingNote);
			});
		}

		private static void UpdateLocalNote (Note localNote, NoteUpdate serverNote, NoteSyncType syncType)
		{
			// In each case, update existingNote's content and revision
			try {
				localNote.LoadForeignNoteXml (serverNote.XmlContent, ChangeType.OtherDataChanged);
			} catch {} // TODO: Handle exception in case that serverNote.XmlContent is invalid XML
			client.SetRevision (localNote, serverNote.LatestRevision);

			// Update dialog's sync status
			if (syncUI != null)
				syncUI.NoteSynchronized (localNote.Title, syncType);
		}

		private static Note FindNoteByUUID (string uuid)
		{
			return NoteMgr.FindByUri ("note://tomboy/" + uuid);
		}

		private static NoteManager NoteMgr
		{
			get {
				return Tomboy.DefaultNoteManager;
			}
		}

		public static bool SynchronizedNoteXmlMatches (string noteXml1, string noteXml2)
		{
			try {
				// TODO: I prefer XPath code.  Why doesn't this work? (SelectSingleNode returns null)
				/*XmlDocument doc1 = new XmlDocument ();
				XmlDocument doc2 = new XmlDocument ();

				doc1.LoadXml (noteXml1);
				doc2.LoadXml (noteXml2);

				// Check parts of XML that truly differentiate note content
				// (enough that we don't need to prompt the user)
				return (XmlNodesMatch (doc1.SelectSingleNode ("//tags"), doc2.SelectSingleNode ("//tags")) &&
				        XmlNodesMatch (doc1.SelectSingleNode ("//title"), doc2.SelectSingleNode ("//title")) &&
				 XmlNodesMatch (doc1.SelectSingleNode ("//text"), doc2.SelectSingleNode ("//text")));
				*/

				string title1, tags1, content1;
				string title2, tags2, content2;

				GetSynchronizedXmlBits (noteXml1, out title1, out tags1, out content1);
				GetSynchronizedXmlBits (noteXml2, out title2, out tags2, out content2);

				return title1 == title2 && tags1 == tags2 && content1 == content2;
			} catch (Exception e){
				Logger.Debug ("SynchronizedNoteXmlMatches threw exception: " + e.ToString ());
				return false;
			}
		}

		private static void GetSynchronizedXmlBits (string noteXml, out string title, out string tags, out string content)
		{
			title = null;
			tags = null;
			content = null;

			XmlTextReader xml = new XmlTextReader (new StringReader (noteXml));
			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					switch (xml.Name) {
					case "title":
						title = xml.ReadString ();
						break;
					case "tags":
						tags = xml.ReadInnerXml ();
						Logger.Debug ("In the bits: tags = " + tags); // TODO: Delete
						break;
					case "text":
						content = xml.ReadInnerXml ();
						break;
					}
					break;
				}
			}
		}

//  private static void OnSyncDialogResponse (object sender, Gtk.ResponseArgs args)
//  {
//   SyncDialog dialog = sender as SyncDialog;
//   dialog.Hide ();
//   dialog.Destroy ();
//  }

		#region Public Properties
		/// <summary>
		/// The state of the SyncManager (lame comment, duh!)
		/// </summary>
		public static SyncState State
		{
			get {
				return state;
			}
		}
		#endregion // Public Properties

		#region Private Methods
		private static void SetState (SyncState newState)
		{
			state = newState;
			if (syncUI != null) {
				// Notify the event handlers
				try {
					syncUI.SyncStateChanged (state);
				} catch {}
			}
		}

		/// <summary>
		/// Read the preferences and load the specified SyncServiceAddin to
		/// perform synchronization.
		/// </summary>
		private static SyncServiceAddin GetConfiguredSyncService ()
		{
			SyncServiceAddin addin = null;

			string syncServiceId =
			        Preferences.Get (Preferences.SYNC_SELECTED_SERVICE_ADDIN) as String;
			if (syncServiceId != null)
				addin = GetSyncServiceAddin (syncServiceId);

			return addin;
		}

		/// <summary>
		/// Return the specified SyncServiceAddin
		/// </summary>
		private static SyncServiceAddin GetSyncServiceAddin (string syncServiceId)
		{
			SyncServiceAddin anAddin = null;

			SyncServiceAddin [] addins = Tomboy.DefaultNoteManager.AddinManager.GetSyncServiceAddins ();
			foreach (SyncServiceAddin addin in addins) {
				if (addin.Id.CompareTo (syncServiceId) == 0) {
					anAddin = addin;
					break;
				}
			}

			return anAddin;
		}
		#endregion // Private Methods
	}

	public class NoteUpdate
	{
		public string XmlContent;//string.Empty if deleted?
		public string Title;
		public string UUID; //needed?
		public int LatestRevision;

		public NoteUpdate (string xmlContent, string title, string uuid, int latestRevision)
		{
			XmlContent = xmlContent;
			Title = title;
			UUID = uuid;
			LatestRevision = latestRevision;

			// TODO: Clean this up (and remove title parameter?)
			if (xmlContent != null && xmlContent.Length > 0) {
				XmlTextReader xml = new XmlTextReader (new StringReader (XmlContent));
				xml.Namespaces = false;

				while (xml.Read ()) {
					switch (xml.NodeType) {
					case XmlNodeType.Element:
						switch (xml.Name) {
						case "title":
							Title = xml.ReadString ();
							break;
						}
						break;
					}
				}
			}
		}

		public bool BasicallyEqualTo (Note existingNote)
		{
//			Logger.Debug ("Comparing NoteData for '{0}'", existingNote.Title);
			// NOTE: This would be so much easier if NoteUpdate
			//       was not just a container for a big XML string
			NoteData updateData = null;
			using (var xml = new XmlTextReader (new StringReader (XmlContent))) {
				xml.Namespaces = false;
				updateData = NoteArchiver.Instance.Read (xml, UUID);
			}

			// NOTE: Mostly a hack to ignore missing version attributes
			string existingInnerContent = GetInnerContent (existingNote.Data.Text);
			string updateInnerContent = GetInnerContent (updateData.Text);

//			Logger.Debug ("existingNote.Data.Title: {0}", existingNote.Data.Title);
//			Logger.Debug ("updateData.Title: {0}", updateData.Title);
//
//			Logger.Debug ("existingInnerContent: {0}", existingInnerContent);
//			Logger.Debug ("updateInnerContent: {0}", updateInnerContent);

			return existingInnerContent == updateInnerContent &&
				existingNote.Data.Title == updateData.Title &&
				CompareTags (existingNote.Data.Tags, updateData.Tags);
				// TODO: Compare open-on-startup, pinned
		}

		private string GetInnerContent (string fullContentElement)
		{
			const string noteContentRegex =
				@"^<note-content([^>]+version=""(?<contentVersion>[^""]*)"")?[^>]*((/>)|(>(?<innerContent>.*)</note-content>))$";
			Match m = Regex.Match (fullContentElement, noteContentRegex, RegexOptions.Singleline);
			Group contentGroup = m.Groups ["innerContent"];
			if (!contentGroup.Success)
				return null;
			return contentGroup.Value;
		}

		private bool CompareTags (Dictionary<string, Tag> set1, Dictionary<string, Tag> set2)
		{
			if (set1.Count != set2.Count)
				return false;
			foreach (string key in set1.Keys)
				if (!set2.ContainsKey (key))
					return false;
			return true;
		}
	}

	public class SyncLockInfo
	{
		/// <summary>
		/// A string to identify which client currently has the
		/// lock open.  Not guaranteed to be unique.
		/// </summary>
		public string ClientId;

		/// <summary>
		/// Unique ID for the sync transaction associated with the lock.
		/// </summary>
		public string TransactionId;

		/// <summary>
		/// Indicates how many times the client has renewed the lock.
		/// Subsequent clients should watch this (along with the LockOwner) to
		/// determine whether the currently synchronizing client has becomeeither
		/// inactive.  Clients currently synchronizing should update the lock
		/// file before the duration expires to prevent other clients from
		/// overtaking the lock.
		/// </summary>
		public int RenewCount;

		/// <summary>
		/// A TimeSpan to indicate how long the current synchronization will
		/// take.  If the current synchronization will take longer than this,
		/// the client synchronizing should update the lock file to indicate
		/// this.
		/// </summary>
		public TimeSpan Duration;

		/// <summary>
		/// Specifies the current revision that this lock is for.  The client
		/// that lays the lock file down should specify which revision they're
		/// creating.  Clients needing to perform cleanup may want to know which
		/// revision files to clean up by reading the value of this.
		/// </summary>
		public int Revision;

		public SyncLockInfo ()
		{
			ClientId = Preferences.Get (Preferences.SYNC_CLIENT_ID) as string;
			TransactionId = System.Guid.NewGuid ().ToString ();
			RenewCount = 0;
			Duration = new TimeSpan (0, 2, 0); // default of 2 minutes
			Revision = 0;
		}

		/// <summary>
		/// The point of this property is to let clients quickly know if a sync
		/// lock has changed.
		/// </summary>
		public string HashString
		{
			get {
				return string.Format ("{0}-{1}-{2}-{3}-{4}",
				TransactionId, ClientId, RenewCount,
				Duration.ToString (), Revision);
			}
		}
	}

	public interface SyncServer
	{
		bool BeginSyncTransaction ();
		bool CommitSyncTransaction ();
		bool CancelSyncTransaction ();
		IList<string> GetAllNoteUUIDs ();
		IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision);
		void DeleteNotes (IList<string> deletedNoteUUIDs);
		void UploadNotes (IList<Note> notes);
		int LatestRevision { get; } // NOTE: Only reliable during a transaction
		SyncLockInfo CurrentSyncLock { get; }
		string Id { get; }
		bool UpdatesAvailableSince (int revision);
	}

	public interface SyncClient
	{
		int LastSynchronizedRevision { get; set; }
		DateTime LastSyncDate { get; set; }
		int GetRevision (Note note);
		void SetRevision (Note note, int revision);
		IDictionary<string, string> DeletedNoteTitles { get; }
		void Reset ();
		string AssociatedServerId { get; set; }
	}

	public class TomboySyncException : ApplicationException
	{
		public TomboySyncException (string message) :
		base (message) {}

		public TomboySyncException (string message, Exception innerException) :
		base (message, innerException) {}
	}
}
