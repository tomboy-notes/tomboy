
#ifndef __TOMBOY_UTIL_H__
#define __TOMBOY_UTIL_H__

#include <gdk/gdkpixmap.h>
#include <gtk/gtkwidget.h>
#include <gtk/gtkwindow.h>

G_BEGIN_DECLS

gint tomboy_window_get_workspace (GtkWindow *window);

void tomboy_window_move_to_current_workspace (GtkWindow *window);

void tomboy_window_present_hardcore (GtkWindow *window);

G_END_DECLS

#endif /* __TOMBOY_UTIL_H__ */
