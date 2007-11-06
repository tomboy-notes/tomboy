/*
 * contrast.h
 *
 * Copyright (c) 2006-2007  David Trowbridge
 *
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to
 * permit persons to whom the Software is furnished to do so, subject to
 * the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
 * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

#include <gdk/gdkcolor.h>

/*
 * Defined colors.  Keep this list alphabetized if you add new ones, but set the
 * enum value to be at the end of the color_regions table in contrast.c to
 * maintain binary compatibility
 */
typedef enum {
	CONTRAST_COLOR_AQUA        =  0,
	CONTRAST_COLOR_BLACK       =  1,
	CONTRAST_COLOR_BLUE        =  2,
	CONTRAST_COLOR_BROWN       =  3,
	CONTRAST_COLOR_CYAN        =  4,
	CONTRAST_COLOR_DARK_BLUE   =  5,
	CONTRAST_COLOR_DARK_GREEN  =  6,
	CONTRAST_COLOR_DARK_GREY   =  7,
	CONTRAST_COLOR_DARK_RED    =  8,
	CONTRAST_COLOR_GREEN       =  9,
	CONTRAST_COLOR_GREY        = 10,
	CONTRAST_COLOR_LIGHT_BLUE  = 11,
	CONTRAST_COLOR_LIGHT_BROWN = 12,
	CONTRAST_COLOR_LIGHT_GREEN = 13,
	CONTRAST_COLOR_LIGHT_GREY  = 14,
	CONTRAST_COLOR_LIGHT_RED   = 15,
	CONTRAST_COLOR_MAGENTA     = 16,
	CONTRAST_COLOR_ORANGE      = 17,
	CONTRAST_COLOR_PURPLE      = 18,
	CONTRAST_COLOR_RED         = 19,
	CONTRAST_COLOR_VIOLET      = 20,
	CONTRAST_COLOR_WHITE       = 21,
	CONTRAST_COLOR_YELLOW      = 22,
	CONTRAST_COLOR_LAST        = 23,
} ContrastPaletteColor;

GdkColor
contrast_render_foreground_color(GdkColor background,
                                 ContrastPaletteColor color);
