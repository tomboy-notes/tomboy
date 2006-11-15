
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gtk;

using Tomboy;

class NoteOfTheDay 
{
	static string title_prefix = Catalog.GetString ("NotD: ");
	static string template_title = Catalog.GetString ("NotD: Template");

	public static string GetTitle (DateTime day)
	{
		// Format: "NotD: Friday, July 01 2005"
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

		foreach (Note note in manager.Notes) {
			if (note.Title.StartsWith (title_prefix) &&
			    note.Title != template_title &&
			    note.CreateDate.Day != DateTime.Now.Day &&
			    !HasChanged (note)) {
				kill_list.Add (note);
			}
		}
		
		foreach (Note note in kill_list) {
			Logger.Log ("NotD: Deleting old unmodified '{0}'",
					   note.Title);
			manager.Delete (note);
		}
	}
}

public class NoteOfTheDayPlugin : NotePlugin
{
	Gtk.CheckMenuItem item;
	bool timeout_owner;
	static bool enabled;
	static InterruptableTimeout timeout;
	static event EventHandler enabled_toggled;

	const string GCONF_ENABLED_KEY = "/apps/tomboy/note_of_the_day/enable_notd";

	static NoteOfTheDayPlugin ()
	{
		enabled = true;
	}

	// Called only by instance with timeout_owner set.
	void CheckNewDay (object sender, EventArgs args)
	{
		Note notd = Manager.Find (NoteOfTheDay.GetTitle (DateTime.Now));
		if (notd == null || notd.CreateDate.Day != DateTime.Now.Day) {
			NoteOfTheDay.CleanupOld (Manager);

			// Create a new NotD if the day has changed
			if (enabled)
				NoteOfTheDay.Create (Manager, DateTime.Now);
		}

		// Re-run every minute
		timeout.Reset (1000 * 60);
	}

	void OnToggleEnabled (object sender, EventArgs args)
	{
		enabled = !enabled;

		try {
			// Will call OnSettingChanged
			Preferences.Set (GCONF_ENABLED_KEY, enabled);
		} catch (Exception e) {
			Logger.Log ("NotD: Error updating GConf enabled key value: {0}", e);
			enabled_toggled (sender, args);
		}
	}

	void OnToggleMenuItem (object sender, EventArgs args)
	{
		item.Toggled -= OnToggleEnabled;
		item.Active = enabled;
		item.Toggled += OnToggleEnabled;
	}

	void OnSettingChanged (object sender, GConf.NotifyEventArgs args)
	{
		if (args.Key ==	GCONF_ENABLED_KEY) {
			enabled = (bool) args.Value;
			enabled_toggled (sender, args);
		}
	}

	protected override void Initialize ()
	{
		if (timeout == null) {
			try {
				// Grab enabled from GConf preference
				// Fails if no schema
				enabled = (bool) Preferences.Get (GCONF_ENABLED_KEY);
			} catch (Exception e) {				
				Logger.Log ("NotD: Error getting GConf enabled key");
				try {
					// Succeeds if no schema
					Preferences.Set (GCONF_ENABLED_KEY, enabled);
				} catch (Exception e2) {
					Logger.Log ("NotD: Error setting initial " +
							   "GConf enabled key value: {0}", e2);
				}
			}

			Preferences.SettingChanged += OnSettingChanged;

			timeout = new InterruptableTimeout ();
			timeout.Timeout += CheckNewDay;
			timeout.Reset (0);
			timeout_owner = true;
		}

		item = new Gtk.CheckMenuItem (Catalog.GetString ("Create Note of the Day"));
		item.Active = enabled;
		item.Toggled += OnToggleEnabled;
		item.Show ();
		AddPluginMenuItem (item);

		// Catch toggles in other instances
		enabled_toggled += OnToggleMenuItem;
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
