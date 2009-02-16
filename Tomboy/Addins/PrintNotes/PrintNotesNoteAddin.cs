using System;
using System.Collections.Generic;
using Mono.Unix;

namespace Tomboy.PrintNotes
{
	// TODO:
	// COMMENT! A lot!
	// Remove magic numbers (margins), turn them into preferences
	// Replace bullet point chars with an image?
	// Split the file if it grows any further

	struct PrintMargins
	{
		public int Top;
		public int Left;
		public int Right;
		public int Bottom;

		public int VerticalMargins ()
		{
			return Top + Bottom;
		}

		public int HorizontalMargins ()
		{
			return Left + Right;
		}
	}

	public class PrintNotesNoteAddin : NoteAddin
	{
		private Gtk.ImageMenuItem item;
		private PrintMargins page_margins;
		private Pango.Layout date_time_footer;
		private int footer_offset;
		private List<int> page_breaks;

		public override void Initialize ()
		{
		}

		public override void Shutdown ()
		{
			if (item != null)
				item.Activated -= PrintButtonClicked;
		}

		public override void OnNoteOpened ()
		{
			item = new Gtk.ImageMenuItem (Catalog.GetString ("Print"));
			item.Image = new Gtk.Image (Gtk.Stock.Print, Gtk.IconSize.Menu);
			item.Activated += PrintButtonClicked;
			item.AddAccelerator ("activate", Window.AccelGroup,
				(uint) Gdk.Key.p, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);
			item.Show ();
			AddPluginMenuItem (item);
		}

		private void PrintButtonClicked (object sender, EventArgs args)
		{
			try {
				using (Gtk.PrintOperation print_op = new Gtk.PrintOperation ()) {
					print_op.JobName = Note.Title;

					print_op.BeginPrint += OnBeginPrint;
					print_op.DrawPage += OnDrawPage;
					print_op.EndPrint += OnEndPrint;

					print_op.Run (Gtk.PrintOperationAction.PrintDialog, Window);
				}
			} catch (Exception e) {
				Logger.Error ("Exception while printing " + Note.Title + ": " + e.ToString ());
				HIGMessageDialog dlg = new HIGMessageDialog (Note.Window,
					Gtk.DialogFlags.Modal,
					Gtk.MessageType.Error,
					Gtk.ButtonsType.Ok,
					Catalog.GetString ("Error printing note"),
					e.Message);
				dlg.Run ();
				dlg.Destroy ();
			}
		}

		private IEnumerable<Pango.Attribute> GetParagraphAttributes (
			Pango.Layout layout, double dpiX, ref PrintMargins margins,
			ref Gtk.TextIter position, Gtk.TextIter limit)
		{
			IList<Pango.Attribute> attributes = new List<Pango.Attribute> ();

			Gtk.TextTag [] tags = position.Tags;
			position.ForwardToTagToggle (null);
			if (position.Compare (limit) > 0) position = limit;

			double screen_dpiX = Note.Window.Screen.WidthMm * 254d / Note.Window.Screen.Width;

			foreach (Gtk.TextTag tag in tags) {
				if (tag.BackgroundSet) {
					Gdk.Color color = tag.BackgroundGdk;
					attributes.Add (new Pango.AttrBackground (
						color.Red, color.Green, color.Blue));
				}
				if (tag.ForegroundSet) {
					Gdk.Color color = tag.ForegroundGdk;
					attributes.Add (new Pango.AttrForeground (
						color.Red, color.Green, color.Blue));
				}
				if (tag.IndentSet) {
					layout.Indent = tag.Indent;
				}
				if (tag.LeftMarginSet) {                                        
					margins.Left = (int) (tag.LeftMargin / screen_dpiX * dpiX);
				}
				if (tag.RightMarginSet) {
					margins.Right = (int) (tag.RightMargin / screen_dpiX * dpiX);
				}
				if (tag.FontDesc != null) {
					attributes.Add (new Pango.AttrFontDesc (tag.FontDesc));
				}
				if (tag.FamilySet) {
					attributes.Add (new Pango.AttrFamily (tag.Family));
				}
				if (tag.SizeSet) {
					attributes.Add (new Pango.AttrSize (tag.Size));
				}
				if (tag.StyleSet) {
					attributes.Add (new Pango.AttrStyle (tag.Style));
				}
				if (tag.UnderlineSet && tag.Underline != Pango.Underline.Error) {
					attributes.Add (new Pango.AttrUnderline (tag.Underline));
				}
				if (tag.WeightSet) {
					attributes.Add (new Pango.AttrWeight (tag.Weight));
				}
				if (tag.StrikethroughSet) {
					attributes.Add (new Pango.AttrStrikethrough (tag.Strikethrough));
				}
				if (tag.RiseSet) {
					attributes.Add (new Pango.AttrRise (tag.Rise));
				}
				if (tag.ScaleSet) {
					attributes.Add (new Pango.AttrScale (tag.Scale));
				}
				if (tag.StretchSet) {
					attributes.Add (new Pango.AttrStretch (tag.Stretch));
				}
			}

			return attributes;
		}

		private Pango.Layout CreateLayoutForParagraph (Gtk.PrintContext context,
							       Gtk.TextIter p_start,
							       Gtk.TextIter p_end,
							       out PrintMargins margins)
		{
			Pango.Layout layout = context.CreatePangoLayout ();
			layout.FontDescription = Window.Editor.Style.FontDesc;
			int start_index = p_start.LineIndex;

			margins = new PrintMargins ();

			using (Pango.AttrList attr_list = new Pango.AttrList ()) {
				Gtk.TextIter segm_start = p_start;
				Gtk.TextIter segm_end;

				double dpiX = context.DpiX;
				while (segm_start.Compare (p_end) < 0) {
					segm_end = segm_start;
					IEnumerable<Pango.Attribute> attrs =
						GetParagraphAttributes (
							layout, dpiX, ref margins,
							ref segm_end, p_end);

					uint si = (uint) (segm_start.LineIndex - start_index);
					uint ei = (uint) (segm_end.LineIndex - start_index);

					foreach (Pango.Attribute a in attrs) {
						a.StartIndex = si;
						a.EndIndex = ei;
						attr_list.Insert (a);
					}
					segm_start = segm_end;
				}

				layout.Attributes = attr_list;
			}

			layout.Width = Pango.Units.FromPixels ((int)context.Width -
				margins.HorizontalMargins () -
				page_margins.HorizontalMargins ());
			layout.SetText (Buffer.GetSlice (p_start, p_end, false));
			return layout;
		}

		private Pango.Layout CreateLayoutForPagenumbers (Gtk.PrintContext context, int page_number, int total_pages)
		{
			Pango.Layout layout = context.CreatePangoLayout ();
			layout.FontDescription = Window.Editor.Style.FontDesc;
			layout.Width = Pango.Units.FromPixels ((int) context.Width);
			layout.FontDescription.Style = Pango.Style.Normal;
			layout.FontDescription.Weight = Pango.Weight.Light;

			string footer_left = string.Format (Catalog.GetString ("Page {0} of {1}"),
			                                    page_number, total_pages);
			layout.Alignment = Pango.Alignment.Left;
			layout.SetText (footer_left);

			return layout;
		}

		private Pango.Layout CreateLayoutForTimestamp (Gtk.PrintContext context)
		{
			Pango.Layout layout = context.CreatePangoLayout ();
			layout.FontDescription = Window.Editor.Style.FontDesc;
			layout.Width = Pango.Units.FromPixels ((int) context.Width);
			layout.FontDescription.Style = Pango.Style.Normal;
			layout.FontDescription.Weight = Pango.Weight.Light;

			string footer_right = DateTime.Now.ToString (
			/* Translators: Explanation of the date and time format specifications can be found here:
			 * http://msdn.microsoft.com/en-us/library/system.globalization.datetimeformatinfo.aspx */
				Catalog.GetString ("dddd MM/dd/yyyy, hh:mm:ss tt"));
			Logger.Debug (footer_right);
			layout.Alignment = Pango.Alignment.Right;
			layout.SetText (footer_right);

			return layout;
		}

		public static int CmToPixel (double cm, double dpi)
		{
			return (int) (cm * dpi / 2.54);
		}
		
		private void OnBeginPrint (object sender, Gtk.BeginPrintArgs args)
		{
			Gtk.PrintContext context = args.Context;

			// Create and initialize the page margins
			page_margins = new PrintMargins ();
			page_margins.Top = CmToPixel (1.5, context.DpiY);
			page_margins.Left = CmToPixel (1, context.DpiX);
			page_margins.Right = CmToPixel (1, context.DpiX);
			page_margins.Bottom = 0;
			
			// Compute the footer height to define the bottom margin 
			date_time_footer = CreateLayoutForTimestamp (context);
			Pango.Rectangle footer_ink_rect;
			Pango.Rectangle footer_logical_rect;
			date_time_footer.GetExtents (
				out footer_ink_rect,
				out footer_logical_rect);
			
			footer_offset = CmToPixel (0.5, context.DpiY);
			
			/* Set the bottom margin to the height of the footer + a constant 
			 * offset for the separation line */  
			page_margins.Bottom += Pango.Units.ToPixels (footer_logical_rect.Height) +
					       footer_offset;

			double height = Pango.Units.FromPixels ((int) context.Height - page_margins.VerticalMargins ());
			double page_height = 0;

			page_breaks = new List<int> ();

			Gtk.TextIter position;
			Gtk.TextIter end_iter;
			Buffer.GetBounds (out position, out end_iter);

			bool done = position.Compare (end_iter) >= 0;
			while (!done) {
				int line_number = position.Line;

				Gtk.TextIter line_end = position;
				if (!line_end.EndsLine ())
					line_end.ForwardToLineEnd ();

				PrintMargins margins;
				using (Pango.Layout layout = CreateLayoutForParagraph (
					context, position, line_end, out margins)) {

					Pango.Rectangle ink_rect;
					Pango.Rectangle logical_rect;
					layout.GetExtents (out ink_rect, out logical_rect);

					if (page_height + logical_rect.Height > height) {
						page_breaks.Add (line_number);
						page_height = 0;
					}

					page_height += logical_rect.Height;
				}

				position.ForwardLine ();
				done = position.Compare (end_iter) >= 0;
			}

			Gtk.PrintOperation op = (Gtk.PrintOperation) sender;
			op.NPages = page_breaks.Count + 1;
		}

		private void PrintFooter (Gtk.DrawPageArgs args)
		{
			int total_height = Pango.Units.FromPixels ((int) args.Context.Height);
			int total_width = Pango.Units.FromPixels ((int) args.Context.Width);

			using (Cairo.Context cr = args.Context.CairoContext) {
				cr.MoveTo (CmToPixel (0.5, args.Context.DpiX), Pango.Units.ToPixels (total_height) - page_margins.Bottom + footer_offset);
				cr.LineTo (Pango.Units.ToPixels (total_width) - CmToPixel (0.5, args.Context.DpiX), Pango.Units.ToPixels (total_height) - page_margins.Bottom + footer_offset);
				cr.Stroke ();

				Pango.Rectangle ink_rect;
				Pango.Rectangle logical_rect;
				date_time_footer.GetExtents (out ink_rect, out logical_rect);
				
				Cairo.PointD footer_anchor = new Cairo.PointD (
					CmToPixel (0.5, args.Context.DpiX), Pango.Units.ToPixels (total_height) - page_margins.Bottom  + footer_offset + Pango.Units.ToPixels (logical_rect.Height));

				cr.MoveTo (Pango.Units.ToPixels (total_width - logical_rect.Width) - CmToPixel (0.5, args.Context.DpiX), footer_anchor.Y);
				Pango.CairoHelper.ShowLayoutLine (cr, date_time_footer.Lines [0]);
				
				cr.MoveTo (footer_anchor);
				using (Pango.Layout pages_footer = CreateLayoutForPagenumbers (
					args.Context, args.PageNr + 1, page_breaks.Count + 1)) {
					Pango.CairoHelper.ShowLayoutLine (cr, pages_footer.Lines [0]);
				}
			}
		}
		
		public void OnDrawPage (object sender, Gtk.DrawPageArgs args)
		{
			using (Cairo.Context cr = args.Context.CairoContext) {
				cr.MoveTo (page_margins.Left, page_margins.Top);

				int start_line = 0;
				if (args.PageNr != 0)
					start_line = page_breaks [args.PageNr - 1];

				int last_line = -1;
				if (page_breaks.Count > args.PageNr)
					last_line = page_breaks [args.PageNr] - 1;

				Gtk.TextIter position;
				Gtk.TextIter end_iter;
				Buffer.GetBounds (out position, out end_iter);

				bool done = position.Compare (end_iter) >= 0;
				int line_number = position.Line;

				// Fast-forward to the starting line
				while (!done && line_number < start_line) {
					Gtk.TextIter line_end = position;
					if (!line_end.EndsLine ())
						line_end.ForwardToLineEnd ();

					position.ForwardLine ();
					done = position.Compare (end_iter) >= 0;
					line_number = position.Line;
				}

				// Print the current page's content
				while (!done && ((last_line == -1) || (line_number < last_line))) {
					line_number = position.Line;

					Gtk.TextIter line_end = position;
					if (!line_end.EndsLine ())
						line_end.ForwardToLineEnd ();

					PrintMargins margins;
					using (Pango.Layout layout =
						CreateLayoutForParagraph (args.Context,
							position, line_end, out margins)) {
						foreach (Pango.LayoutLine line in layout.Lines) {
							Pango.Rectangle ink_rect = Pango.Rectangle.Zero;
							Pango.Rectangle logical_rect = Pango.Rectangle.Zero;
							line.GetExtents (ref ink_rect, ref logical_rect);

							cr.MoveTo (
								margins.Left + page_margins.Left,
								cr.CurrentPoint.Y);
							int line_height = Pango.Units.ToPixels(logical_rect.Height);
							Cairo.PointD new_line_point = new Cairo.PointD (
								margins.Left + page_margins.Left,
								cr.CurrentPoint.Y + line_height);
							Pango.CairoHelper.ShowLayoutLine (cr, line);
							cr.MoveTo (new_line_point);
						}
					}

					position.ForwardLine ();
					done = position.Compare (end_iter) >= 0;
				}

				// Print the footer
				PrintFooter (args);
			}
		}

		private void OnEndPrint (object sender, Gtk.EndPrintArgs args)
		{
			if (date_time_footer != null)
				date_time_footer.Dispose ();
			if (page_breaks != null)
				page_breaks.Clear ();
		}
	}
}
