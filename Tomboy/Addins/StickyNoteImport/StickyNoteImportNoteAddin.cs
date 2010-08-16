// Import notes from Sticky Notes applet to Tomboy
// (C) 2006 Sandy Armstrong <sanfordarmstrong@gmail.com>

using System;
using System.IO;
using System.Xml;
using Mono.Unix;

using Tomboy;

namespace Tomboy.StickyNoteImport
{
	// TODO: This really ought to be changed to an ApplicationAddin that adds
	// an item to the SearchAllNotes Window's Tools menu.
	public class StickyNoteImportNoteAddin : NoteAddin
	{
		private const string sticky_xml_rel_path = "/.gnome2/stickynotes_applet";
		private const string sticky_note_query = "//note";

		private const string base_note_xml =
		        "<note-content><note-title>{0}</note-title>\n\n{1}</note-content>";
		private const string base_duplicate_note_title = "{0} (#{1})";

		private const string debug_no_sticky_file =
		        "StickyNoteImporter: Sticky Notes XML file does not exist or is invalid!";
		private const string debug_create_error_base =
		        "StickyNoteImporter: Error while trying to create note \"{0}\": {1}";
		private const string debug_first_run_detected =
		        "StickyNoteImporter: Detecting that importer has never been run...";
		private const string debug_gconf_set_error_base =
		        "StickyNoteImporter: Error setting initial GConf first run key value: {0}";

		private static string sticky_xml_path =
		        Environment.GetFolderPath (System.Environment.SpecialFolder.Personal)
		        + sticky_xml_rel_path;

		Gtk.ImageMenuItem item;

		private static bool sticky_file_might_exist = true;
		private static bool sticky_file_existence_confirmed = false;

		public override void Initialize ()
		{
			// Don't add item to tools menu if Sticky Notes XML file does not
			// exist. Only check for the file once, since Initialize is called
			// for each note when Tomboy starts up.
			if (sticky_file_might_exist) {
				if (sticky_file_existence_confirmed || File.Exists (sticky_xml_path)) {
					item = new Gtk.ImageMenuItem (
					        Catalog.GetString ("Import from Sticky Notes"));
					item.Image = new Gtk.Image (Gtk.Stock.Convert, Gtk.IconSize.Menu);
					item.Activated += ImportButtonClicked;
					item.Show ();
					AddPluginMenuItem (item);

					sticky_file_existence_confirmed = true;
					CheckForFirstRun ();
				} else {
					sticky_file_might_exist = false;
					Logger.Debug (debug_no_sticky_file);
				}
			}
		}

		public override void Shutdown ()
		{
			// Disconnect the event handlers so
			// there aren't any memory leaks.
			// item is null if this plugin wasn't initialized
			if (item != null)
				item.Activated -= ImportButtonClicked;
		}

		public override void OnNoteOpened ()
		{
			// Do nothing.
		}

		void CheckForFirstRun ()
		{
			bool firstRun = (bool) Preferences.Get (Preferences.STICKYNOTEIMPORTER_FIRST_RUN);

			if (firstRun) {
				try {
					Preferences.Set (Preferences.STICKYNOTEIMPORTER_FIRST_RUN, false);
				} catch (Exception e) {
					Logger.Debug (debug_gconf_set_error_base, e);
				}

				Logger.Log (debug_first_run_detected);

				XmlDocument xmlDoc = GetStickyXmlDoc ();
				if (xmlDoc != null)
					// Don't show dialog when automatically importing
					ImportNotes (xmlDoc, false);
			}
		}

		XmlDocument GetStickyXmlDoc ()
		{
			if (File.Exists (sticky_xml_path)) {
				try {
					XmlDocument xmlDoc = new XmlDocument ();
					xmlDoc.Load (sticky_xml_path);
					return xmlDoc;
				} catch {
					Logger.Debug (debug_no_sticky_file);
					return null;
				}
			}
			else {
				Logger.Debug (debug_no_sticky_file);
				return null;
			}

		}

		void ImportButtonClicked (object sender, EventArgs args)
		{
			XmlDocument xmlDoc = GetStickyXmlDoc ();

			if (xmlDoc != null)
				ImportNotes (xmlDoc, true);
			else
				ShowNoStickyXMLDialog (sticky_xml_path);
		}

		void ShowNoStickyXMLDialog (string xmlPath)
		{
			// TODO: Why does "stickynotes_applet" show up as
			//  "stickynotesapplet" with the "a" underlined???
			ShowMessageDialog (
			        Catalog.GetString ("No Sticky Notes found"),
			        string.Format (Catalog.GetString ("No suitable Sticky Notes file was " +
			                                          "found at \"{0}\"."),
			                       xmlPath),
			        Gtk.MessageType.Error);
		}

		void ShowResultsDialog (int numNotesImported, int numNotesTotal)
		{
			ShowMessageDialog (
			        Catalog.GetString ("Sticky Notes import completed"),
			        string.Format (Catalog.GetString ("<b>{0}</b> of <b>{1}</b> Sticky Notes " +
			                                          "were successfully imported."),
			                       numNotesImported,
			                       numNotesTotal),
			        Gtk.MessageType.Info);
		}

		void ImportNotes (XmlDocument xmlDoc, bool showResultsDialog)
		{
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

			if (showResultsDialog)
				ShowResultsDialog (numSuccessful, nodes.Count);
		}

		bool CreateNoteFromSticky (string stickyTitle, string content)
		{
			// There should be no XML in the content
			// TODO: Report the error in the results dialog
			//  (this error should only happen if somebody has messed with the XML file)
			if (content.IndexOf ('>') != -1 || content.IndexOf ('<') != -1) {
				Logger.Error (string.Format (debug_create_error_base,
				                           stickyTitle,
				                           "Invalid characters in note XML"));
				return false;
			}

			string preferredTitle = Catalog.GetString ("Sticky Note: ") + stickyTitle;
			string title = preferredTitle;

			int i = 2; // Append numbers to create unique title, starting with 2
			while (Manager.Find (title) != null)
				title = string.Format (base_duplicate_note_title, preferredTitle, i++);

			string noteXml = string.Format (base_note_xml, title, content);

			try {
				Note newNote = Manager.Create (title, noteXml);
				newNote.QueueSave (ChangeType.NoChange);
				newNote.Save ();
				return true;
			} catch (Exception e) {
				Logger.Error (string.Format (debug_create_error_base, title, e.Message));
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
}
