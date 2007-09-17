/***************************************************************************
 *  ComplexMenuItem.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Aaron Bockover <abockover@novell.com>
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
using Gtk;

//namespace Banshee.Widgets
namespace Gtk.Extras
{
    public class ComplexMenuItem : MenuItem
    {
        private bool is_selected = false;

        public ComplexMenuItem() : base()
        {
        }
        
        protected void ConnectChildExpose(Widget widget)
        {
            widget.ExposeEvent += OnChildExposeEvent;
        }

        [GLib.ConnectBefore]
        private void OnChildExposeEvent(object o, ExposeEventArgs args)
        {
            // NOTE: This is a little insane, but it allows packing of EventBox based widgets
            // into a GtkMenuItem without breaking the theme (leaving an unstyled void in the item).
            // This method is called before the EventBox child does its drawing and the background
            // is filled in with the proper style.
            
            int x, y, width, height;
            Widget widget = (Widget)o;

            if(IsSelected) {
                x = Allocation.X - widget.Allocation.X;
                y = Allocation.Y - widget.Allocation.Y;
                width = Allocation.Width;
                height = Allocation.Height;
                
                ShadowType shadow_type = (ShadowType)StyleGetProperty("selected-shadow-type");
                Gtk.Style.PaintBox(Style, widget.GdkWindow, StateType.Prelight, shadow_type,
                    args.Event.Area, widget, "menuitem", x, y, width, height);
            } else {
                // Fill only the visible area in solid color, to be most efficient
                widget.GdkWindow.DrawRectangle(Parent.Style.BackgroundGC(StateType.Normal), 
                    true, 0, 0, widget.Allocation.Width, widget.Allocation.Height);
               
                // FIXME: The above should not be necessary, but Clearlooks-based themes apparently 
                // don't provide any style for the menu background so we have to fill it first with 
                // the correct theme color. Weak.
                //
                // Do a complete style paint based on the size of the entire menu to be compatible with
                // themes that provide a real style for "menu"
                x = Parent.Allocation.X - widget.Allocation.X;
                y = Parent.Allocation.Y - widget.Allocation.Y;
                width = Parent.Allocation.Width;
                height = Parent.Allocation.Height;
                
                Gtk.Style.PaintBox(Style, widget.GdkWindow, StateType.Normal, ShadowType.Out,
                    args.Event.Area, widget, "menu", x, y, width, height);
            }
        }
        
        protected override void OnSelected()
        {
            base.OnSelected();
            is_selected = true;
        }
        
        protected override void OnDeselected()
        {
            base.OnDeselected();
            is_selected = false;
        }
        
        protected override void OnParentSet(Widget previous_parent)
        {
            if(previous_parent != null) {
                previous_parent.KeyPressEvent -= OnKeyPressEventProxy;
            }
            
            if(Parent != null) {
                Parent.KeyPressEvent += OnKeyPressEventProxy;
            }
        }
        
        [GLib.ConnectBefore]
        private void OnKeyPressEventProxy(object o, KeyPressEventArgs args)
        {
            if(!IsSelected) {
                return;
            }

            switch(args.Event.Key) {
                case Gdk.Key.Up:
                case Gdk.Key.Down:
                case Gdk.Key.Escape:
                    return;
            }

            args.RetVal = OnKeyPressEvent(args.Event);
        }

        protected override bool OnKeyPressEvent(Gdk.EventKey evnt)
        {
            return false;
        }
        
        protected bool IsSelected {
            get { return is_selected; }
        }
    }
}
