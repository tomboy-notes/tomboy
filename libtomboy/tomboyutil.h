/* tomboyutil.h
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
#ifndef __TOMBOY_UTIL_H__
#define __TOMBOY_UTIL_H__

#include <gdk/gdk.h>
#include <gtk/gtk.h>

G_BEGIN_DECLS

#ifdef HAS_GTK_2_20
#define GTK_WIDGET_REALIZED(wid)	  (gtk_widget_get_realized (wid))
#define GTK_WIDGET_VISIBLE(wid)	  (gtk_widget_get_visible (wid))
#endif

gint tomboy_window_get_workspace (GtkWindow *window);

void tomboy_window_move_to_current_workspace (GtkWindow *window);

G_END_DECLS

#endif /* __TOMBOY_UTIL_H__ */
