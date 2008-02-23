
using System;

namespace Tomboy
{
	public class RecentTreeView : Gtk.TreeView
	{
		public RecentTreeView()
		{
		}
		
		protected override void OnDragBegin (Gdk.DragContext ctx)
		{
			// Block Gtk.TreeView so multi selection dnd works
		}
	}
}
