
using System;
using Mono.Unix;
using Gtk;

using Tomboy;

namespace Tomboy.Tasks
{
	public class TaskOptionsDialog : Gtk.Dialog
	{
#region Private Fields
		Task task;
		Gtk.AccelGroup accel_group;
		
		Gtk.Entry summary_entry;
		
		Gtk.CheckButton completed_check_button;
		Gtk.Label completed_label;
		
		Gtk.CheckButton due_date_check_button;
		Gtk.Extras.DateButton due_date_button;
		
		Gtk.CheckButton priority_check_button;
		Gtk.ComboBox priority_combo_box;
		
		Gtk.TextView details_text_view;
#endregion // Private Fields
		
#region Constructors
		public TaskOptionsDialog(Gtk.Window parent,
					Gtk.DialogFlags flags,
					Task task)
			: base (Catalog.GetString ("Task Options"), parent, flags)
		{
			HasSeparator = false;
			//BorderWidth = 0;
			Resizable = false;
			//Decorated = false;
			this.SetDefaultSize (400, 300);
			this.task = task;

//			Frame frame = new Frame();
//			frame.Shadow = ShadowType.Out;
//			frame.Show();
//			VBox.PackStart (frame, true, true, 0);
			
			VBox vbox = new VBox (false, 6);
			vbox.BorderWidth = 6;
			vbox.Show ();
			VBox.PackStart (vbox, true, true, 0);
//			frame.Add (vbox);
			
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;
			
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);
			
//			Gtk.Label l = new Gtk.Label (
//					string.Format (
//						"<span weight=\"bold\">{0}</span>",
//						Catalog.GetString ("Task Options")));
//			l.UseMarkup = true;
//			l.Show ();
//			vbox.PackStart (l, false, false, 0);
			
			///
			/// Summary
			///
			Gtk.Label l = new Label (Catalog.GetString ("_Summary:"));
			l.Xalign = 0;
			l.Show ();
			vbox.PackStart (l, false, false, 0);
			
			summary_entry = new Gtk.Entry ();
			l.MnemonicWidget = summary_entry;
			summary_entry.Text = task.Summary;
			summary_entry.Show ();
			vbox.PackStart (summary_entry, false, false, 0);
			
			///
			/// Details
			///
			l = new Label (Catalog.GetString ("_Details:"));
			l.Xalign = 0;
			l.Show ();
			vbox.PackStart (l, false, false, 0);
			
			details_text_view = new TextView ();
			l.MnemonicWidget = details_text_view;
			details_text_view.WrapMode = WrapMode.Word;
			details_text_view.Show ();
			
			ScrolledWindow sw = new ScrolledWindow ();
			sw.ShadowType = Gtk.ShadowType.EtchedIn;
			sw.Add (details_text_view);
			sw.Show ();
			
			vbox.PackStart (sw, true, true, 0);

			///
			/// Completion Checkbox
			///
			HBox hbox = new Gtk.HBox (false, 4);
			
			completed_check_button = new Gtk.CheckButton (
					task.IsComplete ?
						Catalog.GetString ("_Completed:") :
						Catalog.GetString ("_Complete"));
			if (task.IsComplete)
				completed_check_button.Active = true;
			completed_check_button.UseUnderline = true;
			completed_check_button.Toggled += OnCompletedCheckButtonToggled;
			completed_check_button.Show ();
			hbox.PackStart (completed_check_button, false, false, 0);
			
			completed_label = new Gtk.Label (
					task.IsComplete ?
						GuiUtils.GetPrettyPrintDate (task.CompletionDate, true) :
						string.Empty);
			completed_label.Xalign = 0;
			completed_label.Show ();
			hbox.PackStart (completed_label, true, true, 0);
			
			hbox.Show ();
			vbox.PackStart (hbox, false, false, 0);
					
			///
			/// Due Date
			///
			hbox = new HBox (false, 4);
			due_date_check_button = new CheckButton (Catalog.GetString ("Due Date:"));
			if (task.DueDate != DateTime.MinValue)
				due_date_check_button.Active = true;
			due_date_check_button.Toggled += OnDueDateCheckButtonToggled;
			due_date_check_button.Show ();
			hbox.PackStart (due_date_check_button, false, false, 0);
			
			due_date_button =
					new Gtk.Extras.DateButton (task.DueDate, false);
			if (task.DueDate == DateTime.MinValue)
				due_date_button.Sensitive = false;
			due_date_button.Show ();
			hbox.PackStart (due_date_button, false, false, 0);
			
			// Spacer
			hbox.PackStart (new Gtk.Label (string.Empty), true, true, 0);
			
			hbox.Show ();
			vbox.PackStart (hbox, false, false, 0);
			
			///
			/// Priority
			///
			hbox = new HBox (false, 4);
			priority_check_button = new CheckButton (Catalog.GetString ("Priority:"));
			if (task.Priority != TaskPriority.Undefined)
				priority_check_button.Active = true;
			priority_check_button.Toggled += OnPriorityCheckButtonToggled;
			priority_check_button.Show ();
			hbox.PackStart (priority_check_button, false, false, 0);
			
			priority_combo_box = ComboBox.NewText ();
			priority_combo_box.AppendText (Catalog.GetString ("None"));
			priority_combo_box.AppendText (Catalog.GetString ("Low"));
			priority_combo_box.AppendText (Catalog.GetString ("Normal"));
			priority_combo_box.AppendText (Catalog.GetString ("High"));
			if (task.Priority == TaskPriority.Undefined)
				priority_combo_box.Sensitive = false;
			priority_combo_box.Active = (int) task.Priority;
			priority_combo_box.Changed += OnPriorityComboBoxChanged;
			priority_combo_box.Show ();
			hbox.PackStart (priority_combo_box, false, false, 0);
			
			// Spacer
			hbox.PackStart (new Gtk.Label (string.Empty), true, true, 0);
			hbox.Show ();
			vbox.PackStart (hbox, false, false, 0);
			
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, false);
			AddButton (Gtk.Stock.Save, Gtk.ResponseType.Ok, true);
			
//			if (parent != null)
//				TransientFor = parent;

//			if ((int) (flags & Gtk.DialogFlags.Modal) != 0)
//				Modal = true;

//			if ((int) (flags & Gtk.DialogFlags.DestroyWithParent) != 0)
//				DestroyWithParent = true;
		}
#endregion // Constructors
#region Public Properties
		public Task Task
		{
			get { return task; }
		}
#endregion // Public Properties

#region Private Methods
		protected override void OnRealized ()
		{
			base.OnRealized ();
			
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
			
			if (response_id != Gtk.ResponseType.Ok)
				return;
			
			// Save summary
			string new_summary = summary_entry.Text.Trim ();
			if (task.Summary.CompareTo (new_summary) != 0)
				task.Summary = new_summary;
			
			// Save details
			string new_details = details_text_view.Buffer.Text;
			if (task.Details != new_details)
				task.Details = new_details;
			
			// Save Completed Status
			if (task.IsComplete != completed_check_button.Active) {
				if (completed_check_button.Active)
					task.Complete ();
				else
					task.ReOpen ();
			}
			
			// Save due date
			DateTime new_due_date;
			if (due_date_check_button.Active)
				new_due_date = due_date_button.Date;
			else
				new_due_date = DateTime.MinValue;
			
			if (task.DueDate != new_due_date)
				task.DueDate = new_due_date;
			
			// Save priority
			TaskPriority new_priority = (TaskPriority) priority_combo_box.Active;
			if (task.Priority != new_priority)
				task.Priority = new_priority;
		}
#endregion // Private Methods

#region Event Handlers
		void OnCompletedCheckButtonToggled (object sender, EventArgs args)
		{
			if (completed_check_button.Active) {
				completed_check_button.Label = Catalog.GetString ("_Completed:");
				completed_label.Text = GuiUtils.GetPrettyPrintDate (DateTime.Now, true);
			} else {
				completed_check_button.Label = Catalog.GetString ("_Complete");
				completed_label.Text = string.Empty;
			}
		}
		
		void OnDueDateCheckButtonToggled (object sender, EventArgs args)
		{
			if (due_date_check_button.Active) {
				due_date_button.Sensitive = true;
				if (due_date_button.Date == DateTime.MinValue)
					due_date_button.Date = DateTime.Now;
			} else {
				due_date_button.Sensitive = false;
			}
		}
		
		void OnPriorityCheckButtonToggled (object sender, EventArgs args)
		{
			if (priority_check_button.Active) {
				priority_combo_box.Sensitive = true;
				// If it's currently set to None, select Normal
				if (priority_combo_box.Active == 0)
					priority_combo_box.Active = 2;
			} else {
				priority_combo_box.Sensitive = false;
			}
		}
		
		void OnPriorityComboBoxChanged (object sender, EventArgs args)
		{
			// If "None" is selected, uncheck the priority_check_button
			if (priority_combo_box.Active == 0)
				priority_check_button.Active = false;
		}
#endregion // Event Handlers
	}
}
