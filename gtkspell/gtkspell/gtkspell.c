/* gtkspell - a spell-checking addon for GTK's TextView widget
 * Copyright (c) 2002 Evan Martin.
 */

/* vim: set ts=4 sw=4 wm=5 : */

#include <gtk/gtk.h>
#include <libintl.h>
#include <locale.h>
#include "../config.h"
#include "gtkspell.h"

#define _(String) dgettext (PACKAGE, String)

#define GTKSPELL_MISSPELLED_TAG "gtkspell-misspelled"

#ifdef HAVE_ASPELL_H
   #define USING_ASPELL
   #include <aspell.h>
#elif defined HAVE_PSPELL_H
   #define USING_PSPELL
   #include <pspell/pspell.h>
   #define AspellSpeller PspellManager
   #define speller manager
   #define aspell_speller_check pspell_manager_check
   #define aspell_speller_add_to_session pspell_manager_add_to_session
   #define aspell_speller_add_to_personal pspell_manager_add_to_personal
   #define aspell_speller_save_all_word_lists pspell_manager_save_all_word_lists
   #define aspell_speller_store_replacement pspell_manager_store_replacement
   #define AspellWordList PspellWordList
   #define AspellStringEnumeration PspellStringEmulation
   #define aspell_speller_suggest pspell_manager_suggest
   #define aspell_word_list_elements pspell_word_list_elements
   #define aspell_string_enumeration_next pspell_string_emulation_next
   #define delete_aspell_string_enumeration delete_pspell_string_emulation
   #define AspellConfig PspellConfig
   #define AspellCanHaveError PspellCanHaveError
   #define new_aspell_config new_pspell_config
   #define aspell_config_replace pspell_config_replace
   #define new_aspell_speller new_pspell_manager
   #define delete_aspell_config delete_pspell_config
   #define aspell_error_message pspell_error_message
   #define delete_aspell_speller delete_pspell_manager
   #define to_aspell_speller to_pspell_manager
   #define aspell_error_number pspell_error_number
   #define aspell pspell
#endif

const int debug = 0;
const int quiet = 0;

struct _GtkSpell {
	GtkTextView *view;
	GtkTextTag *tag_highlight;
	GtkTextMark *mark_insert_start;
	GtkTextMark *mark_insert_end;
	gboolean deferred_check;
	AspellSpeller *speller;
	GtkTextMark *mark_click;
};

static void gtkspell_free(GtkSpell *spell);

#define GTKSPELL_OBJECT_KEY "gtkspell"

GQuark
gtkspell_error_quark(void) {
	static GQuark q = 0;
	if (q == 0)
		q = g_quark_from_static_string("gtkspell-error-quark");
	return q;
}

static gboolean
gtkspell_text_iter_forward_word_end(GtkTextIter *i) {
	GtkTextIter iter;

/* heuristic: 
 * if we're on an singlequote/apostrophe and
 * if the next letter is alphanumeric,
 * this is an apostrophe. */

	if (!gtk_text_iter_forward_word_end(i))
		return FALSE;

	if (gtk_text_iter_get_char(i) != '\'')
		return TRUE;

	iter = *i;
	if (gtk_text_iter_forward_char(&iter)) {
		if (g_unichar_isalpha(gtk_text_iter_get_char(&iter))) {
			return (gtk_text_iter_forward_word_end(i));
		}
	}

	return TRUE;
}

static gboolean
gtkspell_text_iter_backward_word_start(GtkTextIter *i) {
	GtkTextIter iter;

	if (!gtk_text_iter_backward_word_start(i))
		return FALSE;

	iter = *i;
	if (gtk_text_iter_backward_char(&iter)) {
		if (gtk_text_iter_get_char(&iter) == '\'') {
			if (gtk_text_iter_backward_char(&iter)) {
				if (g_unichar_isalpha(gtk_text_iter_get_char(&iter))) {
					return (gtk_text_iter_backward_word_start(i));
				}
			}
		}
	}

	return TRUE;
}

#define gtk_text_iter_backward_word_start gtkspell_text_iter_backward_word_start
#define gtk_text_iter_forward_word_end gtkspell_text_iter_forward_word_end

static void
check_word(GtkSpell *spell, GtkTextBuffer *buffer,
           GtkTextIter *start, GtkTextIter *end) {
	char *text;
	text = gtk_text_buffer_get_text(buffer, start, end, FALSE);
	if (debug) g_print("checking: %s\n", text);
	if (g_unichar_isdigit(*text) == FALSE) /* don't check numbers */
		if (aspell_speller_check(spell->speller, text, -1) == FALSE)
			gtk_text_buffer_apply_tag(buffer, spell->tag_highlight, start, end);
	g_free(text);
}

static void
print_iter(char *name, GtkTextIter *iter) {
	g_print("%1s[%d%c%c%c] ", name, gtk_text_iter_get_offset(iter),
		gtk_text_iter_starts_word(iter) ? 's' : ' ',
		gtk_text_iter_inside_word(iter) ? 'i' : ' ',
		gtk_text_iter_ends_word(iter) ? 'e' : ' ');
}

static void
check_range(GtkSpell *spell, GtkTextBuffer *buffer,
            GtkTextIter start, GtkTextIter end, gboolean force_all) {
	/* we need to "split" on word boundaries.
	 * luckily, pango knows what "words" are 
	 * so we don't have to figure it out. */

	GtkTextIter wstart, wend, cursor, precursor;
	gboolean inword, highlight;
	if (debug) {
		g_print("check_range: "); print_iter("s", &start); print_iter("e", &end); g_print(" -> ");
	}

	if (gtk_text_iter_inside_word(&end))
		gtk_text_iter_forward_word_end(&end);
	if (!gtk_text_iter_starts_word(&start)) {
		if (gtk_text_iter_inside_word(&start) || 
				gtk_text_iter_ends_word(&start)) {
			gtk_text_iter_backward_word_start(&start);
		} else {
			/* if we're neither at the beginning nor inside a word,
			 * me must be in some spaces.
			 * skip forward to the beginning of the next word. */
			//gtk_text_buffer_remove_tag(buffer, tag_highlight, &start, &end);
			if (gtk_text_iter_forward_word_end(&start))
				gtk_text_iter_backward_word_start(&start);
		}
	}
	gtk_text_buffer_get_iter_at_mark(buffer, &cursor,
			gtk_text_buffer_get_insert(buffer));

	precursor = cursor;
	gtk_text_iter_backward_char(&precursor);
	highlight = gtk_text_iter_has_tag(&cursor, spell->tag_highlight) ||
			gtk_text_iter_has_tag(&precursor, spell->tag_highlight);
	
	gtk_text_buffer_remove_tag(buffer, spell->tag_highlight, &start, &end);

	/* Fix a corner case when replacement occurs at beginning of buffer:
	 * An iter at offset 0 seems to always be inside a word,
	 * even if it's not.  Possibly a pango bug.
	 */
	if (gtk_text_iter_get_offset(&start) == 0) {
		gtk_text_iter_forward_word_end(&start);
		gtk_text_iter_backward_word_start(&start);
	}

	if (debug) {print_iter("s", &start); print_iter("e", &end); g_print("\n");}

	wstart = start;
	while (gtk_text_iter_compare(&wstart, &end) < 0) {
		/* move wend to the end of the current word. */
		wend = wstart;
		gtk_text_iter_forward_word_end(&wend);

		inword = (gtk_text_iter_compare(&wstart, &cursor) < 0) && 
				(gtk_text_iter_compare(&cursor, &wend) <= 0);

		if (inword && !force_all) {
			/* this word is being actively edited, 
			 * only check if it's already highligted,
			 * otherwise defer this check until later. */
			if (highlight)
				check_word(spell, buffer, &wstart, &wend);
			else
				spell->deferred_check = TRUE;
		} else {
			check_word(spell, buffer, &wstart, &wend);
			spell->deferred_check = FALSE;
		}

		/* now move wend to the beginning of the next word, */
		gtk_text_iter_forward_word_end(&wend);
		gtk_text_iter_backward_word_start(&wend);
		/* make sure we've actually advanced
		 * (we don't advance in some corner cases), */
		if (gtk_text_iter_equal(&wstart, &wend))
			break; /* we're done in these cases.. */
		/* and then pick this as the new next word beginning. */
		wstart = wend;
	}
}

static void
check_deferred_range(GtkSpell *spell, GtkTextBuffer *buffer, gboolean force_all) {
	GtkTextIter start, end;
	gtk_text_buffer_get_iter_at_mark(buffer, &start, spell->mark_insert_start);
	gtk_text_buffer_get_iter_at_mark(buffer, &end, spell->mark_insert_end);
	check_range(spell, buffer, start, end, force_all);
}

/* insertion works like this:
 *  - before the text is inserted, we mark the position in the buffer.
 *  - after the text is inserted, we see where our mark is and use that and
 *    the current position to check the entire range of inserted text.
 *
 * this may be overkill for the common case (inserting one character). */

static void
insert_text_before(GtkTextBuffer *buffer, GtkTextIter *iter,
                   gchar *text, gint len, GtkSpell *spell) {
	gtk_text_buffer_move_mark(buffer, spell->mark_insert_start, iter);
}

static void
insert_text_after(GtkTextBuffer *buffer, GtkTextIter *iter,
                  gchar *text, gint len, GtkSpell *spell) {
	GtkTextIter start;

	if (debug) g_print("insert\n");

	/* we need to check a range of text. */
	gtk_text_buffer_get_iter_at_mark(buffer, &start, spell->mark_insert_start);
	check_range(spell, buffer, start, *iter, FALSE);
	
	gtk_text_buffer_move_mark(buffer, spell->mark_insert_end, iter);
}

/* deleting is more simple:  we're given the range of deleted text.
 * after deletion, the start and end iters should be at the same position
 * (because all of the text between them was deleted!).
 * this means we only really check the words immediately bounding the
 * deletion.
 */

static void
delete_range_after(GtkTextBuffer *buffer,
                   GtkTextIter *start, GtkTextIter *end, GtkSpell *spell) {
	if (debug) g_print("delete\n");
	check_range(spell, buffer, *start, *end, FALSE);
}

static void
mark_set(GtkTextBuffer *buffer, GtkTextIter *iter, 
		 GtkTextMark *mark, GtkSpell *spell) {
	/* if the cursor has moved and there is a deferred check so handle it now */
	if ((mark == gtk_text_buffer_get_insert(buffer)) && spell->deferred_check)
		check_deferred_range(spell, buffer, FALSE);
}

static void
get_word_extents_from_mark(GtkTextBuffer *buffer,
                     GtkTextIter *start, GtkTextIter *end, GtkTextMark *mark) {
	gtk_text_buffer_get_iter_at_mark(buffer, start, mark);
	if (!gtk_text_iter_starts_word(start)) 
		gtk_text_iter_backward_word_start(start);
	*end = *start;
	if (gtk_text_iter_inside_word(end))
		gtk_text_iter_forward_word_end(end);
}

static void
add_to_dictionary(GtkWidget *menuitem, GtkSpell *spell) {
	char *word;
	GtkTextIter start, end;
	GtkTextBuffer *buffer;
	
	buffer = gtk_text_view_get_buffer(spell->view);

	get_word_extents_from_mark(buffer, &start, &end, spell->mark_click);
	word = gtk_text_buffer_get_text(buffer, &start, &end, FALSE);
	
	aspell_speller_add_to_personal(spell->speller, word, strlen(word));
	aspell_speller_save_all_word_lists(spell->speller);

	gtkspell_recheck_all(spell);

	g_free(word);
}

static void
ignore_all(GtkWidget *menuitem, GtkSpell *spell) {
	char *word;
	GtkTextIter start, end;
	GtkTextBuffer *buffer;
	
	buffer = gtk_text_view_get_buffer(spell->view);

	get_word_extents_from_mark(buffer, &start, &end, spell->mark_click);
	word = gtk_text_buffer_get_text(buffer, &start, &end, FALSE);
	
	aspell_speller_add_to_session(spell->speller, word, strlen(word));

	gtkspell_recheck_all(spell);

	g_free(word);
}

static void
replace_word(GtkWidget *menuitem, GtkSpell *spell) {
	char *oldword;
	const char *newword;
	GtkTextIter start, end;
	GtkTextBuffer *buffer;
	
	buffer = gtk_text_view_get_buffer(spell->view);

	get_word_extents_from_mark(buffer, &start, &end, spell->mark_click);
	oldword = gtk_text_buffer_get_text(buffer, &start, &end, FALSE);
	newword = gtk_label_get_text(GTK_LABEL(GTK_BIN(menuitem)->child));

	if (debug) {
		g_print("old word: '%s'\n", oldword);
		print_iter("s", &start); print_iter("e", &end);
		g_print("\nnew word: '%s'\n", newword);
	}

	gtk_text_buffer_delete(buffer, &start, &end);
	gtk_text_buffer_insert(buffer, &start, newword, -1);

	aspell_speller_store_replacement(spell->speller, 
			oldword, strlen(oldword),
			newword, strlen(newword));

	g_free(oldword);
}

GtkWidget*
build_suggestion_menu(GtkSpell *spell, GtkTextBuffer *buffer,
                      const char *word) {
	const char *suggestion;
	GtkWidget *topmenu, *menu;
	GtkWidget *mi;
	GtkWidget *hbox;
	int count = 0;
	void *spelldata;
	const AspellWordList *suggestions;
	AspellStringEnumeration *elements;
	char *label;
	
	topmenu = menu = gtk_menu_new();

	suggestions = aspell_speller_suggest(spell->speller, word, -1);
	elements = aspell_word_list_elements(suggestions);

	suggestion = aspell_string_enumeration_next(elements);
	if (suggestion == NULL) {
		/* no suggestions.  put something in the menu anyway... */
		GtkWidget *label;
		label = gtk_label_new("");
		gtk_label_set_markup(GTK_LABEL(label), _("<i>(no suggestions)</i>"));

		mi = gtk_menu_item_new();
		gtk_container_add(GTK_CONTAINER(mi), label);
		gtk_widget_show_all(mi);
		gtk_menu_shell_prepend(GTK_MENU_SHELL(menu), mi);
	} else {
		/* build a set of menus with suggestions. */
		while (suggestion != NULL) {
			if (count == 10) {
				mi = gtk_menu_item_new();
				gtk_widget_show(mi);
				gtk_menu_shell_append(GTK_MENU_SHELL(menu), mi);

				mi = gtk_menu_item_new_with_label(_("More..."));
				gtk_widget_show(mi);
				gtk_menu_shell_append(GTK_MENU_SHELL(menu), mi);

				menu = gtk_menu_new();
				gtk_menu_item_set_submenu(GTK_MENU_ITEM(mi), menu);
				count = 0;
			}
			mi = gtk_menu_item_new_with_label(suggestion);
			g_signal_connect(G_OBJECT(mi), "activate",
					G_CALLBACK(replace_word), spell);
			gtk_widget_show(mi);
			gtk_menu_shell_append(GTK_MENU_SHELL(menu), mi);
			count++;
			suggestion = aspell_string_enumeration_next(elements);
		}
	}

	delete_aspell_string_enumeration(elements);

	/* Separator */
	mi = gtk_menu_item_new();
	gtk_widget_show(mi);
	gtk_menu_shell_append(GTK_MENU_SHELL(topmenu), mi);

	/* + Add to Dictionary */
	label = g_strdup_printf(_("Add \"%s\" to Dictionary"), word);
	mi = gtk_image_menu_item_new_with_label(label);
	g_free(label);
	gtk_image_menu_item_set_image(GTK_IMAGE_MENU_ITEM(mi), 
			gtk_image_new_from_stock(GTK_STOCK_ADD, GTK_ICON_SIZE_MENU));
	g_signal_connect(G_OBJECT(mi), "activate",
			G_CALLBACK(add_to_dictionary), spell);
	gtk_widget_show_all(mi);
	gtk_menu_shell_append(GTK_MENU_SHELL(topmenu), mi);

	/* - Ignore All */
	mi = gtk_image_menu_item_new_with_label(_("Ignore All"));
	gtk_image_menu_item_set_image(GTK_IMAGE_MENU_ITEM(mi), 
			gtk_image_new_from_stock(GTK_STOCK_REMOVE, GTK_ICON_SIZE_MENU));
	g_signal_connect(G_OBJECT(mi), "activate",
			G_CALLBACK(ignore_all), spell);
	gtk_widget_show_all(mi);
	gtk_menu_shell_append(GTK_MENU_SHELL(topmenu), mi);

	return topmenu;
}

static void
populate_popup(GtkTextView *textview, GtkMenu *menu, GtkSpell *spell) {
	GtkWidget *img, *mi;
	GtkTextBuffer *buffer = gtk_text_view_get_buffer(textview);
	GtkTextIter start, end;
	char *word;

	/* we need to figure out if they picked a misspelled word. */
	get_word_extents_from_mark(buffer, &start, &end, spell->mark_click);

	/* if our highlight algorithm ever messes up, 
	 * this isn't correct, either. */
	if (!gtk_text_iter_has_tag(&start, spell->tag_highlight))
		return; /* word wasn't misspelled. */

	/* menu separator comes first. */
	mi = gtk_menu_item_new();
	gtk_widget_show(mi);
	gtk_menu_shell_prepend(GTK_MENU_SHELL(menu), mi);

	/* then, on top of it, the suggestions menu. */
	img = gtk_image_new_from_stock(GTK_STOCK_SPELL_CHECK, GTK_ICON_SIZE_MENU);
	mi = gtk_image_menu_item_new_with_label(_("Spelling Suggestions"));
	gtk_image_menu_item_set_image(GTK_IMAGE_MENU_ITEM(mi), img);

	word = gtk_text_buffer_get_text(buffer, &start, &end, FALSE);
	gtk_menu_item_set_submenu(GTK_MENU_ITEM(mi),
			build_suggestion_menu(spell, buffer, word));
	g_free(word);

	gtk_widget_show_all(mi);
	gtk_menu_shell_prepend(GTK_MENU_SHELL(menu), mi);
}

/* when the user right-clicks on a word, they want to check that word.
 * here, we do NOT  move the cursor to the location of the clicked-upon word
 * since that prevents the use of edit functions on the context menu. */
static gboolean
button_press_event(GtkTextView *view, GdkEventButton *event, GtkSpell *spell) {
	if (event->button == 3) {
		gint x, y;
		GtkTextIter iter;
		GtkTextBuffer *buffer = gtk_text_view_get_buffer(view);

		/* handle deferred check if it exists */
		if (spell->deferred_check)
			check_deferred_range(spell, buffer, TRUE);

		gtk_text_view_window_to_buffer_coords(view, 
				GTK_TEXT_WINDOW_TEXT, 
				event->x, event->y,
				&x, &y);
		gtk_text_view_get_iter_at_location(view, &iter, x, y);
		gtk_text_buffer_move_mark(buffer, spell->mark_click, &iter);
	}
	return FALSE; /* false: let gtk process this event, too.
					 we don't want to eat any events. */
}

/* This event occurs when the popup menu is requested through a key-binding
 * (Menu Key or <shift>+F10 by default).  In this case we want to set
 * spell->mark_click to the cursor position. */
static gboolean
popup_menu_event(GtkTextView *view, GtkSpell *spell) {
	GtkTextIter iter;
	GtkTextBuffer *buffer = gtk_text_view_get_buffer(view);

	gtk_text_buffer_get_iter_at_mark(buffer, &iter, 
			gtk_text_buffer_get_insert(buffer));
	gtk_text_buffer_move_mark(buffer, spell->mark_click, &iter);
	return FALSE; /* false: let gtk process this event, too. */
}

static gboolean
gtkspell_set_language_internal(GtkSpell *spell, const gchar *lang, GError **error) {
	AspellConfig *config;
	AspellCanHaveError *err;
	AspellSpeller *speller;

	if (lang == NULL) {
		lang = g_getenv("LANG");
		if (lang) {
			if (g_strncasecmp(lang, "C", 1) == 0)
				lang = NULL;
			else if (lang[0] == 0)
				lang = NULL;
		}
	}

	config = new_aspell_config();
	if (lang)
		aspell_config_replace(config, "language-tag", lang);
	aspell_config_replace(config, "encoding", "utf-8");
	err = new_aspell_speller(config);
	delete_aspell_config(config);

	if (aspell_error_number(err) != 0) {
#ifdef USING_ASPELL
		g_set_error(error, GTKSPELL_ERROR, GTKSPELL_ERROR_BACKEND,
				"aspell: %s", aspell_error_message(err));
#elif defined USING_PSPELL
		g_set_error(error, GTKSPELL_ERROR, GTKSPELL_ERROR_BACKEND,
				"pspell: %s", aspell_error_message(err));
#endif
		return FALSE;
	} 
	if (spell->speller)
		delete_aspell_speller(spell->speller);
	spell->speller = to_aspell_speller(err);

	return TRUE;
}

/**
 * gtkspell_set_language:
 * @spell:  The #GtkSpell object.
 * @lang: The language to use, in a form pspell understands (it appears to
 * be a locale specifier?).
 * @error: Return location for error.
 *
 * Set the language on @spell to @lang, possibily returning an error in
 * @error.
 *
 * Returns: FALSE if there was an error.
 */
gboolean
gtkspell_set_language(GtkSpell *spell, const gchar *lang, GError **error) {
	gboolean ret;

	if (error)
		g_return_val_if_fail(*error == NULL, FALSE);

	ret = gtkspell_set_language_internal(spell, lang, error);
	if (ret)
		gtkspell_recheck_all(spell);

	return ret;
}

/**
 * gtkspell_recheck_all:
 * @spell:  The #GtkSpell object.
 *
 * Recheck the spelling in the entire buffer.
 */
void
gtkspell_recheck_all(GtkSpell *spell) {
	GtkTextBuffer *buffer;
	GtkTextIter start, end;

	buffer = gtk_text_view_get_buffer(spell->view);

	gtk_text_buffer_get_bounds(buffer, &start, &end);

	check_range(spell, buffer, start, end, TRUE);
}

/**
 * gtkspell_new_attach:
 * @view: The #GtkTextView to attach to.
 * @lang: The language to use, in a form pspell understands (it appears to
 * be a locale specifier?).
 * @error: Return location for error.
 *
 * Create a new #GtkSpell object attached to @view with language @lang.
 *
 * Returns: a new #GtkSpell object, or %NULL on error.
 */
GtkSpell*
gtkspell_new_attach(GtkTextView *view, const gchar *lang, GError **error) {
	GtkTextBuffer *buffer;
	GtkTextTagTable *tagtable;
	GtkTextIter start, end;

	GtkSpell *spell;

#ifdef ENABLE_NLS
	bindtextdomain(PACKAGE, LOCALEDIR);
#endif

	if (error)
		g_return_val_if_fail(*error == NULL, NULL);

	spell = g_object_get_data(G_OBJECT(view), GTKSPELL_OBJECT_KEY);
	g_assert(spell == NULL);

	/* attach to the widget */
	spell = g_new0(GtkSpell, 1);
	spell->view = view;
	if (!gtkspell_set_language_internal(spell, lang, error)) {
		g_free(spell);
		return NULL;
	}
	g_object_set_data(G_OBJECT(view), GTKSPELL_OBJECT_KEY, spell);

	g_signal_connect_swapped(G_OBJECT(view), "destroy",
			G_CALLBACK(gtkspell_free), spell);
	g_signal_connect(G_OBJECT(view), "button-press-event",
			G_CALLBACK(button_press_event), spell);
	g_signal_connect(G_OBJECT(view), "populate-popup",
			G_CALLBACK(populate_popup), spell);
	g_signal_connect(G_OBJECT(view), "popup-menu",
			G_CALLBACK(popup_menu_event), spell);

	buffer = gtk_text_view_get_buffer(view);

	g_signal_connect(G_OBJECT(buffer),
			"insert-text",
			G_CALLBACK(insert_text_before), spell);
	g_signal_connect_after(G_OBJECT(buffer),
			"insert-text",
			G_CALLBACK(insert_text_after), spell);
	g_signal_connect_after(G_OBJECT(buffer),
			"delete-range",
			G_CALLBACK(delete_range_after), spell);
	g_signal_connect(G_OBJECT(buffer),
			"mark-set",
			G_CALLBACK(mark_set), spell);

	tagtable = gtk_text_buffer_get_tag_table(buffer);
	spell->tag_highlight = gtk_text_tag_table_lookup(tagtable, GTKSPELL_MISSPELLED_TAG);

	if (spell->tag_highlight == NULL) {
		spell->tag_highlight = gtk_text_buffer_create_tag(buffer,
				GTKSPELL_MISSPELLED_TAG,
#ifdef HAVE_PANGO_UNDERLINE_ERROR
				"underline", PANGO_UNDERLINE_ERROR,
#else
				"foreground", "red", 
				"underline", PANGO_UNDERLINE_SINGLE,
#endif
				NULL);
	}

	/* we create the mark here, but we don't use it until text is
	 * inserted, so we don't really care where iter points.  */
	gtk_text_buffer_get_bounds(buffer, &start, &end);
	spell->mark_insert_start = gtk_text_buffer_create_mark(buffer,
			"gtkspell-insert-start",
			&start, TRUE);
	spell->mark_insert_end = gtk_text_buffer_create_mark(buffer,
			"gtkspell-insert-end",
			&start, TRUE);
	spell->mark_click = gtk_text_buffer_create_mark(buffer,
			"gtkspell-click",
			&start, TRUE);
		
	spell->deferred_check = FALSE;

	/* now check the entire text buffer. */
	gtkspell_recheck_all(spell);
	return spell;
}

static void
gtkspell_free(GtkSpell *spell) {
	GtkTextBuffer *buffer;
	GtkTextTagTable *table;
	GtkTextIter start, end;

	buffer = gtk_text_view_get_buffer(spell->view);
	table = gtk_text_buffer_get_tag_table(buffer);

	gtk_text_buffer_get_bounds(buffer, &start, &end);
	gtk_text_buffer_remove_tag(buffer, spell->tag_highlight, &start, &end);
	gtk_text_tag_table_remove(table, spell->tag_highlight);

	gtk_text_buffer_delete_mark(buffer, spell->mark_insert_start);
	gtk_text_buffer_delete_mark(buffer, spell->mark_insert_end);
	gtk_text_buffer_delete_mark(buffer, spell->mark_click);

	delete_aspell_speller(spell->speller);

	g_signal_handlers_disconnect_matched(spell->view,
			G_SIGNAL_MATCH_DATA,
			0, 0, NULL, NULL,
			spell);
	g_signal_handlers_disconnect_matched(buffer,
			G_SIGNAL_MATCH_DATA,
			0, 0, NULL, NULL,
			spell);
	g_free(spell);
}

/**
 * gtkspell_get_from_text_view:
 * @view: A #GtkTextView.
 *
 * Retrieves the #GtkSpell object attached to a text view.
 *
 * Returns: the #GtkSpell object, or %NULL if there is no #GtkSpell
 * attached to @view.
 */
GtkSpell*
gtkspell_get_from_text_view(GtkTextView *view) {
	return g_object_get_data(G_OBJECT(view), GTKSPELL_OBJECT_KEY);
}

/**
 * gtkspell_detach:
 * @spell: A #GtkSpell.
 *
 * Detaches this #GtkSpell from its text view.  Use
 * gtkspell_get_from_text_view() to retrieve a GtkSpell from a
 * #GtkTextView.
 */
void
gtkspell_detach(GtkSpell *spell) {
	g_return_if_fail(spell != NULL);

	g_object_set_data(G_OBJECT(spell->view), GTKSPELL_OBJECT_KEY, NULL);
	gtkspell_free(spell);
}


