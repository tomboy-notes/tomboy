// Import notes from StickyNote applet to Tomboy
// (C) 2006 Sandy Armstrong <sanfordarmstrong@gmail.com>

using System;
using System.IO;
using System.Xml;
using Mono.Unix;

using Tomboy;

public class StickyNoteImporter : NotePlugin
{
	private const string sticky_xml_rel_path = "/.gnome2/stickynotes_applet";
	private const string sticky_note_query = "//note";

	private const string base_note_xml = 
		"<note-content><note-title>{0}</note-title>\n\n{1}</note-content>";
	private const string base_duplicate_note_title = "{0} ({1})";

	private const string debug_no_sticky_file =
		"StickyNoteImporter: Sticky Notes XML file does not exist!";
	private const string debug_create_error_base =
		"StickyNoteImporter: Error while trying to create note \"{0}\": {1}";
	
	protected override void Initialize ()
	{
		Gtk.ImageMenuItem item = 
			new Gtk.ImageMenuItem (Catalog.GetString ("Import From Sticky Notes"));
		item.Image = new Gtk.Image (Gtk.Stock.Convert, Gtk.IconSize.Menu);
		item.Activated += ImportButtonClicked;
		item.Show ();
		AddPluginMenuItem (item);
	}
	
	protected override void Shutdown ()
	{
		// Do nothing.
	}

	protected override void OnNoteOpened () 
	{
		// Do nothing.
	}
	
	void ImportButtonClicked (object sender, EventArgs args)
	{
		string sticky_xml_path =
			Environment.GetFolderPath (System.Environment.SpecialFolder.Personal)
			+ sticky_xml_rel_path;

		if (File.Exists (sticky_xml_path))
			ImportNotes (sticky_xml_path);
		else {
			Logger.Log (debug_no_sticky_file);
			ShowNoStickyXMLDialog (sticky_xml_path);
		}
	}
	
	void ShowNoStickyXMLDialog (string xmlPath)
	{
		// TODO: Why does "stickynotes_applet" show up as
		//	 "stickynotesapplet" with the "a" underlined???
		ShowMessageDialog (
			Catalog.GetString ("No Sticky Notes found."),
			string.Format (Catalog.GetString ("No suitable Sticky Notes file was " + 
							  "found at \"{0}\""),
				       xmlPath),
			Gtk.MessageType.Error);
	}
	
	void ShowResultsDialog (int numNotesImported, int numNotesTotal)
	{
		string message = "{0}/{1} {2}";

		ShowMessageDialog (
			Catalog.GetString ("Sticky Notes import completed."),
			string.Format ("{0} of {1} Sticky Notes were successfully imported.",
				       numNotesImported,
				       numNotesTotal),
			Gtk.MessageType.Info);
	}
	
	void ImportNotes (string xmlPath)
	{
		XmlDocument xmlDoc = new XmlDocument ();
		try {
			xmlDoc.Load (xmlPath);
		}
		catch {
			// TODO: Should this show a different message?
			ShowNoStickyXMLDialog (xmlPath);
			return;
		}

		XmlNodeList nodes = xmlDoc.SelectNodes (sticky_note_query);
		
		int numSuccessful = 0;
		string defaultTitle = Catalog.GetString ("Untitled");

		foreach (XmlNode node in nodes) {
			XmlAttribute titleAttr = node.Attributes["title"];
			string stickyTitle = defaultTitle;
			if (titleAttr != null && titleAttr.InnerXml.Length > 0)
				stickyTitle = titleAttr.InnerXml;
			string stickyContent = node.InnerXml;

			if (CreateNoteFromSticky (stickyTitle, stickyContent))
				numSuccessful++;
		}
		
		ShowResultsDialog (numSuccessful, nodes.Count);
	}
	
	bool CreateNoteFromSticky (string stickyTitle, string content)
	{
		// There should be no XML in the content
		// TODO: Report the error in the results dialog
		//	 (this error should only happen if somebody has messed with the XML file)
		if (content.IndexOf ('>') != -1 || content.IndexOf ('<') != -1) {
			Logger.Log (string.Format (debug_create_error_base, 
						   stickyTitle, 
						   "Invalid characters in note XML"));
			return false;
		}

		string preferredTitle = Catalog.GetString ("Sticky Note: ") + stickyTitle;
		
		string title = preferredTitle;
		int i = 0;
		while (Manager.Find (title) != null)
			title = string.Format (base_duplicate_note_title, preferredTitle, ++i);
		
		string noteXml = string.Format (base_note_xml, title, content);
		
		try {
			Note newNote = Manager.Create (title, noteXml);
			newNote.Save ();
			return true;
		} catch (Exception e) {
			Logger.Log (string.Format (debug_create_error_base, title, e.Message));
			return false;
		}
	}
	
	void ShowMessageDialog (string title, string message, Gtk.MessageType messageType)
	{
		HIGMessageDialog dialog =
			new HIGMessageDialog (
				Note.Window,
				Gtk.DialogFlags.DestroyWithParent,
				messageType,
				Gtk.ButtonsType.Ok,
				title,
				message);
		dialog.Run ();
		dialog.Destroy ();		
	}
}
