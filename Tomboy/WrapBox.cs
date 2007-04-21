/* Original file gladly copied from http://svn.ndesk.org/ndesk/Parts/WrapBox.cs */

using System;
using System.Collections.Generic;
using Gtk;

namespace Tomboy
{
	public class WrapBox : TextView
	{
		EventBox eb;
		TextView tv;
		public bool IsBase;
		Dictionary<Widget, TextChildAnchor> child_anchors;
		
		#region Constructors
		public WrapBox()
		{
			eb = new EventBox ();
			tv = this;
			IsBase = false;
			
			tv.Realized += WrapBoxRealized;
			tv.WidgetEvent += WrapBoxWidgetEvent;
			
			tv.WrapMode = WrapMode.Char;
			tv.CursorVisible = false;
			tv.Editable = false;
			
			// Not sure what the following line does
			Raw = tv.Handle;
			
			child_anchors = new Dictionary<Widget, TextChildAnchor> ();
		}
		#endregion
		
		#region Base Class Overrides
		protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
		{
			return false;
		}
		
		protected override bool OnKeyPressEvent (Gdk.EventKey evnt)
		{
			return false;
		}
		
		protected override bool OnKeyReleaseEvent (Gdk.EventKey evnt)
		{
			return false;
		}

		#endregion
		
		#region Event Handlers
		[GLib.ConnectBefore]
		void WrapBoxRealized (object sender, EventArgs args)
		{
			if (IsBase) {
				tv.ModifyBg (StateType.Normal, tv.Style.Base (StateType.Normal));
			} else {
				tv.ModifyBg (StateType.Normal, tv.Style.Background (StateType.Normal));
				tv.ModifyBase (StateType.Normal, tv.Style.Background (StateType.Normal));
			}
			
			Gdk.Cursor cursor = new Gdk.Cursor (Gdk.CursorType.TopLeftArrow);
			tv.GetWindow (TextWindowType.Text).Cursor = cursor;
		}
		
		void WrapBoxWidgetEvent (object sender, WidgetEventArgs args)
		{
			// TODO: do scrolling like acrobat reader.  drag across
		}
		#endregion
		
		#region Public Methods
//		public void Insert (string text)
//		{
//			TextIter end_iter = tv.Buffer.EndIter;
//			tv.Buffer.Insert (ref end_iter, text);
//		}
		
		public void Insert (Widget child)
		{
			TextIter end_iter = tv.Buffer.EndIter;
			TextChildAnchor anchor;
			
			anchor = tv.Buffer.CreateChildAnchor (ref end_iter);
			
			tv.AddChildAtAnchor (child, anchor);
			
			// Keep track of the widget's anchor so that we can
			// remove the anchor and the widget inside of the
			// Remove () call.
			child_anchors [child] = anchor;
		}
		
		public void Clear ()
		{
			tv.Buffer.Clear ();
		}
		
		public void Remove (Widget child)
		{
			tv.Remove (child);

			// Remove the TextChildAnchor also
			if (child_anchors.ContainsKey (child)) {
				TextChildAnchor anchor =
						child_anchors [child] as TextChildAnchor;
				
				if (anchor.Widgets.Length == 0) {
					TextIter start = tv.Buffer.GetIterAtChildAnchor (anchor);
					TextIter end = start.Copy ();
					end.ForwardChar ();
					tv.Buffer.Delete (ref start, ref end);
				}
				
				child_anchors.Remove (child);
			}
		}
		#endregion
	}
}
