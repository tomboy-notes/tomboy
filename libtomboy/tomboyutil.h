/* tomboyutil.h
 * Copyright (C) 2008 Novell
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Library General Public
 * License as published by the Free Software Foundation; either
 * version 2 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Library General Public License for more details.
 *
 * You should have received a copy of the GNU Library General Public
 * License along with this library; if not, write to the
 * Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110, USA 
 */
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
