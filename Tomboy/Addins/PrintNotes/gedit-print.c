/* -*- Mode: C; tab-width: 8; indent-tabs-mode: t; c-basic-offset: 8 -*- */
/*
 * gedit-print.c
 * This file is part of gedit
 *
 * Copyright (C) 2000, 2001 Chema Celorio, Paolo Maggi
 * Copyright (C) 2002  Paolo Maggi  
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, 
 * Boston, MA 02111-1307, USA. 
 */
 
/*
 * Modified by the gedit Team, 1998-2002. See the AUTHORS file for a 
 * list of people on the gedit Team.  
 * See the ChangeLog files for a list of changes. 
 */

#ifdef HAVE_CONFIG_H
#include <config.h>
#endif

#include <sys/types.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#include <string.h>	/* For strlen */

#include <glib/gi18n.h>
#include <gtk/gtkframe.h>
#include <gtk/gtkhbox.h>
#include <gtk/gtkvbox.h>
#include <gtk/gtklabel.h>
#include <gtk/gtkmain.h>
#include <gtk/gtkprogressbar.h>
#include <gtk/gtkimage.h>
#include <gtk/gtkwindow.h>
#include <libgnomeprintui/gnome-print-dialog.h>
#include <libgnomeprintui/gnome-print-job-preview.h>

#include "gedit-print.h"
#include "gtksourceprintjob.h"

#ifdef DEBUG
#  define DEBUG_PRINT "DEBUG_PRINT: %s"
#  define gedit_debug(x, y) g_warning(x, y)
#else
#  define gedit_debug(x, y) do {} while (FALSE);
#endif

enum
{
	PREVIEW_NO,
	PREVIEW,
	PREVIEW_FROM_DIALOG
};

typedef struct _GeditPrintJobInfo	GeditPrintJobInfo;

struct _GeditPrintJobInfo 
{
	GtkTextBuffer     *doc;
	
	GtkSourcePrintJob *pjob;
		
	gint               preview;

	gint               range_type;

	gint               first_line_to_print;
	gint               last_line_to_print;

	/* Popup dialog */
	GtkWidget	  *dialog;
	GtkWidget         *label;
	GtkWidget         *progressbar;

	GtkWindow	  *parent;
};

static GeditPrintJobInfo* gedit_print_job_info_new 	(GtkTextView       *view);
static void gedit_print_job_info_destroy		(GeditPrintJobInfo *pji, 
							 gboolean           save_config);
static void gedit_print_real 				(GeditPrintJobInfo *pji, 
							 GtkTextIter       *start, 
							 GtkTextIter       *end, 
							 GtkWindow         *parent);
static void gedit_print_preview_real 			(GeditPrintJobInfo *pji, 
							 GtkTextIter       *start, 
							 GtkTextIter       *end, 
							 GtkWindow         *parent);


static void
gedit_print_job_info_destroy (GeditPrintJobInfo *pji, gboolean save_config)
{
	gedit_debug (DEBUG_PRINT, "");

	g_return_if_fail (pji != NULL);

	if (pji->pjob != NULL)
		g_object_unref (pji->pjob);

	g_free (pji);
}

static GtkWidget *
get_print_dialog (GeditPrintJobInfo *pji, GtkWindow *parent)
{
	GtkWidget *dialog;
	gint selection_flag;
	gint lines;
	GnomePrintConfig *config;

	gedit_debug (DEBUG_PRINT, "");

	g_return_val_if_fail (pji != NULL, NULL);
	
	if (!gtk_text_buffer_get_selection_bounds (GTK_TEXT_BUFFER (pji->doc), NULL, NULL))
		selection_flag = GNOME_PRINT_RANGE_SELECTION_UNSENSITIVE;
	else
		selection_flag = GNOME_PRINT_RANGE_SELECTION;
	
	g_return_val_if_fail(pji->pjob != NULL, NULL);
	config = gtk_source_print_job_get_config (pji->pjob);
	
	dialog = g_object_new (GNOME_TYPE_PRINT_DIALOG, "print_config", config, NULL);
	
	gnome_print_dialog_construct (GNOME_PRINT_DIALOG (dialog), 
				      (guchar *) _("Print"),
			              GNOME_PRINT_DIALOG_RANGE | GNOME_PRINT_DIALOG_COPIES);

	lines = gtk_text_buffer_get_line_count (GTK_TEXT_BUFFER (pji->doc));

	gnome_print_dialog_construct_range_any ( GNOME_PRINT_DIALOG (dialog),
						 (GNOME_PRINT_RANGE_ALL | selection_flag), 
						 NULL, NULL, NULL);

	gtk_window_set_transient_for (GTK_WINDOW (dialog), parent);

	gtk_window_set_modal (GTK_WINDOW (dialog), TRUE); 
	gtk_window_set_destroy_with_parent (GTK_WINDOW (dialog), TRUE);

	return dialog;
}

static void
gedit_print_dialog_response (GtkWidget *dialog, int response, GeditPrintJobInfo *pji)
{
	GtkTextIter start, end;

	pji->range_type = gnome_print_dialog_get_range (GNOME_PRINT_DIALOG (dialog));
	gtk_text_buffer_get_bounds (GTK_TEXT_BUFFER (pji->doc), &start, &end);

	switch (pji->range_type)
	{
	case GNOME_PRINT_RANGE_ALL:
		break;

	case GNOME_PRINT_RANGE_SELECTION:
		gtk_text_buffer_get_selection_bounds (GTK_TEXT_BUFFER (pji->doc),
						      &start, &end);
		break;

	default:
		g_return_if_reached ();
	}

	switch (response)
	{
	case GNOME_PRINT_DIALOG_RESPONSE_PRINT:
		gedit_debug (DEBUG_PRINT, "Print button pressed.");
		pji->preview = PREVIEW_NO;
		gedit_print_real (pji, &start, &end, 
				  gtk_window_get_transient_for (GTK_WINDOW (dialog)));
		gtk_widget_destroy (dialog);
		break;

	case GNOME_PRINT_DIALOG_RESPONSE_PREVIEW:
		gedit_debug (DEBUG_PRINT, "Preview button pressed.");
		pji->preview = PREVIEW_FROM_DIALOG;
		gedit_print_preview_real (pji, &start, &end, GTK_WINDOW (dialog));
		break;

	default:
		gtk_widget_destroy (dialog);
		gedit_print_job_info_destroy (pji, FALSE);
        }
} 

static void
show_printing_dialog (GeditPrintJobInfo *pji, GtkWindow *parent)
{
	GtkWidget *window;
	GtkWidget *frame;
	GtkWidget *hbox;
	GtkWidget *image;
	GtkWidget *vbox;
	GtkWidget *label;
	GtkWidget *progressbar;

	window = gtk_window_new (GTK_WINDOW_TOPLEVEL);
	gtk_window_set_modal (GTK_WINDOW (window), TRUE);
	gtk_window_set_resizable (GTK_WINDOW (window), FALSE);
	gtk_window_set_destroy_with_parent (GTK_WINDOW (window), TRUE);
	gtk_window_set_position (GTK_WINDOW (window), GTK_WIN_POS_CENTER_ON_PARENT);
		
	gtk_window_set_decorated (GTK_WINDOW (window), FALSE);
	gtk_window_set_skip_taskbar_hint (GTK_WINDOW (window), TRUE);
	gtk_window_set_skip_pager_hint (GTK_WINDOW (window), TRUE);
 
	gtk_window_set_transient_for (GTK_WINDOW (window), parent);

	frame = gtk_frame_new (NULL);
	gtk_frame_set_shadow_type (GTK_FRAME (frame), GTK_SHADOW_OUT);
	gtk_container_add (GTK_CONTAINER (window), frame);

	hbox = gtk_hbox_new (FALSE, 12);
 	gtk_container_add (GTK_CONTAINER (frame), hbox);
	gtk_container_set_border_width (GTK_CONTAINER (hbox), 12);

	image = gtk_image_new_from_stock ("gtk-print", GTK_ICON_SIZE_DIALOG);
	gtk_box_pack_start (GTK_BOX (hbox), image, FALSE, FALSE, 0);

	vbox = gtk_vbox_new (FALSE, 12);
	gtk_box_pack_start (GTK_BOX (hbox), vbox, FALSE, FALSE, 0);

	label = gtk_label_new (_("Preparing pages..."));
	gtk_box_pack_start (GTK_BOX (vbox), label, FALSE, FALSE, 0);
	gtk_label_set_justify (GTK_LABEL (label), GTK_JUSTIFY_LEFT);
	gtk_misc_set_alignment (GTK_MISC (label), 0, 0.5);

	progressbar = gtk_progress_bar_new ();
	gtk_box_pack_start (GTK_BOX (vbox), progressbar, FALSE, FALSE, 0);
	
	pji->dialog = window;
	pji->label = label;
	pji->progressbar = progressbar;
	
	gtk_widget_show_all (pji->dialog);

	/* Update UI */
	while (gtk_events_pending ())
		gtk_main_iteration ();
}

static void
page_cb (GtkSourcePrintJob *job, GeditPrintJobInfo *pji)
{
	gchar *str;
	gint page_num = gtk_source_print_job_get_page (pji->pjob);
	gint total = gtk_source_print_job_get_page_count (pji->pjob);

	if (pji->preview != PREVIEW_NO)
		str = g_strdup_printf (_("Rendering page %d of %d..."), page_num, total);
	else
		str = g_strdup_printf (_("Printing page %d of %d..."), page_num, total);

	gtk_label_set_label (GTK_LABEL (pji->label), str);
	g_free (str);

	gtk_progress_bar_set_fraction (GTK_PROGRESS_BAR (pji->progressbar), 
				       1.0 * page_num / total);

	/* Update UI */
	while (gtk_events_pending ())
		gtk_main_iteration ();

}

static void
preview_finished_cb (GtkSourcePrintJob *job, GeditPrintJobInfo *pji)
{
	GnomePrintJob *gjob;
	GtkWidget *preview = NULL;

	gjob = gtk_source_print_job_get_print_job (job);

	preview = gnome_print_job_preview_new (gjob, (guchar *) _("Print preview"));
	if (pji->parent != NULL)
	{
		gtk_window_set_transient_for (GTK_WINDOW (preview), pji->parent);
		gtk_window_set_modal (GTK_WINDOW (preview), TRUE);
	}
	
 	g_object_unref (gjob);

	gtk_widget_destroy (pji->dialog);

	if (pji->preview == PREVIEW)
		gedit_print_job_info_destroy (pji, FALSE);
	else
	{
		g_signal_handlers_disconnect_by_func (pji->pjob, (GCallback) page_cb, pji);
		g_signal_handlers_disconnect_by_func (pji->pjob, (GCallback) preview_finished_cb, pji);
	}
	
	gtk_widget_show (preview);
}

static void
print_finished_cb (GtkSourcePrintJob *job, GeditPrintJobInfo *pji)
{
	GnomePrintJob *gjob;

	gjob = gtk_source_print_job_get_print_job (job);

	gnome_print_job_print (gjob);
	
 	g_object_unref (gjob);

	gtk_widget_destroy (pji->dialog);

	gedit_print_job_info_destroy (pji, TRUE);
}

void 
gedit_print (GtkTextView *view)
{
	GeditPrintJobInfo *pji;
	GtkWidget *dialog;

	gedit_debug (DEBUG_PRINT, "");

	g_return_if_fail (view != NULL);

	pji = gedit_print_job_info_new (view);
	pji->preview = PREVIEW_NO;

	dialog = get_print_dialog (pji, GTK_WINDOW (gtk_widget_get_toplevel (GTK_WIDGET (view))));
	
	g_signal_connect (dialog, "response",
			  G_CALLBACK (gedit_print_dialog_response),
			  pji);

	gtk_widget_show (dialog);
}

static void 
gedit_print_preview_real (GeditPrintJobInfo *pji, 
			  GtkTextIter       *start, 
			  GtkTextIter       *end, 
			  GtkWindow         *parent)
{
	show_printing_dialog (pji, parent);

	pji->parent = parent;

	g_signal_connect (pji->pjob, "begin_page", (GCallback) page_cb, pji);
	g_signal_connect (pji->pjob, "finished", (GCallback) preview_finished_cb, pji);

	if (!gtk_source_print_job_print_range_async (pji->pjob, start, end))
	{
		/* FIXME */
		g_warning ("Async print failed");
		gtk_widget_destroy (pji->dialog);
	}
}

static void 
gedit_print_real (GeditPrintJobInfo *pji, 
		  GtkTextIter       *start, 
		  GtkTextIter       *end, 
		  GtkWindow         *parent)
{
	show_printing_dialog (pji, parent);

	g_signal_connect (pji->pjob, "begin_page", (GCallback) page_cb, pji);
	g_signal_connect (pji->pjob, "finished", (GCallback) print_finished_cb, pji);

	if (!gtk_source_print_job_print_range_async (pji->pjob, start, end))
	{
		/* FIXME */
		g_warning ("Async print failed");
		gtk_widget_destroy (pji->dialog);
	}
}

void 
gedit_print_preview (GtkTextView *view)
{
	GeditPrintJobInfo *pji;
	GtkTextIter start, end;

	gedit_debug (DEBUG_PRINT, "");
		
	g_return_if_fail (view != NULL);

	pji = gedit_print_job_info_new (view);

	gtk_text_buffer_get_bounds (pji->doc, &start, &end);

	pji->preview = PREVIEW;
	gedit_print_preview_real (pji, &start, &end, 
				  GTK_WINDOW (gtk_widget_get_toplevel (GTK_WIDGET (view))));
}

static GeditPrintJobInfo *
gedit_print_job_info_new (GtkTextView* view)
{	
	GtkSourcePrintJob *pjob;
	GnomePrintConfig *config;
	GeditPrintJobInfo *pji;
	PangoContext *pango_context;
	PangoFontDescription *font_desc;
	GtkTextBuffer *buffer;
	
	gedit_debug (DEBUG_PRINT, "");
	
	g_return_val_if_fail (view != NULL, NULL);

	buffer = gtk_text_view_get_buffer (view);
	g_return_val_if_fail (buffer != NULL, NULL);

	config = gnome_print_config_default ();
	g_return_val_if_fail (config != NULL, NULL);

	gnome_print_config_set_int (config, (const guchar *) GNOME_PRINT_KEY_NUM_COPIES, 1);
	gnome_print_config_set_boolean (config, (const guchar *) GNOME_PRINT_KEY_COLLATE, FALSE);

	pjob = gtk_source_print_job_new_with_buffer (config, buffer);
	gnome_print_config_unref (config);

	gtk_source_print_job_set_highlight (pjob, TRUE);
	gtk_source_print_job_set_print_numbers (pjob, FALSE);
	gtk_source_print_job_set_wrap_mode (pjob, gtk_text_view_get_wrap_mode (view));
	gtk_source_print_job_set_tabs_width (pjob, 8);
	
	gtk_source_print_job_set_footer_format (pjob,
						_("Page %N of %Q"), 
						NULL, 
						/* xgettext:no-c-format */
						_("%A %x, %X"), 
						TRUE);

	gtk_source_print_job_set_print_header (pjob, FALSE);
	gtk_source_print_job_set_print_footer (pjob, TRUE);

	pango_context = gtk_widget_get_pango_context (GTK_WIDGET (view));
	font_desc = pango_context_get_font_description (pango_context);

	gtk_source_print_job_set_font_desc (pjob, font_desc);

	pji = g_new0 (GeditPrintJobInfo, 1);

	pji->pjob = pjob;

	pji->doc = buffer;
	pji->preview = PREVIEW_NO;
	pji->range_type = GNOME_PRINT_RANGE_ALL;

	return pji;
}

