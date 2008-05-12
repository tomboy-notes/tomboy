
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gtk;

using Tomboy;
 
namespace Tomboy.PrintNotes
{
	public class PrintNotesNoteAddin : NoteAddin
	{
		private Gtk.ImageMenuItem item;
		private int font_size = 12;			
		private List<int> pageBreaks;
		private Pango.Layout layout;
	
		public override void Initialize ()
		{
			item = new Gtk.ImageMenuItem (Catalog.GetString ("Print"));
			item.Image = new Gtk.Image (Gtk.Stock.Print,
			                            Gtk.IconSize.Menu);
			item.Activated += PrintButtonClicked;
			item.Show ();
			AddPluginMenuItem (item);
		}

		public override void Shutdown ()
		{
			// Disconnect the event handlers so
			// there aren't any memory leaks.
			item.Activated -= PrintButtonClicked;
		}

		public override void OnNoteOpened ()
		{
			// Do nothing.
		}
		
		private void PrintButtonClicked (object sender, EventArgs args)
		{
			Gtk.PrintOperation op = new PrintOperation ();
			op.BeginPrint += OnBeginPrint;
			op.DrawPage += OnDrawPage;
			op.EndPrint += OnEndPrint;
			
			op.Run (Gtk.PrintOperationAction.PrintDialog, this.Window);
		}
		
		private void OnBeginPrint (object sender, Gtk.BeginPrintArgs args)
		{
			PrintContext context = args.Context;
			double width = context.Width;
			double height = context.Height;
			
			layout = context.CreatePangoLayout ();
			
			Pango.FontDescription desc =
				Pango.FontDescription.FromString ("sans " +
				                                  font_size);
			layout.FontDescription = desc;
			
			layout.Width = Pango.Units.FromDouble (context.Width);
			
			Gtk.TextIter start_iter, end_iter;
			Buffer.GetBounds (out start_iter, out end_iter);
			layout.SetText (
				Buffer.GetText (start_iter, end_iter, false));
			
			int numLines = layout.LineCount;
			
			pageBreaks = new List<int> ();
			double pageHeight = 0;
			
			for (int i = 0; i < numLines; i++) {
				Pango.Rectangle inkRect =
					new Pango.Rectangle ();
				Pango.Rectangle logicalRect =
					new Pango.Rectangle ();
				
				Pango.LayoutLine layoutLine =
					layout.GetLine (i);
				layoutLine.GetExtents (ref inkRect,
				                       ref logicalRect);
				
				double lineHeight = logicalRect.Height / 1024.0;
				
				if (pageHeight + lineHeight > height) {
					pageBreaks.Add (i);
					pageHeight = 0;
				}
				
				pageHeight += lineHeight;
			}
			
			PrintOperation op = (PrintOperation) sender;
			op.NPages = pageBreaks.Count + 1;
		}
		
		public void OnDrawPage (object sender, Gtk.DrawPageArgs args)
		{
			Cairo.Context cr = args.Context.CairoContext;
			
			int start, end, i;
			
			if (args.PageNr == 0)
				start = 0;
			else
				start = pageBreaks [args.PageNr - 1];
			
			if (pageBreaks.Count <= args.PageNr)
				end = layout.LineCount;
			else
				end = pageBreaks [args.PageNr];
			
			i = 0;
			Pango.LayoutIter iter = layout.Iter;
			do {
				int baseline = 0;
				
				if (i >= start) {
					Pango.LayoutLine line = iter.Line;
					Pango.Rectangle logicalRect =
						new Pango.Rectangle ();
					Pango.Rectangle dummyRect =
						new Pango.Rectangle ();

					line.GetExtents (ref dummyRect, 
					                 ref logicalRect);
					baseline = iter.Baseline;
					
					if (i == start) {
						cr.MoveTo (0, 0);
					}
					
					Pango.CairoHelper.ShowLayoutLine (cr, line);
					
					int height, width;
					layout.GetSize (out width, out height);
					cr.RelMoveTo (0, (int)
					              (logicalRect.Height /
					               Pango.Scale.PangoScale));
				}
				
				i++;
			}			
			while (i < end && iter.NextLine ());
		}
		
		private void OnEndPrint (object sender, Gtk.EndPrintArgs args)
		{
			layout = null;
			pageBreaks.Clear ();
			
			// TODO: How to fix this error when quitting?
			// "Cairo.Context: called from finalization thread
			// programmer is missing a call to Dispose"
			// May be a cairo/pango/gtk-sharp problem?
		}
	}
}
