/*
 * Contrast.cs
 *
 * Copyright (c) 2006-2007  David Trowbridge
 * Copyright (c) 2008       Sebastian Dröge <slomo@circular-chaos.org>
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


using System;
using Gdk;

namespace Tomboy
{
	/* Defined colors.  Keep this list alphabetized if you add new ones,
	 * but set the enum value to be at the end of the color_regions table
	 * in contrast.c to maintain binary compatibility
	 */
	
	public enum ContrastPaletteColor
	{
		Aqua        =  0,
		Black       =  1,
		Blue        =  2,
		Brown       =  3,
		Cyan        =  4,
		DarkBlue    =  5,
		DarkGreen   =  6,
		DarkGrey    =  7,
		DarkRed     =  8,
		Green       =  9,
		Grey        = 10,
		LightBlue   = 11,
		LightBrown  = 12,
		LightGreen  = 13,
		LightGrey   = 14,
		LightRed    = 15,
		Magenta     = 16,
		Orange      = 17,
		Purple      = 18,
		Red         = 19,
		Violet      = 20,
		White       = 21,
		Yellow      = 22,
		Last        = 23
	};
	
	public static class Contrast
	{
	
		/* Data for color palette optimization.
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
		private static readonly float[,] color_regions = {
			{40.0f,  60.0f, -100.0f, -80.0f,  -10.0f,  20.0f}, /* Aqua */
			{ 0.0f,  30.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* Black */
			{25.0f,  35.0f, -100.0f,   0.0f, -100.0f, -50.0f}, /* Blue */
			{30.0f,  60.0f,   30.0f,  50.0f,   70.0f, 100.0f}, /* Brown */
			{50.0f,  65.0f, -100.0f, -30.0f, -100.0f, -50.0f}, /* Cyan */
			{ 0.0f,  20.0f,  -40.0f,  50.0f, -100.0f, -60.0f}, /* Dark Blue */
			{20.0f,  35.0f, -100.0f, -70.0f,   60.0f, 100.0f}, /* Dark Green */
			{20.0f,  40.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* Dark Grey */
			{10.0f,  40.0f,   90.0f, 100.0f,   70.0f, 100.0f}, /* Dark Red */
			{15.0f,  40.0f, -100.0f, -80.0f,   80.0f, 100.0f}, /* Green */
			{35.0f,  60.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* Grey */
			{40.0f,  50.0f, -100.0f,   0.0f, -100.0f, -60.0f}, /* Light Blue */
			{60.0f,  75.0f,   30.0f,  50.0f,   80.0f, 100.0f}, /* Light Brown */
			{80.0f,  90.0f, -100.0f, -70.0f,   70.0f, 100.0f}, /* Light Green */
			{50.0f,  80.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* Light Grey */
			{55.0f,  65.0f,   80.0f,  90.0f,   75.0f, 100.0f}, /* Light Red */
			{40.0f,  55.0f,   90.0f, 100.0f,  -50.0f,   0.0f}, /* Magenta */
			{65.0f,  80.0f,   20.0f,  65.0f,   90.0f, 100.0f}, /* Orange */
			{35.0f,  45.0f,   85.0f, 100.0f,  -90.0f, -80.0f}, /* Purple */
			{40.0f,  50.0f,   80.0f, 100.0f,   75.0f, 100.0f}, /* Red */
			{70.0f,  95.0f,   90.0f, 100.0f, -100.0f,   0.0f}, /* Violet */
			{75.0f, 100.0f,    0.0f,   0.0f,    0.0f,   0.0f}, /* White */
			{90.0f, 100.0f,    5.0f,  15.0f,   92.5f, 105.0f}, /* Yellow */
		};

		/* This performs the non-linear transformation that accounts for the gamma curve
		 * in sRGB, but avoids numerical problems.
		 */

		private static float srgb_to_xyz_g (float K)
		{
			const float a     = 0.055f;
			const float gamma = 2.4f;

			
			if (K > 0.04045f)
				return (float) Math.Pow((K + a) / (1 + a), gamma);
			else
				return K / 12.92f;
		}

		private static float xyz_to_lab_f(float t)
		{
			if (t > 0.008856f)
				return (float) Math.Pow(t, 1.0f/3.0f);
			else
				return 7.787f*t + 16.0f/116.0f;
		}

		private static void rgb_to_lab(ushort R, ushort G, ushort B, out float L, out float a, out float b)
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

			L = 116.0f * fy - 16.0f;
			a = 500.0f * (xyz_to_lab_f(x / Xn) - fy);
			b = 200.0f * (fy - xyz_to_lab_f(z / Zn));
		}

		private static float xyz_to_srgb_C(float K)
		{
			const float a     = 0.055f;
			const float gamma = 2.4f;

			
			if (K > 0.00304f)
				return (1.0f + a) * ((float) Math.Pow(K, (1.0f / gamma))) - a;
			else
				return K * 12.92f;
		}

		private static void lab_to_rgb(float L, float a, float b, out ushort R, out ushort G, out ushort B)
		{		
			float x, y, z, fy, fx, fz, delta, delta2, rs, gs, bs;

			const float Xn = 0.93819f;
			const float Yn = 0.98705f;
			const float Zn = 1.07475f;
			
			fy = (L + 16.0f) / 116.0f;
			fx = fy + a / 500.0f;
			fz = fy - b / 200.0f;
			delta = 6.0f / 29.0f;
			delta2 = (float) Math.Pow(delta, 2.0f);
			
			if (fx > delta)
				x = Xn * ((float) Math.Pow(fx, 3.0f));
			else
				x = (fx - 16.0f/116.0f) * 3.0f * delta2 * Xn;

			if (fy > delta)
				y = Yn * ((float) Math.Pow(fy, 3.0f));
			else
				y = (fy - 16.0f/116.0f) * 3.0f * delta2 * Yn;

			
			if (fz > delta)
				z = Zn * ((float) Math.Pow(fz, 3.0f));
			else
				z = (fz - 16.0f/116.0f) * 3.0f * delta2 * Zn;

			
			rs =  3.2410f * x - 1.5374f * y - 0.4986f * z;
			gs = -0.9692f * x + 1.8760f * y + 0.0416f * z;
			bs =  0.0556f * x - 0.2040f * y + 1.0570f * z;

			float tmp;
			tmp = (float) Math.Floor(xyz_to_srgb_C(rs) * 65535.0f + 0.5f);
			if (tmp < 0.0f)
				R = 0;
			else if (tmp > 65535.0f)
				R = 65535;
			else
				R = (ushort) tmp;
			
			tmp = (float) Math.Floor(xyz_to_srgb_C(gs) * 65535.0f + 0.5f);
			if (tmp < 0.0f)
				G = 0;
			else if (tmp > 65535.0f)
				G = 65535;
			else
				G = (ushort) tmp;

			tmp = (float) Math.Floor(xyz_to_srgb_C(bs) * 65535.0f + 0.5f);
			if (tmp < 0.0f)
				B = 0;
			else if (tmp > 65535.0f)
				B = 65535;
			else
				B = (ushort) tmp;
		}

		private static float lab_distance(float La, float aa, float ba, float Lb, float ab, float bb)
		{
			float dL, da, db;

			dL = Math.Abs(Lb - La);
			da = Math.Abs(ab - aa);
			db = Math.Abs(bb - ba);

			return (float) Math.Sqrt(dL*dL + da*da + db*db);
		}

		/* Creates a specific color value for a foreground color, optimizing for
		 * maximum readability against the background.
		 */
		
		public static Gdk.Color RenderForegroundColor(Gdk.Color background, ContrastPaletteColor color)
		{
			float L, a, b;
			int max_color;
			float max_dist;
			float[,] points = new float[8,3];
			float ld, cd;
			int i;
			Gdk.Color rcolor = new Gdk.Color();
			
			rgb_to_lab(background.Red, background.Green, background.Blue, out L, out a, out b);
			
			points[0,0] = color_regions[(int)color,0];
			points[0,1] = color_regions[(int)color,2];
			points[0,2] = color_regions[(int)color,4];
			
			points[1,0] = color_regions[(int)color,0];
			points[1,1] = color_regions[(int)color,2];
			points[1,2] = color_regions[(int)color,5];
			
			points[2,0] = color_regions[(int)color,0];
			points[2,1] = color_regions[(int)color,3];
			points[2,2] = color_regions[(int)color,4];

			points[3,0] = color_regions[(int)color,0];
			points[3,1] = color_regions[(int)color,3];
			points[3,2] = color_regions[(int)color,5];

			points[4,0] = color_regions[(int)color,1];
			points[4,1] = color_regions[(int)color,2];
			points[4,2] = color_regions[(int)color,4];

			points[5,0] = color_regions[(int)color,1];
			points[5,1] = color_regions[(int)color,2];
			points[5,2] = color_regions[(int)color,5];

			points[6,0] = color_regions[(int)color,1];
			points[6,1] = color_regions[(int)color,3];
			points[6,2] = color_regions[(int)color,4];
			
			points[7,0] = color_regions[(int)color,1];
			points[7,1] = color_regions[(int)color,3];
			points[7,2] = color_regions[(int)color,5];
			
			max_dist = 0;
			max_color = 0;
			
			for (i = 0; i < 8; i++) {
				float dist = lab_distance(L, a, b, points[i,0], points[i,1], points[i,2]);
				
				if (dist > max_dist) {
					max_dist = dist;
					max_color = i;
				}
			}

			/* If the luminosity distance is really short, extend the vector further
			 * out.  This may push it outside the bounds of the region that a color
			 * is specified in, but it keeps things readable when the background and
			 * foreground are really close.
			 */
			
			ld = Math.Abs(L - points[max_color,0]);
			cd = (float) Math.Sqrt (Math.Pow (Math.Abs (a - points[max_color,1]), 2.0f) + Math.Pow (Math.Abs (b - points[max_color,2]), 2.0f));
			
			if ((ld < 10.0f) && (cd < 60.0f)) {
				float dL, da, db;
				
				dL = points[max_color,0] - L;
				da = points[max_color,1] - a;
				db = points[max_color,2] - b;
				points[max_color,0] = L + (dL * 4.0f);
				points[max_color,1] = a + (da * 1.5f);
				points[max_color,2] = b + (db * 1.5f);
			}

			rcolor.Pixel = 0;
			lab_to_rgb(points[max_color,0], points[max_color,1], points[max_color,2], out rcolor.Red, out rcolor.Green, out rcolor.Blue);
			
			return rcolor;
		}
	}
}
