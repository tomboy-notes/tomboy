using System;

namespace Tomboy.Sync
{
	/// <summary>
	/// A SyncServiceAddin provides Tomboy Note Synchronization to a
	/// service such as WebDav, SSH, FTP, etc.
	/// <summary>
	public abstract class SyncServiceAddin : ApplicationAddin
	{
		/// <summary>
		/// Creates a SyncServer instance that the SyncManager can use to
		/// synchronize with this service.  This method is called during
		/// every synchronization process.  If the same SyncServer object
		/// is returned here, it should be reset as if it were new.
		/// </summary>
		public abstract SyncServer CreateSyncServer ();

		// TODO: Document, rename?
		public abstract void PostSyncCleanup ();

		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.  This
		/// will be used in the Synchronization Preferences.  Preferences should
		/// not automatically be saved by a GConf Property Editor.  Preferences
		/// should be saved when SaveConfiguration () is called. requiredPrefChanged
		/// should be called when a required setting is changed.
		/// </summary>
		/// <param name="requiredPrefChanged">Delegate to be called when a required preference is changed</param>
		public abstract Gtk.Widget CreatePreferencesControl (EventHandler requiredPrefChanged);

		/// <summary>
		/// The Addin should verify and check the connection to the service
		/// when this is called.  If verification and connection is successful,
		/// the addin should save the configuration and return true.
		/// </summary>
		public abstract bool SaveConfiguration ();

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		public abstract void ResetConfiguration ();

		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public abstract bool IsConfigured
		{
			get;
		}
		
		/// <summary>
		/// Returns true if required settings are valid in the widget
		/// (Required setings are non-empty)
		/// </summary>
		public virtual bool AreSettingsValid
		{
			get { return true; }
		}

		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public abstract string Name
		{
			get;
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public abstract string Id
		{
			get;
		}

		/// <summary>
		/// Returns true if the addin has all the supporting libraries installed
		/// on the machine or false if the proper environment is not available.
		/// If false, the preferences dialog will still call
		/// CreatePreferencesControl () when the service is selected.  It's up
		/// to the addin to present the user with what they should install/do so
		/// IsSupported will be true.
		/// </summary>
		public abstract bool IsSupported
		{
			get;
		}
	}
}
