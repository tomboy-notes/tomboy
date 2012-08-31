using System;
#if ENABLE_DBUS
using DBus;
using org.freedesktop.DBus;
#else
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
#endif

namespace Tomboy
{
	public static class RemoteControlProxy {
#if ENABLE_DBUS
		private const string Path = "/org/gnome/Tomboy/RemoteControl";
		private const string Namespace = "org.gnome.Tomboy";
		private static bool? firstInstance;
#else
		private static Mutex mutex;
		private static bool firstInstance;
		private const string MutexName = "{9EF7D32D-3392-4940-8A28-1320A7BD42AB}";

		private static IpcChannel IpcChannel;
		private const string ServerName = "TomboyServer";
		private const string ClientName = "TomboyClient";
		private const string WrapperName = "TomboyRemoteControlWrapper";
		private static string ServiceUrl =
			string.Format ("ipc://{0}/{1}", ServerName, WrapperName);
#endif

		public static IRemoteControl GetInstance () {
#if ENABLE_DBUS
			BusG.Init ();

			if (! Bus.Session.NameHasOwner (Namespace))
				Bus.Session.StartServiceByName (Namespace);

			return Bus.Session.GetObject<RemoteControl> (Namespace,
			                new ObjectPath (Path));
#else
			RemoteControlWrapper remote = (RemoteControlWrapper) Activator.GetObject (
				typeof (RemoteControlWrapper),
				ServiceUrl);

			return remote;
#endif
		}

		public static RemoteControl Register (NoteManager manager)
		{
#if ENABLE_DBUS
			if (!FirstInstance)
				return null;

			RemoteControl remote_control = new RemoteControl (manager);
			Bus.Session.Register (Namespace,
			                      new ObjectPath (Path),
			                      remote_control);
			return remote_control;
#else
			if (FirstInstance) {
				// Register an IPC channel for .NET remoting
				// access to our Remote Control
				IpcChannel = new IpcChannel (ServerName);
				ChannelServices.RegisterChannel (IpcChannel, false);
				RemotingConfiguration.RegisterWellKnownServiceType (
					typeof (RemoteControlWrapper),
					WrapperName,
					WellKnownObjectMode.Singleton);

				// The actual Remote Control has many methods
				// that need to be called in the GTK+ mainloop,
				// which will not happen when the method calls
				// come from a .NET remoting client. So we wrap
				// the Remote Control in a class that implements
				// the same interface, but wraps most method
				// calls in Gtk.Application.Invoke.
				//
				// Note that only one RemoteControl is ever
				// created, and that it is stored statically
				// in the RemoteControlWrapper.
				RemoteControl realRemote = new RemoteControl (manager);
				RemoteControlWrapper.Initialize (realRemote);

				RemoteControlWrapper remoteWrapper = (RemoteControlWrapper) Activator.GetObject (
					typeof (RemoteControlWrapper),
					ServiceUrl);
				return realRemote;
			} else {
				// If Tomboy is already running, register a
				// client IPC channel.
				IpcChannel = new IpcChannel (ClientName);
				ChannelServices.RegisterChannel (IpcChannel, false);
				return null;
			}
#endif
		}

		public static bool FirstInstance {
			get {
#if ENABLE_DBUS
				// We use DBus to provide single-instance detection
				if (!firstInstance.HasValue) {
					BusG.Init ();
					firstInstance = Bus.Session.RequestName (Namespace) == RequestNameReply.PrimaryOwner;
				}
				return firstInstance.Value;
#else
				// Use a mutex to provide single-instance detection
				if (mutex == null)
					mutex = new Mutex (true, MutexName, out firstInstance);
				return firstInstance;
#endif
			}
		}
	}
}
