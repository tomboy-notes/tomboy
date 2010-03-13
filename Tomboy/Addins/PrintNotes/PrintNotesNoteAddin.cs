using System;
using System.Collections.Generic;
using Mono.Unix;

namespace Tomboy.PrintNotes
{
	public class PageBreak
	{
		private readonly int break_paragraph;
		private readonly int break_line;

		public int Paragraph
		{
			get { return break_paragraph; }
		}
		
		public int Line
		{
			get { return break_line; }
		}

		public PageBreak(int paragraph, int line)
		{
			break_paragraph = paragraph;
			break_line = line;
		}
	}
	
	public class PrintNotesNoteAddin : NoteAddin
	{		
		private Gtk.ImageMenuItem item;
		private int margin_top;
		private int margin_left;
		private int margin_right;
		private int margin_bottom;

		private Pango.Layout timestamp_footer;
		private IList<PageBreak> page_breaks;

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
				page_breaks = new List<PageBreak> ();				
				
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

		private static int CmToPixel (double cm, double dpi)
		{
			return (int) (cm * dpi / 2.54);
		}

		private static int InchToPixel (double inch, double dpi)
		{
			return (int) (inch * dpi);
		}
		
		private IEnumerable<Pango.Attribute> GetParagraphAttributes (
			Pango.Layout layout, double dpiX, out int indentation,
			ref Gtk.TextIter position, Gtk.TextIter limit)
		{
			IList<Pango.Attribute> attributes = new List<Pango.Attribute> ();
			indentation = 0;
			
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
					indentation = (int) (tag.LeftMargin / screen_dpiX * dpiX);
				}
				if (tag.RightMarginSet) {
					indentation = (int) (tag.RightMargin / screen_dpiX * dpiX);
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

		private Pango.Layout CreateParagraphLayout (Gtk.PrintContext context,
							       Gtk.TextIter p_start,
							       Gtk.TextIter p_end,
							       out int indentation)
		{
			Pango.Layout layout = context.CreatePangoLayout ();
			layout.FontDescription = Window.Editor.Style.FontDesc;
			int start_index = p_start.LineIndex;
			indentation = 0;

			double dpiX = context.DpiX;
			using (Pango.AttrList attr_list = new Pango.AttrList ()) {
				Gtk.TextIter segm_start = p_start;
				Gtk.TextIter segm_end;
				
				while (segm_start.Compare (p_end) < 0) {
					segm_end = segm_start;
					IEnumerable<Pango.Attribute> attrs =
						GetParagraphAttributes (
							layout, dpiX, out indentation,
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

			DepthNoteTag depth = Buffer.FindDepthTag (p_start);
			if (depth != null)
				indentation += ((int) (dpiX / 3)) * depth.Depth;

			layout.Width = Pango.Units.FromPixels ((int)context.Width -
				margin_left - margin_right - indentation);
			layout.Wrap = Pango.WrapMode.WordChar;
			layout.SetText (Buffer.GetSlice (p_start, p_end, false));
			return layout;
		}
		
		private Pango.Layout CreatePagenumbersLayout (Gtk.PrintContext context,
		                                              int page_number, int total_pages)
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

                private Pango.Layout CreateTimestampLayout (Gtk.PrintContext context)
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
                        layout.Alignment = Pango.Alignment.Right;
                        layout.SetText (footer_right);

                        return layout;
                }
		
		private int ComputeFooterHeight (Gtk.PrintContext context) {
			using (Pango.Layout layout = CreateTimestampLayout (context)) {	                        
				Pango.Rectangle ink_rect;
				Pango.Rectangle logical_rect;
				layout.GetExtents (out ink_rect, out logical_rect);
				
				// Compute the footer height, include the space for the horizontal line
				return  Pango.Units.ToPixels (ink_rect.Height) +
                                               CmToPixel (0.5, context.DpiY);
			}			
		}
		
		private void OnBeginPrint (object sender, Gtk.BeginPrintArgs args)
		{
			Gtk.PrintOperation op = (Gtk.PrintOperation) sender;			
			Gtk.PrintContext context = args.Context;
			timestamp_footer = CreateTimestampLayout (context);
			
			// FIXME: These should be configurable settings later (UI Change)
			margin_top = CmToPixel (1.5, context.DpiY);
			margin_left = CmToPixel (1, context.DpiX);
			margin_right = CmToPixel (1, context.DpiX);
			margin_bottom = 0;
			double max_height = Pango.Units.FromPixels ((int) context.Height - 
						margin_top - margin_bottom - ComputeFooterHeight (context));
			
			Gtk.TextIter position;
			Gtk.TextIter end_iter;
			Buffer.GetBounds (out position, out end_iter);

			double page_height = 0;
			bool done = position.Compare (end_iter) >= 0;
			while (!done) {
				Gtk.TextIter line_end = position;
				if (!line_end.EndsLine ())
					line_end.ForwardToLineEnd ();
				
				int paragraph_number = position.Line;
				int indentation;
				using (Pango.Layout layout = CreateParagraphLayout (
					context, position, line_end, out indentation)) {

					Pango.Rectangle ink_rect = Pango.Rectangle.Zero;
					Pango.Rectangle logical_rect = Pango.Rectangle.Zero;
					for (int line_in_paragraph = 0; line_in_paragraph < layout.LineCount;
					     line_in_paragraph++) {
						Pango.LayoutLine line = layout.GetLine (line_in_paragraph);
						line.GetExtents (ref ink_rect, ref logical_rect);

						if (page_height + logical_rect.Height >= max_height) {
							PageBreak page_break = new PageBreak (
								paragraph_number, line_in_paragraph);
							page_breaks.Add (page_break);
	
							page_height = 0;
						}
						page_height += logical_rect.Height;
					}

					position.ForwardLine ();
					done = position.Compare (end_iter) >= 0;
				}
			}

			op.NPages = page_breaks.Count + 1;
		}

		public void OnDrawPage (object sender, Gtk.DrawPageArgs args)
		{
			using (Cairo.Context cr = args.Context.CairoContext) {
				cr.MoveTo (margin_left, margin_top);

				PageBreak start;
				if (args.PageNr == 0) {
					start = new PageBreak (0, 0);
				} else {
					start = page_breaks [args.PageNr - 1];
				}				

				PageBreak end;			
				if (args.PageNr < page_breaks.Count) {
					end = page_breaks [args.PageNr];
				} else {
					end = new PageBreak (-1, -1);
				}

				Gtk.PrintContext context = args.Context;
				Gtk.TextIter position;
				Gtk.TextIter end_iter;
				Buffer.GetBounds (out position, out end_iter);

				// Fast-forward to the right starting paragraph
				while (position.Line < start.Paragraph) {
					position.ForwardLine ();
				}
				
				bool done = position.Compare (end_iter) >= 0;
				while (!done) {
					Gtk.TextIter line_end = position;
					if (!line_end.EndsLine ())
						line_end.ForwardToLineEnd ();

					int paragraph_number = position.Line;
					int indentation;
					using (Pango.Layout layout = CreateParagraphLayout (
						context, position, line_end, out indentation)) {
						
						for (int line_number = 0;
						     line_number < layout.LineCount && !done;
						     line_number++) {
							// Skip the lines up to the starting line in the
							// first paragraph on this page
							if ((paragraph_number == start.Paragraph) &&
							    (line_number < start.Line)) {
								continue;
							}

							// Break as soon as we hit the end line
							if ((paragraph_number == end.Paragraph) &&
							    (line_number == end.Line)) {
								done = true;
								break;
							}

							Pango.LayoutLine line = layout.Lines [line_number];
							Pango.Rectangle ink_rect = Pango.Rectangle.Zero;
							Pango.Rectangle logical_rect = Pango.Rectangle.Zero;
							line.GetExtents (ref ink_rect, ref logical_rect);
		
							cr.MoveTo (margin_left + indentation,
								cr.CurrentPoint.Y);
							int line_height = Pango.Units.ToPixels (logical_rect.Height);
							
							Cairo.PointD new_line_point = new Cairo.PointD (
								margin_left + indentation,
								cr.CurrentPoint.Y + line_height);
							
							Pango.CairoHelper.ShowLayoutLine (cr, line);
							cr.MoveTo (new_line_point);
						}
					}
	
					position.ForwardLine ();
					done = done || position.Compare (end_iter) >= 0;
				}				

				int total_height = (int) args.Context.Height;
				int total_width = (int) args.Context.Width; 
				int footer_height = 0;

				Cairo.PointD footer_anchor;
				using (Pango.Layout pages_footer = CreatePagenumbersLayout (
					args.Context, args.PageNr + 1, page_breaks.Count + 1)) {
					Pango.Rectangle ink_footer_rect;
					Pango.Rectangle logical_footer_rect;
					pages_footer.GetExtents (out ink_footer_rect, out logical_footer_rect);

					footer_anchor = new Cairo.PointD (
						CmToPixel (0.5, args.Context.DpiX),
						total_height - margin_bottom);
					footer_height = Pango.Units.ToPixels (logical_footer_rect.Height);
					
					cr.MoveTo (
				           total_width - Pango.Units.ToPixels (logical_footer_rect.Width) -
				           CmToPixel (0.5, args.Context.DpiX),
				           footer_anchor.Y);
				
					Pango.CairoHelper.ShowLayoutLine (cr, pages_footer.Lines [0]);
				}
				
				cr.MoveTo (footer_anchor);
				Pango.CairoHelper.ShowLayoutLine (cr, timestamp_footer.Lines [0]);
				
				cr.MoveTo (CmToPixel (0.5, args.Context.DpiX),
				           total_height - margin_bottom - footer_height);
				cr.LineTo (total_width - CmToPixel (0.5, args.Context.DpiX),
				           total_height - margin_bottom - footer_height);
				cr.Stroke ();
			}
		}

		private void OnEndPrint (object sender, Gtk.EndPrintArgs args)
		{
			if (timestamp_footer != null) {
				timestamp_footer.Dispose ();
				timestamp_footer = null;
			}
		}
	}
}