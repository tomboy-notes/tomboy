
using System;
using Gtk;

namespace Gtk.Extras
{
	public delegate void DateSelectedHandler (DateTimeChooser chooser, DateTime date);
	
	/// <summary>
	/// This Widget displays a simple month calendar which the user can
	/// select a date from it.
	/// FIXME: Implement DateTimecChooser.
	/// </summary>
	public class DateTimeChooser : Gtk.EventBox
	{
#region Private Fields
		DateTime date;
		Gtk.Entry entry;
#endregion // Private Fields

#region Constructors
		public DateTimeChooser()
		{
			date = DateTime.MinValue;
			
			entry = new Gtk.Entry (date.ToString ());
			entry.Changed += OnEntryChanged;
			entry.Show ();
			this.Add (entry);
		}
#endregion // Constructors

#region Public Properties
		public DateTime Date
		{
			get { return date; }
			set {
				date = value;
				entry.Text = date.ToString ();
			}
		}
#endregion // Public Properties

#region Event Handlers
		void OnEntryChanged (object sender, EventArgs args)
		{
			try {
				DateTime new_date = DateTime.Parse (entry.Text.Trim ());
				date = new_date;
				if (DateSelected != null)
					DateSelected (this, date);
			} catch {}
		}
#endregion

#region Public Events
		public event DateSelectedHandler DateSelected;
#endregion // Public Events
	}
}
