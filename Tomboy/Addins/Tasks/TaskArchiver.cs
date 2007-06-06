
using System;
using System.IO;
using System.Xml;

using Tomboy;

namespace Tomboy.Tasks
{
	/// <summary>
	/// Reads/Writes Tasks to/from XML files.
	/// </summary>
	public class TaskArchiver
	{
		public const string CURRENT_VERSION = "1.2";
		
		public const string DATE_TIME_FORMAT = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";

		static TaskArchiver instance = null;
		static readonly object lock_ = new object();

		protected TaskArchiver ()
		{
		}

		public static TaskArchiver Instance
		{
			get
			{
				lock (lock_)
				{
					if (instance == null)
						instance = new TaskArchiver ();
					return instance;
				}
			}
			set {
				lock (lock_)
				{
					instance = value;
				}
			}
		}

		public static TaskData Read (string read_file, string uri)
		{
			return Instance.ReadFile (read_file, uri);
		}

		public virtual TaskData ReadFile (string read_file, string uri) 
		{
			TaskData data = new TaskData (uri);
			string version = string.Empty;

			StreamReader reader = new StreamReader (read_file, 
								System.Text.Encoding.UTF8);
			XmlTextReader xml = new XmlTextReader (reader);
			xml.Namespaces = false;

			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					switch (xml.Name) {
					case "task":
						version = xml.GetAttribute ("version");
						break;
					case "summary":
						data.Summary = xml.ReadString ();
						break;
					case "details":
						data.Details = xml.ReadInnerXml ();
						break;
					case "create-date":
						data.CreateDate = 
							XmlConvert.ToDateTime (xml.ReadString (), DATE_TIME_FORMAT);
						break;
					case "last-change-date":
						data.LastChangeDate = 
							XmlConvert.ToDateTime (xml.ReadString (), DATE_TIME_FORMAT);
						break;
					case "due-date":
						data.DueDate = 
							XmlConvert.ToDateTime (xml.ReadString (), DATE_TIME_FORMAT);
						break;
					case "completion-date":
						data.CompletionDate = 
							XmlConvert.ToDateTime (xml.ReadString (), DATE_TIME_FORMAT);
						break;
					case "priority":
						string priority_str = xml.ReadString ();
						if (priority_str == null || priority_str.Length == 0)
							data.Priority = TaskPriority.Normal;
						else {
							switch (priority_str.ToLower ()) {
							case "low":
								data.Priority = TaskPriority.Low;
								break;
							case "normal":
								data.Priority = TaskPriority.Normal;
								break;
							case "high":
								data.Priority = TaskPriority.High;
								break;
							default:
								data.Priority = TaskPriority.Undefined;
								break;
							}
						}
						break;
					case "origin-note-uri":
						data.OriginNoteUri = xml.ReadString ();
						break;
					}
					break;
				}
			}

			xml.Close ();

			if (version != TaskArchiver.CURRENT_VERSION) {
				// Task has old format, so rewrite it.  No need
				// to reread, since we are not adding anything.
				Logger.Log ("Updating task XML to newest format...");
				TaskArchiver.Write (read_file, data);
			}

			return data;
		}

		public static void Write (string write_file, TaskData data)
		{
			Instance.WriteFile (write_file, data);
		}

		public virtual void WriteFile (string write_file, TaskData data) 
		{
			string tmp_file = write_file + ".tmp";

			XmlTextWriter xml = new XmlTextWriter (tmp_file, System.Text.Encoding.UTF8);
			Write (xml, data);
			xml.Close ();

			if (File.Exists (write_file)) {
				string backup_path = write_file + "~";
				if (File.Exists (backup_path))
					File.Delete (backup_path);

				// Backup the to a ~ file, just in case
				File.Move (write_file, backup_path);

				// Move the temp file to write_file
				File.Move (tmp_file, write_file);

				// Delete the ~ file
				File.Delete (backup_path);
			} else {
				// Move the temp file to write_file
				File.Move (tmp_file, write_file);
			}
		}

		public static void Write (TextWriter writer, TaskData data)
		{
			Instance.WriteFile (writer, data);
		}

		public void WriteFile (TextWriter writer, TaskData data)
		{
			XmlTextWriter xml = new XmlTextWriter (writer);
			Write (xml, data);
			xml.Close ();
		}

		void Write (XmlTextWriter xml, TaskData data)
		{
			xml.Formatting = Formatting.Indented;

			xml.WriteStartDocument ();
			xml.WriteStartElement (null, "task", "http://gnome.org/tomboy");
			xml.WriteAttributeString(null, 
						 "version", 
						 null, 
						 CURRENT_VERSION);

			xml.WriteStartElement (null, "summary", null);
			xml.WriteString (data.Summary);
			xml.WriteEndElement ();

			xml.WriteStartElement (null, "text", null);
			xml.WriteAttributeString ("xml", "space", null, "preserve");
			// Insert <details> blob...
			xml.WriteStartElement (null, "details", null);
			xml.WriteRaw (data.Details);
			xml.WriteEndElement ();
			xml.WriteEndElement ();

			if (data.CreateDate != DateTime.MinValue) {
				xml.WriteStartElement (null, "create-date", null);
				xml.WriteString (
						XmlConvert.ToString (data.CreateDate, DATE_TIME_FORMAT));
				xml.WriteEndElement ();
			}
			
			if (data.LastChangeDate != DateTime.MinValue) {
				xml.WriteStartElement (null, "last-change-date", null);
				xml.WriteString (
							XmlConvert.ToString (data.LastChangeDate, DATE_TIME_FORMAT));
				xml.WriteEndElement ();
			}
			
			if (data.DueDate != DateTime.MinValue) {
				xml.WriteStartElement (null, "due-date", null);
				xml.WriteString (
							XmlConvert.ToString (data.DueDate, DATE_TIME_FORMAT));
				xml.WriteEndElement ();
			}
			
			if (data.CompletionDate != DateTime.MinValue) {
				xml.WriteStartElement (null, "completion-date", null);
				xml.WriteString (
							XmlConvert.ToString (data.CompletionDate, DATE_TIME_FORMAT));
				xml.WriteEndElement ();
			}
			
			if (data.Priority != TaskPriority.Undefined) {
				xml.WriteStartElement (null, "priority", null);
				xml.WriteString (data.Priority.ToString ().ToLower ());
				xml.WriteEndElement ();
			}
			
			if (data.OriginNoteUri != string.Empty) {
				xml.WriteStartElement (null, "origin-note-uri", null);
				xml.WriteString (data.OriginNoteUri);
				xml.WriteEndElement ();
			}

			xml.WriteEndElement (); // </task>
			xml.WriteEndDocument ();
		}
	}
}
