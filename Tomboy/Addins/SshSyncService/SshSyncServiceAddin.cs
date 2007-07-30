using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Gtk;

using Mono.Unix;

using Tomboy;

namespace Tomboy.Sync
{
	public class SshSyncServiceAddin : FuseSyncServiceAddin
	{
		Entry serverEntry;
		Entry folderEntry;
		Entry usernameEntry;
		Entry passwordEntry;
		
		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.  This
		/// will be used in the Synchronization Preferences.  Preferences should
		/// not automatically be saved by a GConf Property Editor.  Preferences
		/// should be saved when SaveConfiguration () is called.
		/// </summary>
		public override Gtk.Widget CreatePreferencesControl ()
		{
			Gtk.Table table = new Gtk.Table (3, 2, false);
			
			// Read settings out of gconf
			string server = Preferences.Get ("/apps/tomboy/sync_sshfs_server") as String;
			string folder = Preferences.Get ("/apps/tomboy/sync_sshfs_folder") as String;
			string username = Preferences.Get ("/apps/tomboy/sync_sshfs_username") as String;
			string password = Preferences.Get ("/apps/tomboy/sync_sshfs_password") as String;
			if (server == null)
				server = string.Empty;
			if (folder == null)
				folder = string.Empty;
			if (username == null)
				username = string.Empty;
			if (password == null)
				password = string.Empty;
			
			bool activeSyncService = server != string.Empty || folder != string.Empty ||
				username != string.Empty || password != string.Empty;
			
			Label l = new Label (Catalog.GetString ("Server:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 0, 1);
			
			serverEntry = new Entry ();
			serverEntry.Text = server;
			serverEntry.Show ();
			table.Attach (serverEntry, 1, 2, 0, 1);
			
			l = new Label (Catalog.GetString ("Folder (optional):"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 1, 2);
			
			folderEntry = new Entry ();
			folderEntry.Text = folder;
			folderEntry.Show ();
			table.Attach (folderEntry, 1, 2, 1, 2);
			
			l = new Label (Catalog.GetString ("Username:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 2, 3);
			
			usernameEntry = new Entry ();
			usernameEntry.Text = username;
			usernameEntry.Show ();
			table.Attach (usernameEntry, 1, 2, 2, 3);
			
			l = new Label (Catalog.GetString ("Password:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 3, 4);
			l.Sensitive = false;
			
			passwordEntry = new Entry ();
			passwordEntry.Text = password;
			passwordEntry.Visibility = false;
			passwordEntry.Show ();
			table.Attach (passwordEntry, 1, 2, 3, 4);
			passwordEntry.Sensitive = false;
			
			table.Sensitive = !activeSyncService;
			table.Show ();
			return table;
		}

		protected override bool VerifyConfiguration ()
		{
			string server, folder, username, password;
			
			if (!GetPrefWidgetSettings (out server, out folder, out username, out password)) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("One of url, username was empty");
				return false;
			}
			
			return true;
		}
		
		protected override void SaveConfigurationValues ()
		{
			string server, folder, username, password;
			GetPrefWidgetSettings (out server, out folder, out username, out password);
			
			Preferences.Set ("/apps/tomboy/sync_sshfs_server", server);
			Preferences.Set ("/apps/tomboy/sync_sshfs_folder", folder);
			Preferences.Set ("/apps/tomboy/sync_sshfs_username", username);
			// TODO: MUST FIX THIS.  DO NOT STORE CLEAR TEXT PASSWORD IN GCONF!
			Preferences.Set ("/apps/tomboy/sync_sshfs_password", password);
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		protected override void ResetConfigurationValues ()
		{
			Preferences.Set ("/apps/tomboy/sync_sshfs_server", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_folder", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_username", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_password", string.Empty);
		}
		
		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string server, folder, username, password;				
				return GetConfigSettings (out server, out folder, out username, out password);
			}
		}
		
		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get {
				return Mono.Unix.Catalog.GetString ("SSH (sshfs FUSE)");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "sshfs";
			}
		}
		
		protected override string GetFuseMountExeArgs (string mountPath, bool fromStoredValues)
		{
			string server, folder, username, password;
			if (fromStoredValues)
				GetConfigSettings (out server, out folder, out username, out password);
			else
				GetPrefWidgetSettings (out server, out folder, out username, out password);
			return string.Format (
					"{0}@{1}:{2} {3}",
					username,
					server,
				        folder,
					mountPath);
		}

		protected override string FuseMountExeName {
			get { return "sshfs"; }
		}
		
		#region Private Methods
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string server, out string folder, out string username, out string password)
		{
			server = Preferences.Get ("/apps/tomboy/sync_sshfs_server") as String;
			folder = Preferences.Get ("/apps/tomboy/sync_sshfs_folder") as String;
			username = Preferences.Get ("/apps/tomboy/sync_sshfs_username") as String;
			password = Preferences.Get ("/apps/tomboy/sync_sshfs_password") as String;
			
			return !string.IsNullOrEmpty (server) && !string.IsNullOrEmpty (username);
		}

		
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetPrefWidgetSettings (out string server, out string folder, out string username, out string password)
		{
			server = serverEntry.Text.Trim ();
			folder = folderEntry.Text.Trim ();
			username = usernameEntry.Text.Trim ();
			password = passwordEntry.Text.Trim ();
				
			return !string.IsNullOrEmpty (server)
					&& !string.IsNullOrEmpty (username);
		}
		#endregion // Private Methods
	}
}
