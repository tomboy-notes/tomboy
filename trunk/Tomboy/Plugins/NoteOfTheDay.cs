
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gtk;

using Tomboy;

class NoteOfTheDay 
{
	static string title_prefix = Catalog.GetString ("Today: ");
	static string template_title = Catalog.GetString ("Today: Template");

	public static string GetTitle (DateTime day)
	{
		// Format: "Today: Friday, July 01 2005"
		return title_prefix + day.ToString (Catalog.GetString ("dddd, MMMM d yyyy"));
	}

	public static string GetContent (DateTime day, NoteManager manager)
	{
		const string base_xml = 
			"<note-content>" +
			"<note-title>{0}</note-title>\n\n\n\n" +
			"<size:huge>{1}</size:huge>\n\n\n" +
			"<size:huge>{2}</size:huge>\n\n\n" +
			"</note-content>";
			
		string title = GetTitle (day);

		// Attempt to load content from template
		Note templateNote = manager.Find (template_title);
		if (templateNote != null)
			return templateNote.XmlContent.Replace (template_title, title);
		else
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

		if (notd != null)
			notd.Save ();

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
			    note.Title != template_title &&
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
					note.Title != template_title) {
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
	WebSite = "http://www.gnome.org/projects/tomboy/"
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
