
using System;
using Gtk;

namespace Tomboy
{
	// <summary>
	// This widget is meant to be used in the NoteTagBar and allows the user to
	// click the tag to remove it from the tag list.
	// </summary>
	public class TagButton : Gtk.Button
	{
		Image image;
		Tag tag;

		public TagButton (Tag tag)
		{
			this.tag = tag;
			this.Label = tag.Name;
			this.Relief = ReliefStyle.None;
			this.EnterNotifyEvent += EnterNotifyEventHandler;
			this.LeaveNotifyEvent += LeaveNotifyEventHandler;

			image = new Image ();
			image.Visible = false;
			image.NoShowAll = true;

			this.Image = image;
		}

		// <summary>
		// Show the remove image
		// </summary>
		void EnterNotifyEventHandler (object sender, EnterNotifyEventArgs args)
		{
			image.SetFromStock (Stock.Remove, IconSize.Menu);
		}

		// <summary>
		// Hide the remove image
		// </summary>
		void LeaveNotifyEventHandler (object sender, LeaveNotifyEventArgs args)
		{
			image.Clear ();
		}

		#region Properties
		public Tag Tag
		{
			get {
				return tag;
			}
		}
		#endregion
	}
}
