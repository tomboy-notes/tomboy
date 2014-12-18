using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using Mono.Unix;

namespace Tomboy.Sync
{
	public abstract class FuseSyncServiceAddin : SyncServiceAddin
	{
		#region Private Data
		const int defaultMountTimeoutMs = 10000;
		private string mountPath;
		private InterruptableTimeout unmountTimeout;

		private string fuseMountExePath;
		private string fuseUnmountExePath;
		private string mountExePath;
		private bool initialized = false;
		#endregion // Private Data

		#region SyncServiceAddin Overrides
		public override void Shutdown ()
		{
			// TODO: Consider replacing TomboyExitHandler with this!
		}

		public override bool Initialized {
			get {
				return initialized;
			}
		}

		public override void Initialize ()
		{
			// TODO: When/how best to handle this?  Okay to install wdfs while Tomboy is running?  When set up mount path, timer, etc, then?
			if (IsSupported) {
				// Determine mount path, etc
				SetUpMountPath ();

				// Setup unmount timer
				unmountTimeout = new InterruptableTimeout ();
				unmountTimeout.Timeout += UnmountTimeout;
				Tomboy.ExitingEvent += TomboyExitHandler;
			}
			initialized = true;
		}

		public override SyncServer CreateSyncServer ()
		{
			SyncServer server = null;

			// Cancel timer
			unmountTimeout.Cancel ();

			// Mount if necessary
			if (IsConfigured) {
				if (!IsMounted && !MountFuse (true)) // MountFuse may throw TomboySyncException!
					throw new Exception ("Could not mount " + mountPath);
				server = new FileSystemSyncServer (mountPath);
			} else
				throw new InvalidOperationException ("CreateSyncServer called without being configured");

			// Return FileSystemSyncServer
			return server;
		}

		public override void PostSyncCleanup ()
		{
			// Set unmount timeout to 5 minutes or something
			unmountTimeout.Reset (1000 * 60 * 5);
		}

		public override bool IsSupported
		{
			get
			{
				// Check for fusermount and child-specific executable
				fuseMountExePath = SyncUtils.FindFirstExecutableInPath (FuseMountExeName);
				fuseUnmountExePath = SyncUtils.FindFirstExecutableInPath ("fusermount");
				mountExePath = SyncUtils.FindFirstExecutableInPath ("mount");

				return !string.IsNullOrEmpty (fuseMountExePath) &&
				       !string.IsNullOrEmpty (fuseUnmountExePath) &&
				       !string.IsNullOrEmpty (mountExePath);
			}
		}

		public override bool SaveConfiguration ()
		{
			// TODO: When/how best to handle this?
			if (!IsSupported)
				throw new TomboySyncException (string.Format (Catalog.GetString ("This synchronization addin is not supported on your computer. " +
				                               "Please make sure you have FUSE and {0} correctly installed and configured"),
				                               FuseMountExeName));

			if (!VerifyConfiguration ())
				return false;

			// TODO: Check to see if the mount is already mounted
			bool mounted = MountFuse (false);

			if (mounted) {
				try {
					// Test creating/writing/deleting a file
					string testPathBase = Path.Combine (mountPath, "test");
					string testPath = testPathBase;
					int count = 0;

					// Get unique new file name
					while (File.Exists (testPath))
						testPath = testPathBase + (++count).ToString ();

					// Test ability to create and write
					string testLine = "Testing write capabilities.";
					using (StreamWriter writer = new StreamWriter (File.Create (testPath))) {
						writer.WriteLine (testLine);
					}

					// Test ability to read
					bool testFileFound = false;
					foreach (string filePath in Directory.GetFiles (mountPath))
						if (filePath == testPath) {
							testFileFound = true;
							break;
					}
					if (!testFileFound)
						throw new TomboySyncException (Catalog.GetString ("Could not read testfile."));
					using (StreamReader reader = new StreamReader (testPath)) {
						if (reader.ReadLine () != testLine)
							throw new TomboySyncException (Catalog.GetString ("Write test failed."));
					}

					// Test ability to delete
					File.Delete (testPath);
				} finally {
					// Clean up
					PostSyncCleanup ();
				}

				// Finish save process
				SaveConfigurationValues ();
			}

			return mounted;
		}

		public override void ResetConfiguration ()
		{
			// Unmount immediately, then reset configuration
			UnmountTimeout (this, null);
			ResetConfigurationValues ();
		}
		#endregion // SyncServiceAddin Overrides

		#region Abstract Members
		protected abstract bool VerifyConfiguration ();

		protected abstract void SaveConfigurationValues ();

		protected abstract void ResetConfigurationValues ();

		protected abstract string FuseMountExeName { get; }

		protected abstract string GetFuseMountExeArgs (string mountPath, bool fromStoredValues);
		
		protected abstract string GetFuseMountExeArgsForDisplay (string mountPath, bool fromStoredValues);
		#endregion // Abstract Members

		#region Public Virtual Members
		public virtual string FuseMountTimeoutError
		{
			get
			{
				return Catalog.GetString ("Timeout connecting to server.");
			}
		}

		public virtual string FuseMountDirectoryError
		{
			get
			{
				return Catalog.GetString ("Error connecting to server.");
			}
		}
		#endregion

		#region Private Methods
		private bool MountFuse (bool useStoredValues)
		{
			if (string.IsNullOrEmpty (mountPath))
				return false;

			if (SyncUtils.IsFuseEnabled () == false) {
				if (SyncUtils.EnableFuse () == false) {
					Logger.Debug ("User canceled or something went wrong enabling FUSE");
					throw new TomboySyncException (Catalog.GetString ("FUSE could not be enabled."));
				}
			}

			PrepareMountPath ();

			Process p = new Process ();

			// Need to redirect stderr for displaying errors to user,
			// but we can't use stdout and by not redirecting it, it
			// should appear in the console Tomboy is started from.
			p.StartInfo.RedirectStandardOutput = false;
			p.StartInfo.RedirectStandardError = true;

			p.StartInfo.UseShellExecute = false;
			p.StartInfo.FileName = fuseMountExePath;
			p.StartInfo.Arguments = GetFuseMountExeArgs (mountPath, useStoredValues);
			Logger.Debug (string.Format (
			                             "Mounting sync path with this command: {0} {1}",
			                             p.StartInfo.FileName,
			                             // Args could include password, so may need to mask
			                             GetFuseMountExeArgsForDisplay (mountPath, useStoredValues)));
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			int timeoutMs = GetTimeoutMs ();
			bool exited = p.WaitForExit (timeoutMs);

			if (!exited) {
				UnmountTimeout (null, null); // TODO: This is awfully ugly
				Logger.Debug (string.Format ("Error calling {0}: timed out after {1} seconds",
				                             fuseMountExePath, timeoutMs / 1000));
				throw new TomboySyncException (FuseMountTimeoutError);
			} else if (p.ExitCode == 1) {
				UnmountTimeout (null, null); // TODO: This is awfully ugly
				Logger.Debug ("Error calling " + fuseMountExePath);
				throw new TomboySyncException (Catalog.GetString ("An error occurred while connecting to the specified server:") +
				                               "\n\n" + p.StandardError.ReadToEnd ());
			}

			// For wdfs, incorrect user credentials will cause the mountPath to
			// be messed up, and not recognized as a directory.  This is the only
			// way I can find to report that the username/password may be incorrect (for wdfs).
			if (!Directory.Exists (mountPath)) {
				Logger.Debug ("FUSE mount call succeeded, but mount path does not exist. " +
				              "This may be an indication that incorrect user credentials were " +
				              "provided, but it may also represent any number of error states " +
				              "not properly handled by the FUSE filesystem.");
				// Even though the mountPath is screwed up, it is still (apparently)
				// a valid FUSE mount and must be unmounted.
				UnmountTimeout (null, null); // TODO: This is awfully ugly
				throw new TomboySyncException (FuseMountDirectoryError);
			}

			return true;
		}
		
		private int GetTimeoutMs ()
		{
			try {
				return (int) Preferences.Get ("/apps/tomboy/sync_fuse_mount_timeout_ms");
			} catch {
				try {
					Preferences.Set ("/apps/tomboy/sync_fuse_mount_timeout_ms", defaultMountTimeoutMs);
				} catch {}
				return defaultMountTimeoutMs;
			}
		}

		private void SetUpMountPath ()
		{
			string notesPath = Services.NativeApplication.CacheDirectory;
			mountPath = Path.Combine (notesPath, "sync-" + Id); // TODO: Best mount path name?
		}

		private void PrepareMountPath ()
		{
			if (Directory.Exists (mountPath) == false) {
				try {
					Directory.CreateDirectory (mountPath);
				} catch (Exception e) {
					throw new Exception (
					        string.Format (
					                "Couldn't create \"{0}\" directory: {1}",
					                mountPath, e.Message));
				}
			} else
				// Just in case, make sure there is no
				// existing FUSE mount at mountPath.
				UnmountTimeout (null, null);
		}

		//private bool

		/// <summary>
		/// Perform clean up when Tomboy exits.
		/// </summary>
		private void TomboyExitHandler (object sender, System.EventArgs e)
		{
			// TODO: Any further checking required?  Should I just hook UnmountTimeout
			//       directly to the Tomboy.Exiting event?
			UnmountTimeout (sender, e);
		}

		private void UnmountTimeout (object sender, System.EventArgs e)
		{
			if (IsMounted)
			{
				Process p = new Process ();
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = false;
				p.StartInfo.FileName = fuseUnmountExePath;
				p.StartInfo.Arguments =
				        string.Format (
				                "-u {0}",
				                mountPath);
				p.StartInfo.CreateNoWindow = true;
				p.Start ();
				p.WaitForExit ();

				// TODO: What does this return if it was not mounted?
				if (p.ExitCode == 1) {
					Logger.Debug ("Error unmounting " + Id);
					unmountTimeout.Reset (1000 * 60 * 5); // Try again in five minutes
				}
				else {
					Logger.Debug ("Successfully unmounted " + Id);
					unmountTimeout.Cancel ();
				}
			}
		}

		/// <summary>
		/// Checks to see if the mount is actually mounted and alive
		/// </summary>
		private bool IsMounted
		{
			get
			{
				Process p = new Process ();
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.FileName = mountExePath;
				p.StartInfo.CreateNoWindow = true;
				p.Start ();
				List<string> outputLines = new List<string> ();
				string line;
				while (!p.StandardOutput.EndOfStream) {
					line = p.StandardOutput.ReadLine ();
					outputLines.Add (line);
				}
				p.WaitForExit ();

				if (p.ExitCode == 1) {
					Logger.Debug ("Error calling " + mountExePath);
					return false;
				}

				// TODO: Review this methodology...is it really the exe name, for example?
				foreach (string outputLine in outputLines)
				if ((outputLine.StartsWith (FuseMountExeName) || outputLine.Contains (" type fuse." + FuseMountExeName)) &&
				                outputLine.IndexOf (string.Format ("on {0} ", mountPath)) > -1)
					return true;

				return false;
			}
		}
		#endregion // Private Methods
	}
}
