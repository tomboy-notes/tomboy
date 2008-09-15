/* -*- Mode: C; tab-width: 8; indent-tabs-mode: t; c-basic-offset: 8; coding: utf-8 -*- */
/*
 * gtksourceprintjob.c
 * This file is part of GtkSourceView
 *
 * Derived from gedit-print.c
 *
 * Copyright (C) 2000, 2001 Chema Celorio, Paolo Maggi
 * Copyright (C) 2002  Paolo Maggi  
 * Copyright (C) 2003  Gustavo Giráldez
 * Copyright (C) 2004  Red Hat, Inc.
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
 
#ifdef HAVE_CONFIG_H
#include <config.h>
#endif

#include <string.h>
#include <time.h>

#include "gtksourceprintjob.h"

#include <glib/gi18n.h>
#include <gtk/gtkmain.h>
#include <gtk/gtktextview.h>
#include <libgnomeprint/gnome-print-pango.h>

#ifdef ENABLE_PROFILE
#define PROFILE(x) x
#else
#define PROFILE(x)
#endif

#ifdef ENABLE_DEBUG
#define DEBUG(x) x
#else
#define DEBUG(x)
#endif


#define DEFAULT_FONT_NAME   "Monospace 10"
#define DEFAULT_COLOR       0x000000ff

#define CM(v) ((v) * 72.0 / 2.54)
#define A4_WIDTH (210.0 * 72 / 25.4)
#define A4_HEIGHT (297.0 * 72 / 25.4)

#define NUMBERS_TEXT_SEPARATION CM(0.5)

#define HEADER_FOOTER_SIZE      2.5
#define SEPARATOR_SPACING       1.5
#define SEPARATOR_LINE_WIDTH    1.0


typedef struct _TextSegment TextSegment;
typedef struct _Paragraph   Paragraph;
typedef struct _TextStyle   TextStyle;

/* a piece of text (within a paragraph) of the same style */
struct _TextSegment
{
	TextSegment             *next;
	TextStyle               *style;
	gchar                   *text;
};

/* a printable line */
struct _Paragraph
{
	guint                    line_number;
	TextSegment             *segment;
};

/* the style of a TextSegment */
struct _TextStyle
{
	PangoFontDescription    *font_desc;
	GdkColor                *foreground;
	GdkColor                *background;
	gdouble                  scale;
	gboolean                 strikethrough;
	PangoUnderline           underline;
};


struct _GtkSourcePrintJobPrivate
{
	/* General job configuration */
	GnomePrintConfig 	*config;
	GtkTextBuffer           *buffer;
	guint			 tabs_width;
	GtkWrapMode		 wrap_mode;
	gboolean                 highlight;
	PangoLanguage           *language;
	PangoFontDescription	*font;
	PangoFontDescription	*numbers_font;
	guint 			 print_numbers;
	gdouble                  margin_top;
	gdouble                  margin_bottom;
	gdouble                  margin_left;
	gdouble                  margin_right;

	/* Default header and footer configuration */
	gboolean                 print_header;
	gboolean                 print_footer;
	PangoFontDescription	*header_footer_font;
	gchar                   *header_format_left;
	gchar                   *header_format_center;
	gchar                   *header_format_right;
	gboolean                 header_separator;
	gchar                   *footer_format_left;
	gchar                   *footer_format_center;
	gchar                   *footer_format_right;
	gboolean                 footer_separator;

	/* Job data */
	guint                    first_line_number;
	guint                    last_line_number;
	GSList                  *paragraphs;

	/* Job state */
	gboolean                 printing;
	guint                    idle_printing_tag;
	GnomePrintContext	*print_ctxt;
	GnomePrintJob           *print_job;
	PangoContext            *pango_context;
	PangoTabArray           *tab_array;
	gint                     page;
	gint                     page_count;
	gdouble                  available_height;
	GSList                  *current_paragraph;
	gint                     current_paragraph_line;
	guint                    printed_lines;

	/* Cached information - all this information is obtained from
	 * other fields in the configuration */
	GHashTable              *tag_styles;

	gdouble			 page_width;
	gdouble			 page_height;
	/* outer margins */
	gdouble			 doc_margin_top;
	gdouble			 doc_margin_left;
	gdouble			 doc_margin_right;
	gdouble			 doc_margin_bottom;

	gdouble                  header_height;
	gdouble                  footer_height;
	gdouble                  numbers_width;

	/* printable (for the document itself) size */
	gdouble                  text_width;
	gdouble                  text_height;
};


enum
{
	PROP_0,
	PROP_CONFIG,
	PROP_BUFFER,
	PROP_TABS_WIDTH,
	PROP_WRAP_MODE,
	PROP_HIGHLIGHT,
	PROP_FONT,
	PROP_FONT_DESC,
	PROP_NUMBERS_FONT,
	PROP_NUMBERS_FONT_DESC,
	PROP_PRINT_NUMBERS,
	PROP_PRINT_HEADER,
	PROP_PRINT_FOOTER,
	PROP_HEADER_FOOTER_FONT,
	PROP_HEADER_FOOTER_FONT_DESC
};

enum
{
	BEGIN_PAGE = 0,
	FINISHED,
	LAST_SIGNAL
};

static GObjectClass *parent_class = NULL;
static guint 	     print_job_signals [LAST_SIGNAL] = { 0 };

static void     gtk_source_print_job_class_init    (GtkSourcePrintJobClass *klass);
static void     gtk_source_print_job_instance_init (GtkSourcePrintJob      *job);
static void     gtk_source_print_job_finalize      (GObject                *object);
static void     gtk_source_print_job_get_property  (GObject                *object,
						    guint                   property_id,
						    GValue                 *value,
						    GParamSpec             *pspec);
static void     gtk_source_print_job_set_property  (GObject                *object,
						    guint                   property_id,
						    const GValue           *value,
						    GParamSpec             *pspec);
static void     gtk_source_print_job_begin_page    (GtkSourcePrintJob      *job);

static void     default_print_header               (GtkSourcePrintJob      *job,
						    gdouble                 x,
						    gdouble                 y);
static void     default_print_footer               (GtkSourcePrintJob      *job,
						    gdouble                 x,
						    gdouble                 y);


GType
gtk_source_print_job_get_type (void)
{
	static GType our_type = 0;

	if (our_type == 0)
	{
		static const GTypeInfo our_info = {
			sizeof (GtkSourcePrintJobClass),
			NULL,	/* base_init */
			NULL,	/* base_finalize */
			(GClassInitFunc) gtk_source_print_job_class_init,
			NULL,	/* class_finalize */
			NULL,	/* class_data */
			sizeof (GtkSourcePrintJob),
			0,	/* n_preallocs */
			(GInstanceInitFunc) gtk_source_print_job_instance_init
		};

		our_type = g_type_register_static (G_TYPE_OBJECT,
						   "GtkSourcePrintJob",
						   &our_info, 
						   0);
	}
	
	return our_type;
}
	
static void
gtk_source_print_job_class_init (GtkSourcePrintJobClass *klass)
{
	GObjectClass *object_class;

	object_class = G_OBJECT_CLASS (klass);
	parent_class = g_type_class_peek_parent (klass);
		
	object_class->finalize	   = gtk_source_print_job_finalize;
	object_class->get_property = gtk_source_print_job_get_property;
	object_class->set_property = gtk_source_print_job_set_property;

	klass->begin_page = gtk_source_print_job_begin_page;
	klass->finished = NULL;
	
	g_object_class_install_property (object_class,
					 PROP_CONFIG,
					 g_param_spec_object ("config",
							      _("Configuration"),
							      _("Configuration options for "
								"the print job"),
							      GNOME_TYPE_PRINT_CONFIG,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_BUFFER,
					 g_param_spec_object ("buffer",
							      _("Source Buffer"),
							      _("GtkTextBuffer object to print"),
							      GTK_TYPE_TEXT_BUFFER,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_TABS_WIDTH,
					 g_param_spec_uint ("tabs_width",
							    _("Tabs Width"),
							    _("Width in equivalent space "
							      "characters of tabs"),
							    0, 100, 8,
							    G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_WRAP_MODE,
					 g_param_spec_enum ("wrap_mode",
							    _("Wrap Mode"),
							    _("Word wrapping mode"),
							    GTK_TYPE_WRAP_MODE,
							    GTK_WRAP_NONE,
							    G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_HIGHLIGHT,
					 g_param_spec_boolean ("highlight",
							       _("Highlight"),
							       _("Whether to print the "
								 "document with highlighted "
								 "syntax"),
							       TRUE,
							       G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_FONT,
					 g_param_spec_string ("font",
							      _("Font"),
							      _("GnomeFont name to use for the "
								"document text (deprecated)"),
							      NULL,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_FONT_DESC,
					 g_param_spec_boxed ("font_desc",
							     _("Font Description"),
							     _("Font to use for the document text "
							       "(e.g. \"Monospace 10\")"),
							     PANGO_TYPE_FONT_DESCRIPTION,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_NUMBERS_FONT,
					 g_param_spec_string ("numbers_font",
							      _("Numbers Font"),
							      _("GnomeFont name to use for the "
								"line numbers (deprecated)"),
							      NULL,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_NUMBERS_FONT_DESC,
					 g_param_spec_boxed ("numbers_font_desc",
							     _("Numbers Font"),
							     _("Font description to use for the "
							       "line numbers"),
							     PANGO_TYPE_FONT_DESCRIPTION,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_PRINT_NUMBERS,
					 g_param_spec_uint ("print_numbers",
							    _("Print Line Numbers"),
							    _("Interval of printed line numbers "
							      "(0 means no numbers)"),
							    0, 100, 1,
							    G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_PRINT_HEADER,
					 g_param_spec_boolean ("print_header",
							       _("Print Header"),
							       _("Whether to print a header "
								 "in each page"),
							       FALSE,
							       G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_PRINT_FOOTER,
					 g_param_spec_boolean ("print_footer",
							       _("Print Footer"),
							       _("Whether to print a footer "
								 "in each page"),
							       FALSE,
							       G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_HEADER_FOOTER_FONT,
					 g_param_spec_string ("header_footer_font",
							      _("Header and Footer Font"),
							      _("GnomeFont name to use for the header "
								"and footer (deprecated)"),
							      NULL,
							      G_PARAM_READWRITE));
	g_object_class_install_property (object_class,
					 PROP_HEADER_FOOTER_FONT_DESC,
					 g_param_spec_boxed ("header_footer_font_desc",
							     _("Header and Footer Font Description"),
							     _("Font to use for headers and footers "
							       "(e.g. \"Monospace 10\")"),
							     PANGO_TYPE_FONT_DESCRIPTION,
							     G_PARAM_READWRITE));
	
	print_job_signals [BEGIN_PAGE] =
	    g_signal_new ("begin_page",
			  G_OBJECT_CLASS_TYPE (object_class),
			  G_SIGNAL_RUN_LAST,
			  G_STRUCT_OFFSET (GtkSourcePrintJobClass, begin_page),
			  NULL, NULL,
			  g_cclosure_marshal_VOID__VOID,
			  G_TYPE_NONE, 
			  0);
	print_job_signals [FINISHED] =
	    g_signal_new ("finished",
			  G_OBJECT_CLASS_TYPE (object_class),
			  G_SIGNAL_RUN_FIRST,
			  G_STRUCT_OFFSET (GtkSourcePrintJobClass, finished),
			  NULL, NULL,
			  g_cclosure_marshal_VOID__VOID,
			  G_TYPE_NONE, 
			  0);
}

static void
gtk_source_print_job_instance_init (GtkSourcePrintJob *job)
{
	GtkSourcePrintJobPrivate *priv;

	priv = g_new0 (GtkSourcePrintJobPrivate, 1);
	job->priv = priv;

	/* default job configuration */
	priv->config = NULL;
	priv->buffer = NULL;

	priv->tabs_width = 8;
	priv->wrap_mode = GTK_WRAP_NONE;
	priv->highlight = TRUE;
	priv->language = gtk_get_default_language ();
	priv->font = NULL;
	priv->numbers_font = NULL;
	priv->print_numbers = 1;
	priv->margin_top = 0.0;
	priv->margin_bottom = 0.0;
	priv->margin_left = 0.0;
	priv->margin_right = 0.0;

	priv->print_header = FALSE;
	priv->print_footer = FALSE;
	priv->header_footer_font = NULL;
	priv->header_format_left = NULL;
	priv->header_format_center = NULL;
	priv->header_format_right = NULL;
	priv->header_separator = FALSE;
	priv->footer_format_left = NULL;
	priv->footer_format_center = NULL;
	priv->footer_format_right = NULL;
	priv->footer_separator = FALSE;

	/* initial state */
	priv->printing = FALSE;
	priv->print_ctxt = NULL;
	priv->print_job = NULL;
	priv->page = 0;
	priv->page_count = 0;

	priv->first_line_number = 0;
	priv->paragraphs = NULL;
	priv->tag_styles = NULL;

	/* some default, sane values */
	priv->page_width = A4_WIDTH;
	priv->page_height = A4_HEIGHT;
	priv->doc_margin_top = CM (1);
	priv->doc_margin_left = CM (1);
	priv->doc_margin_right = CM (1);
	priv->doc_margin_bottom = CM (1);
}

static void
free_paragraphs (GSList *paras)
{
	while (paras != NULL)
	{
		Paragraph *para = paras->data;
		TextSegment *seg =  para->segment;
		while (seg != NULL)
		{
			TextSegment *next = seg->next;
			g_free (seg->text);
			g_free (seg);
			seg = next;
		}
		g_free (para);
		paras = g_slist_delete_link (paras, paras);
	}
}

static void
gtk_source_print_job_finalize (GObject *object)
{
	GtkSourcePrintJob *job;
	GtkSourcePrintJobPrivate *priv;
	
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (object));
	
	job = GTK_SOURCE_PRINT_JOB (object);
	priv = job->priv;
	
	if (priv != NULL)
	{
		if (priv->config != NULL)
			gnome_print_config_unref (priv->config);
		if (priv->buffer != NULL)
			g_object_unref (priv->buffer);
		if (priv->font != NULL)
			pango_font_description_free (priv->font);
		if (priv->numbers_font != NULL)
			pango_font_description_free (priv->numbers_font);
		if (priv->header_footer_font != NULL)
			pango_font_description_free (priv->header_footer_font);
		g_free (priv->header_format_left);
		g_free (priv->header_format_right);
		g_free (priv->header_format_center);
		g_free (priv->footer_format_left);
		g_free (priv->footer_format_right);
		g_free (priv->footer_format_center);
		
		if (priv->print_ctxt != NULL)
			g_object_unref (priv->print_ctxt);
		if (priv->print_job != NULL)
			g_object_unref (priv->print_job);
		if (priv->pango_context != NULL)
			g_object_unref (priv->pango_context);
		if (priv->tab_array != NULL)
			pango_tab_array_free (priv->tab_array);

		if (priv->paragraphs != NULL)
			free_paragraphs (priv->paragraphs);
		if (priv->tag_styles != NULL)
			g_hash_table_destroy (priv->tag_styles);
		
		g_free (priv);
		job->priv = NULL;
	}
	
	G_OBJECT_CLASS (parent_class)->finalize (object);
}

static void 
gtk_source_print_job_get_property (GObject    *object,
				   guint       prop_id,
				   GValue     *value,
				   GParamSpec *pspec)
{
	GtkSourcePrintJob *job = GTK_SOURCE_PRINT_JOB (object);

	switch (prop_id)
	{
		case PROP_CONFIG:
			g_value_set_object (value, job->priv->config);
			break;
			
		case PROP_BUFFER:
			g_value_set_object (value, job->priv->buffer);
			break;

		case PROP_TABS_WIDTH:
			g_value_set_uint (value, job->priv->tabs_width);
			break;
			
		case PROP_WRAP_MODE:
			g_value_set_enum (value, job->priv->wrap_mode);
			break;

		case PROP_HIGHLIGHT:
			g_value_set_boolean (value, job->priv->highlight);
			break;
			
		case PROP_FONT:
			g_value_take_string (value, gtk_source_print_job_get_font (job));
			break;
			
		case PROP_FONT_DESC:
			g_value_set_boxed (value, gtk_source_print_job_get_font_desc (job));
			break;
			
		case PROP_NUMBERS_FONT:
 			g_value_take_string (value, gtk_source_print_job_get_numbers_font (job));
			break;
			
		case PROP_NUMBERS_FONT_DESC:
 			g_value_set_boxed (value, gtk_source_print_job_get_numbers_font_desc (job));
			break;
			
		case PROP_PRINT_NUMBERS:
			g_value_set_uint (value, job->priv->print_numbers);
			break;
			
		case PROP_PRINT_HEADER:
			g_value_set_boolean (value, job->priv->print_header);
			break;
			
		case PROP_PRINT_FOOTER:
			g_value_set_boolean (value, job->priv->print_footer);
			break;
			
		case PROP_HEADER_FOOTER_FONT:
			g_value_take_string (value,
					     gtk_source_print_job_get_header_footer_font (job));
			break;
			
		case PROP_HEADER_FOOTER_FONT_DESC:
			g_value_set_boxed (value,
					   gtk_source_print_job_get_header_footer_font_desc (job));
			break;
			
		default:
			G_OBJECT_WARN_INVALID_PROPERTY_ID (object, prop_id, pspec);
			break;
	}
}

static void 
gtk_source_print_job_set_property (GObject      *object,
				   guint         prop_id,
				   const GValue *value,
				   GParamSpec   *pspec)
{
	GtkSourcePrintJob *job = GTK_SOURCE_PRINT_JOB (object);

	switch (prop_id)
	{
		case PROP_CONFIG:
			gtk_source_print_job_set_config (job, g_value_get_object (value));
			break;
			
		case PROP_BUFFER:
			gtk_source_print_job_set_buffer (job, g_value_get_object (value));
			break;
			
		case PROP_TABS_WIDTH:
			gtk_source_print_job_set_tabs_width (job, g_value_get_uint (value));
			break;
			
		case PROP_WRAP_MODE:
			gtk_source_print_job_set_wrap_mode (job, g_value_get_enum (value));
			break;

		case PROP_HIGHLIGHT:
			gtk_source_print_job_set_highlight (job, g_value_get_boolean (value));
			break;

		case PROP_FONT:
			gtk_source_print_job_set_font (job, g_value_get_string (value));
			break;

		case PROP_FONT_DESC:
			gtk_source_print_job_set_font_desc (job, g_value_get_boxed (value));
			break;
			
		case PROP_NUMBERS_FONT:
			gtk_source_print_job_set_numbers_font (job, g_value_get_string (value));
			break;
			
		case PROP_NUMBERS_FONT_DESC:
			gtk_source_print_job_set_numbers_font_desc (job, g_value_get_boxed (value));
			break;

		case PROP_PRINT_NUMBERS:
			gtk_source_print_job_set_print_numbers (job, g_value_get_uint (value));
			break;
			
		case PROP_PRINT_HEADER:
			gtk_source_print_job_set_print_header (job, g_value_get_boolean (value));
			break;

		case PROP_PRINT_FOOTER:
			gtk_source_print_job_set_print_footer (job, g_value_get_boolean (value));
			break;

		case PROP_HEADER_FOOTER_FONT:
			gtk_source_print_job_set_header_footer_font (job,
								     g_value_get_string (value));
			break;
			
		case PROP_HEADER_FOOTER_FONT_DESC:
			gtk_source_print_job_set_header_footer_font_desc (job,
									  g_value_get_boxed (value));
			break;

		default:
			G_OBJECT_WARN_INVALID_PROPERTY_ID (object, prop_id, pspec);
			break;
	}
}

static void 
gtk_source_print_job_begin_page (GtkSourcePrintJob *job)
{
	g_return_if_fail (job->priv->printing);
	
	if (job->priv->print_header && job->priv->header_height > 0)
	{
		gdouble x, y;

		x = job->priv->doc_margin_left + job->priv->margin_left;
		y = job->priv->page_height - job->priv->doc_margin_top - job->priv->margin_top;
		default_print_header (job, x, y);
	}

	if (job->priv->print_footer && job->priv->footer_height > 0)
	{
		gdouble x, y;

		x = job->priv->doc_margin_left + job->priv->margin_left;
		y = job->priv->doc_margin_bottom +
			job->priv->margin_bottom +
			job->priv->footer_height;
		default_print_footer (job, x, y);
	}
}

/* ---- gnome-print / Pango convenience functions */

/* Gets the width of a layout in gnome-print coordinates */
static gdouble
get_layout_width (PangoLayout *layout)
{
	gint layout_width;

	pango_layout_get_size (layout, &layout_width, NULL);
	return (gdouble) layout_width / PANGO_SCALE;
}

/* Gets the ascent/descent of a font in gnome-print coordinates */
static void
get_font_ascent_descent (GtkSourcePrintJob    *job,
			 PangoFontDescription *desc,
			 gdouble              *ascent,
			 gdouble              *descent)
{
	PangoFontMetrics *metrics;
	
	metrics = pango_context_get_metrics (job->priv->pango_context,
					     desc,
					     job->priv->language);

	if (ascent)
		*ascent = (gdouble) pango_font_metrics_get_ascent (metrics) / PANGO_SCALE;
	if (descent)
		*descent = (gdouble) pango_font_metrics_get_descent (metrics) / PANGO_SCALE;

	pango_font_metrics_unref (metrics);
}

/* Draws the first line in a layout; we use this for one-line layouts
 * to get baseline alignment */
static void
show_first_layout_line (GnomePrintContext *print_ctxt,
			PangoLayout       *layout)
{
	PangoLayoutLine *line;

	line = pango_layout_get_lines (layout)->data;
	gnome_print_pango_layout_line (print_ctxt, line);
}

static PangoLayout *
get_line_number_layout (GtkSourcePrintJob *job,
			guint              line_number)
{
	PangoLayout *layout;
	gchar *num_str;

	num_str = g_strdup_printf ("%d", line_number);
	layout = pango_layout_new (job->priv->pango_context);
	pango_layout_set_font_description (layout, job->priv->numbers_font);
	pango_layout_set_text (layout, num_str, -1);
	g_free (num_str);

	return layout;
}

/* ---- Configuration functions */

static void
ensure_print_config (GtkSourcePrintJob *job)
{
	if (job->priv->config == NULL)
		job->priv->config = gnome_print_config_default ();
	if (job->priv->font == NULL)
		job->priv->font = pango_font_description_from_string (DEFAULT_FONT_NAME);
}

static gboolean
update_page_size_and_margins (GtkSourcePrintJob *job)
{
	PangoLayout *layout;
	gdouble ascent, descent;
	
	gnome_print_job_get_page_size_from_config (job->priv->config, 
						   &job->priv->page_width,
						   &job->priv->page_height);

	gnome_print_config_get_length (job->priv->config, 
				       (const guchar *) GNOME_PRINT_KEY_PAGE_MARGIN_TOP,
				       &job->priv->doc_margin_top, NULL);
	gnome_print_config_get_length (job->priv->config, 
				       (const guchar *) GNOME_PRINT_KEY_PAGE_MARGIN_BOTTOM,
				       &job->priv->doc_margin_bottom, NULL);
	gnome_print_config_get_length (job->priv->config, 
				       (const guchar *) GNOME_PRINT_KEY_PAGE_MARGIN_LEFT,
				       &job->priv->doc_margin_left, NULL);
	gnome_print_config_get_length (job->priv->config, 
				       (const guchar *) GNOME_PRINT_KEY_PAGE_MARGIN_RIGHT,
				       &job->priv->doc_margin_right, NULL);

	/* set default fonts for numbers and header/footer */
	if (job->priv->numbers_font == NULL)
		job->priv->numbers_font = pango_font_description_copy (job->priv->font);
	
	if (job->priv->header_footer_font == NULL)
		job->priv->header_footer_font = pango_font_description_copy (job->priv->font);
	
	/* calculate numbers width */
	if (job->priv->print_numbers > 0)
	{
		layout = get_line_number_layout (job, job->priv->last_line_number);
		job->priv->numbers_width = get_layout_width (layout) + NUMBERS_TEXT_SEPARATION;
		g_object_unref (layout);
	}
	else
		job->priv->numbers_width = 0.0;

	get_font_ascent_descent (job, job->priv->header_footer_font, &ascent, &descent);

	/* calculate header/footer height */
	if (job->priv->print_header &&
	    (job->priv->header_format_left != NULL ||
	     job->priv->header_format_center != NULL ||
	     job->priv->header_format_right != NULL))
		job->priv->header_height = HEADER_FOOTER_SIZE * (ascent + descent);
	else
		job->priv->header_height = 0.0;

	if (job->priv->print_footer &&
	    (job->priv->footer_format_left != NULL ||
	     job->priv->footer_format_center != NULL ||
	     job->priv->footer_format_right != NULL))
		job->priv->footer_height = HEADER_FOOTER_SIZE * (ascent + descent);
	else
		job->priv->footer_height = 0.0;

	/* verify that the user provided margins are not too excesive
	 * and that we still have room for the text */
	job->priv->text_width = (job->priv->page_width -
				 job->priv->doc_margin_left - job->priv->doc_margin_right -
				 job->priv->margin_left - job->priv->margin_right -
				 job->priv->numbers_width);
	
	job->priv->text_height = (job->priv->page_height -
				  job->priv->doc_margin_top - job->priv->doc_margin_bottom -
				  job->priv->margin_top - job->priv->margin_bottom -
				  job->priv->header_height - job->priv->footer_height);

	/* FIXME: put some saner values than 5cm - Gustavo */
	g_return_val_if_fail (job->priv->text_width > CM(5.0), FALSE);
	g_return_val_if_fail (job->priv->text_height > CM(5.0), FALSE);

	return TRUE;
}

/* We want a uniform tab width for the entire job without regard to style
 * See comments in gtksourceview.c:calculate_real_tab_width
 */
static gint
calculate_real_tab_width (GtkSourcePrintJob *job, guint tab_size, gchar c)
{
	PangoLayout *layout;
	gchar *tab_string;
	gint tab_width = 0;

	if (tab_size == 0)
		return -1;

	tab_string = g_strnfill (tab_size, c);
	layout = pango_layout_new (job->priv->pango_context);
	pango_layout_set_text (layout, tab_string, -1);
	g_free (tab_string);

	pango_layout_get_size (layout, &tab_width, NULL);
	g_object_unref (G_OBJECT (layout));
	
	return tab_width;
}

static gboolean
setup_pango_context (GtkSourcePrintJob *job)
{
	PangoFontMap *font_map;
	gint real_tab_width;

	if (!job->priv->pango_context)
	{
		font_map = gnome_print_pango_get_default_font_map ();
		job->priv->pango_context = gnome_print_pango_create_context (font_map);
	}

	pango_context_set_language (job->priv->pango_context, job->priv->language);
	pango_context_set_font_description (job->priv->pango_context, job->priv->font);

	if (job->priv->tab_array)
	{
		pango_tab_array_free (job->priv->tab_array);
		job->priv->tab_array = NULL;
	}
	
	real_tab_width = calculate_real_tab_width (job, job->priv->tabs_width, ' ');
	if (real_tab_width > 0)
	{
		job->priv->tab_array = pango_tab_array_new (1, FALSE);
		pango_tab_array_set_tab (job->priv->tab_array, 0, PANGO_TAB_LEFT, real_tab_width);
	}
	
	return TRUE;
}

/* ----- Helper functions */

static gchar * 
font_description_to_gnome_font_name (PangoFontDescription *desc)
{
	GnomeFontFace *font_face;
	gchar *retval;

	/* Will always return some font */
	font_face = gnome_font_face_find_closest_from_pango_description (desc);

	retval = g_strdup_printf("%s %f",
				 gnome_font_face_get_name (font_face),
				 (double) pango_font_description_get_size (desc) / PANGO_SCALE);
	g_object_unref (font_face);

	return retval;
}

/*
 * The following routines are duplicated in gedit/gedit/gedit-prefs-manager.c
 */

/* Do this ourselves since gnome_font_find_closest() doesn't call
 * gnome_font_face_find_closest() (probably a gnome-print bug)
 */
static void
face_and_size_from_full_name (const gchar   *name,
			      GnomeFontFace **face,
			      gdouble        *size)
{
	char *copy;
	char *str_size;

	copy = g_strdup (name);
	str_size = strrchr (copy, ' ');
	if (str_size)
	{
		*str_size = 0;
		str_size ++;
		*size = atof (str_size);
	}
	else
	{
		*size = 12;
	}

	*face = gnome_font_face_find_closest ((const guchar *) copy);
	g_free (copy);
}

static PangoFontDescription *
font_description_from_gnome_font_name (const char *font_name)
{
	GnomeFontFace *face;
	PangoFontDescription *desc;
	PangoStyle style;
	PangoWeight weight;
	gdouble size;

	face_and_size_from_full_name (font_name, &face, &size);

	/* Pango and GnomePrint have basically the same numeric weight values */
	weight = (PangoWeight) gnome_font_face_get_weight_code (face);
	style = gnome_font_face_is_italic (face) ? PANGO_STYLE_ITALIC : PANGO_STYLE_NORMAL;

	desc = pango_font_description_new ();
	pango_font_description_set_family (desc, 
					   (const char *) gnome_font_face_get_family_name (face));
	pango_font_description_set_weight (desc, weight);
	pango_font_description_set_style (desc, style);
	pango_font_description_set_size (desc, size * PANGO_SCALE);

	g_object_unref (face);

	return desc;
}

/* ---- TextStyle functions */

static TextStyle * 
text_style_new (GtkSourcePrintJob *job, GtkTextTag *tag)
{
	TextStyle *style;
	gboolean bg_set, fg_set;
	
	g_return_val_if_fail (tag != NULL && GTK_IS_TEXT_TAG (tag), NULL);

	style = g_new0 (TextStyle, 1);

	g_object_get (G_OBJECT (tag),
		      "background_set", &bg_set,
		      "foreground_set", &fg_set,
		      "font_desc", &style->font_desc,
		      "scale", &style->scale,
		      "underline", &style->underline,
		      "strikethrough", &style->strikethrough,
		      NULL);

	if (fg_set)
		g_object_get (G_OBJECT (tag), "foreground_gdk", &style->foreground, NULL);

	if (bg_set)
		g_object_get (G_OBJECT (tag), "background_gdk", &style->background, NULL);
	
	return style;
}

static void
text_style_free (TextStyle *style)
{
	pango_font_description_free (style->font_desc);
	if (style->foreground)
		gdk_color_free (style->foreground);
	if (style->background)
		gdk_color_free (style->background);
	g_free (style);
}

static TextStyle * 
get_style (GtkSourcePrintJob *job, const GtkTextIter *iter)
{
	GSList *tags, *t;
	GtkTextTag *tag = NULL;
	TextStyle *style = NULL;
	
	if (job->priv->tag_styles == NULL)
	{
		job->priv->tag_styles = g_hash_table_new_full (
			g_direct_hash, g_direct_equal,
			NULL, (GDestroyNotify) text_style_free);
	}
	
	/* get the tags at iter */
	tags = gtk_text_iter_get_tags (iter);

	/* now find the GtkSourceTag (if any) which applies at iter */
	/* FIXME: this makes the assumption that the style at a given
	 * iter is only determined by one GtkSourceTag (the one with
	 * highest priority).  This is true for now, but could change
	 * in the future - Gustavo */
	t = tags;
	while (t != NULL)
	{
		if (GTK_IS_TEXT_TAG (t->data))
			tag = t->data;
		t = g_slist_next (t);
	}
	g_slist_free (tags);

	/* now we lookup the tag style in the cache */
	if (tag != NULL)
	{
		style = g_hash_table_lookup (job->priv->tag_styles, tag);
		if (style == NULL)
		{
			/* create a style for the tag and cache it */
			style = text_style_new (job, tag);
			g_hash_table_insert (job->priv->tag_styles, tag, style);
		}
	}

	return style;
}

/* ----- Text fetching functions */

static gboolean 
get_text_simple (GtkSourcePrintJob *job,
		 GtkTextIter       *start,
		 GtkTextIter       *end)
{
	GtkTextIter iter;

	while (gtk_text_iter_compare (start, end) < 0)
	{
		Paragraph *para;
		TextSegment *seg;
		
		/* get a line of text */
		iter = *start;
		if (!gtk_text_iter_ends_line (&iter))
			gtk_text_iter_forward_to_line_end (&iter);
		
		if (gtk_text_iter_compare (&iter, end) > 0)
			iter = *end;

		
		seg = g_new0 (TextSegment, 1);
		seg->next = NULL;  /* only one segment per line, since there's no style change */
		seg->style = NULL; /* use default style */
		/* FIXME: handle invisible text properly.  This also
		 * assumes the text has no embedded images and
		 * stuff */
		seg->text = gtk_text_iter_get_slice (start, &iter);

		para = g_new0 (Paragraph, 1);
		para->segment = seg;

		/* add the line of text to the job */
		job->priv->paragraphs = g_slist_prepend (job->priv->paragraphs, para);

		gtk_text_iter_forward_line (&iter);
		
		/* advance to next line */
		*start = iter;
	}
	job->priv->paragraphs = g_slist_reverse (job->priv->paragraphs);

	return TRUE;
}

static gboolean 
get_text_with_style (GtkSourcePrintJob *job,
		     GtkTextIter       *start,
		     GtkTextIter       *end)
{
	GtkTextIter limit, next_toggle;
	gboolean have_toggle;
	
	/* make sure the region to print is highlighted */
	/*_gtk_source_buffer_highlight_region (job->priv->buffer, start, end, TRUE); */

	next_toggle = *start;
	have_toggle = gtk_text_iter_forward_to_tag_toggle (&next_toggle, NULL);
	
	/* FIXME: handle invisible text properly.  This also assumes
	 * the text has no embedded images and stuff */
	while (gtk_text_iter_compare (start, end) < 0)
	{
		TextStyle *style;
		TextSegment *seg;
		Paragraph *para;
		
		para = g_new0 (Paragraph, 1);

		/* get the style at the start of the line */
		style = get_style (job, start);

		/* get a line of text - limit points to the end of the line */
		limit = *start;
		if (!gtk_text_iter_ends_line (&limit))
			gtk_text_iter_forward_to_line_end (&limit);
		
		if (gtk_text_iter_compare (&limit, end) > 0)
			limit = *end;

		/* create the first segment for the line */
		para->segment = seg = g_new0 (TextSegment, 1);
		seg->style = style;

		/* while the next tag toggle is within the line, we check to see
		 * if the style has changed at each tag toggle position, and if so,
		 * create new segments */
		while (have_toggle && gtk_text_iter_compare (&next_toggle, &limit) < 0)
		{
			/* check style changes */
			style = get_style (job, &next_toggle);
			if (style != seg->style)
			{
				TextSegment *new_seg;
				
				/* style has changed, thus we need to
				 * create a new segment */
				/* close the current segment */
				seg->text = gtk_text_iter_get_slice (start, &next_toggle);
				*start = next_toggle;
				
				new_seg = g_new0 (TextSegment, 1);
				seg->next = new_seg;
				seg = new_seg;
				seg->style = style;
			}

			have_toggle = gtk_text_iter_forward_to_tag_toggle (&next_toggle, NULL);			
		}
		
		/* close the line */
		seg->next = NULL;
		seg->text = gtk_text_iter_get_slice (start, &limit);

		/* add the line of text to the job */
		job->priv->paragraphs = g_slist_prepend (job->priv->paragraphs, para);

		/* advance to next line */
		*start = limit;
		gtk_text_iter_forward_line (start);

		if (gtk_text_iter_compare (&next_toggle, start) < 0) {
			next_toggle = *start;
			have_toggle = gtk_text_iter_forward_to_tag_toggle (&next_toggle, NULL);
		}
	}
	job->priv->paragraphs = g_slist_reverse (job->priv->paragraphs);

	return TRUE;
}

static gboolean 
get_text_to_print (GtkSourcePrintJob *job,
		   const GtkTextIter *start,
		   const GtkTextIter *end)
{
	GtkTextIter _start, _end;
	gboolean retval;
	
	g_return_val_if_fail (start != NULL && end != NULL, FALSE);
	g_return_val_if_fail (job->priv->buffer != NULL, FALSE);

	_start = *start;
	_end = *end;

	/* erase any previous data */
	if (job->priv->paragraphs != NULL)
	{
		free_paragraphs (job->priv->paragraphs);
		job->priv->paragraphs = NULL;
	}
	if (job->priv->tag_styles != NULL)
	{
		g_hash_table_destroy (job->priv->tag_styles);
		job->priv->tag_styles = NULL;
	}

	/* provide ordered iters */
	gtk_text_iter_order (&_start, &_end);

	/* save the first and last line numbers for future reference */
	job->priv->first_line_number = gtk_text_iter_get_line (&_start) + 1;
	job->priv->last_line_number = gtk_text_iter_get_line (&_end) + 1;

	if (!job->priv->highlight)
		retval = get_text_simple (job, &_start, &_end);
	else
		retval = get_text_with_style (job, &_start, &_end);

	if (retval && job->priv->paragraphs == NULL)
	{
		Paragraph *para;
		TextSegment *seg;
		
		/* add an empty line to allow printing empty documents */
		seg = g_new0 (TextSegment, 1);
		seg->next = NULL;
		seg->style = NULL; /* use default style */
		seg->text = g_strdup ("");

		para = g_new0 (Paragraph, 1);
		para->segment = seg;

		job->priv->paragraphs = g_slist_prepend (job->priv->paragraphs, para);
	}

	return retval;
}

/* ----- Pagination functions */

static void
add_attribute_to_list (PangoAttribute *attr, 
		       PangoAttrList  *list,
		       guint           index,
		       gsize           len)
{
	attr->start_index = index;
	attr->end_index = index + len;
	pango_attr_list_insert (list, attr);
}

static PangoLayout *
create_layout_for_para (GtkSourcePrintJob *job,
			Paragraph         *para)
{
	GString *text;
	PangoLayout *layout;
	PangoAttrList *attrs;
	TextSegment *seg;
	gint index;

	text = g_string_new (NULL);
	attrs = pango_attr_list_new ();
	
	seg = para->segment;
	index = 0;

	while (seg != NULL)
	{
		gsize seg_len = strlen (seg->text);
		g_string_append (text, seg->text);

		if (seg->style)
		{
			PangoAttribute *attr;

			attr = pango_attr_font_desc_new (seg->style->font_desc);
			add_attribute_to_list (attr, attrs, index, seg_len);

			if (seg->style->scale != PANGO_SCALE_MEDIUM) 
			{
				attr = pango_attr_scale_new (seg->style->scale);
				add_attribute_to_list (attr, attrs, index, seg_len);
			}

			if (seg->style->foreground)
			{
				attr = pango_attr_foreground_new (seg->style->foreground->red,
								  seg->style->foreground->green,
								  seg->style->foreground->blue);
				add_attribute_to_list (attr, attrs, index, seg_len);
			}

			if (seg->style->background)
			{
				attr = pango_attr_background_new (seg->style->background->red,
								  seg->style->background->green,
								  seg->style->background->blue);
				add_attribute_to_list (attr, attrs, index, seg_len);
			}

			if (seg->style->strikethrough)
			{
				attr = pango_attr_strikethrough_new (TRUE);
				add_attribute_to_list (attr, attrs, index, seg_len);
			}

			if (seg->style->underline != PANGO_UNDERLINE_NONE &&
			    seg->style->underline != PANGO_UNDERLINE_ERROR)
			{
				attr = pango_attr_underline_new (seg->style->underline);
				add_attribute_to_list (attr, attrs, index, seg_len);
			}
		}

		index += seg_len;
		seg = seg->next;
	}

	layout = pango_layout_new (job->priv->pango_context);
	
/*	if (job->priv->wrap_mode != GTK_WRAP_NONE)*/
		pango_layout_set_width (layout, job->priv->text_width * PANGO_SCALE);
	
	switch (job->priv->wrap_mode)	{
	case GTK_WRAP_CHAR:
		pango_layout_set_wrap (layout, PANGO_WRAP_CHAR);
		break;
	case GTK_WRAP_WORD:
		pango_layout_set_wrap (layout, PANGO_WRAP_WORD);
		break;
	case GTK_WRAP_WORD_CHAR:
		pango_layout_set_wrap (layout, PANGO_WRAP_WORD_CHAR);
		break;
	case GTK_WRAP_NONE:
		/* FIXME: hack 
		 * Ellipsize the paragraph when text wrapping is disabled.
		 * Another possibility would be to set the width so the text 
		 * breaks into multiple lines, and paginate/render just the 
		 * first one.
		 * See also Comment #23 by Owen on bug #143874.
		 */

		/* orph says to comment this out and commit it.
		   PANGO_ELLIPSIZE_END is not available in pango
		   1.4.1, at least, and he says this code is never
		   used. */
		/*pango_layout_set_ellipsize (layout, PANGO_ELLIPSIZE_END);*/

		break;
	}

	if (job->priv->tab_array)
		pango_layout_set_tabs (layout, job->priv->tab_array);
	
	pango_layout_set_text (layout, text->str, text->len);
	pango_layout_set_attributes (layout, attrs);

	/* FIXME: <horrible-hack> 
	 * For empty paragraphs, pango_layout_iter_get_baseline() returns 0,
	 * so I check this condition and add a space character to force 
	 * the calculation of the baseline. I don't like that, but I
	 * didn't find a better way to do it. Note that a paragraph is 
	 * considered empty either when it has no characters, or when 
	 * it only has tabs.
	 * See comment #22 and #23 on bug #143874.
	 */
	if (job->priv->print_numbers > 0)
	{
		PangoLayoutIter *iter;
		iter = pango_layout_get_iter (layout);
		if (pango_layout_iter_get_baseline (iter) == 0)
		{
			g_string_append_c (text, ' ');
			pango_layout_set_text (layout, text->str, text->len);
		}
		pango_layout_iter_free (iter);
	}
	/* FIXME: </horrible-hack> */
	
	g_string_free (text, TRUE);
	pango_attr_list_unref (attrs);

	return layout;
}

/* The break logic in this function needs to match that in print_paragraph */
static void
paginate_paragraph (GtkSourcePrintJob *job,
		    Paragraph         *para)
{
	PangoLayout *layout;
	PangoLayoutIter *iter;
	PangoRectangle logical_rect;
	gdouble max;
	gdouble page_skip;

	layout = create_layout_for_para (job, para);

	iter = pango_layout_get_iter (layout);

	max = 0;
	page_skip = 0;

	do
	{
		pango_layout_iter_get_line_extents (iter, NULL, &logical_rect);
		max = (gdouble) (logical_rect.y + logical_rect.height) / PANGO_SCALE;
		
		if (max - page_skip > job->priv->available_height)
		{
			/* "create" a new page */
			job->priv->page_count++;
			job->priv->available_height = job->priv->text_height;
			page_skip = (gdouble) logical_rect.y / PANGO_SCALE;
		}

	}
	while (pango_layout_iter_next_line (iter));

	job->priv->available_height -= max - page_skip;
	
	pango_layout_iter_free (iter);
	g_object_unref (layout);
}

static gboolean 
paginate_text (GtkSourcePrintJob *job)
{
	GSList *l;
	guint line_number;
	
	/* set these to zero so the first break_line creates a new page */
	job->priv->page_count = 0;
	job->priv->available_height = 0;
	line_number = job->priv->first_line_number;
	l = job->priv->paragraphs;
	while (l != NULL)
	{
		Paragraph *para = l->data;

		para->line_number = line_number;
		paginate_paragraph (job, para);
		
		line_number++;
		l = g_slist_next (l);
	}

	/* FIXME: do we have any error condition which can force us to
	 * return %FALSE? - Gustavo */
	return TRUE;
}

/* ---- Printing functions */

static void
begin_page (GtkSourcePrintJob *job)
{
	gnome_print_beginpage (job->priv->print_ctxt, NULL);

	g_signal_emit (job, print_job_signals [BEGIN_PAGE], 0);
}

static void
end_page (GtkSourcePrintJob *job)
{
	gnome_print_showpage (job->priv->print_ctxt);
}

static void 
print_line_number (GtkSourcePrintJob *job,
		   guint              line_number,
		   gdouble            x,
		   gdouble            y)
{
	PangoLayout *layout;

	layout = get_line_number_layout (job, line_number);

	x = x + job->priv->numbers_width - get_layout_width (layout) - NUMBERS_TEXT_SEPARATION;
	gnome_print_moveto (job->priv->print_ctxt, x, y);
	
	show_first_layout_line (job->priv->print_ctxt, layout);
	
	g_object_unref (layout);
}	

/* The break logic in this function needs to match that in paginate_paragraph
 *
 * @start_line is the first line in the paragraph to print
 * @y is updated to the position after the portion of the paragraph we printed
 * @baseline_out is set to the baseline of the first line of the paragraph
 *   if we printed it. (And not set otherwise)
 * 
 * Returns the first unprinted line in the paragraph (unprinted because it
 * flowed onto the next page) or -1 if the entire paragraph was printed.
 */
static gint
print_paragraph (GtkSourcePrintJob *job,
		 Paragraph         *para,
		 gint               start_line,
		 gdouble            x,
		 gdouble           *y,
		 gdouble           *baseline_out,
		 gboolean           force_fit)
{
	PangoLayout *layout;
	PangoLayoutIter *iter;
	PangoRectangle logical_rect;
	int current_line;
	gdouble max;
	gdouble page_skip;
	gdouble baseline;
	int result = -1;
	
	layout = create_layout_for_para (job, para);

	iter = pango_layout_get_iter (layout);

	/* Skip over lines already printed on previous page(s) */
	for (current_line = 0; current_line < start_line; current_line++)
		pango_layout_iter_next_line (iter);

	max = 0;
	page_skip = 0;
	
	do
	{
		pango_layout_iter_get_line_extents (iter, NULL, &logical_rect);
		max = (gdouble) (logical_rect.y + logical_rect.height) / PANGO_SCALE;
		
		if (current_line == start_line)
			page_skip = (gdouble) logical_rect.y / PANGO_SCALE;

		if (max - page_skip > job->priv->available_height)
		{
			result = current_line; /* Save position for next page */
			break;
		}

		baseline = (gdouble) pango_layout_iter_get_baseline (iter) / PANGO_SCALE;
		baseline = *y + page_skip - baseline; /* Adjust to global coordinates */
		if (current_line == 0)
			*baseline_out = baseline;
		
		gnome_print_moveto (job->priv->print_ctxt,
				    x + (gdouble) logical_rect.x / PANGO_SCALE,
				    baseline);
		gnome_print_pango_layout_line (job->priv->print_ctxt,
					       pango_layout_iter_get_line (iter));

		current_line++;
	}
	while (pango_layout_iter_next_line (iter));

	job->priv->available_height -= max - page_skip;
	*y -= max - page_skip;

	pango_layout_iter_free (iter);
	g_object_unref (layout);
	
	return result;
}

static void
print_page (GtkSourcePrintJob *job)
{
	GSList *l;
	gdouble x, y;
	gint line;
	gboolean force_fit = TRUE;
	
	job->priv->page++;
	
	
	begin_page (job);
	job->priv->available_height = job->priv->text_height;

	y = job->priv->page_height -
		job->priv->doc_margin_top - job->priv->margin_top -
		job->priv->header_height;
	x = job->priv->doc_margin_left + job->priv->margin_left +
		job->priv->numbers_width;
	l = job->priv->current_paragraph;
	line = job->priv->current_paragraph_line;

	while (l != NULL)
	{
		Paragraph *para = l->data;
		gdouble baseline = 0;
		gint last_line = line;
		
		line = print_paragraph (job, para, line, x, &y, &baseline, force_fit);

		if (last_line == 0 && line != 0)
		{
			/* We printed the first line of a paragraph */
			if (job->priv->print_numbers > 0 &&
			    ((para->line_number % job->priv->print_numbers) == 0))
				print_line_number (job,
						   para->line_number,
						   job->priv->doc_margin_left +
						   job->priv->margin_left,
						   baseline);

			job->priv->printed_lines++;
		}

		if (line >= 0)
			break;	/* Didn't all fit on this page */
		
		l = l->next;
		line = 0;
		force_fit = FALSE;
	}
	end_page (job);
	job->priv->current_paragraph = l;
	job->priv->current_paragraph_line = line;
}

static void
setup_for_print (GtkSourcePrintJob *job)
{
	job->priv->current_paragraph = job->priv->paragraphs;
	job->priv->page = 0;
	job->priv->printed_lines = 0;

	if (job->priv->print_job != NULL)
		g_object_unref (job->priv->print_job);
	if (job->priv->print_ctxt != NULL)
		g_object_unref (job->priv->print_ctxt);
	
	job->priv->print_job = gnome_print_job_new (job->priv->config);
	job->priv->print_ctxt = gnome_print_job_get_context (job->priv->print_job);

	gnome_print_pango_update_context (job->priv->pango_context, job->priv->print_ctxt);
}

static void
print_job (GtkSourcePrintJob *job)
{
	while (job->priv->current_paragraph != NULL)
		print_page (job);

	gnome_print_job_close (job->priv->print_job);
}

static gboolean
idle_printing_handler (GtkSourcePrintJob *job)
{
	g_assert (job->priv->current_paragraph != NULL);

	print_page (job);

	if (job->priv->current_paragraph == NULL)
	{
		gnome_print_job_close (job->priv->print_job);
		job->priv->printing = FALSE;
		job->priv->idle_printing_tag = 0;

		g_signal_emit (job, print_job_signals [FINISHED], 0);
		/* after this the print job object is possibly
		 * destroyed (common use case) */
		
		return FALSE;
	}
	return TRUE;
}


/* Public API ------------------- */

/**
 * gtk_source_print_job_new:
 * @config: an optional #GnomePrintConfig object.
 * 
 * Creates a new print job object, initially setting the print configuration.
 * 
 * Return value: the new print job object.
 **/
GtkSourcePrintJob *
gtk_source_print_job_new (GnomePrintConfig  *config)
{
	GtkSourcePrintJob *job;

	g_return_val_if_fail (config == NULL || GNOME_IS_PRINT_CONFIG (config), NULL);

	job = GTK_SOURCE_PRINT_JOB (g_object_new (GTK_TYPE_SOURCE_PRINT_JOB, NULL));
	if (config != NULL)
		gtk_source_print_job_set_config (job, config);

	return job;
}

/**
 * gtk_source_print_job_new_with_buffer:
 * @config: an optional #GnomePrintConfig.
 * @buffer: the #GtkTextBuffer to print (might be %NULL).
 * 
 * Creates a new print job to print @buffer.
 * 
 * Return value: a new print job object.
 **/
GtkSourcePrintJob *
gtk_source_print_job_new_with_buffer (GnomePrintConfig  *config,
				      GtkTextBuffer   *buffer)
{
	GtkSourcePrintJob *job;

	g_return_val_if_fail (config == NULL || GNOME_IS_PRINT_CONFIG (config), NULL);
	g_return_val_if_fail (buffer == NULL || GTK_IS_TEXT_BUFFER (buffer), NULL);

	job = gtk_source_print_job_new (config);
	if (buffer != NULL)
		gtk_source_print_job_set_buffer (job, buffer);

	return job;
}

/* --- print job basic configuration */

/**
 * gtk_source_print_job_set_config:
 * @job: a #GtkSourcePrintJob.
 * @config: a #GnomePrintConfig object to get printing configuration from.
 * 
 * Sets the print configuration for the job.  If you don't set a
 * configuration object for the print job, when needed one will be
 * created with gnome_print_config_default().
 **/
void
gtk_source_print_job_set_config (GtkSourcePrintJob *job,
				 GnomePrintConfig  *config)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (GNOME_IS_PRINT_CONFIG (config));
	g_return_if_fail (!job->priv->printing);
	
	if (config == job->priv->config)
		return;
	
	if (job->priv->config != NULL)
		gnome_print_config_unref (job->priv->config);

	job->priv->config = config;
	gnome_print_config_ref (config);

	g_object_notify (G_OBJECT (job), "config");
}

/**
 * gtk_source_print_job_get_config:
 * @job: a #GtkSourcePrintJob.
 * 
 * Gets the current #GnomePrintConfig the print job will use.  If not
 * previously set, this will create a default configuration and return
 * it.  The returned object reference is owned by the print job.
 * 
 * Return value: the #GnomePrintConfig for the print job.
 **/
GnomePrintConfig * 
gtk_source_print_job_get_config (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	ensure_print_config (job);
	
	return job->priv->config;
}

/**
 * gtk_source_print_job_set_buffer:
 * @job: a #GtkSourcePrintJob.
 * @buffer: a #GtkTextBuffer.
 * 
 * Sets the #GtkTextBuffer the print job will print.  You need to
 * specify a buffer to print, either by the use of this function or by
 * creating the print job with gtk_source_print_job_new_with_buffer().
 **/
void 
gtk_source_print_job_set_buffer (GtkSourcePrintJob *job,
				 GtkTextBuffer   *buffer)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (GTK_IS_TEXT_BUFFER (buffer));
	g_return_if_fail (!job->priv->printing);

	if (buffer == job->priv->buffer)
		return;
	
	if (job->priv->buffer != NULL)
		g_object_unref (job->priv->buffer);

	job->priv->buffer = buffer;
	g_object_ref (buffer);

	g_object_notify (G_OBJECT (job), "buffer");
}

/**
 * gtk_source_print_job_get_buffer:
 * @job: a #GtkSourcePrintJob.
 * 
 * Gets the #GtkTextBuffer the print job would print.  The returned
 * object reference (if non %NULL) is owned by the job object and
 * should not be unreferenced.
 * 
 * Return value: the #GtkTextBuffer to print.
 **/
GtkTextBuffer *
gtk_source_print_job_get_buffer (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	return job->priv->buffer;
}

/* --- print job layout and style configuration */

/**
 * gtk_source_print_job_set_tabs_width:
 * @job: a #GtkSourcePrintJob.
 * @tabs_width: the number of equivalent spaces for a tabulation.
 * 
 * Sets the width (in equivalent spaces) of tabulations for the
 * printed text.  The width in printing units will be calculated as
 * the width of a string containing @tabs_width spaces of the default
 * font.  Tabulation stops are set for the full width of printed text.
 **/
void 
gtk_source_print_job_set_tabs_width (GtkSourcePrintJob *job,
				     guint              tabs_width)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	if (tabs_width == job->priv->tabs_width)
		return;
	
	job->priv->tabs_width = tabs_width;

	g_object_notify (G_OBJECT (job), "tabs_width");
}

/**
 * gtk_source_print_job_get_tabs_width:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the configured width (in equivalent spaces) of
 * tabulations.  The default value is 8.
 * 
 * Return value: the width (in equivalent spaces) of a tabulation.
 **/
guint 
gtk_source_print_job_get_tabs_width (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), 0);

	return job->priv->tabs_width;
}

/**
 * gtk_source_print_job_set_wrap_mode:
 * @job: a #GtkSourcePrintJob.
 * @wrap: the wrap mode.
 * 
 * Sets the wrap mode for lines of text larger than the printable
 * width.  See #GtkWrapMode for a definition of the possible values.
 **/
void 
gtk_source_print_job_set_wrap_mode (GtkSourcePrintJob *job,
				    GtkWrapMode        wrap)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	if (wrap == job->priv->wrap_mode)
		return;
	
	job->priv->wrap_mode = wrap;

	g_object_notify (G_OBJECT (job), "wrap_mode");
}

/**
 * gtk_source_print_job_get_wrap_mode:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the wrapping style for text lines wider than the
 * printable width.  The default is no wrapping.
 * 
 * Return value: the current wrapping mode for the print job.
 **/
GtkWrapMode 
gtk_source_print_job_get_wrap_mode (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), GTK_WRAP_NONE);

	return job->priv->wrap_mode;
}

/**
 * gtk_source_print_job_set_highlight:
 * @job: a #GtkSourcePrintJob.
 * @highlight: %TRUE if the printed text should be highlighted.
 * 
 * Sets whether the printed text will be highlighted according to the
 * buffer rules.  Both color and font style are applied.
 **/
void 
gtk_source_print_job_set_highlight (GtkSourcePrintJob *job,
				    gboolean           highlight)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	highlight = (highlight != FALSE);
	
	if (highlight == job->priv->highlight)
		return;
	
	job->priv->highlight = highlight;

	g_object_notify (G_OBJECT (job), "highlight");
}

/**
 * gtk_source_print_job_get_highlight:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines if the job is configured to print the text highlighted
 * with colors and font styles.  Note that highlighting will happen
 * only if the buffer to print has highlighting activated.
 * 
 * Return value: %TRUE if the printed output will be highlighted.
 **/
gboolean 
gtk_source_print_job_get_highlight (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), FALSE);

	return job->priv->highlight;
}

/**
 * gtk_source_print_job_set_font_desc:
 * @job: a #GtkSourcePrintJob.
 * @desc: the #PangoFontDescription for the default font
 * 
 * Sets the default font for the printed text.
 **/
void 
gtk_source_print_job_set_font_desc (GtkSourcePrintJob    *job,
				    PangoFontDescription *desc)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (desc != NULL);
	g_return_if_fail (!job->priv->printing);

	desc = pango_font_description_copy (desc);
	if (job->priv->font != NULL)
		pango_font_description_free (job->priv->font);
	job->priv->font = desc;
	g_object_freeze_notify (G_OBJECT (job));
	g_object_notify (G_OBJECT (job), "font");
	g_object_notify (G_OBJECT (job), "font_desc");
	g_object_thaw_notify (G_OBJECT (job));
}

/**
 * gtk_source_print_job_set_font:
 * @job: a #GtkSourcePrintJob.
 * @font_name: the name of the default font.
 * 
 * Sets the default font for the printed text.  @font_name should be a
 * <emphasis>full font name</emphasis> GnomePrint can understand
 * (e.g. &quot;Monospace Regular 10.0&quot;).
 *
 * Note that @font_name is a #GnomeFont name not a Pango font
 * description string. This function is deprecated since #GnomeFont is
 * no longer used when implementing printing for GtkSourceView; you
 * should use gtk_source_print_job_set_font_desc() instead.
 **/
void 
gtk_source_print_job_set_font (GtkSourcePrintJob *job,
			       const gchar       *font_name)
{
	PangoFontDescription *desc;
	
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (font_name != NULL);
	g_return_if_fail (!job->priv->printing);

	desc = font_description_from_gnome_font_name (font_name);
	if (desc)
	{
		gtk_source_print_job_set_font_desc (job, desc);
		pango_font_description_free (desc);
	}
}

/**
 * gtk_source_print_job_get_font_desc:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the default font to be used for the printed text.  The
 * returned string is of the form &quot;Fontfamily Style Size&quot;,
 * for example &quot;Monospace Regular 10.0&quot;.  The returned value
 * should be freed when no longer needed.
 * 
 * Return value: the current text font description. This value is
 *  owned by the job and must not be modified or freed.
 **/
PangoFontDescription *
gtk_source_print_job_get_font_desc (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	ensure_print_config (job);
	
	return job->priv->font;
}

/**
 * gtk_source_print_job_get_font:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the default font to be used for the printed text.  The
 * returned string is of the form &quot;Fontfamily Style Size&quot;,
 * for example &quot;Monospace Regular 10.0&quot;.  The returned value
 * should be freed when no longer needed.
 * 
 * Note that the result is a #GnomeFont name not a Pango font
 * description string. This function is deprecated since #GnomeFont is
 * no longer used when implementing printing for GtkSourceView; you
 * should use gtk_source_print_job_get_font_desc() instead.
 *
 * Return value: a newly allocated string with the name of the current
 * text font.
 **/
gchar *
gtk_source_print_job_get_font (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	ensure_print_config (job);
	
	return font_description_to_gnome_font_name (job->priv->font);
}

/**
 * gtk_source_print_job_setup_from_view:
 * @job: a #GtkSourcePrintJob.
 * @view: a #GtkSourceView to get configuration from.
 * 
 * Convenience function to set several configuration options at once,
 * so that the printed output matches @view.  The options set are
 * buffer (if not set already), tabs width, highlighting, wrap mode
 * and default font.
 **/
void 
gtk_source_print_job_setup_from_view (GtkSourcePrintJob *job,
				      GtkTextView       *view)
{
	GtkTextBuffer *buffer = NULL;
	PangoContext *pango_context;
	
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (GTK_IS_TEXT_VIEW (view));
	g_return_if_fail (!job->priv->printing);

	buffer = gtk_text_view_get_buffer (view);
	
	if (job->priv->buffer == NULL && buffer != NULL)
		gtk_source_print_job_set_buffer (job, buffer);

	/* gtk_source_print_job_set_tabs_width (job, gtk_source_view_get_tabs_width (view)); */
	gtk_source_print_job_set_highlight (job, TRUE);
	gtk_source_print_job_set_wrap_mode (job, gtk_text_view_get_wrap_mode (view));

	pango_context = gtk_widget_get_pango_context (GTK_WIDGET (view));
	gtk_source_print_job_set_font_desc (job, 
					    pango_context_get_font_description (pango_context));
}

/**
 * gtk_source_print_job_set_numbers_font_desc:
 * @job: a #GtkSourcePrintJob.
 * @desc: the #PangoFontDescription for the font for line numbers, or %NULL
 * 
 * Sets the font for printing line numbers on the left margin.  If
 * NULL is supplied, the default font (i.e. the one being used for the
 * text) will be used instead.
 **/
void 
gtk_source_print_job_set_numbers_font_desc (GtkSourcePrintJob    *job,
					    PangoFontDescription *desc)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);
	
	if (desc)
		desc = pango_font_description_copy (desc);
	if (job->priv->numbers_font != NULL)
		pango_font_description_free (job->priv->numbers_font);
	job->priv->numbers_font = desc;
	g_object_freeze_notify (G_OBJECT (job));
	g_object_notify (G_OBJECT (job), "numbers_font");
	g_object_notify (G_OBJECT (job), "numbers_font_desc");
	g_object_thaw_notify (G_OBJECT (job));
}

/**
 * gtk_source_print_job_set_numbers_font:
 * @job: a #GtkSourcePrintJob.
 * @font_name: the full name of the font for line numbers, or %NULL.
 * 
 * Sets the font for printing line numbers on the left margin.  If
 * %NULL is supplied, the default font (i.e. the one being used for the
 * text) will be used instead.
 *
 * Note that @font_name is a #GnomeFont name not a Pango font
 * description string. This function is deprecated since #GnomeFont is
 * no longer used when implementing printing for GtkSourceView; you
 * should use gtk_source_print_job_set_numbers_font_desc() instead.
 **/
void 
gtk_source_print_job_set_numbers_font (GtkSourcePrintJob *job,
				       const gchar       *font_name)
{
	PangoFontDescription *desc;
	
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	if (font_name != NULL)
	{
		desc = font_description_from_gnome_font_name (font_name);
		if (desc)
		{
			gtk_source_print_job_set_numbers_font_desc (job, desc);
			pango_font_description_free (desc);
		}
	}
	else
		gtk_source_print_job_set_numbers_font (job, NULL);
}

/**
 * gtk_source_print_job_get_numbers_font_desc:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the font to be used for the line numbers. This function
 * might return %NULL if a specific font for numbers has not been set.
 * 
 * Return value: the line numbers font description or %NULL. This value is
 * owned by the job and must not be modified or freed.
 **/
PangoFontDescription *
gtk_source_print_job_get_numbers_font_desc (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	return job->priv->numbers_font;
}

/**
 * gtk_source_print_job_get_numbers_font:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the font to be used for the line numbers.  The returned
 * string is of the form &quot;Fontfamily Style Size&quot;, for
 * example &quot;Monospace Regular 10.0&quot;.  The returned value
 * should be freed when no longer needed.  This function might return
 * %NULL if a specific font for numbers has not been set.
 * 
 * Note that the result is a #GnomeFont name not a Pango font
 * description string. This function is deprecated since #GnomeFont is
 * no longer used when implementing printing for GtkSourceView; you
 * should use gtk_source_print_job_get_numbers_font_desc() instead.
 *
 * Return value: a newly allocated string with the name of the current
 * line numbers font, or %NULL.
 **/
gchar *
gtk_source_print_job_get_numbers_font (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	if (job->priv->numbers_font != NULL)
		return font_description_to_gnome_font_name (job->priv->numbers_font);
	else
		return NULL;
}

/**
 * gtk_source_print_job_set_print_numbers:
 * @job: a #GtkSourcePrintJob.
 * @interval: interval for printed line numbers.
 * 
 * Sets the interval for printed line numbers.  If @interval is 0 no
 * numbers will be printed.  If greater than 0, a number will be
 * printed every @interval lines (i.e. 1 will print all line numbers).
 **/
void 
gtk_source_print_job_set_print_numbers (GtkSourcePrintJob *job,
					guint              interval)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	if (interval == job->priv->print_numbers)
		return;
	
	job->priv->print_numbers = interval;

	g_object_notify (G_OBJECT (job), "print_numbers");
}

/**
 * gtk_source_print_job_get_print_numbers:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the interval used for line number printing.  If the
 * value is 0, no line numbers will be printed.  The default value is
 * 1 (i.e. numbers printed in all lines).
 * 
 * Return value: the interval of printed line numbers.
 **/
guint 
gtk_source_print_job_get_print_numbers (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), 0);

	return job->priv->print_numbers;
}

/**
 * gtk_source_print_job_set_text_margins:
 * @job: a #GtkSourcePrintJob.
 * @top: the top user margin.
 * @bottom: the bottom user margin.
 * @left: the left user margin.
 * @right: the right user margin.
 * 
 * Sets the four user margins for the print job.  These margins are in
 * addition to the document margins provided in the #GnomePrintConfig
 * and will not be used for headers, footers or line numbers (those
 * are calculated separatedly).  You can print in the space allocated
 * by these margins by connecting to the <link
 * linkend="GtkSourcePrintJob-begin-page">&quot;begin_page&quot;</link> signal.  The
 * space is around the printed text, and inside the margins specified
 * in the #GnomePrintConfig.
 *
 * The margin numbers are given in device units.  If any of the given
 * values is less than 0, that particular margin is not altered by
 * this function.
 **/
void 
gtk_source_print_job_set_text_margins (GtkSourcePrintJob *job,
				       gdouble            top,
				       gdouble            bottom,
				       gdouble            left,
				       gdouble            right)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	if (top >= 0)
		job->priv->margin_top = top;
	if (bottom >= 0)
		job->priv->margin_bottom = bottom;
	if (left >= 0)
		job->priv->margin_left = left;
	if (right >= 0)
		job->priv->margin_right = right;
}

/**
 * gtk_source_print_job_get_text_margins:
 * @job: a #GtkSourcePrintJob.
 * @top: a pointer to a #gdouble to return the top margin.
 * @bottom: a pointer to a #gdouble to return the bottom margin.
 * @left: a pointer to a #gdouble to return the left margin.
 * @right: a pointer to a #gdouble to return the right margin.
 * 
 * Determines the user set margins for the job.  This function
 * retrieves the values previously set by
 * gtk_source_print_job_set_text_margins().  The default for all four
 * margins is 0.  Any of the pointers can be %NULL if you want to
 * ignore that value.
 **/
void 
gtk_source_print_job_get_text_margins (GtkSourcePrintJob *job,
				       gdouble           *top,
				       gdouble           *bottom,
				       gdouble           *left,
				       gdouble           *right)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));

	if (top != NULL)
		*top = job->priv->margin_top;
	if (bottom != NULL)
		*bottom = job->priv->margin_bottom;
	if (left != NULL)
		*left = job->priv->margin_left;
	if (right != NULL)
		*right = job->priv->margin_right;
}

/* --- printing operations */

static gboolean
gtk_source_print_job_prepare (GtkSourcePrintJob *job,
			      const GtkTextIter *start,
			      const GtkTextIter *end)
{
	PROFILE (GTimer *timer);
	
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), FALSE);
	g_return_val_if_fail (!job->priv->printing, FALSE);
	g_return_val_if_fail (job->priv->buffer != NULL, FALSE);
	g_return_val_if_fail (start != NULL && end != NULL, FALSE);

	/* make sure we have a sane configuration to start printing */
	ensure_print_config (job);

	PROFILE (timer = g_timer_new ());

	/* get the text to print */
	if (!get_text_to_print (job, start, end))
		return FALSE;

	PROFILE (g_message ("get_text_to_print: %.2f", g_timer_elapsed (timer, NULL)));

	if (!setup_pango_context (job))
		return FALSE;

	/* check margins */
	if (!update_page_size_and_margins (job))
		return FALSE;

	/* split the document in pages */
	if (!paginate_text (job))
		return FALSE;

	PROFILE ({
		g_message ("paginate_text: %.2f", g_timer_elapsed (timer, NULL));
		g_timer_destroy (timer);
	});

	return TRUE;
}

/**
 * gtk_source_print_job_print:
 * @job: a configured #GtkSourcePrintJob.
 * 
 * Produces a #GnomePrintJob with the printed document.  The whole
 * contents of the configured #GtkTextBuffer are printed.  The
 * returned job is already closed and ready to be previewed (using
 * gnome_print_job_preview_new()) or printed directly.  The caller of
 * this function owns a reference to the returned object, so @job can
 * be destroyed and the output will still be valid, or the document
 * can be printed again with different settings.
 * 
 * Return value: a closed #GnomePrintJob with the printed document, or
 * %NULL if printing could not be completed.
 **/
GnomePrintJob *
gtk_source_print_job_print (GtkSourcePrintJob *job)
{
	GtkTextIter start, end;

	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);
	g_return_val_if_fail (!job->priv->printing, NULL);
	g_return_val_if_fail (job->priv->buffer != NULL, NULL);

	gtk_text_buffer_get_bounds (GTK_TEXT_BUFFER (job->priv->buffer), &start, &end);

	return gtk_source_print_job_print_range (job, &start, &end);
}

/**
 * gtk_source_print_job_print_range:
 * @job: a configured #GtkSourcePrintJob.
 * @start: the start of the region of text to print.
 * @end: the end of the region of text to print.
 * 
 * Similar to gtk_source_print_job_print(), except you can specify a
 * range of text to print.  The passed #GtkTextIter values might be
 * out of order.
 * 
 * Return value: a closed #GnomePrintJob with the text from @start to
 * @end printed, or %NULL if @job could not print.
 **/
GnomePrintJob *
gtk_source_print_job_print_range (GtkSourcePrintJob *job,
				  const GtkTextIter *start,
				  const GtkTextIter *end)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);
	g_return_val_if_fail (!job->priv->printing, NULL);
	g_return_val_if_fail (job->priv->buffer != NULL, NULL);
	g_return_val_if_fail (start != NULL && end != NULL, NULL);
	g_return_val_if_fail (gtk_text_iter_get_buffer (start) ==
			      GTK_TEXT_BUFFER (job->priv->buffer) &&
			      gtk_text_iter_get_buffer (end) ==
			      GTK_TEXT_BUFFER (job->priv->buffer), NULL);

	if (!gtk_source_print_job_prepare (job, start, end))
		return NULL;

	/* real work starts here */
	setup_for_print (job);

	job->priv->printing = TRUE;
	print_job (job);
	job->priv->printing = FALSE;

	g_object_ref (job->priv->print_job);
	return job->priv->print_job;
}

/* --- asynchronous printing */

/**
 * gtk_source_print_job_print_range_async:
 * @job: a configured #GtkSourcePrintJob.
 * @start: the start of the region of text to print.
 * @end: the end of the region of text to print.
 * 
 * Starts to print @job asynchronously.  This function will ready the
 * @job for printing and install an idle handler that will render one
 * page at a time.
 *
 * This function will not return immediatly, as only page rendering is
 * done asynchronously.  Text retrieval and paginating happens within
 * this function.  Also, if highlighting is enabled, the whole buffer
 * needs to be highlighted first.
 *
 * To get notification when the job has finished, you must connect to
 * the <link
 * linkend="GtkSourcePrintJob-finished">&quot;finished&quot;</link>
 * signal.  After this signal is emitted you can retrieve the
 * resulting #GnomePrintJob with gtk_source_print_job_get_print_job().
 * You may cancel the job with gtk_source_print_job_cancel().
 *
 * Return value: %TRUE if the print started.
 **/
gboolean 
gtk_source_print_job_print_range_async (GtkSourcePrintJob *job,
					const GtkTextIter *start,
					const GtkTextIter *end)
{
	GSource *idle_source;

	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), FALSE);
	g_return_val_if_fail (!job->priv->printing, FALSE);
	g_return_val_if_fail (job->priv->buffer != NULL, FALSE);
	g_return_val_if_fail (start != NULL && end != NULL, FALSE);
	g_return_val_if_fail (gtk_text_iter_get_buffer (start) ==
			      GTK_TEXT_BUFFER (job->priv->buffer) &&
			      gtk_text_iter_get_buffer (end) ==
			      GTK_TEXT_BUFFER (job->priv->buffer), FALSE);

	if (!gtk_source_print_job_prepare (job, start, end))
		return FALSE;

	/* real work starts here */
	setup_for_print (job);
	if (job->priv->current_paragraph == NULL)
		return FALSE;
	
	/* setup the idle handler to print each page at a time */
	idle_source = g_idle_source_new ();
	g_source_set_priority (idle_source, GTK_SOURCE_PRINT_JOB_PRIORITY);
	g_source_set_closure (idle_source,
			      g_cclosure_new_object ((GCallback) idle_printing_handler,
						     G_OBJECT (job)));
	job->priv->idle_printing_tag = g_source_attach (idle_source, NULL);
	g_source_unref (idle_source);

	job->priv->printing = TRUE;

	return TRUE;
}

/**
 * gtk_source_print_job_cancel:
 * @job: a #GtkSourcePrintJob.
 * 
 * Cancels an asynchronous printing operation.  This will remove any
 * pending print idle handler and unref the current #GnomePrintJob.
 *
 * Note that if you got a reference to the job's #GnomePrintJob (using
 * gtk_source_print_job_get_print_job()) it will not be destroyed
 * (since you hold a reference to it), but it will not be closed
 * either.  If you wish to show or print the partially printed
 * document you need to close it yourself.
 *
 * This function has no effect when called from a non-asynchronous
 * print operation.
 **/
void 
gtk_source_print_job_cancel (GtkSourcePrintJob *job)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (job->priv->printing);

	if (job->priv->idle_printing_tag > 0)
	{
		g_source_remove (job->priv->idle_printing_tag);
		job->priv->current_paragraph = NULL;
		job->priv->idle_printing_tag = 0;
		job->priv->printing = FALSE;
		g_object_unref (job->priv->print_job);
		g_object_unref (job->priv->print_ctxt);
		job->priv->print_job = NULL;
		job->priv->print_ctxt = NULL;
	}
}

/**
 * gtk_source_print_job_get_print_job:
 * @job: a #GtkSourcePrintJob.
 * 
 * Gets a reference to the #GnomePrintJob which the @job is printing
 * or has recently finished printing.  You need to unref the returned
 * object.
 *
 * You may call this function in the middle of an asynchronous
 * printing operation, but the returned #GnomePrintJob will not be
 * closed until the last page is printed and the <link
 * linkend="GtkSourcePrintJob-finished">&quot;finished&quot;</link>
 * signal is emitted.
 * 
 * Return value: a new reference to the @job's #GnomePrintJob, or
 * %NULL.
 **/
GnomePrintJob *
gtk_source_print_job_get_print_job (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	if (job->priv->print_job)
		g_object_ref (job->priv->print_job);
	
	return job->priv->print_job;
}

/* --- information for asynchronous ops and headers and footers callback */

/**
 * gtk_source_print_job_get_page:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the currently printing page number.  This function is
 * only valid while printing (either synchronously or asynchronously).
 * 
 * Return value: the current page number.
 **/
guint 
gtk_source_print_job_get_page (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), 0);
	g_return_val_if_fail (job->priv->printing, 0);

	return job->priv->page;
}

/**
 * gtk_source_print_job_get_page_count:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the total number of pages the job will print.  The
 * returned value is only meaninful after pagination has finished.  In
 * practice, for synchronous printing this means when <link
 * linkend="GtkSourcePrintJob-begin-page">&quot;begin_page&quot;</link>
 * is emitted, or after gtk_source_print_job_print_range_async() has
 * returned.
 * 
 * Return value: the number of pages of the printed job.
 **/
guint 
gtk_source_print_job_get_page_count (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), 0);

	return job->priv->page_count;
}

/**
 * gtk_source_print_job_get_print_context:
 * @job: a printing #GtkSourcePrintJob.
 * 
 * Determines the #GnomePrintContext of the current job.  This
 * function is only valid while printing.  Normally you would use this
 * function to print in the margins set by
 * gtk_source_print_job_set_margins() in a handler for the <link
 * linkend="GtkSourcePrintJob-begin-page">&quot;begin_page&quot;</link>
 * signal.
 * 
 * Return value: the current #GnomePrintContext.  The returned object
 * is owned by @job.
 **/
GnomePrintContext *
gtk_source_print_job_get_print_context (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);
	g_return_val_if_fail (job->priv->printing, NULL);

	return job->priv->print_ctxt;
}

/* ---- Header and footer (default implementation) */

/* Most of this code taken from GLib's g_date_strftime() in gdate.c
 * GLIB - Library of useful routines for C programming
 * Copyright (C) 1995-1997  Peter Mattis, Spencer Kimball and Josh MacDonald */

static gchar *
strdup_strftime (const gchar *format, const struct tm *tm)
{
	gsize locale_format_len = 0;
	gchar *locale_format;
	gsize tmplen;
	gchar *tmpbuf;
	gsize tmpbufsize;
	gchar *convbuf;
	gsize convlen = 0;
	GError *error = NULL;

	g_return_val_if_fail (format != NULL, NULL);
	g_return_val_if_fail (tm != NULL, NULL);

	locale_format = g_locale_from_utf8 (format, -1, NULL, &locale_format_len, &error);

	if (error)
	{
		g_warning (G_STRLOC "Error converting format to locale encoding: %s",
			   error->message);
		g_error_free (error);
		
		return NULL;
	}

	tmpbufsize = MAX (128, locale_format_len * 2);
	while (TRUE)
	{
		tmpbuf = g_malloc (tmpbufsize);
		
		/* Set the first byte to something other than '\0', to be able to
		 * recognize whether strftime actually failed or just returned "".
		 */
		tmpbuf[0] = '\1';
		tmplen = strftime (tmpbuf, tmpbufsize, locale_format, tm);
		
		if (tmplen == 0 && tmpbuf[0] != '\0')
		{
			g_free (tmpbuf);
			tmpbufsize *= 2;
			
			if (tmpbufsize > 65536)
			{
				g_warning (G_STRLOC "Maximum buffer size for strdup_strftime "
					   "exceeded: giving up");
				g_free (locale_format);
				return NULL;
			}
		}
		else
			break;
	}
	g_free (locale_format);

	convbuf = g_locale_to_utf8 (tmpbuf, tmplen, NULL, &convlen, &error);
	g_free (tmpbuf);

	if (error)
	{
		g_warning (G_STRLOC "Error converting results of strftime to UTF-8: %s",
			   error->message);
		g_error_free (error);
		
		return NULL;
	}

	return convbuf;
}

static gchar *
evaluate_format_string (GtkSourcePrintJob *job, const gchar *format)
{
	GString *eval;
	gchar *eval_str, *retval;
	const struct tm *tm;
	time_t now;
	gunichar ch;
	
	/* get time */
	time (&now);
	tm = localtime (&now);

	/* analyze format string and replace the codes we know */
	eval = g_string_new_len (NULL, strlen (format));
	ch = g_utf8_get_char (format);
	while (ch != 0)
	{
		if (ch == '%')
		{
			format = g_utf8_next_char (format);
			ch = g_utf8_get_char (format);
			if (ch == 'N')
				g_string_append_printf (eval, "%d", job->priv->page);
			else if (ch == 'Q')
				g_string_append_printf (eval, "%d", job->priv->page_count);
			else
			{
				g_string_append_c (eval, '%');
				g_string_append_unichar (eval, ch);
			}
		}
		else
			g_string_append_unichar (eval, ch);

		format = g_utf8_next_char (format);
		ch = g_utf8_get_char (format);
	}

	eval_str = g_string_free (eval, FALSE);
	retval = strdup_strftime (eval_str, tm);
	g_free (eval_str);

	return retval;
}

static void
print_header_footer_string (GtkSourcePrintJob *job,
			    const gchar       *format,
			    gdouble            x_align,
			    gdouble            x,
			    gdouble            y)
{
	PangoLayout *layout;
	gchar *text;
	gdouble width;
	gdouble xx;
	
	width = job->priv->text_width + job->priv->numbers_width;
	
	text = evaluate_format_string (job, format);
	if (text != NULL)
	{
		layout = pango_layout_new (job->priv->pango_context);
		pango_layout_set_font_description (layout, job->priv->header_footer_font);
		pango_layout_set_text (layout, text, -1);
		
		xx = x + x_align * (width - get_layout_width (layout));
		gnome_print_moveto (job->priv->print_ctxt, xx, y);
		show_first_layout_line (job->priv->print_ctxt, layout);
		
		g_free (text);
		g_object_unref (layout);
	}
}

static void 
default_print_header (GtkSourcePrintJob *job,
		      gdouble            x,
		      gdouble            y)
{
	gdouble width;
	gdouble yy;
	gdouble ascent, descent;
	
	width = job->priv->text_width + job->priv->numbers_width;

	get_font_ascent_descent (job, job->priv->header_footer_font, &ascent, &descent);

	yy = y - ascent;

	/* left format */
	if (job->priv->header_format_left != NULL)
		print_header_footer_string (job, job->priv->header_format_left, 0.0, x, yy);
	
	/* right format */
	if (job->priv->header_format_right != NULL)
		print_header_footer_string (job, job->priv->header_format_right, 1.0, x, yy);

	/* center format */
	if (job->priv->header_format_center != NULL)
		print_header_footer_string (job, job->priv->header_format_center, 0.5, x, yy);

	/* separator */
	if (job->priv->header_separator)
	{
		yy = y - (SEPARATOR_SPACING * (ascent + descent));
		gnome_print_setlinewidth (job->priv->print_ctxt, SEPARATOR_LINE_WIDTH);
		gnome_print_moveto (job->priv->print_ctxt, x, yy);
		gnome_print_lineto (job->priv->print_ctxt, x + width, yy);
		gnome_print_stroke (job->priv->print_ctxt);
	}
}

static void 
default_print_footer (GtkSourcePrintJob *job,
		      gdouble            x,
		      gdouble            y)
{
	gdouble width;
	gdouble yy;
	gdouble ascent, descent;
	
	width = job->priv->text_width + job->priv->numbers_width;

	get_font_ascent_descent (job, job->priv->header_footer_font, &ascent, &descent);

	yy = y - job->priv->footer_height + descent;

	/* left format */
	if (job->priv->footer_format_left != NULL)
		print_header_footer_string (job, job->priv->footer_format_left, 0.0, x, yy);
	
	/* right format */
	if (job->priv->footer_format_right != NULL)
		print_header_footer_string (job, job->priv->footer_format_right, 1.0, x, yy);

	/* center format */
	if (job->priv->footer_format_center != NULL)
		print_header_footer_string (job, job->priv->footer_format_center, 0.5, x, yy);

	/* separator */
	if (job->priv->footer_separator)
	{
		yy = y - job->priv->footer_height +
			(SEPARATOR_SPACING * (ascent + descent));
		gnome_print_setlinewidth (job->priv->print_ctxt, SEPARATOR_LINE_WIDTH);
		gnome_print_moveto (job->priv->print_ctxt, x, yy);
		gnome_print_lineto (job->priv->print_ctxt, x + width, yy);
		gnome_print_stroke (job->priv->print_ctxt);
	}
}

/**
 * gtk_source_print_job_set_print_header:
 * @job: a #GtkSourcePrintJob.
 * @setting: %TRUE if you want the header to be printed.
 * 
 * Sets whether you want to print a header in each page.  The default
 * header consists of three pieces of text and an optional line
 * separator, configurable with
 * gtk_source_print_job_set_header_format().
 *
 * Note that by default the header format is unspecified, and if it's
 * empty it will not be printed, regardless of this setting.
 **/
void 
gtk_source_print_job_set_print_header (GtkSourcePrintJob *job,
				       gboolean           setting)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	setting = (setting != FALSE);
	
	if (setting == job->priv->print_header)
		return;
	
	job->priv->print_header = setting;

	g_object_notify (G_OBJECT (job), "print_header");
}

/**
 * gtk_source_print_job_get_print_header:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines if a header is set to be printed for each page.  A
 * header will be printed if this function returns %TRUE
 * <emphasis>and</emphasis> some format strings have been specified
 * with gtk_source_print_job_set_header_format().
 * 
 * Return value: %TRUE if the header is set to be printed.
 **/
gboolean 
gtk_source_print_job_get_print_header (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), FALSE);

	return job->priv->print_header;
}

/**
 * gtk_source_print_job_set_print_footer:
 * @job: a #GtkSourcePrintJob.
 * @setting: %TRUE if you want the footer to be printed.
 * 
 * Sets whether you want to print a footer in each page.  The default
 * footer consists of three pieces of text and an optional line
 * separator, configurable with
 * gtk_source_print_job_set_footer_format().
 *
 * Note that by default the footer format is unspecified, and if it's
 * empty it will not be printed, regardless of this setting.
 **/
void 
gtk_source_print_job_set_print_footer (GtkSourcePrintJob *job,
				       gboolean           setting)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	setting = (setting != FALSE);
	
	if (setting == job->priv->print_footer)
		return;
	
	job->priv->print_footer = setting;

	g_object_notify (G_OBJECT (job), "print_footer");
}

/**
 * gtk_source_print_job_get_print_footer:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines if a footer is set to be printed for each page.  A
 * footer will be printed if this function returns %TRUE
 * <emphasis>and</emphasis> some format strings have been specified
 * with gtk_source_print_job_set_footer_format().
 * 
 * Return value: %TRUE if the footer is set to be printed.
 **/
gboolean 
gtk_source_print_job_get_print_footer (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), FALSE);

	return job->priv->print_footer;
}

/**
 * gtk_source_print_job_set_header_footer_font_desc:
 * @job: a #GtkSourcePrintJob.
 * @desc: the #PangoFontDescription for the font to be used in headers and footers, or %NULL.
 * 
 * Sets the font for printing headers and footers.  If %NULL is
 * supplied, the default font (i.e. the one being used for the text)
 * will be used instead.
 **/
void 
gtk_source_print_job_set_header_footer_font_desc (GtkSourcePrintJob    *job,
						  PangoFontDescription *desc)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);
	
	if (desc)
		desc = pango_font_description_copy (desc);
	if (job->priv->header_footer_font != NULL)
		pango_font_description_free (job->priv->header_footer_font);
	job->priv->header_footer_font = desc;
	g_object_freeze_notify (G_OBJECT (job));
	g_object_notify (G_OBJECT (job), "header_footer_font");
	g_object_notify (G_OBJECT (job), "header_footer_font_desc");
	g_object_thaw_notify (G_OBJECT (job));
}

/**
 * gtk_source_print_job_set_header_footer_font:
 * @job: a #GtkSourcePrintJob.
 * @font_name: the full name of the font to be used in headers and footers, or %NULL.
 * 
 * Sets the font for printing headers and footers.  If %NULL is
 * supplied, the default font (i.e. the one being used for the text)
 * will be used instead.
 *
 * Note that @font_name is a #GnomeFont name not a Pango font
 * description string. This function is deprecated since #GnomeFont is
 * no longer used when implementing printing for GtkSourceView; you
 * should use gtk_source_print_job_set_header_footer_font_desc() instead.
 **/
void 
gtk_source_print_job_set_header_footer_font (GtkSourcePrintJob *job,
					     const gchar       *font_name)
{
	PangoFontDescription *desc;
	
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	if (font_name != NULL)
	{
		desc = font_description_from_gnome_font_name (font_name);
		if (desc)
		{
			gtk_source_print_job_set_header_footer_font_desc (job, desc);
			pango_font_description_free (desc);
		}
	}
	else
		gtk_source_print_job_set_header_footer_font_desc (job, NULL);
}

/**
 * gtk_source_print_job_get_header_footer_font_desc:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the font to be used for the header and footer.  This function
 * might return %NULL if a specific font has not been set.
 * 
 * Return value: the header and footer font description or %NULL. This value is
 * owned by the job and must not be modified or freed.
 **/
PangoFontDescription *
gtk_source_print_job_get_header_footer_font_desc (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	return job->priv->header_footer_font;
}

/**
 * gtk_source_print_job_get_header_footer_font:
 * @job: a #GtkSourcePrintJob.
 * 
 * Determines the font to be used for the header and footer.  The
 * returned string is of the form &quot;Fontfamily Style Size&quot;,
 * for example &quot;Monospace Regular 10.0&quot;.  The returned value
 * should be freed when no longer needed.  This function might return
 * %NULL if a specific font has not been set.
 * 
 * Note that the result is a #GnomeFont name not a Pango font
 * description string. This function is deprecated since #GnomeFont is
 * no longer used when implementing printing for GtkSourceView; you
 * should use gtk_source_print_job_get_header_footer_font_desc() instead.
 *
 * Return value: a newly allocated string with the name of the current
 * header and footer font, or %NULL.
 **/
gchar *
gtk_source_print_job_get_header_footer_font (GtkSourcePrintJob *job)
{
	g_return_val_if_fail (GTK_IS_SOURCE_PRINT_JOB (job), NULL);

	if (job->priv->header_footer_font != NULL)
		return font_description_to_gnome_font_name (job->priv->header_footer_font);
	else
		return NULL;
}

/**
 * gtk_source_print_job_set_header_format:
 * @job: a #GtkSourcePrintJob.
 * @left: a format string to print on the left of the header.
 * @center: a format string to print on the center of the header.
 * @right: a format string to print on the right of the header.
 * @separator: %TRUE if you want a separator line to be printed.
 * 
 * Sets strftime like header format strings, to be printed on the
 * left, center and right of the top of each page.  The strings may
 * include strftime(3) codes which will be expanded at print time.
 * All strftime() codes are accepted, with the addition of %N for the
 * page number and %Q for the page count.
 *
 * @separator specifies if a solid line should be drawn to separate
 * the header from the document text.
 *
 * If %NULL is given for any of the three arguments, that particular
 * string will not be printed.  For the header to be printed, in
 * addition to specifying format strings, you need to enable header
 * printing with gtk_source_print_job_set_print_header().
 **/
void 
gtk_source_print_job_set_header_format (GtkSourcePrintJob *job,
					const gchar       *left,
					const gchar       *center,
					const gchar       *right,
					gboolean           separator)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	/* FIXME: validate given strings? */
	g_free (job->priv->header_format_left);
	g_free (job->priv->header_format_center);
	g_free (job->priv->header_format_right);
	job->priv->header_format_left = g_strdup (left);
	job->priv->header_format_center = g_strdup (center);
	job->priv->header_format_right = g_strdup (right);
	job->priv->header_separator = separator;
}

/**
 * gtk_source_print_job_set_footer_format:
 * @job: a #GtkSourcePrintJob.
 * @left: a format string to print on the left of the footer.
 * @center: a format string to print on the center of the footer.
 * @right: a format string to print on the right of the footer.
 * @separator: %TRUE if you want a separator line to be printed.
 * 
 * Like gtk_source_print_job_set_header_format(), but for the footer.
 **/
void 
gtk_source_print_job_set_footer_format (GtkSourcePrintJob *job,
					const gchar       *left,
					const gchar       *center,
					const gchar       *right,
					gboolean           separator)
{
	g_return_if_fail (GTK_IS_SOURCE_PRINT_JOB (job));
	g_return_if_fail (!job->priv->printing);

	/* FIXME: validate given strings? */
	g_free (job->priv->footer_format_left);
	g_free (job->priv->footer_format_center);
	g_free (job->priv->footer_format_right);
	job->priv->footer_format_left = g_strdup (left);
	job->priv->footer_format_center = g_strdup (center);
	job->priv->footer_format_right = g_strdup (right);
	job->priv->footer_separator = separator;
}
