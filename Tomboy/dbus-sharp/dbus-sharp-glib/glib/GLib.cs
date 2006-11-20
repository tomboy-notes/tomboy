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
		static bool SystemDispatch (IOChannel source, IOCondition condition, IntPtr data)
		{
			Bus.System.Iterate ();
			return true;
		}

		static bool SessionDispatch (IOChannel source, IOCondition condition, IntPtr data)
		{
			Bus.Session.Iterate ();
			return true;
		}

		static bool initialized = false;
		public static void Init ()
		{
			if (initialized)
				return;

			Init (Bus.System, SystemDispatch);
			Init (Bus.Session, SessionDispatch);

			initialized = true;
		}

		public static void Init (Connection conn, IOFunc dispatchHandler)
		{
			IOChannel channel = new IOChannel ((int)conn.SocketHandle);
			IO.AddWatch (channel, IOCondition.In, dispatchHandler);
		}

		//TODO: add public API to watch an arbitrary connection
	}
}
