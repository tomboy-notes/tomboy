using System;
using System.Collections.Generic;
using Tomboy;
using Mono.Unix;

namespace Tomboy.NoteOfTheDay
{
	public class NoteOfTheDay
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
				Tag notd_tag = TagManager.GetOrCreateSystemTag ("NoteOfTheDay");
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
			List<Note> kill_list = new List<Note> ();
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
				Logger.Debug ("NoteOfTheDay: Deleting old unmodified '{0}'",
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
}
