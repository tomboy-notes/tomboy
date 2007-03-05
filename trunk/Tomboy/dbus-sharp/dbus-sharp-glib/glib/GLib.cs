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
				if ((condition & IOCondition.Hup) == IOCondition.Hup) {
					if (Protocol.Verbose)
						Console.Error.WriteLine ("Warning: Connection was probably hung up (" + condition + ")");

					//TODO: handle disconnection properly, consider memory management
					return false;
				}

				//this may not provide expected behaviour all the time, but works for now
				conn.Iterate ();
				return true;
			};

			Init (conn, dispatchHandler);
		}

		static void Init (Connection conn, IOFunc dispatchHandler)
		{
			IOChannel channel = new IOChannel ((int)conn.Transport.SocketHandle);
			IO.AddWatch (channel, IOCondition.In | IOCondition.Hup, dispatchHandler);
		}
	}
}
