using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
		/// Common places where tools might be found, in case
		/// $PATH is not set up as expected.
		/// </summary>
		static string[] commonPaths = {"/sbin", "/bin", "/usr/bin"};

		static bool toolsFound = false;

		static SyncUtils ()
		{
			toolsFound = SetUpTools ();
			if (toolsFound) {
				Logger.Debug ("Successfully found all system tools");
			} else {
				Logger.Debug ("Failed to find all system tools for SyncUtils");
			}
		}

		/// <summary>
		/// Indicates that all tools needed by this class were found.
		/// Not all methods require all tools, though.
		/// </summary>
		public static bool ToolsValid
		{
			get {
				return toolsFound;
			}
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

			if (lsmodTool == null || modprobeTool == null
			                || guisuTool == null)
				return false;

			return true;
		}


		/// <summary>
		/// Checks /proc/filesystems to check for fuse in the output
		/// </summary>
		public static bool IsFuseEnabled ()
		{
			try {
				string fsFileName = "/proc/filesystems";
				if (File.Exists (fsFileName)) {
					string fsOutput = new System.IO.StreamReader (fsFileName).ReadToEnd ();
					Regex fuseEx = new Regex ("\\s+fuse\\s+");
					return fuseEx.Match (fsOutput).Success;
				}
			} catch { }
			return false;
		}

		/// <summary>
		/// Enable fuse.  This requires root access.
		/// </summary>
		public static bool EnableFuse ()
		{
			if (IsFuseEnabled () == true)
				return true; // nothing to do

			if (string.IsNullOrEmpty (guisuTool) ||
			                string.IsNullOrEmpty (modprobeTool)) {
				Logger.Warn ("Couldn't enable fuse; missing either GUI 'su' tool or modprobe");

				// Let the user know that FUSE could not be enabled
				HIGMessageDialog cannotEnableDlg =
				        new HIGMessageDialog (null,
				                              Gtk.DialogFlags.Modal,
				                              Gtk.MessageType.Error,
				                              Gtk.ButtonsType.Ok,
				                              Catalog.GetString ("Could not enable FUSE"),
				                              Catalog.GetString ("The FUSE module could not be loaded. " +
				                                                 "Please check that it is installed properly " +
				                                                 "and try again."));

				cannotEnableDlg.Run ();
				cannotEnableDlg.Destroy ();
				return false;
			}

			// Prompt the user first about enabling fuse
			HIGMessageDialog dialog =
			        new HIGMessageDialog (null,
			                              Gtk.DialogFlags.Modal,
			                              Gtk.MessageType.Question,
			                              Gtk.ButtonsType.YesNo,
			                              Catalog.GetString ("Enable FUSE?"),
			                              Catalog.GetString (
			                                      "The synchronization option you have chosen requires the FUSE module to be loaded.\n\n" +
			                                      "To avoid getting this prompt in the future, you should load FUSE at startup.  " +
			                                      "Add \"modprobe fuse\" to /etc/init.d/boot.local or \"fuse\" to /etc/modules.\n\n" +
			                                      "Do you want to load the FUSE module now?"));
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

					// Let the user know that they don't have FUSE installed on their machine
					HIGMessageDialog failedDlg =
					        new HIGMessageDialog (null,
					                              Gtk.DialogFlags.Modal,
					                              Gtk.MessageType.Error,
					                              Gtk.ButtonsType.Ok,
					                              Catalog.GetString ("Could not enable FUSE"),
					                              Catalog.GetString ("The FUSE module could not be loaded. " +
					                                                 "Please check that it is installed properly " +
					                                                 "and try again."));
					failedDlg.Run ();
					failedDlg.Destroy ();
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
		/// Search in $PATH and a few other common locations for the
		/// given executables.  Return full executable path
		/// of first executable found.  If none found, return null.
		/// </summary>
		public static string FindFirstExecutableInPath (params string[] executableNames)
		{
			foreach (string executableName in executableNames) {
				string pathVar = System.Environment.GetEnvironmentVariable ("PATH");
				List<string> paths = new List<string> (pathVar.Split (Path.PathSeparator));

				foreach (string commonPath in commonPaths)
				if (!paths.Contains (commonPath))
					paths.Add (commonPath);

				foreach (string path in paths) {
					string testExecutablePath = Path.Combine (path, executableName);
					if (File.Exists (testExecutablePath))
						return testExecutablePath;
				}
				Logger.Debug ("Unable to locate '" + executableName + "' in your PATH");
			}

			return null;
		}
	}
}
