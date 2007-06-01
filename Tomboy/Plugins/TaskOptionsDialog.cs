
using System;
using Mono.Unix;
using Gtk;

using Tomboy;

public class TaskOptionsDialog : Gtk.Dialog
{
#region Private Fields
	Task task;
	Gtk.AccelGroup accel_group;
	
	Gtk.CheckButton due_date_check_button;
	Gtk.Extras.DateButton due_date_button;
	
	Gtk.CheckButton priority_check_button;
	
	Gtk.TextView details_text_view;
#endregion // Private Fields
	
#region Constructors
	public TaskOptionsDialog(Gtk.Window parent,
				Gtk.DialogFlags flags,
				Task task)
		: base ()
	{
		HasSeparator = false;
		BorderWidth = 5;
		Resizable = false;
		//Title = string.Empty;
		Decorated = false;
		this.SetDefaultSize (300, 200);
		this.task = task;
		
		VBox.Spacing = 12;
		ActionArea.Layout = Gtk.ButtonBoxStyle.End;
		
		accel_group = new Gtk.AccelGroup ();
		AddAccelGroup (accel_group);
		
		Gtk.Label l = new Gtk.Label (
				string.Format (
					"<span weight=\"bold\" size=\"small\">{0}</span>",
					Catalog.GetString ("Task Options")));
		l.UseMarkup = true;
		l.Show ();
		VBox.PackStart (l, false, false, 0);
		
		HBox hbox = new HBox (false, 4);
		
		///
		/// Due Date
		///
		due_date_check_button = new CheckButton (Catalog.GetString ("Due Date:"));
		if (task.DueDate != DateTime.MinValue)
			due_date_check_button.Active = true;
		due_date_check_button.Toggled += OnDueDateCheckButtonToggled;
		due_date_check_button.Show ();
		hbox.PackStart (due_date_check_button, false, false, 0);
		
		due_date_button =
				new Gtk.Extras.DateButton (task.DueDate);
		if (task.DueDate == DateTime.MinValue)
			due_date_button.Sensitive = false;
		due_date_button.Show ();
		hbox.PackStart (due_date_button, false, false, 0);
		
		// Spacer
		hbox.PackStart (new Gtk.Label (string.Empty), true, true, 0);
		
		hbox.Show ();
		VBox.PackStart (hbox, false, false, 0);
		
		///
		/// Details
		///
		l = new Label (Catalog.GetString ("Details:"));
		l.Xalign = 0;
		l.Show ();
		VBox.PackStart (l, false, false, 0);
		
		details_text_view = new TextView ();
		details_text_view.Show ();
		
		ScrolledWindow sw = new ScrolledWindow ();
		sw.ShadowType = Gtk.ShadowType.EtchedIn;
		sw.Add (details_text_view);
		sw.Show ();
		
		VBox.PackStart (sw, true, true, 0);
		
		AddButton (Gtk.Stock.Close, Gtk.ResponseType.Close, true);
		
		if (parent != null)
			TransientFor = parent;

		if ((int) (flags & Gtk.DialogFlags.Modal) != 0)
			Modal = true;

		if ((int) (flags & Gtk.DialogFlags.DestroyWithParent) != 0)
			DestroyWithParent = true;
	}
#endregion // Constructors

#region Private Methods
	protected override void OnRealized ()
	{
		base.OnRealized ();
		
		Logger.Debug ("Attempting to set the text buffer to: {0}", task.Details);
		details_text_view.Buffer.Text = task.Details;
	}

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
		Hide ();
		
		task.Details = details_text_view.Buffer.Text;
	}
#endregion // Private Methods

#region Event Handlers
	void OnDueDateCheckButtonToggled (object sender, EventArgs args)
	{
		if (due_date_check_button.Active) {
			due_date_button.Sensitive = true;
			if (due_date_button.Date == DateTime.MinValue)
				due_date_button.Date = DateTime.Now.AddDays (1);
			task.DueDate = due_date_button.Date;
		} else {
			due_date_button.Sensitive = false;
			task.DueDate = DateTime.MinValue;
		}
	}
#endregion // Event Handlers
}
