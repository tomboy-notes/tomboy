
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gtk;

using Tomboy;

class NoteOfTheDay 
{
	public static string TemplateTitle = Catalog.GetString ("Today: Template");
	
	static string title_prefix = Catalog.GetString ("Today: ");

	public static string GetTitle (DateTime day)
	{
		// Format: "Today: Friday, July 01 2005"
		return title_prefix + day.ToString (Catalog.GetString ("dddd, MMMM d yyyy"));
	}

	public static string GetContent (DateTime day, NoteManager manager)
	{
		string title = GetTitle (day);

		// Attempt to load content from template
		Note templateNote = manager.Find (TemplateTitle);
		if (templateNote != null)
			return templateNote.XmlContent.Replace (TemplateTitle, title);
		else
			return GetTemplateContent (title);
	}
	
	public static string GetTemplateContent (string title)
	{
		const string base_xml =
			"<note-content>" +
			"<note-title>{0}</note-title>\n\n\n\n" +
			"<size:huge>{1}</size:huge>\n\n\n" +
			"<size:huge>{2}</size:huge>\n\n\n" +
			"</note-content>";

		return string.Format (base_xml, 
			      title, 
			      Catalog.GetString ("Tasks"),
			      Catalog.GetString ("Appointments"));
	}

	public static Note Create (NoteManager manager, DateTime day)
	{
		string title = GetTitle (day);
		string xml = GetContent (day, manager);

		Note notd = null;
		try {
			notd = manager.Create (title, xml);
		} catch (Exception e) {
			// Prevent blowup if note creation fails
			Logger.Error (
				"NoteOfTheDay could not create \"{0}\": {1}",
				title,
				e.Message);
			notd = null;
		}

		if (notd != null) {
			// Automatically tag all new Note of the Day notes
			Tag notd_tag = TagManager.GetOrCreateTag (
					Catalog.GetString ("Note of the Day"));
			notd.AddTag (notd_tag);
			
			// notd.AddTag queues a save so the following is no longer necessary
			//notd.Save ();
		}

		return notd;
	}

	static string GetContentWithoutTitle (string content)
	{
		return content.Substring (content.IndexOf ("\n"));
	}

	public static bool HasChanged (Note note)
	{
		string original_xml = GetContent(note.CreateDate, note.Manager);
		if (GetContentWithoutTitle (note.TextContent) == 
		    GetContentWithoutTitle (XmlDecoder.Decode (original_xml))) {
			return false;
		}
		return true;
	}

	public static void CleanupOld (NoteManager manager)
	{
		ArrayList kill_list = new ArrayList();
		DateTime date_today = DateTime.Today; // time set to 00:00:00

		foreach (Note note in manager.Notes) {
			if (note.Title.StartsWith (title_prefix) &&
			    note.Title != TemplateTitle &&
			    note.CreateDate.Date != date_today &&
			    !HasChanged (note)) {
				kill_list.Add (note);
			}
		}
		
		foreach (Note note in kill_list) {
			Logger.Log ("NoteOfTheDay: Deleting old unmodified '{0}'",
					   note.Title);
			manager.Delete (note);
		}
	}
	
	///
	/// Returns the NotD note for the specified date
	///
	public static Note GetNoteByDate (NoteManager manager, DateTime date)
	{
		DateTime normalized_date = date.Date; // same date with time set to 00:00:00
		Note found_note = null;

		// Go through all the NotD notes and look for the one that was
		// created on the date specified.
		foreach (Note note in manager.Notes) {
			if (note.Title.StartsWith (title_prefix) &&
					note.Title != TemplateTitle) {
				DateTime note_date = note.CreateDate.Date;
				if (note_date == normalized_date) {
					found_note = note;
					break;
				}
			}
		}
		
		return found_note;
	}
}

[PluginInfo(
	"Note of the Day", Defines.VERSION,
	PluginInfoAttribute.OFFICIAL_AUTHOR,
	"Automatically creates a \"Today\" note for easily jotting down " +
	"daily thoughts.",
	WebSite = "http://www.gnome.org/projects/tomboy/",
    PreferencesWidget = typeof (NoteOfTheDayPreferences)
	)]
public class NoteOfTheDayPlugin : NotePlugin
{
	bool timeout_owner;
	static InterruptableTimeout timeout;

	// Called only by instance with timeout_owner set.
	void CheckNewDay (object sender, EventArgs args)
	{
		Note notd = NoteOfTheDay.GetNoteByDate (Manager, DateTime.Today);
		if (notd == null) {
			NoteOfTheDay.CleanupOld (Manager);
			
			// Create a new NotD if the day has changed
			NoteOfTheDay.Create (Manager, DateTime.Now);
		}

		// Re-run every minute
		timeout.Reset (1000 * 60);
	}

	protected override void Initialize ()
	{
		if (timeout == null) {
			timeout = new InterruptableTimeout ();
			timeout.Timeout += CheckNewDay;
			timeout.Reset (0);
			timeout_owner = true;
		}
	}

	protected override void Shutdown ()
	{
		if (timeout_owner) {
			NoteOfTheDay.CleanupOld (Manager);
			timeout.Timeout -= CheckNewDay;
			timeout.Cancel();
			timeout = null;
		}
	}

	protected override void OnNoteOpened () 
	{
		// Do nothing.
	}
}

class NoteOfTheDayPreferences : Gtk.VBox
{
	Gtk.Button open_template_button;

	public NoteOfTheDayPreferences ()
		: base (false, 12)
	{
		Gtk.Label label = new Gtk.Label (
				Catalog.GetString (
					"Change the <span weight=\"bold\">Today: Template</span> " +
					"note to customize the text that new Today notes have."));
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
		NoteManager manager = Tomboy.Tomboy.DefaultNoteManager;
		Note template_note = manager.Find (NoteOfTheDay.TemplateTitle);

		if (template_note == null) {
			// Create a new template note for the user
			try {
				template_note = manager.Create (
						NoteOfTheDay.TemplateTitle,
						NoteOfTheDay.GetTemplateContent (
								NoteOfTheDay.TemplateTitle));
				template_note.QueueSave (true);
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
