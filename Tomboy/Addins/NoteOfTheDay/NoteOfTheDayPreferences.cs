using System;
using Tomboy;
using Mono.Unix;

namespace Tomboy.NoteOfTheDay
{
	public class NoteOfTheDayPreferences : Gtk.VBox
	{
		Gtk.Button open_template_button;

		public NoteOfTheDayPreferences ()
: base (false, 12)
		{
			Gtk.Label label = new Gtk.Label (
			        Catalog.GetString (
						"Change the <b>Today: Template</b> note to customize " +
						"the text that new Today notes have."));
			label.UseMarkup = true;
			label.Wrap = true;
			label.Show ();
			PackStart (label, true, true, 0);

			open_template_button = new Gtk.Button (
			        Catalog.GetString ("_Open Today: Template"));
			open_template_button.UseUnderline = true;
			open_template_button.Clicked += OpenTemplateButtonClicked;
			open_template_button.Show ();
			PackStart (open_template_button, false, false, 0);

			ShowAll ();
		}

		void OpenTemplateButtonClicked (object sender, EventArgs args)
		{
			NoteManager manager = Tomboy.DefaultNoteManager;
			Note template_note = manager.Find (NoteOfTheDay.TemplateTitle);

			if (template_note == null) {
				// Create a new template note for the user
				try {
					template_note = manager.Create (
					                        NoteOfTheDay.TemplateTitle,
					                        NoteOfTheDay.GetTemplateContent (
					                                NoteOfTheDay.TemplateTitle));
					template_note.QueueSave (ChangeType.ContentChanged);
				} catch (Exception e) {
					Logger.Warn ("Error creating Note of the Day Template note: {0}\n{1}",
					             e.Message, e.StackTrace);
				}
			}

			// Open the template note
			if (template_note != null)
				template_note.Window.Show ();
		}
	}
}
