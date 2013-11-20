using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace Tomboy.Sync
{
	public class TomboySyncClient : SyncClient
	{
		private const string localManifestFileName = "manifest.xml";

		private DateTime lastSyncDate;
		private int lastSyncRev;
		private string serverId;
		private string localManifestFilePath;
		private Dictionary<string, int> fileRevisions;
		private Dictionary<string, string> deletedNotes;

		public TomboySyncClient ()
		{
			// TODO: Why doesn't OnChanged ever get fired?!
			FileSystemWatcher w = new FileSystemWatcher ();
			w.Path = Services.NativeApplication.ConfigurationDirectory;
			w.Filter = localManifestFileName;
			w.Changed += OnChanged;

			localManifestFilePath =
			        Path.Combine (Services.NativeApplication.ConfigurationDirectory,
			                      localManifestFileName);
			Parse (localManifestFilePath);

			Tomboy.DefaultNoteManager.NoteDeleted += NoteDeletedHandler;
		}

		private void NoteDeletedHandler (object noteMgr, Note deletedNote)
		{
			deletedNotes [deletedNote.Id] = deletedNote.Title;
			fileRevisions.Remove (deletedNote.Id);

			Write (localManifestFilePath);
		}

		private void OnChanged(object source, FileSystemEventArgs e)
		{
			Parse (localManifestFilePath);
		}

		private void Parse (string manifestPath)
		{
			// Set defaults before parsing
			lastSyncDate = DateTime.Today.AddDays (-1);
			lastSyncRev = -1;
			fileRevisions = new Dictionary<string,int> ();
			deletedNotes = new Dictionary<string,string> ();

			if (!File.Exists (manifestPath)) {
				lastSyncDate = DateTime.MinValue;
				Write (manifestPath);
			}

			XmlDocument doc = new XmlDocument ();
			// TODO: File permission errors?
			FileStream fs = new FileStream (manifestPath, FileMode.Open);
			try {
				try {
					doc.Load (fs);
				} catch (Exception e) {
					Logger.Warn ("Invalid XML in " + manifestPath + ".  Recreating from scratch.  Exception was: " +
					             e.Message);
					lastSyncDate = DateTime.MinValue;

					// Try again
					fs.Close ();
					Write (manifestPath);
					fs = new FileStream (manifestPath, FileMode.Open);
					doc.Load (fs); // TODO: Could throw if Write method messed up?
				}

				// TODO: Error checking
				foreach (XmlNode revisionsNode in doc.GetElementsByTagName ("note-revisions")) {
					foreach (XmlNode noteNode in revisionsNode.ChildNodes) {
						try {
							string guid = noteNode.Attributes ["guid"].InnerXml;
							int revision = -1;
							try {
								revision = int.Parse (noteNode.Attributes ["latest-revision"].InnerXml);
							} catch { }

							fileRevisions [guid] = revision;
						} catch {
						// Any errors in XML will be ignored as long as
						// bad data doesn't end up in fileRevisions.
						// Perhaps in the future we will check against a schema?
						Logger.Warn ("Ignoring bad data in " + manifestPath);
						}
					}
				}

				foreach (XmlNode deletionsNode in doc.GetElementsByTagName ("note-deletions")) {
					foreach (XmlNode noteNode in deletionsNode.ChildNodes) {
						try {
							string guid = noteNode.Attributes ["guid"].InnerXml;
							string title = noteNode.Attributes ["title"].InnerXml;

							deletedNotes [guid] = title;
						} catch {
						// Any errors in XML will be ignored as long as
						// bad data doesn't end up in deletedNotes.
						// Perhaps in the future we will check against a schema?
						Logger.Warn ("Ignoring bad data in " + manifestPath);
						}
					}
				}

				foreach (XmlNode node in doc.GetElementsByTagName ("last-sync-rev")) {
					try {
						lastSyncRev = int.Parse (node.InnerText);
					} catch {
					Logger.Error ("Unparsable last-sync-rev '" + node.InnerText + "' in " + manifestPath);
					}
				}

				foreach (XmlNode node in doc.GetElementsByTagName ("server-id"))
				serverId = node.InnerText;

				foreach (XmlNode node in doc.GetElementsByTagName ("last-sync-date")) {
					try {
						lastSyncDate = DateTime.Parse (node.InnerText);
					} catch {
					Logger.Error ("Unparsable last-sync-date '" + node.InnerText + "' in " + manifestPath);
					}
				}
			} finally {
				fs.Close ();
			}
		}

		private void Write (string manifestPath)
		{
			// TODO: Handle file permission errors
			XmlWriter xml = XmlWriter.Create (manifestPath, XmlEncoder.DocumentSettings);

			try {
				xml.WriteStartDocument ();
				xml.WriteStartElement (null, "manifest", "http://beatniksoftware.com/tomboy");

				xml.WriteStartElement (null, "last-sync-date", null);
				xml.WriteString (XmlConvert.ToString (lastSyncDate, NoteArchiver.DATE_TIME_FORMAT));
				xml.WriteEndElement ();

				xml.WriteStartElement (null, "last-sync-rev", null);
				xml.WriteString (lastSyncRev.ToString ());
				xml.WriteEndElement ();

				xml.WriteStartElement (null, "server-id", null);
				xml.WriteString (serverId);
				xml.WriteEndElement ();

				xml.WriteStartElement (null, "note-revisions", null);

				foreach (string noteGuid in fileRevisions.Keys) {
					xml.WriteStartElement (null, "note", null);
					xml.WriteAttributeString (null, "guid", null, noteGuid);
					xml.WriteAttributeString (null, "latest-revision", null, fileRevisions [noteGuid].ToString ());
					xml.WriteEndElement ();
				}

				xml.WriteEndElement (); // </note-revisons>

				xml.WriteStartElement (null, "note-deletions", null);

				foreach (string noteGuid in deletedNotes.Keys) {
					xml.WriteStartElement (null, "note", null);
					xml.WriteAttributeString (null, "guid", null, noteGuid);
					xml.WriteAttributeString (null, "title", null, deletedNotes [noteGuid]);
					xml.WriteEndElement ();
				}

				xml.WriteEndElement (); // </note-deletions>

				xml.WriteEndElement (); // </manifest>
			} finally {
				xml.Close ();
			}
		}

		public virtual DateTime LastSyncDate
		{
			get {
				return lastSyncDate;
			}
			set
			{
				lastSyncDate = value;
				// If we just did a sync, we should be able to forget older deleted notes
				deletedNotes.Clear ();
				Write (localManifestFilePath);
			}
		}

		public virtual int LastSynchronizedRevision
		{
			get {
				return lastSyncRev;
			}
			set
			{
				lastSyncRev = value;
				Write (localManifestFilePath);
			}
		}

		public virtual int GetRevision (Note note)
		{
			string noteGuid = note.Id;
			if (fileRevisions.ContainsKey (noteGuid))
				return fileRevisions [noteGuid];
			else
				return -1;
		}

		public virtual void SetRevision (Note note, int revision)
		{
			fileRevisions [note.Id] = revision;
			// TODO: Should we write on each of these or no?
			Write (localManifestFilePath);
		}

		/// <summary>
		/// Return a dictionary keyed on deleted note GUIDs, where
		/// the value is the note title.  This list may have obsolete
		/// entries.
		/// </summary>
		public virtual IDictionary<string, string> DeletedNoteTitles
		{
			get {
				return deletedNotes;
			}
		}

		/// <summary>
		/// Client info is no good, delete manifest and start over.
		/// </summary>
		public void Reset ()
		{
			if (File.Exists (localManifestFilePath))
				File.Delete (localManifestFilePath);
			Parse (localManifestFilePath);
		}

		/// <summary>
		/// The server ID should be verified before commencing
		/// sync.  If they do not match, then this client should
		/// be Reset.
		public virtual string AssociatedServerId
		{
			get
			{
				return serverId;
			}
			set
			{
				if (serverId != value) {
					serverId = value;
					Write (localManifestFilePath);
				}
			}
		}
	}
}