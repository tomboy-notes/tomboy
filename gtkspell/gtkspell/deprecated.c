/* gtkspell - a spell-checking addon for GTK's TextView widget
 * Copyright (c) 2002 Evan Martin.
 */

/* vim: set ts=4 sw=4 wm=5 : */

#include <gtk/gtk.h>
#include "gtkspell.h"

/**
 * gtkspell_init:
 *
 * This function is deprecated and included only for backward
 * compatibility. 
 *
 * Returns: nothing.
 */
int
gtkspell_init() {
	/* we do nothing. */
	return 0;
}

/**
 * gtkspell_attach:
 * @view: a #GtkTextView.
 *
 * This function is deprecated and included only for backward
 * compatibility.  It calls gtkspell_new_attach() with the default language
 * and without error handling.
 */
void
gtkspell_attach(GtkTextView *view) {
	gtkspell_new_attach(view, NULL, NULL);
}
