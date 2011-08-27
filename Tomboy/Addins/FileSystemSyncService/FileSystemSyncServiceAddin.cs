using System;
using System.IO;

using Gtk;
using Mono.Unix;

using Tomboy;

namespace Tomboy.Sync
{
	public class FileSystemSyncServiceAddin : SyncServiceAddin
	{
		// TODO: Extract most of the code here and build GenericSyncServiceAddin
		// that supports a field, a username, and password.  This could be useful
		// in quickly building SshSyncServiceAddin, FtpSyncServiceAddin, etc.

		private FileChooserButton pathButton;
		private string path;
		private bool initialized = false;

		/// <summary>
		/// Called as soon as Tomboy needs to do anything with the service
		/// </summary>
		public override void Initialize ()
		{
			initialized = true;
		}

		public override void Shutdown ()
		{
			// Do nothing for now
		}

		public override bool Initialized {
			get {
				return initialized;
			}
		}



		/// <summary>
		/// Creates a SyncServer instance that the SyncManager can use to
		/// synchronize with this service.  This method is called during
		/// every synchronization process.  If the same SyncServer object
		/// is returned here, it should be reset as if it were new.
		/// </summary>
		public override SyncServer CreateSyncServer ()
		{
			SyncServer server = null;

			string syncPath;
			if (GetConfigSettings (out syncPath)) {
				path = syncPath;
				if (Directory.Exists (path) == false) {
					try {
						Directory.CreateDirectory (path);
					} catch (Exception e) {
						throw new Exception ("Could not create \"" + path + "\": " + e.Message);
					}
				}

				server = new FileSystemSyncServer (path);
			} else {
				throw new InvalidOperationException ("FileSystemSyncServiceAddin.CreateSyncServer () called without being configured");
			}

			return server;
		}

		public override void PostSyncCleanup ()
		{
			// Nothing to do
		}

		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.  This
		/// will be used in the Synchronization Preferences.  Preferences should
		/// not automatically be saved by a GConf Property Editor.  Preferences
		/// should be saved when SaveConfiguration () is called.
		/// </summary>
		public override Gtk.Widget CreatePreferencesControl (EventHandler requiredPrefChanged)
		{
			Gtk.Table table = new Gtk.Table (1, 2, false);
			table.RowSpacing = 5;
			table.ColumnSpacing = 10;

			// Read settings out of gconf
			string syncPath;
			if (GetConfigSettings (out syncPath) == false)
				syncPath = string.Empty;

			Label l = new Label (Catalog.GetString ("_Folder Path:"));
			l.Xalign = 1;
			table.Attach (l, 0, 1, 0, 1,
			              Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);

			pathButton = new FileChooserButton (Catalog.GetString ("Select Synchronization Folder..."),
			                                    FileChooserAction.SelectFolder);
			pathButton.CurrentFolderChanged += requiredPrefChanged;
			l.MnemonicWidget = pathButton;
			pathButton.SetFilename (syncPath);

			table.Attach (pathButton, 1, 2, 0, 1,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);

			table.ShowAll ();
			return table;
		}

		/// <summary>
		/// The Addin should verify and check the connection to the service
		/// when this is called.  If verification and connection is successful,
		/// the addin should save the configuration and return true.
		/// </summary>
		public override bool SaveConfiguration ()
		{
			string syncPath = pathButton.Filename;

			if (syncPath == string.Empty) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("The path is empty");
				throw new TomboySyncException (Catalog.GetString ("Folder path field is empty."));
			}

			// Attempt to create the path and fail if we can't
			if (Directory.Exists (syncPath) == false) {
				try {
					Directory.CreateDirectory (syncPath);
				} catch (Exception e) {
					Logger.Debug ("Could not create \"{0}\": {1}", path, e.Message);
					throw new TomboySyncException (Catalog.GetString ("Specified folder path does not exist, " +
					                               "and Tomboy was unable to create it."));
				}
			} else {
				// Test creating/writing/deleting a file
				// FIXME: Should throw TomboySyncException once string changes are OK again
				string testPathBase = Path.Combine (syncPath, "test");
				string testPath = testPathBase;
				int count = 0;

				// Get unique new file name
				while (File.Exists (testPath))
					testPath = testPathBase + (++count).ToString ();

				// Test ability to create and write
				string testLine = "Testing write capabilities.";
				using (FileStream fs = File.Create (testPath)) {
					StreamWriter writer = new StreamWriter (fs);
					writer.WriteLine (testLine);
				}

				// Test ability to read
				bool testFileFound = false;
				foreach (string filePath in Directory.GetFiles (syncPath))
				if (filePath == testPath) {
					testFileFound = true;
					break;
				}
				if (!testFileFound)
					; // TODO: Throw TomboySyncException
				using (StreamReader reader = new StreamReader (testPath)) {
					if (reader.ReadLine () != testLine)
						; // TODO: Throw TomboySyncException
				}

				// Test ability to delete
				File.Delete (testPath);
			}

			path = syncPath;

			// TODO: Try to create and delete a file.  If it fails, this should fail
			Preferences.Set (Preferences.SYNC_LOCAL_PATH, path);

			return true;
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		public override void ResetConfiguration ()
		{
			Preferences.Set (Preferences.SYNC_LOCAL_PATH, string.Empty);
		}

		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string syncPath = Preferences.Get (Preferences.SYNC_LOCAL_PATH) as String;

				if (syncPath != null && syncPath != string.Empty) {
					return true;
				}

				return false;
			}
		}

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get {
				return Mono.Unix.Catalog.GetString ("Local Folder");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "local";
			}
		}

		/// <summary>
		/// Returns true if the addin has all the supporting libraries installed
		/// on the machine or false if the proper environment is not available.
		/// If false, the preferences dialog will still call
		/// CreatePreferencesControl () when the service is selected.  It's up
		/// to the addin to present the user with what they should install/do so
		/// IsSupported will be true.
		/// </summary>
		public override bool IsSupported
		{
			get {
				return true;
			}
		}

		#region Private Methods
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string syncPath)
		{
			syncPath = Preferences.Get (Preferences.SYNC_LOCAL_PATH) as String;

			if (syncPath != null && syncPath != string.Empty) {
				return true;
			}

			return false;
		}
		#endregion // Private Methods
	}
}
