using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Gtk;

using Mono.Unix;

using Tomboy;
using Gnome.Keyring;

namespace Tomboy.Sync
{
	public class WebDavSyncServiceAddin : FuseSyncServiceAddin
	{
		private Entry urlEntry;
		private Entry usernameEntry;
		private Entry passwordEntry;

		private const string keyring_item_name = "Tomboy sync WebDAV account";
		private static Hashtable request_attributes = new Hashtable();

		static WebDavSyncServiceAddin ()
		{
			request_attributes ["name"] = keyring_item_name;
		}

		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.  This
		/// will be used in the Synchronization Preferences.  Preferences should
		/// not automatically be saved by a GConf Property Editor.  Preferences
		/// should be saved when SaveConfiguration () is called.
		/// </summary>
		public override Gtk.Widget CreatePreferencesControl (EventHandler requiredPrefChanged)
		{
			Gtk.Table table = new Gtk.Table (3, 2, false);
			table.RowSpacing = 5;
			table.ColumnSpacing = 10;

			// Read settings out of gconf
			string url, username, password;
			GetConfigSettings (out url, out username, out password);

			if (url == null)
				url = string.Empty;
			if (username == null)
				username = string.Empty;
			if (password == null)
				password = string.Empty;

			urlEntry = new Entry ();
			urlEntry.Text = url;
			urlEntry.Changed += requiredPrefChanged;
			AddRow (table, urlEntry, Catalog.GetString ("_URL:"), 0);

			usernameEntry = new Entry ();
			usernameEntry.Text = username;
			usernameEntry.Changed += requiredPrefChanged;
			AddRow (table, usernameEntry, Catalog.GetString ("User_name:"), 1);

			passwordEntry = new Entry ();
			passwordEntry.Text = password;
			passwordEntry.Visibility = false;
			passwordEntry.Changed += requiredPrefChanged;
			AddRow (table, passwordEntry, Catalog.GetString ("_Password:"), 2);

			table.ShowAll ();
			return table;
		}

		protected override bool VerifyConfiguration ()
		{
			string url, username, password;

			if (!GetPrefWidgetSettings (out url, out username, out password)) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("One of url, username, or password was empty");
				throw new TomboySyncException (Catalog.GetString ("URL, username, or password field is empty."));
			}

			return true;
		}

		protected override void SaveConfigurationValues ()
		{
			string url, username, password;
			GetPrefWidgetSettings (out url, out username, out password);

			SaveConfigSettings (url, username, password);
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		protected override void ResetConfigurationValues ()
		{
			SaveConfigSettings (string.Empty, string.Empty, string.Empty);

			// TODO: Unmount the FUSE mount!
		}

		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string url, username, password;
				return GetConfigSettings (out url, out username, out password);
			}
		}
		
		/// <summary>
		/// Returns true if required settings are non-empty in the preferences widget
		/// </summary>
		public override bool AreSettingsValid
		{
			get {
				string url, username, password;
				return GetPrefWidgetSettings (out url, out username, out password);
			}
		}

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get {
				return Mono.Unix.Catalog.GetString ("WebDAV");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "wdfs";
			}
		}

		protected override string GetFuseMountExeArgs (string mountPath, bool fromStoredValues)
		{
			string url, username, password;
			if (fromStoredValues)
				GetConfigSettings (out url, out username, out password);
			else
				GetPrefWidgetSettings (out url, out username, out password);
			
			return GetFuseMountExeArgs (mountPath, url, username, password, AcceptSslCert);
		}
		
		protected override string GetFuseMountExeArgsForDisplay (string mountPath, bool fromStoredValues)
		{
			string url, username, password;
			if (fromStoredValues)
				GetConfigSettings (out url, out username, out password);
			else
				GetPrefWidgetSettings (out url, out username, out password);
			
			// Mask password
			return GetFuseMountExeArgs (mountPath, url, username, "*****", AcceptSslCert);
		}
		
		private string GetFuseMountExeArgs (string mountPath, string url, string username, string password, bool acceptSsl)
		{
			return string.Format ("{0} -a {1} -u {2} -p {3} {4} -o fsname=tomboywdfs",
			                      mountPath,
			                      url,
			                      username,
			                      password,
			                      acceptSsl ? "-ac" : string.Empty);
		}

		protected override string FuseMountExeName
		{
			get {
				return "wdfs";
			}
		}

		public override string FuseMountDirectoryError
		{
			get
			{
				return Catalog.GetString ("There was an error connecting to the server.  " +
				"This may be caused by using an " +
				"incorrect user name and/or password.");
			}
		}


		#region Private Methods
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string url, out string username, out string password)
		{
			// Retrieve configuration from the GNOME Keyring
			url = null;
			username = null;
			password = null;

			try {
				foreach (ItemData result in Ring.Find (ItemType.NetworkPassword, request_attributes)) {
					if (result.Attributes ["name"] as string != keyring_item_name)
						continue;

					username = ((string) result.Attributes ["user"]).Trim ();
					url = ((string) result.Attributes ["url"]).Trim ();
					password = result.Secret.Trim ();
				}
			} catch (KeyringException ke) {
				Logger.Warn ("Getting configuration from the GNOME " +
				             "keyring failed with the following message: " +
				             ke.Message);
				// TODO: If the following fails, retrieve all but password from GConf,
				//       and prompt user for password. (some password caching would be nice, too)
				// Retrieve configuration from GConf
				//url = Preferences.Get ("/apps/tomboy/sync_wdfs_url") as String;
				//username = Preferences.Get ("/apps/tomboy/sync_wdfs_username") as String;
				//password = null; // TODO: Prompt user for password
				//throw;
			}

			return !string.IsNullOrEmpty (url)
			       && !string.IsNullOrEmpty (username)
			       && !string.IsNullOrEmpty (password);
		}

		/// <summary>
		/// Save config settings
		/// </summary>
		private void SaveConfigSettings (string url, string username, string password)
		{
			// Save configuration into the GNOME Keyring
			try {
				Hashtable update_request_attributes = request_attributes.Clone () as Hashtable;
				update_request_attributes ["user"] = username;
				update_request_attributes ["url"] = url;

				ItemData [] items = Ring.Find (ItemType.NetworkPassword, request_attributes);
				string keyring = Ring.GetDefaultKeyring ();

				if (items.Length == 0)
					Ring.CreateItem (keyring, ItemType.NetworkPassword, keyring_item_name,
					                 update_request_attributes, password, true);
				else {
					Ring.SetItemInfo (keyring, items [0].ItemID, ItemType.NetworkPassword,
					                  keyring_item_name, password);
					Ring.SetItemAttributes (keyring, items [0].ItemID, update_request_attributes);
				}
			} catch (KeyringException ke) {
				Logger.Warn ("Saving configuration to the GNOME " +
				             "keyring failed with the following message: " +
				             ke.Message);
				// TODO: If the above fails (no keyring daemon), save all but password
				//       to GConf, and notify user.
				// Save configuration into GConf
				//Preferences.Set ("/apps/tomboy/sync_wdfs_url", url ?? string.Empty);
				//Preferences.Set ("/apps/tomboy/sync_wdfs_username", username ?? string.Empty);
				throw new TomboySyncException (Catalog.GetString ("Saving configuration to the GNOME keyring " +
				                               "failed with the following message:") +
				                               "\n\n" + ke.Message);
			}
		}

		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetPrefWidgetSettings (out string url, out string username, out string password)
		{
			url = urlEntry.Text.Trim ();
			username = usernameEntry.Text.Trim ();
			password = passwordEntry.Text.Trim ();

			return !string.IsNullOrEmpty (url)
			       && !string.IsNullOrEmpty (username)
			       && !string.IsNullOrEmpty (password);
		}

		private bool AcceptSslCert {
			get {
				try {
					return (bool) Preferences.Get ("/apps/tomboy/sync/wdfs/accept_sslcert");
				} catch {
					return false;
				}
			}
		}

		// TODO: Centralize duplicated code
		private void AddRow (Gtk.Table table, Gtk.Widget widget, string labelText, uint row)
		{
			Gtk.Label l = new Gtk.Label (labelText);
			l.UseUnderline = true;
			l.Xalign = 0.0f;
			l.Show ();
			table.Attach (l, 0, 1, row, row + 1,
			              Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);

			widget.Show ();
			table.Attach (widget, 1, 2, row, row + 1,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);

			l.MnemonicWidget = widget;

			// TODO: Tooltips
		}
		#endregion // Private Methods
	}
}
