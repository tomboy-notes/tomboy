/*
 * contrast.c
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

#include <config.h>
#include <math.h>
#include "contrast.h"

/*
 * Data for color palette optimization.
 *
 * These numbers are completely arbitrary decisions, uninformed by the experts
 * at crayola.  These colors are defined as boxes within the CIE L*a*b* color
 * space -- while they're not fully inclusive, they are "safe" in that anywhere
 * within a given region is guaranteed to be the expected color.  The data here
 * are endpoints in each dimension of CIELAB, from which the 8 corners of the
 * region are created to test.
 *
 * Add new entries to the end of this list.
 */
static const float color_regions[][6] = {
	{40.0f,  60.0f, -100.0f, -80.0f,  -10.0f,  20.0f}, /* CONTRAST_COLOR_AQUA */
	{ 0.0f,  30.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* CONTRAST_COLOR_BLACK */
	{25.0f,  35.0f, -100.0f,   0.0f, -100.0f, -50.0f}, /* CONTRAST_COLOR_BLUE */
	{30.0f,  60.0f,   30.0f,  50.0f,   70.0f, 100.0f}, /* CONTRAST_COLOR_BROWN */
	{50.0f,  65.0f, -100.0f, -30.0f, -100.0f, -50.0f}, /* CONTRAST_COLOR_CYAN */
	{ 0.0f,  20.0f,  -40.0f,  50.0f, -100.0f, -60.0f}, /* CONTRAST_COLOR_DARK_BLUE */
	{20.0f,  35.0f, -100.0f, -70.0f,   60.0f, 100.0f}, /* CONTRAST_COLOR_DARK_GREEN */
	{20.0f,  40.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* CONTRAST_COLOR_DARK_GREY */
	{10.0f,  40.0f,   90.0f, 100.0f,   70.0f, 100.0f}, /* CONTRAST_COLOR_DARK_RED */
	{15.0f,  40.0f, -100.0f, -80.0f,   80.0f, 100.0f}, /* CONTRAST_COLOR_GREEN */
	{35.0f,  60.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* CONTRAST_COLOR_GREY */
	{40.0f,  50.0f, -100.0f,   0.0f, -100.0f, -60.0f}, /* CONTRAST_COLOR_LIGHT_BLUE */
	{60.0f,  75.0f,   30.0f,  50.0f,   80.0f, 100.0f}, /* CONTRAST_COLOR_LIGHT_BROWN */
	{80.0f,  90.0f, -100.0f, -70.0f,   70.0f, 100.0f}, /* CONTRAST_COLOR_LIGHT_GREEN */
	{50.0f,  80.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* CONTRAST_COLOR_LIGHT_GREY */
	{55.0f,  65.0f,   80.0f,  90.0f,   75.0f, 100.0f}, /* CONTRAST_COLOR_LIGHT_RED */
	{40.0f,  55.0f,   90.0f, 100.0f,  -50.0f,   0.0f}, /* CONTRAST_COLOR_MAGENTA */
	{65.0f,  80.0f,   20.0f,  65.0f,   90.0f, 100.0f}, /* CONTRAST_COLOR_ORANGE */
	{35.0f,  45.0f,   85.0f, 100.0f,  -90.0f, -80.0f}, /* CONTRAST_COLOR_PURPLE */
	{40.0f,  50.0f,   80.0f, 100.0f,   75.0f, 100.0f}, /* CONTRAST_COLOR_RED */
	{70.0f,  95.0f,   90.0f, 100.0f, -100.0f,   0.0f}, /* CONTRAST_COLOR_VIOLET */
	{75.0f, 100.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* CONTRAST_COLOR_WHITE */
	{90.0f, 100.0f,    5.0f,  15.0f,   92.5f, 105.0f}, /* CONTRAST_COLOR_YELLOW */
};

/*
 * This performs the non-linear transformation that accounts for the gamma curve
 * in sRGB, but avoids numerical problems.
 */
static inline float
srgb_to_xyz_g(float K)
{
	const float a     = 0.055f;
	const float gamma = 2.4f;

	if (K > 0.04045)
		return pow((K + a) / (1 + a), gamma);
	else
		return K / 12.92;
}

static inline float
xyz_to_lab_f(float t)
{
	if (t > 0.008856f)
		return pow(t, 1/3.0f);
	else
		return 7.787*t + 16/116.0f;
}

static inline void
rgb_to_lab(guint16  R,
           guint16  G,
           guint16  B,
           float   *L,
           float   *a,
           float   *b)
{
	float x, y, z, gr, gg, gb, fy;

	/* This is the reference white point.  Since we're treating "RGB" as
	 * sRGB, this is the D65 point.
	 */
	const float Xn = 0.93819f;
	const float Yn = 0.98705f;
	const float Zn = 1.07475f;

	gr = srgb_to_xyz_g(R / 65535.0f);
	gg = srgb_to_xyz_g(G / 65535.0f);
	gb = srgb_to_xyz_g(B / 65535.0f);

	x = 0.412424f * gr + 0.357579f * gg + 0.180464f * gb;
	y = 0.212656f * gr + 0.715158f * gg + 0.072186f * gb;
	z = 0.019332f * gr + 0.119193f * gg + 0.950444f * gb;

	fy = xyz_to_lab_f(y / Yn);
	*L = 116 * fy - 16;
	*a = 500 * (xyz_to_lab_f(x / Xn) - fy);
	*b = 200 * (fy - xyz_to_lab_f(z / Zn));
}

static inline float
xyz_to_srgb_C(float K)
{
	const float a     = 0.055;
	const float gamma = 2.4;

	if (K > 0.00304)
		return (1 + a) * pow(K, (1.0 / gamma)) - a;
	else
		return K * 12.92;
}

static inline void
lab_to_rgb(float L,
           float a,
           float b,
           guint16 *R,
           guint16 *G,
           guint16 *B)
{
	float x, y, z, fy, fx, fz, delta, delta2, rs, gs, bs;
	const float Xn = 0.93819f;
	const float Yn = 0.98705f;
	const float Zn = 1.07475f;

	fy = (L + 16) / 116;
	fx = fy + a / 500;
	fz = fy - b / 200;
	delta = 6.0f / 29;
	delta2 = pow(delta, 2);

	if (fx > delta) x = Xn * pow(fx, 3);
	else            x = (fx - 16.0f/116) * 3 * delta2 * Xn;

	if (fy > delta) y = Yn * pow(fy, 3);
	else            y = (fy - 16.0f/116) * 3 * delta2 * Yn;

	if (fz > delta) z = Zn * pow(fz, 3);
	else            z = (fz - 16.0f/116) * 3 * delta2 * Zn;

	rs =  3.2410f * x - 1.5374f * y - 0.4986f * z;
	gs = -0.9692f * x + 1.8760f * y + 0.0416f * z;
	bs =  0.0556f * x - 0.2040f * y + 1.0570f * z;

	*R = CLAMP((int) roundf(xyz_to_srgb_C(rs) * 65535), 0, 65535);
	*G = CLAMP((int) roundf(xyz_to_srgb_C(gs) * 65535), 0, 65535);
	*B = CLAMP((int) roundf(xyz_to_srgb_C(bs) * 65535), 0, 65535);
}

static inline float
lab_distance(float La,
             float aa,
             float ba,
             float Lb,
             float ab,
             float bb)
{
  float dL, da, db;
  dL = fabs(Lb - La);
  da = fabs(ab - aa);
  db = fabs(bb - ba);
  return sqrt(dL*dL + da*da + db*db);
}

/**
 * contrast_render_foreground_color
 * @background: the background color
 * @color: the desired foreground color
 *
 * Creates a specific color value for a foreground color, optimizing for
 * maximum readability against the background.
 */
GdkColor
contrast_render_foreground_color(GdkColor background,
                                 ContrastPaletteColor color)
{
	float L, a, b;
	int max_color;
	float max_dist;
	float points[8][3];
	float ld, cd;
	int i;
	GdkColor rcolor;

	rgb_to_lab(background.red, background.green, background.blue, &L, &a, &b);

	points[0][0] = color_regions[color][0]; points[0][1] = color_regions[color][2]; points[0][2] = color_regions[color][4];
	points[1][0] = color_regions[color][0]; points[1][1] = color_regions[color][2]; points[1][2] = color_regions[color][5];
	points[2][0] = color_regions[color][0]; points[2][1] = color_regions[color][3]; points[2][2] = color_regions[color][4];
	points[3][0] = color_regions[color][0]; points[3][1] = color_regions[color][3]; points[3][2] = color_regions[color][5];
	points[4][0] = color_regions[color][1]; points[4][1] = color_regions[color][2]; points[4][2] = color_regions[color][4];
	points[5][0] = color_regions[color][1]; points[5][1] = color_regions[color][2]; points[5][2] = color_regions[color][5];
	points[6][0] = color_regions[color][1]; points[6][1] = color_regions[color][3]; points[6][2] = color_regions[color][4];
	points[7][0] = color_regions[color][1]; points[7][1] = color_regions[color][3]; points[7][2] = color_regions[color][5];

	max_dist = 0;
	max_color = 0;
	for (i = 0; i < 8; i++) {
		float dist = lab_distance(L, a, b, points[i][0], points[i][1], points[i][2]);
		if (dist > max_dist) {
			max_dist = dist;
			max_color = i;
		}
	}

	/*
	 * If the luminosity distance is really short, extend the vector further
	 * out.  This may push it outside the bounds of the region that a color
	 * is specified in, but it keeps things readable when the background and
	 * foreground are really close.
	 */
	ld = fabs (L - points[max_color][0]);
	cd = sqrt (pow (fabs (a - points[max_color][1]), 2) + pow (fabs (b - points[max_color][2]), 2));
	if ((ld < 10.0f) && (cd < 60.0f)) {
		float dL, da, db;
		dL = points[max_color][0] - L;
		da = points[max_color][1] - a;
		db = points[max_color][2] - b;
		points[max_color][0] = L + (dL * 4.0f);
		points[max_color][1] = a + (da * 1.5f);
		points[max_color][2] = b + (db * 1.5f);
	}

	rcolor.pixel = 0;
	lab_to_rgb(points[max_color][0],
	           points[max_color][1],
	           points[max_color][2],
	           &rcolor.red,
	           &rcolor.green,
	           &rcolor.blue);

	return rcolor;
}
