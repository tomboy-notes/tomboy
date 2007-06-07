
using System;
using Mono.Unix;
using Gtk;

namespace Gtk.Extras
{
	public delegate void DateEditedHandler (CellRendererDate renderer, string path);
	
	public class CellRendererDate : Gtk.CellRenderer
	{
		DateTime date;
		bool editable;
		bool show_time;
		
		Window popup;
		Calendar cal;
		string path;
		
		// The following variable is set during StartEditing ()
		// and this is kind of a hack, but it "works".  The widget
		// that is passed-in to StartEditing() is the TreeView.
		// This is used to position the calendar popup.
		Gtk.TreeView tree;

		private const uint CURRENT_TIME = 0;

#region Constructors
		/// <summary>
		/// <param name="parent">The parent window where this CellRendererDate
		/// will be used from.  This is needed to access the Gdk.Screen so the
		/// Calendar will popup in the proper location.</param>
		/// </summary>
		public CellRendererDate()
		{
			date = DateTime.MinValue;
			editable = false;
			popup = null;
			show_time = true;
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
		
		/// <summary>
		/// If true, both the date and time will be shown.  If false, the time
		/// will be omitted.  The default is true.
		/// </summary>
		public bool ShowTime
		{
			get { return show_time; }
			set { show_time = value; }
		}
#endregion // Public Properties

#region Public Methods
		public override void GetSize (Widget widget, ref Gdk.Rectangle cell_area,
				out int x_offset, out int y_offset, out int width, out int height)
		{
			Pango.Layout layout = GetLayout (widget);
			
			// FIXME: If this code is ever built into its own library,
			// the call to Tomboy will definitely have to change
			layout.SetText (Tomboy.GuiUtils.GetPrettyPrintDate (date, show_time));
			
			CalculateSize (layout, out x_offset, out y_offset, out width, out height);
		}
		
		public override CellEditable StartEditing (Gdk.Event evnt,
				Widget widget, string path, Gdk.Rectangle background_area,
				Gdk.Rectangle cell_area, CellRendererState flags)
		{
			this.path = path;
			this.tree = widget as Gtk.TreeView;
			ShowCalendar();
			
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
			layout.SetText (Tomboy.GuiUtils.GetPrettyPrintDate (date, show_time));
			
			int x, y, w, h;
			CalculateSize (layout, out x, out y, out w, out h);

            StateType state = RendererStateToWidgetState(flags);

			Gdk.GC gc;
			if (state.Equals(StateType.Selected)) {
				// Use the proper Gtk.StateType so text appears properly when selected
				gc = new Gdk.GC(drawable);
				gc.Copy(widget.Style.TextGC(state));
				gc.RgbFgColor = widget.Style.Foreground(state);
			} else
				gc = widget.Style.TextGC(Gtk.StateType.Normal);
			
			drawable.DrawLayout (
				gc,
				cell_area.X + (int)Xalign + (int)Xpad,
				cell_area.Y + ((cell_area.Height - h) / 2), 
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

		private void ShowCalendar()
		{
			popup = new Window(WindowType.Popup);
			popup.Screen = tree.Screen;

			Frame frame = new Frame();
			frame.Shadow = ShadowType.Out;
			frame.Show();

			popup.Add(frame);

			VBox box = new VBox(false, 0);
			box.Show();
			frame.Add(box);
			
			cal = new Calendar();
			cal.DisplayOptions = CalendarDisplayOptions.ShowHeading
				| CalendarDisplayOptions.ShowDayNames 
				| CalendarDisplayOptions.ShowWeekNumbers;
				
			cal.KeyPressEvent += OnCalendarKeyPressed;
			popup.ButtonPressEvent += OnButtonPressed;
			
			cal.Show();
			
			Alignment calAlignment = new Alignment(0.0f, 0.0f, 1.0f, 1.0f);
			calAlignment.Show();
			calAlignment.SetPadding(4, 4, 4, 4);
			calAlignment.Add(cal);
		
			box.PackStart(calAlignment, false, false, 0);
			
			// FIXME: Make the popup appear directly below the date
			Gdk.Rectangle allocation = tree.Allocation;
//			Gtk.Requisition req = tree.SizeRequest ();
			int x = 0, y = 0;
			tree.GdkWindow.GetOrigin(out x, out y);
//			popup.Move(x + allocation.X, y + allocation.Y + req.Height + 3);
			popup.Move(x + allocation.X, y + allocation.Y);
			popup.Show();
			popup.GrabFocus();
				
			Grab.Add(popup);

			Gdk.GrabStatus grabbed = Gdk.Pointer.Grab(popup.GdkWindow, true, 
				Gdk.EventMask.ButtonPressMask 
				| Gdk.EventMask.ButtonReleaseMask 
				| Gdk.EventMask.PointerMotionMask, null, null, CURRENT_TIME);

			if(grabbed == Gdk.GrabStatus.Success) {
				grabbed = Gdk.Keyboard.Grab(popup.GdkWindow, 
					true, CURRENT_TIME);

				if(grabbed != Gdk.GrabStatus.Success) {
					Grab.Remove(popup);
					popup.Destroy();
					popup = null;
				}
			} else {
				Grab.Remove(popup);
				popup.Destroy();
				popup = null;
			}
				
    		cal.DaySelectedDoubleClick += OnCalendarDaySelected;
			cal.ButtonPressEvent += OnCalendarButtonPressed;

			cal.Date = date == DateTime.MinValue ? DateTime.Now : date;
		}
		
		public void HideCalendar(bool update)
		{
			if(popup != null) {
				Grab.Remove(popup);
				Gdk.Pointer.Ungrab(CURRENT_TIME);
				Gdk.Keyboard.Ungrab(CURRENT_TIME);

				popup.Destroy();
				popup = null;
			}

			if(update) {
				date = cal.GetDate();

				if (Edited != null)
					Edited (this, path);
			}
		}

        private StateType RendererStateToWidgetState(CellRendererState flags)
        {
            StateType state = StateType.Normal;
            if((CellRendererState.Selected & flags).Equals(
                CellRendererState.Selected))
                state = StateType.Selected;
            return state;
        }
#endregion

#region Event Handlers
		private void OnButtonPressed(object o, ButtonPressEventArgs args)
		{
			if(popup != null)
				HideCalendar(false);
		}
		
		private void OnCalendarDaySelected(object o, EventArgs args)
		{
			HideCalendar(true);
		}
		
		private void OnCalendarButtonPressed(object o, 
			ButtonPressEventArgs args)
		{
			args.RetVal = true;
		}
	
		private void OnCalendarKeyPressed(object o, KeyPressEventArgs args)
		{
			switch(args.Event.Key) {
				case Gdk.Key.Escape:
					HideCalendar(false);
					break;
				case Gdk.Key.KP_Enter:
				case Gdk.Key.ISO_Enter:
				case Gdk.Key.Key_3270_Enter:
				case Gdk.Key.Return:
				case Gdk.Key.space:
				case Gdk.Key.KP_Space:
					HideCalendar(true);
					break;
				default:
					break;
			}
		}

#endregion // Event Handlers
	}
}
