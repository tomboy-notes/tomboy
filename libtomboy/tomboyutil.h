
#ifndef __TOMBOY_KEY_BINDER_H__
#define __TOMBOY_KEY_BINDER_H__

#include <gdk/gdkpixmap.h>
#include <gtk/gtkwidget.h>
#include <gtk/gtkwindow.h>

G_BEGIN_DECLS

void tomboy_widget_set_bg_pixmap (GtkWidget *applet, GdkPixmap *pixmap);

void tomboy_window_move_to_current_workspace (GtkWindow *window);

void tomboy_window_present_hardcore (GtkWindow *window);

G_END_DECLS

#endif /* __TOMBOY_KEY_BINDER_H__ */

