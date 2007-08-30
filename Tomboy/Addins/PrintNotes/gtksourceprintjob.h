/* -*- Mode: C; tab-width: 8; indent-tabs-mode: t; c-basic-offset: 8; coding: utf-8 -*- */
/*
 * gtksourceprintjob.h
 * This file is part of GtkSourceView
 *
 * Copyright (C) 2003  Gustavo Giráldez
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
 
#ifndef __GTK_SOURCE_PRINT_JOB_H__
#define __GTK_SOURCE_PRINT_JOB_H__

#include <libgnomeprint/gnome-print-config.h>
#include <libgnomeprint/gnome-print-job.h>
#include <gtk/gtktextbuffer.h>
//#include <gtksourceview/gtksourceview.h>

G_BEGIN_DECLS

#define GTK_TYPE_SOURCE_PRINT_JOB            (gtk_source_print_job_get_type ())
#define GTK_SOURCE_PRINT_JOB(obj)            (G_TYPE_CHECK_INSTANCE_CAST ((obj), GTK_TYPE_SOURCE_PRINT_JOB, GtkSourcePrintJob))
#define GTK_SOURCE_PRINT_JOB_CLASS(klass)    (G_TYPE_CHECK_CLASS_CAST ((klass), GTK_TYPE_SOURCE_PRINT_JOB, GtkSourcePrintJobClass))
#define GTK_IS_SOURCE_PRINT_JOB(obj)         (G_TYPE_CHECK_INSTANCE_TYPE ((obj), GTK_TYPE_SOURCE_PRINT_JOB))
#define GTK_IS_SOURCE_PRINT_JOB_CLASS(klass) (G_TYPE_CHECK_CLASS_TYPE ((klass), GTK_TYPE_SOURCE_PRINT_JOB))
#define GTK_SOURCE_PRINT_JOB_GET_CLASS(obj)  (G_TYPE_INSTANCE_GET_CLASS ((obj), GTK_TYPE_SOURCE_PRINT_JOB, GtkSourcePrintJobClass))

typedef struct _GtkSourcePrintJob         GtkSourcePrintJob;
typedef struct _GtkSourcePrintJobClass    GtkSourcePrintJobClass;
typedef struct _GtkSourcePrintJobPrivate  GtkSourcePrintJobPrivate;

struct _GtkSourcePrintJob
{
	GObject parent_instance;

	GtkSourcePrintJobPrivate *priv;
};

struct _GtkSourcePrintJobClass
{
	GObjectClass parent_class;

	void   (* begin_page)    (GtkSourcePrintJob  *job);
	void   (* finished)      (GtkSourcePrintJob  *job);
};

/* we want the idle handler to run before the view validation, but do
 * not interfere with ui updates */
#define GTK_SOURCE_PRINT_JOB_PRIORITY ((GDK_PRIORITY_REDRAW + GTK_TEXT_VIEW_PRIORITY_VALIDATE) / 2)

GType              gtk_source_print_job_get_type               (void) G_GNUC_CONST;

/* constructor functions */
GtkSourcePrintJob *gtk_source_print_job_new                    (GnomePrintConfig  *config);
GtkSourcePrintJob *gtk_source_print_job_new_with_buffer        (GnomePrintConfig  *config,
								GtkTextBuffer   *buffer);
/* print job basic configuration */
void               gtk_source_print_job_set_config             (GtkSourcePrintJob *job,
								GnomePrintConfig  *config);
GnomePrintConfig  *gtk_source_print_job_get_config             (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_buffer             (GtkSourcePrintJob *job,
								GtkTextBuffer   *buffer);
GtkTextBuffer   *gtk_source_print_job_get_buffer             (GtkSourcePrintJob *job);

/* print job layout and style configuration */
//void               gtk_source_print_job_setup_from_view        (GtkSourcePrintJob *job,
//								GtkSourceView     *view);
void               gtk_source_print_job_set_tabs_width         (GtkSourcePrintJob *job,
								guint              tabs_width);
guint              gtk_source_print_job_get_tabs_width         (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_wrap_mode          (GtkSourcePrintJob *job,
								GtkWrapMode        wrap);
GtkWrapMode        gtk_source_print_job_get_wrap_mode          (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_highlight          (GtkSourcePrintJob *job,
								gboolean           highlight);
gboolean           gtk_source_print_job_get_highlight          (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_font               (GtkSourcePrintJob *job,
								const gchar       *font_name);
gchar             *gtk_source_print_job_get_font               (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_numbers_font       (GtkSourcePrintJob *job,
								const gchar       *font_name);
gchar             *gtk_source_print_job_get_numbers_font       (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_print_numbers      (GtkSourcePrintJob *job,
								guint              interval);
guint              gtk_source_print_job_get_print_numbers      (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_text_margins       (GtkSourcePrintJob *job,
								gdouble            top,
								gdouble            bottom,
								gdouble            left,
								gdouble            right);
void               gtk_source_print_job_get_text_margins       (GtkSourcePrintJob *job,
								gdouble           *top,
								gdouble           *bottom,
								gdouble           *left,
								gdouble           *right);

/* New non-deprecated font-setting API */
void                   gtk_source_print_job_set_font_desc               (GtkSourcePrintJob    *job,
									 PangoFontDescription *desc);
PangoFontDescription  *gtk_source_print_job_get_font_desc               (GtkSourcePrintJob    *job);
void                   gtk_source_print_job_set_numbers_font_desc       (GtkSourcePrintJob    *job,
									 PangoFontDescription *desc);
PangoFontDescription  *gtk_source_print_job_get_numbers_font_desc       (GtkSourcePrintJob    *job);
void                   gtk_source_print_job_set_header_footer_font_desc (GtkSourcePrintJob    *job,
									 PangoFontDescription *desc);
PangoFontDescription  *gtk_source_print_job_get_header_footer_font_desc (GtkSourcePrintJob    *job);

/* printing operations */
GnomePrintJob     *gtk_source_print_job_print                  (GtkSourcePrintJob *job);
GnomePrintJob     *gtk_source_print_job_print_range            (GtkSourcePrintJob *job,
								const GtkTextIter *start,
								const GtkTextIter *end);

/* asynchronous printing */
gboolean           gtk_source_print_job_print_range_async      (GtkSourcePrintJob *job,
								const GtkTextIter *start,
								const GtkTextIter *end);
void               gtk_source_print_job_cancel                 (GtkSourcePrintJob *job);
GnomePrintJob     *gtk_source_print_job_get_print_job          (GtkSourcePrintJob *job);

/* information for asynchronous ops and headers and footers callback */
guint              gtk_source_print_job_get_page               (GtkSourcePrintJob *job);
guint              gtk_source_print_job_get_page_count         (GtkSourcePrintJob *job);
GnomePrintContext *gtk_source_print_job_get_print_context      (GtkSourcePrintJob *job);


/* header and footer */
void               gtk_source_print_job_set_print_header       (GtkSourcePrintJob *job,
								gboolean           setting);
gboolean           gtk_source_print_job_get_print_header       (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_print_footer       (GtkSourcePrintJob *job,
								gboolean           setting);
gboolean           gtk_source_print_job_get_print_footer       (GtkSourcePrintJob *job);
void               gtk_source_print_job_set_header_footer_font (GtkSourcePrintJob *job,
								const gchar       *font_name);
gchar             *gtk_source_print_job_get_header_footer_font (GtkSourcePrintJob *job);
/* format strings are strftime like */
void               gtk_source_print_job_set_header_format      (GtkSourcePrintJob *job,
								const gchar       *left,
								const gchar       *center,
								const gchar       *right,
								gboolean           separator);
void               gtk_source_print_job_set_footer_format      (GtkSourcePrintJob *job,
								const gchar       *left,
								const gchar       *center,
								const gchar       *right,
								gboolean           separator);

G_END_DECLS

#endif /* __GTK_SOURCE_PRINT_JOB_H__ */
