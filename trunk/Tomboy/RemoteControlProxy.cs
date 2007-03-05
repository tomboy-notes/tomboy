using System;
using NDesk.DBus;
using org.freedesktop.DBus;

namespace Tomboy
{
	public static class RemoteControlProxy {
		private const string Path = "/org/gnome/Tomboy/RemoteControl";
		private const string Namespace = "org.gnome.Tomboy";

		public static RemoteControl GetInstance () {
			BusG.Init ();

			if (! Bus.Session.NameHasOwner (Namespace))
				Bus.Session.StartServiceByName (Namespace);

			return Bus.Session.GetObject<RemoteControl> (Namespace,
				new ObjectPath (Path));
		}

		public static RemoteControl Register (NoteManager manager) {
			BusG.Init ();
			
			RemoteControl remote_control = new RemoteControl (manager);
			Bus.Session.Register (Namespace,
				new ObjectPath (Path),
				remote_control);
			
			if (Bus.Session.RequestName (Namespace)
					!= RequestNameReply.PrimaryOwner)
				return null;

			return remote_control;
		}
	}
}
