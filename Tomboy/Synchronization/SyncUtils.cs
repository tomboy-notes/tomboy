using System;
using System.Diagnostics;
using System.IO;
using Mono.Unix;

namespace Tomboy.Sync
{
	public class SyncUtils
	{
		/// <summary>
		/// Tool used to execute a process as root user by prompting the user
		/// for the root password.
		/// </summary>
		static string guisuTool = null;
		
		/// <summary>
		/// Tool used to query what kernel modules are enabled
		/// </summary>
		static string lsmodTool = null;
		
		/// <summary>
		/// Tool used to enable a kernel module
		/// </summary>
		static string modprobeTool = null;
		
		/// <summary>
		/// Tool used to echo text
		/// </summary>
		static string echoTool = null;
		
		/// <summary>
		/// Path to the file to append kernel modules that should be enabled
		/// </summary>
		static string bootLocalFile = null;
		
		static bool toolsFound = false;
		
		static SyncUtils ()
		{
			toolsFound = SetUpTools (); 
			if (toolsFound) {
				Logger.Debug ("Successfully found system tools");
			} else {
				Logger.Debug ("Failed to find necessary system tools for SyncUtils");
			}
		}
		
		public static bool ToolsValid
		{
			get { return toolsFound; }
		}
		
		private static bool SetUpTools ()
		{
			lsmodTool = FindFirstExecutableInPath ("lsmod");
			if (lsmodTool == null)
				Logger.Warn ("lsmod not found");
			
			modprobeTool = FindFirstExecutableInPath ("modprobe");
			if (modprobeTool == null)
				Logger.Warn ("modprobe not found");
			
			guisuTool = FindFirstExecutableInPath (
			                                       "gnomesu",
			                                       "gksu",
			                                       "gksudo",
			                                       "kdesu");
			if (guisuTool != null)
				Logger.Debug (string.Format ("Using '{0}' as GUI 'su' tool", guisuTool));
			else
				Logger.Warn ("No GUI 'su' tool found");
			
			echoTool = FindFirstExecutableInPath ("echo");
			if (echoTool == null)
				Logger.Warn ("echo tool not found");
			
			// bootLocalFile, SUSE: /etc/init.d/boot.local
			// bootLocalFile, Ubuntu: /etc/modules
			if (File.Exists ("/etc/init.d/boot.local") == true)
				bootLocalFile = "/etc/init.d/boot.local";
			else if (File.Exists ("/etc/modules") == true)
				bootLocalFile = "/etc/modules";
			else
				Logger.Warn ("No bootLocalFile found");
			
			if (lsmodTool == null || modprobeTool == null
					|| guisuTool == null || echoTool == null
					|| bootLocalFile == null)
				return false;
			
			return true;
		}
		
		/// <summary>
		/// Throws an exception if the tools required by this utility class are
		/// not set up properly
		/// </summary>
		private static void CheckToolsValid ()
		{
			if (toolsFound == false)
				throw new InvalidOperationException ("SyncUtils cannot be used because one or more of the required system tools could not be found.");
		} 


		/// <summary>
		/// Calls lsmod to check for fuse in the output
		/// </summary>
		public static bool IsFuseEnabled ()
		{
			CheckToolsValid ();
			Process p = new Process ();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = true;
			// TODO: Is calling /sbin/lsmod safe enough?  i.e., can we be guaranteed it's gonna be there?
			p.StartInfo.FileName = lsmodTool;
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			string output = p.StandardOutput.ReadToEnd ();
			p.WaitForExit ();
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error calling lsmod in SyncUtils.IsFuseEnabled ()");
				return false;
			}
			
			if (output.IndexOf ("fuse") == -1) {
				Logger.Debug ("Could not find fuse in lsmod output");
				return false;
			}
			
			return true;
		}
		
		/// <summary>
		/// Enable fuse.  This requires root access.
		/// </summary>
		public static bool EnableFuse ()
		{
			if (IsFuseEnabled () == true)
				return true; // nothing to do
			
			// Prompt the user first about enabling fuse
			HIGMessageDialog dialog = 
				new HIGMessageDialog (null,
						      Gtk.DialogFlags.Modal,
						      Gtk.MessageType.Question,
						      Gtk.ButtonsType.YesNo,
						      Catalog.GetString ("Enable FUSE?"),
						      Catalog.GetString (
					"The synchronization you've chosen requires the FUSE module to be loaded.\n\n" +
					"To avoid getting this prompt in the future, you should load FUSE at startup.  " +
					"Add \"modprobe fuse\" to /etc/init.d/boot.local or \"fuse\" to /etc/modules."));
			int response = dialog.Run ();
			dialog.Destroy ();
			if (response == (int) Gtk.ResponseType.Yes) {
				// "modprobe fuse"
				Process p = new Process ();
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.FileName = guisuTool;
				p.StartInfo.Arguments = string.Format ("{0} fuse", modprobeTool);
				p.StartInfo.CreateNoWindow = true;
				p.Start ();
				p.WaitForExit ();
				
				if (p.ExitCode != 0) {
					Logger.Warn ("Couldn't enable fuse");
					
					// TODO: Figure out a way to let the user know that they don't have FUSE installed on their machine
					return false;
				}
				
				// "echo fuse >> /etc/modules"
				/*
				// Punting for now.  Can't seem to get this to work.
				// When using /etc/init.d/boot.local, you should add "modprobe fuse",
				// and not what has been coded below.
				p = new Process ();
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.FileName = guisuTool;
				p.StartInfo.Arguments = string.Format ("\"{0} fuse >> {1}\"",
						echoTool, bootLocalFile);
				p.StartInfo.CreateNoWindow = true;
				p.Start ();
				p.WaitForExit ();
				if (p.ExitCode != 0) {
					Logger.Warn ("Could not enable FUSE persistently.  User will have to be prompted again during their next login session.");
				}
				*/
				return true;
			}

			return false;
		}
		
		/// <summary>
		/// Search in $PATH for the given executables.  Return full executable path
		/// of first executable found.  If none found, return null.
		/// </summary>
		public static string FindFirstExecutableInPath (params string[] executableNames)
		{
			foreach (string executableName in executableNames) {
				string pathVar = System.Environment.GetEnvironmentVariable ("PATH");
				foreach (string path in pathVar.Split (Path.PathSeparator)) {
					string testExecutablePath = Path.Combine (path, executableName);
					if (File.Exists (testExecutablePath))
						return testExecutablePath;
				}
				Logger.Debug ("Unable to locate '" + executableName + "' in your PATH");
			}
			// TODO: Any reason to extend search outside of $PATH?
			return null;
		}
	}
}
