

#include <gdk/gdk.h>
#include <gdk/gdkwindow.h>
#include <gdk/gdkx.h>
#include <X11/Xlib.h>

#include "tomboyutil.h"

/* Uncomment the next line to print a debug trace. */
/* #define DEBUG */

#ifdef DEBUG
#  define TRACE(x) x
#else
#  define TRACE(x) do {} while (FALSE);
#endif

void
tomboy_widget_set_bg_pixmap (GtkWidget *applet, GdkPixmap *pixmap)
{
	GtkStyle *style;

	style = gtk_style_copy (GTK_WIDGET (applet)->style);
	if (style->bg_pixmap[GTK_STATE_NORMAL])
		g_object_unref (style->bg_pixmap[GTK_STATE_NORMAL]);
	style->bg_pixmap[GTK_STATE_NORMAL] = g_object_ref (pixmap);
	gtk_widget_set_style (GTK_WIDGET (applet), style);
	g_object_unref (style);
}
