/* gtkspell - a spell-checking addon for GTK's TextView widget
 * Copyright (c) 2002 Evan Martin.
 */

/* vim: set ts=4 sw=4 wm=5 : */

#ifndef GTKSPELL_H
#define GTKSPELL_H

#define GTKSPELL_ERROR gtkspell_error_quark()

typedef enum {
	GTKSPELL_ERROR_BACKEND,
} GtkSpellError;

GQuark gtkspell_error_quark();

typedef struct _GtkSpell GtkSpell;

/* the idea is to have a GtkSpell object that is analagous to the
 * GtkTextBuffer-- it lives as an attribute of the GtkTextView but
 * it can be referred to directly. */

GtkSpell* gtkspell_new_attach(GtkTextView *view,
                                     const gchar *lang, GError **error);
GtkSpell* gtkspell_get_from_text_view(GtkTextView *view);
void      gtkspell_detach(GtkSpell *spell);

gboolean  gtkspell_set_language(GtkSpell *spell,
                                       const gchar *lang, GError **error);

void      gtkspell_recheck_all(GtkSpell *spell);


/*** old API-- deprecated. ***/
#ifndef GTKSPELL_DISABLE_DEPRECATED
#define GTKSPELL_ERROR_PSPELL GTKSPELL_ERROR_BACKEND

int gtkspell_init();
/* no-op. */

void gtkspell_attach(GtkTextView *view);
/* gtkspell_new_attach(view, NULL, NULL); */

#endif /* GTKSPELL_DISABLE_DEPRECATED */

#endif /* GTKSPELL_H */
