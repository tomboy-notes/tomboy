
using System;
using Mono.Unix;
using Gtk;

namespace Gtk.Extras
{
	public delegate void DateEditedHandler (CellRendererDate renderer, string path);
	
	public class CellRendererDate : Gtk.CellRenderer//, Gtk.CellEditable
	{
		DateTime date;
		bool editable;
		Gtk.ResponseType response_type;

#region Constructors		
		public CellRendererDate()
		{
			date = DateTime.MinValue;
			editable = false;
			response_type = Gtk.ResponseType.None;
		}
		
		protected CellRendererDate (System.IntPtr ptr) : base (ptr)
		{
		}
#endregion // Constructors
		
#region Public Properties
		public DateTime Date
		{
			get { return date; }
			set { date = value; }
		}
		
		/// <summary>
		/// If the renderer is editable, a date picker widget will appear when
		/// the user attempts to edit the cell.
		/// </summary>
		public bool Editable
		{
			get { return editable; }
			set {
				editable = value;
				
				if (editable)
					Mode = CellRendererMode.Editable;
				else
					Mode = CellRendererMode.Inert;
			}
		}
#endregion // Public Properties

#region Public Methods
		public override void GetSize (Widget widget, ref Gdk.Rectangle cell_area,
				out int x_offset, out int y_offset, out int width, out int height)
		{
			Pango.Layout layout = GetLayout (widget);
			
			// FIXME: If this code is ever built into its own library,
			// the call to Tomboy will definitely have to change
			layout.SetText (Tomboy.GuiUtils.GetPrettyPrintDate (date));
			
			CalculateSize (layout, out x_offset, out y_offset, out width, out height);
		}
		
		public override CellEditable StartEditing (Gdk.Event evnt,
				Widget widget, string path, Gdk.Rectangle background_area,
				Gdk.Rectangle cell_area, CellRendererState flags)
		{
			Gtk.Extras.DateTimeChooserDialog dialog =
					new Gtk.Extras.DateTimeChooserDialog (null, DialogFlags.Modal,
						date == DateTime.MinValue ? DateTime.Now : date);
			dialog.TreePathString = path;
			dialog.EditingDone += OnDateEditingDone;
			dialog.Response += OnDialogResponse;
			dialog.Show ();
			
			return null;
		}
 
#endregion

#region Public Events

		public event DateEditedHandler Edited;

#endregion // Public Events

#region Private Methods
		protected override void Render (Gdk.Drawable drawable, Widget widget,
				Gdk.Rectangle background_area, Gdk.Rectangle cell_area,
				Gdk.Rectangle expose_area, CellRendererState flags)
		{
			Pango.Layout layout = GetLayout (widget);

			// FIXME: If this code is ever built into its own library,
			// the call to Tomboy will definitely have to change
			layout.SetText (Tomboy.GuiUtils.GetPrettyPrintDate (date));
			
			int x, y, w, h;
			CalculateSize (layout, out x, out y, out w, out h);
			Xalign = x;
			Yalign = y;
			Width = w;
			Height = h;
			
			// FIXME: Use the proper Gtk.StateType so text appears properly
			
			Gdk.GC gc = widget.Style.TextGC(Gtk.StateType.Normal);
			
			drawable.DrawLayout (
				gc,
				cell_area.X + (int)Xalign + (int)Xpad,
				cell_area.Y + (int)Yalign + (int)Ypad,
				layout);
		}

		Pango.Layout GetLayout (Gtk.Widget widget)
		{
			return widget.CreatePangoLayout (string.Empty);
		}
		
		void CalculateSize (Pango.Layout layout, out int x, out int y,
				out int width, out int height)
		{
			int w, h;
			
			layout.GetPixelSize (out w, out h);
			
			x = 0;
			y = 0;
			width = w + ((int) Xpad) * 2;
			height = h + ((int) Ypad) * 2;
		}
#endregion

#region Event Handlers
		[GLib.ConnectBefore]
		void OnDialogResponse (object sender, Gtk.ResponseArgs args)
		{
			System.Console.WriteLine ("Response: {0}", args.ResponseId);
			response_type = args.ResponseId;
		}
		
		void OnDateEditingDone (object sender, EventArgs args)
		{
			Gtk.Extras.DateTimeChooserDialog dialog = sender as Gtk.Extras.DateTimeChooserDialog;

			if (response_type != Gtk.ResponseType.Cancel) {
				date = dialog.Date;
				
				if (Edited != null)
					Edited (this, dialog.TreePathString);
			}

			dialog.Destroy ();
		}
#endregion // Event Handlers
	}
}
