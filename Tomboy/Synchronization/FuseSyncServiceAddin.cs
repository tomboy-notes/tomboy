using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace Tomboy.Sync
{
	public abstract class FuseSyncServiceAddin : SyncServiceAddin
	{
#region Private Data
		private string mountPath;		
		private InterruptableTimeout unmountTimeout;
		
		private string fuseMountExePath;
		private string fuseUnmountExePath;
		private string mountExePath;
#endregion // Private Data

#region SyncServiceAddin Overrides
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
		}

		public override SyncServer CreateSyncServer ()
		{
			SyncServer server = null;
			
			// Cancel timer
			unmountTimeout.Cancel ();
			
			// Mount if necessary
			if (IsConfigured) {
				if (!IsMounted && !MountFuse (true))
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
				return false;

			if (!VerifyConfiguration ())
				return false;
			
			// TODO: Check to see if the mount is already mounted
			bool mounted = MountFuse (false);
			
			if (mounted) {
				PostSyncCleanup ();
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
#endregion // Abstract Members
		
#region Private Methods
		private bool MountFuse (bool useStoredValues)
		{
			if (string.IsNullOrEmpty (mountPath))
				return false;
			
			if (SyncUtils.IsFuseEnabled () == false) {
				if (SyncUtils.EnableFuse () == false) {
					Logger.Debug ("User canceled or something went wrong enabling FUSE");
					return false;
				}
			}
			
			CreateMountPath ();

			Process p = new Process ();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = false;
			p.StartInfo.FileName = fuseMountExePath;
			p.StartInfo.Arguments = GetFuseMountExeArgs (mountPath, useStoredValues);
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			p.WaitForExit ();	
			
			// TODO: Handle password for sshfs
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error calling " + fuseMountExePath);
				return false;
			}
			return true;
		}
		
		private void SetUpMountPath ()
		{
			string notesPath = Tomboy.DefaultNoteManager.NoteDirectoryPath;
			mountPath = Path.Combine (notesPath, "sync-" + Id); // TODO: Best mount path name?
		}
		
		private void CreateMountPath ()
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
			}
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
					if (outputLine.StartsWith (FuseMountExeName) &&
					    outputLine.IndexOf (string.Format ("on {0} ", mountPath)) > -1)
						return true;
				
				return false;
			}
		}
#endregion // Private Methods
	}
}
