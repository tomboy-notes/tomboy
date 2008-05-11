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
			string server, folder, username;
			int port;
			GetConfigSettings (out server, out folder, out username, out port);
			if (server == null)
				server = string.Empty;
			if (port != 22)
				server += ":" + port.ToString ();
			if (folder == null)
				folder = string.Empty;
			if (username == null)
				username = string.Empty;

			Label l = new Label (Catalog.GetString ("Se_rver:"));
			l.Xalign = 1;
			table.Attach (l, 0, 1, 0, 1);

			serverEntry = new Entry ();
			l.MnemonicWidget = serverEntry;
			serverEntry.Text = server;
			table.Attach (serverEntry, 1, 2, 0, 1);

			l = new Label (Catalog.GetString ("User_name:"));
			l.Xalign = 1;
			table.Attach (l, 0, 1, 1, 2);

			usernameEntry = new Entry ();
			l.MnemonicWidget = usernameEntry;
			usernameEntry.Text = username;
			table.Attach (usernameEntry, 1, 2, 1, 2);

			l = new Label (Catalog.GetString ("_Folder Path (optional):"));
			l.Xalign = 1;
			table.Attach (l, 0, 1, 2, 3);

			folderEntry = new Entry ();
			l.MnemonicWidget = folderEntry;
			folderEntry.Text = folder;
			table.Attach (folderEntry, 1, 2, 2, 3);

			// Text for label describing setup required for SSH sync addin to work
			string sshInfo = Catalog.GetString ("SSH synchronization requires an existing SSH key for this " +
			                                    "server and user, added to a running SSH daemon.");
			l = new Label ();
			l.UseMarkup = true;
			l.Markup = string.Format ("<span size=\"small\">{0}</span>",
			                          sshInfo);
			l.Wrap = true;

			VBox vbox = new VBox (false, 5);
			vbox.PackStart (table);
			vbox.PackStart (l);
			vbox.ShowAll ();

			return vbox;
		}

		protected override bool VerifyConfiguration ()
		{
			string server, folder, username;
			int port;

			if (!GetPrefWidgetSettings (out server, out folder, out username, out port)) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("One of url, username was empty");
				throw new TomboySyncException (Catalog.GetString ("Server or username field is empty."));
			}

			return true;
		}

		protected override void SaveConfigurationValues ()
		{
			string server, folder, username;
			int port;
			GetPrefWidgetSettings (out server, out folder, out username, out port);

			Preferences.Set ("/apps/tomboy/sync_sshfs_server", server);
			Preferences.Set ("/apps/tomboy/sync_sshfs_port", port);
			Preferences.Set ("/apps/tomboy/sync_sshfs_folder", folder);
			Preferences.Set ("/apps/tomboy/sync_sshfs_username", username);
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		protected override void ResetConfigurationValues ()
		{
			Preferences.Set ("/apps/tomboy/sync_sshfs_server", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_port", 22);
			Preferences.Set ("/apps/tomboy/sync_sshfs_folder", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_username", string.Empty);
		}

		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string server, folder, username;
				int port;
				return GetConfigSettings (out server, out folder, out username, out port);
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
			int port = 22;
			string server, folder, username;
			if (fromStoredValues)
				GetConfigSettings (out server, out folder, out username, out port);
			else
				GetPrefWidgetSettings (out server, out folder, out username, out port);
			return string.Format (
			               "-p {0} {1}@{2}:{3} {4}",
			               port,
			               username,
			               server,
			               folder,
			               mountPath);
		}
		
		protected override string GetFuseMountExeArgsForDisplay (string mountPath, bool fromStoredValues)
		{
			return GetFuseMountExeArgs (mountPath, fromStoredValues);
		}

		protected override string FuseMountExeName
		{
			get {
				return "sshfs";
			}
		}

		public override string FuseMountTimeoutError
		{
			get
			{
				return Catalog.GetString ("Timeout connecting to server. " +
				"Please ensure that your SSH key has been " +
				"added to a running SSH daemon.");
			}
		}


		#region Private Methods
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string server, out string folder, out string username, out int port)
		{
			server = Preferences.Get ("/apps/tomboy/sync_sshfs_server") as String;
			port = 22;
			try {
				port = (int)Preferences.Get ("/apps/tomboy/sync_sshfs_port");
			} catch {}
			folder = Preferences.Get ("/apps/tomboy/sync_sshfs_folder") as String;
			username = Preferences.Get ("/apps/tomboy/sync_sshfs_username") as String;

			return !string.IsNullOrEmpty (server) && !string.IsNullOrEmpty (username);
		}


		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetPrefWidgetSettings (out string server, out string folder, out string username, out int port)
		{
			port = 22;
			server = serverEntry.Text.Trim ();
			int lastColonIndex = server.LastIndexOf(":");
			if (lastColonIndex > 0) {
				try {
					port = int.Parse (server.Substring (lastColonIndex + 1));
				} catch {}
				server = server.Substring (0, lastColonIndex);
			}
			folder = folderEntry.Text.Trim ();
			username = usernameEntry.Text.Trim ();

			return !string.IsNullOrEmpty (server)
			       && !string.IsNullOrEmpty (username);
		}
		#endregion // Private Methods
	}
}
