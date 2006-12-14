// Copyright 2006 Alp Toker <alp@atoker.com>
// This software is made available under the MIT License
// See COPYING for details

using System;
//using GLib;
//using Gtk;
using NDesk.DBus;
using NDesk.GLib;
using org.freedesktop.DBus;

namespace NDesk.DBus
{
	//FIXME: this API needs review and de-unixification. It is horrid, but gets the job done.
	public static class BusG
	{
		static bool initialized = false;
		public static void Init ()
		{
			if (initialized)
				return;

			Init (Bus.System);
			Init (Bus.Session);
			//TODO: consider starter bus?

			initialized = true;
		}

		public static void Init (Connection conn)
		{
			IOFunc dispatchHandler = delegate (IOChannel source, IOCondition condition, IntPtr data) {
				conn.Iterate ();
				return true;
			};

			Init (conn, dispatchHandler);
		}

		public static void Init (Connection conn, IOFunc dispatchHandler)
		{
			IOChannel channel = new IOChannel ((int)conn.Transport.SocketHandle);
			IO.AddWatch (channel, IOCondition.In, dispatchHandler);
		}
	}
}
