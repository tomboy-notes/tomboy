// GtkBeans.Global.cs
//
// Author(s):
//      Stephane Delcroix <stephane@delcroix.org>
//
// Copyright (c) 2009 Novell, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General 
// Public License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace GtkBeans {
	public static class Global {
		[DllImport("libgtk-win32-2.0-0.dll")]
		static extern unsafe bool gtk_show_uri(IntPtr screen, IntPtr uri, uint timestamp, out IntPtr error);

		public static unsafe bool ShowUri(Gdk.Screen screen, string uri, uint timestamp) {
			IntPtr native_uri = GLib.Marshaller.StringToPtrGStrdup (uri);
			IntPtr error = IntPtr.Zero;
			bool raw_ret = gtk_show_uri(screen == null ? IntPtr.Zero : screen.Handle, native_uri, timestamp, out error);
			bool ret = raw_ret;
			GLib.Marshaller.Free (native_uri);
			if (error != IntPtr.Zero) throw new GLib.GException (error);
			return ret;
		}

		public static bool ShowUri (Gdk.Screen screen, string uri)
		{
			return ShowUri (screen, uri, Gdk.EventHelper.GetTime (new Gdk.Event(IntPtr.Zero)));
		}
	}
}
