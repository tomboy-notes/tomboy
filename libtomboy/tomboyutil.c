/* tomboyutil.c
 * Copyright (C) 2008 Alex Graveley
 *
 * Permission is hereby granted, free of charge, to any person obtaining 
 * a copy of this software and associated documentation files (the 
 * "Software"), to deal in the Software without restriction, including 
 * without limitation the rights to use, copy, modify, merge, publish, 
 * distribute, sublicense, and/or sell copies of the Software, and to 
 * permit persons to whom the Software is furnished to do so, subject to 
 * the following conditions: 
 *  
 * The above copyright notice and this permission notice shall be 
 * included in all copies or substantial portions of the Software. 
 *  
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
 * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
 * WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
 */

#include <gdk/gdk.h>
#include <gdk/gdkx.h>
#include <gtk/gtk.h>
#include <X11/Xlib.h>
#include <X11/Xatom.h>

#include "config.h"
#include "tomboykeybinder.h"
#include "tomboyutil.h"

/* Uncomment the next line to print a debug trace. */
/* #define DEBUG */

#ifdef DEBUG
#  define TRACE(x) x
#else
#  define TRACE(x) do {} while (FALSE);
#endif

gint
tomboy_window_get_workspace (GtkWindow *window)
{
	GdkWindow *gdkwin = gtk_widget_get_window (GTK_WIDGET (window));
	GdkAtom wm_desktop = gdk_atom_intern ("_NET_WM_DESKTOP", FALSE);
	GdkAtom out_type;
	gint out_format, out_length;
	gulong *out_val;
	int workspace;

	if (!gdk_property_get (gdkwin,
			       wm_desktop,
			       _GDK_MAKE_ATOM (XA_CARDINAL),
			       0, G_MAXLONG,
			       FALSE,
			       &out_type,
			       &out_format,
			       &out_length,
			       (guchar **) &out_val))
		return -1;

	workspace = *out_val;
	g_free (out_val);

	return workspace;
}

void
tomboy_window_move_to_current_workspace (GtkWindow *window)
{
	GdkWindow *gdkwin = gtk_widget_get_window (GTK_WIDGET (window));
	GdkWindow *rootwin = 
		gdk_screen_get_root_window (gdk_drawable_get_screen (gdkwin));

	GdkAtom current_desktop = 
		gdk_atom_intern ("_NET_CURRENT_DESKTOP", FALSE);
	GdkAtom wm_desktop = gdk_atom_intern ("_NET_WM_DESKTOP", FALSE);
	GdkAtom out_type;
	gint out_format, out_length;
	gulong *out_val;
	int workspace;
	XEvent xev;

	if (!gdk_property_get (rootwin,
			       current_desktop,
			       _GDK_MAKE_ATOM (XA_CARDINAL),
			       0, G_MAXLONG,
			       FALSE,
			       &out_type,
			       &out_format,
			       &out_length,
			       (guchar **) &out_val))
		return;

	workspace = *out_val;
	g_free (out_val);

	TRACE (g_print ("Setting _NET_WM_DESKTOP to: %d\n", workspace));

	xev.xclient.type = ClientMessage;
	xev.xclient.serial = 0;
	xev.xclient.send_event = True;
	xev.xclient.display = GDK_WINDOW_XDISPLAY (gdkwin);
	xev.xclient.window = GDK_WINDOW_XWINDOW (gdkwin);
	xev.xclient.message_type = 
		gdk_x11_atom_to_xatom_for_display(
			gdk_drawable_get_display (gdkwin),
			wm_desktop);
	xev.xclient.format = 32;
	xev.xclient.data.l[0] = workspace;
	xev.xclient.data.l[1] = 0;
	xev.xclient.data.l[2] = 0;

	XSendEvent (GDK_WINDOW_XDISPLAY (rootwin),
		    GDK_WINDOW_XWINDOW (rootwin),
		    False,
		    SubstructureRedirectMask | SubstructureNotifyMask,
		    &xev);
}

static void
tomboy_window_override_user_time (GtkWindow *window)
{
	guint32 ev_time = gtk_get_current_event_time();

	if (ev_time == 0) {
		/* 
		 * FIXME: Global keypresses use an event filter on the root
		 * window, which processes events before GDK sees them.
		 */
		ev_time = tomboy_keybinder_get_current_event_time ();
	}
	if (ev_time == 0) {
		gint ev_mask = gtk_widget_get_events (GTK_WIDGET(window));
		if (!(ev_mask & GDK_PROPERTY_CHANGE_MASK)) {
			gtk_widget_add_events (GTK_WIDGET (window),
					       GDK_PROPERTY_CHANGE_MASK);
		}

		/* 
		 * NOTE: Last resort for D-BUS or other non-interactive
		 *       openings.  Causes roundtrip to server.  Lame. 
		 */
		ev_time = gdk_x11_get_server_time (gtk_widget_get_window (GTK_WIDGET (window)));
	}

	TRACE (g_print("Setting _NET_WM_USER_TIME to: %d\n", ev_time));
	gdk_x11_window_set_user_time (gtk_widget_get_window (GTK_WIDGET (window)), ev_time);
}

void
tomboy_window_present_hardcore (GtkWindow *window)
{
	if (!GTK_WIDGET_REALIZED (window))
		gtk_widget_realize (GTK_WIDGET (window));
	else if (GTK_WIDGET_VISIBLE (window))
		tomboy_window_move_to_current_workspace (window);

	tomboy_window_override_user_time (window);

	gtk_window_present (window);
}

