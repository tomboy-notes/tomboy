// Copyright 2006 Alp Toker <alp@atoker.com>
// This software is made available under the MIT License
// See COPYING for details

using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace NDesk.GLib
{
	/*
	Specifies the type of function which is called when a data element is destroyed. It is passed the pointer to the data element and should free any memory and resources allocated for it.

	@data: the data element.
	*/
	delegate void DestroyNotify (IntPtr data);

	/*
	Specifies the type of function passed to g_io_add_watch() or g_io_add_watch_full(), which is called when the requested condition on a GIOChannel is satisfied.

	@source: the GIOChannel event source.
	@condition: the condition which has been satisfied.
	@data: user data set in g_io_add_watch() or g_io_add_watch_full().

	Returns: the function should return FALSE if the event source should be removed.
	*/
	delegate bool IOFunc (IOChannel source, IOCondition condition, IntPtr data);

	//this is actually somewhat like Stream, but we don't use it that way
	[StructLayout (LayoutKind.Sequential)]
	struct IOChannel
	{
		const string GLIB = "libglib-2.0-0.dll";

		public IntPtr Handle;

		[DllImport(GLIB)]
			static extern IntPtr g_io_channel_unix_new (int fd);

		public IOChannel (int fd)
		{
			Handle = g_io_channel_unix_new (fd);
		}

		[DllImport(GLIB)]
			//static extern int g_io_channel_unix_get_fd (IntPtr channel);
			static extern int g_io_channel_unix_get_fd (IOChannel channel);

		public int UnixFd
		{
			get {
				//return g_io_channel_unix_get_fd (Handle);
				return g_io_channel_unix_get_fd (this);
			}
		}

		[DllImport(GLIB)]
			public static extern IntPtr g_io_channel_win32_new_fd (int fd);

		[DllImport(GLIB)]
			public static extern IntPtr g_io_channel_win32_new_socket (int socket);

		[DllImport(GLIB)]
			public static extern IntPtr g_io_channel_win32_new_messages (uint hwnd);


		[DllImport(GLIB)]
			public static extern uint g_io_channel_get_buffer_size (IOChannel channel);

		[DllImport(GLIB)]
			public static extern void g_io_channel_set_buffer_size (IOChannel channel, uint size);

		public uint BufferSize
		{
			get {
				return g_io_channel_get_buffer_size (this);
			} set {
				g_io_channel_set_buffer_size (this, value);
			}
		}

		[DllImport(GLIB)]
			public static extern IOCondition g_io_channel_get_buffer_condition (IOChannel channel);

		public IOCondition BufferCondition
		{
			get {
				return g_io_channel_get_buffer_condition (this);
			}
		}

		[DllImport(GLIB)]
			public static extern IOFlags g_io_channel_get_flags (IOChannel channel);

		[DllImport(GLIB)]
			static extern short g_io_channel_set_flags (IOChannel channel, IOFlags flags, IntPtr error);

		public IOFlags Flags
		{
			get {
				return g_io_channel_get_flags (this);
			} set {
				//TODO: fix return and error
				g_io_channel_set_flags (this, value, IntPtr.Zero);
			}
		}
	}

	class IO
	{
		const string GLIB = "libglib-2.0-0.dll";

		//TODO: better memory management
		public static ArrayList objs = new ArrayList ();

		/*
		Adds the GIOChannel into the main event loop with the default priority.

		@channel: a GIOChannel.
		@condition: the condition to watch for.
		@func: the function to call when the condition is satisfied.
		@user_data: user data to pass to func.

		Returns: the event source id.
		*/
		[DllImport(GLIB)]
			protected static extern uint g_io_add_watch (IOChannel channel, IOCondition condition, IOFunc func, IntPtr user_data);

		public static uint AddWatch (IOChannel channel, IOCondition condition, IOFunc func)
		{
			objs.Add (func);

			return g_io_add_watch (channel, condition, func, IntPtr.Zero);
		}

		/*
		Adds the GIOChannel into the main event loop with the given priority.

		@channel: a GIOChannel.
		@priority: the priority of the GIOChannel source.
		@condition: the condition to watch for.
		@func: the function to call when the condition is satisfied.
		@user_data: user data to pass to func.
		@notify: the function to call when the source is removed.

		Returns: the event source id.
		*/
		[DllImport(GLIB)]
			protected static extern uint g_io_add_watch_full (IOChannel channel, int priority, IOCondition condition, IOFunc func, IntPtr user_data, DestroyNotify notify);

		public static uint AddWatch (IOChannel channel, int priority, IOCondition condition, IOFunc func, DestroyNotify notify)
		{
			objs.Add (func);
			objs.Add (notify);

			return g_io_add_watch_full (channel, priority, condition, func, IntPtr.Zero, notify);
		}

		[DllImport(GLIB)]
			protected static extern IntPtr g_main_context_default ();

		public static IntPtr MainContextDefault ()
		{
			return g_main_context_default ();
		}

		[DllImport(GLIB)]
			protected static extern void g_main_context_wakeup (IntPtr context);

		public static void MainContextWakeup (IntPtr context)
		{
			g_main_context_wakeup (context);
		}
	}

	//From Mono.Unix and poll(2)
	[Flags]
	enum PollEvents : short {
		POLLIN      = 0x0001, // There is data to read
		POLLPRI     = 0x0002, // There is urgent data to read
		POLLOUT     = 0x0004, // Writing now will not block
		POLLERR     = 0x0008, // Error condition
		POLLHUP     = 0x0010, // Hung up
		POLLNVAL    = 0x0020, // Invalid request; fd not open
		// XPG4.2 definitions (via _XOPEN_SOURCE)
		POLLRDNORM  = 0x0040, // Normal data may be read
		POLLRDBAND  = 0x0080, // Priority data may be read
		POLLWRNORM  = 0x0100, // Writing now will not block
		POLLWRBAND  = 0x0200, // Priority data may be written
	}

	//A bitwise combination representing a condition to watch for on an event source.
	[Flags]
	enum IOCondition : short
	{
		//There is data to read.
		In = PollEvents.POLLIN,
		//Data can be written (without blocking).
		Out = PollEvents.POLLOUT,
		//There is urgent data to read.
		Pri = PollEvents.POLLPRI,
		//Error condition.
		Err = PollEvents.POLLERR,
		//Hung up (the connection has been broken, usually for pipes and sockets).
		Hup = PollEvents.POLLHUP,
		//Invalid request. The file descriptor is not open.
		Nval = PollEvents.POLLNVAL,
	}

	[Flags]
	enum IOFlags : short
	{
		Append = 1 << 0,
		Nonblock = 1 << 1,
		//Read only flag
		IsReadable = 1 << 2,
		//Read only flag
		isWriteable = 1 << 3,
		//Read only flag
		IsSeekable = 1 << 4,
		//?
		Mask = (1 << 5) - 1,
		GetMask = Mask,
		SetMask = Append | Nonblock,
	}
}
