/***************************************************************************
 *  DateButton.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using GLib;
using Gtk;

namespace Gtk.Extras
{
	public class DateButton : ToggleButton
	{
		private Window popup;
		private DateTime date;
		private Calendar cal;
		private bool show_time;
	
		private const uint CURRENT_TIME = 0;
		
		// FIXME: If this is ever moved to its own library
		// this reference to Tomboy will obviously have to
		// be removed.
		public DateButton(DateTime date_time, bool show_time)
			: base(Tomboy.GuiUtils.GetPrettyPrintDate (date_time, show_time))
		{
			Toggled += OnToggled;
			popup = null;
			date = date_time;
			this.show_time = show_time;
		}
		
		private void ShowCalendar()
		{
			popup = new Window(WindowType.Popup);
			popup.Screen = this.Screen;

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
				
			Requisition req = SizeRequest();
			int x = 0, y = 0;
			GdkWindow.GetOrigin(out x, out y);
			popup.Move(x + Allocation.X, y + Allocation.Y + req.Height + 3);
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

			cal.Date = date;
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
				// FIXME: If this is ever moved to its own library
				// this reference to Tomboy will obviously have to
				// be removed.
				Label = Tomboy.GuiUtils.GetPrettyPrintDate (date, show_time);
			}

			Active = false;
		}
		
		private void OnToggled(object o, EventArgs args)
		{
			if(Active) {
				ShowCalendar();
			} else {
				HideCalendar(false);
			}
		}
		
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
		
		public DateTime Date
		{
			get { return date; }
			set {
				date = value;
				Label = Tomboy.GuiUtils.GetPrettyPrintDate (date, show_time);
			}
		}
		
		/// <summary>
		/// If true, both the date and time will be shown.  If false, the time
		/// will be omitted.
		/// </summary>
		public bool ShowTime
		{
			get { return show_time; }
			set { show_time = value; }
		}
	}
}
