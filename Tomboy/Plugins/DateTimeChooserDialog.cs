
using System;
using Mono.Unix;

namespace Gtk.Extras
{
	/// <summary>
	/// This dialog was built for CellRendererDate and allows a user to select
	/// a date from a simple calendar.
	/// </summary>
	public class DateTimeChooserDialog : Gtk.Dialog, Gtk.CellEditable
	{
		Gtk.AccelGroup accel_group;
		DateTimeChooser date_time_chooser;
		string path;
		
#region Constructors
		public DateTimeChooserDialog(Gtk.Window parent,
					Gtk.DialogFlags flags,
					DateTime date)
			: base ()
		{
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = string.Empty;
			path = string.Empty;
			
			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;
			
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);
			
			date_time_chooser = new DateTimeChooser ();
			date_time_chooser.Date = date;
			date_time_chooser.DateSelected += OnDateSelected;
			date_time_chooser.Show ();
			VBox.PackStart (date_time_chooser, true, true, 0);
			
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, false);
			AddButton (Catalog.GetString ("Today"), Gtk.ResponseType.Ok);
			AddButton (Catalog.GetString ("None"), Gtk.ResponseType.No);
			
			DefaultResponse = Gtk.ResponseType.Ok;

			if (parent != null)
				TransientFor = parent;

			if ((int) (flags & Gtk.DialogFlags.Modal) != 0)
				Modal = true;

			if ((int) (flags & Gtk.DialogFlags.DestroyWithParent) != 0)
				DestroyWithParent = true;
		}
#endregion

#region Public Properties
		public DateTime Date
		{
			get { return date_time_chooser.Date; }
			set { date_time_chooser.Date = value; }
		}
		
		public string Path
		{
			get { return path; }
			set { path = value; }
		}
#endregion // Public Properties

#region Private Methods
		void AddButton (string stock_id, Gtk.ResponseType response, bool is_default)
		{
			Gtk.Button button = new Gtk.Button (stock_id);
			button.CanDefault = true;
			button.Show ();

			AddActionWidget (button, response);

			if (is_default) {
				DefaultResponse = response;
				button.AddAccelerator ("activate",
						       accel_group,
						       (uint) Gdk.Key.Escape, 
						       0,
						       Gtk.AccelFlags.Visible);
			}
		}
		
		protected override void OnResponse (ResponseType response_id)
		{
			switch (response_id) {
			case Gtk.ResponseType.Ok:
				date_time_chooser.Date = DateTime.Now;
				break;
			case Gtk.ResponseType.No:
				date_time_chooser.Date = DateTime.MinValue;
				break;
			default:
				break;
			}
			
			Hide ();
			
			if (EditingDone != null)
				EditingDone (this, EventArgs.Empty);
		}

#endregion // Private Methods

#region Event Handlers
		void OnDateSelected (DateTimeChooser chooser, DateTime date)
		{
			this.Respond (ResponseType.Apply);
		}
#endregion // EventHandlers

#region Gtk.CellEditable Interfaces
		public void StartEditing (Gdk.Event evnt)
		{
		}
		
		public void FinishEditing ()
		{
		}
		
		public void RemoveWidget ()
		{
		}
		
		public event System.EventHandler WidgetRemoved;
		public event System.EventHandler EditingDone;
#endregion // Gtk.CellEditable Interfaces
	}
}
