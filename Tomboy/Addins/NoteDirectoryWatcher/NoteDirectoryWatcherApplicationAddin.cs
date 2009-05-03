
using System;
using System.Collections.Generic;
using System.IO;

using Tomboy;

namespace Tomboy.NoteDirectoryWatcher
{
	class NoteFileChangeRecord 
	{
		public DateTime last_change;
		public bool deleted;
		public bool changed;
	}

	public class NoteDirectoryWatcherApplicationAddin : ApplicationAddin
	{
		private static bool VERBOSE_LOGGING = false;

		private FileSystemWatcher file_system_watcher;
		private bool initialized;

		private Dictionary<string, NoteFileChangeRecord> file_change_records;

		public override void Initialize ()
		{
			string note_path = Tomboy.DefaultNoteManager.NoteDirectoryPath;

			file_change_records = new Dictionary<string, NoteFileChangeRecord> ();

			file_system_watcher = new FileSystemWatcher (note_path);

			file_system_watcher.Changed += HandleFileSystemChangeEvent;
			file_system_watcher.Deleted += HandleFileSystemChangeEvent;
			file_system_watcher.Created += HandleFileSystemChangeEvent;
			file_system_watcher.Renamed += HandleFileSystemChangeEvent;

			file_system_watcher.Error += HandleFileSystemErrorEvent;

			// Setting to true will starts the FileSystemWatcher.
			file_system_watcher.EnableRaisingEvents = true;

			initialized = true;
		}

		public override void Shutdown ()
		{
			file_system_watcher.EnableRaisingEvents = false;
			initialized = false;
		}

		public override bool Initialized
		{
			get { return initialized; }
		}

		private void HandleFileSystemErrorEvent (Object sender, ErrorEventArgs arg) 
		{
			// TODO Rescan the local notes in case some of them have changed.
		}

		private void HandleFileSystemChangeEvent (Object sender, FileSystemEventArgs arg) 
		{
			string note_id = GetId (arg.FullPath);

			if (VERBOSE_LOGGING)
				Logger.Debug ("{0} has {1} (note_id={2})", arg.FullPath, arg.ChangeType, note_id);

			// If the note_id is long 36 characters then the file probably wasn't a note.
			if (note_id.Length != 36) {
				if (VERBOSE_LOGGING)
					Logger.Debug ("Ignoring change to {0}", arg.FullPath);

				return;
			}
			
			// Record that the file has been added/changed/deleted.  Adds/changes trump
			// deletes.  Record the date.
			lock (file_change_records) {
				NoteFileChangeRecord record = null;

				if (file_change_records.ContainsKey (note_id)) {
					record = file_change_records [note_id];
				} else {
					record = new NoteFileChangeRecord ();
					file_change_records [note_id] = record;
				}

				if (arg.ChangeType == WatcherChangeTypes.Changed) {
					record.changed = true;
					record.deleted = false;
				} else if (arg.ChangeType == WatcherChangeTypes.Created) {
					record.changed = true;
					record.deleted = false;
				} else if (arg.ChangeType == WatcherChangeTypes.Renamed) {
					record.changed = true;
					record.deleted = false;
				} else if (arg.ChangeType == WatcherChangeTypes.Deleted) {
					if (!record.changed)
						record.deleted = true;
				} else {
					String message = "Unexpected WatcherChangeType " + arg.ChangeType;
					Logger.Error (message);
					throw new Exception (message);
				}

				record.last_change = DateTime.Now;
			}

			GLib.Timeout.Add (5000, new GLib.TimeoutHandler (HandleTimeout));
		}

		private bool HandleTimeout () 
		{
			lock (file_change_records) {
				List<string> keysToRemove = new List<string> (file_change_records.Count);

				foreach (KeyValuePair<string, NoteFileChangeRecord> pair in file_change_records)  {
					if (VERBOSE_LOGGING)
						Logger.Debug ("Handling (timeout) {0}", pair.Key);

					if (DateTime.Now > pair.Value.last_change.Add (new TimeSpan (4000)) ) {
						if (pair.Value.deleted)
							DeleteNote (pair.Key);
						else
							AddOrUpdateNote (pair.Key);

						keysToRemove.Add (pair.Key);
					}
				}

				foreach (string note_id in keysToRemove)
					file_change_records.Remove (note_id);
			}
			
			return false;
		}

		private static void DeleteNote (string note_id)
		{
			Logger.Debug ("Deleting {0} because file deleted.", note_id);

			string note_uri = MakeUri (note_id);

			Note note_to_delete = Tomboy.DefaultNoteManager.FindByUri (note_uri);
			
			Tomboy.DefaultNoteManager.Notes.Remove (note_to_delete);

			note_to_delete.Delete ();
		}

		private static void AddOrUpdateNote (string note_id)
		{
			string note_path = Tomboy.DefaultNoteManager.NoteDirectoryPath +
						Path.DirectorySeparatorChar + note_id + ".note";

			string note_uri = MakeUri (note_id);

			Note note = Tomboy.DefaultNoteManager.FindByUri (note_uri);

			if (note == null) {
				Logger.Debug ("Adding {0} because file changed.", note_id);
				Note new_note = Note.Load (note_path, Tomboy.DefaultNoteManager);
				Tomboy.DefaultNoteManager.Notes.Add (new_note);	
			} else {
				NoteData data = NoteArchiver.Instance.ReadFile (note_path, note_uri);

				// Only record changes if the note actually changes.  This prevents the Addin from
				// noticing changes from Tomboy itself.
				if (data.Text == note.XmlContent)
				{
					if (VERBOSE_LOGGING)
						Logger.Debug ("Ignoring {0} because contents identical", note_id);
				} else  {
					Logger.Debug ("Updating {0} because file changed.", note_id);
					note.XmlContent = data.Text;
					note.Title = data.Title;
				}
			}
		}

		private static String MakeUri (string note_id) 
		{
			return "note://tomboy/" + note_id;
		}

		private static string GetId (string path) 
		{
			int last_slash = path.LastIndexOf (Path.DirectorySeparatorChar);
			int first_period = path.IndexOf ('.', last_slash);

			return path.Substring (last_slash + 1, first_period - last_slash - 1);
		}
	}
}
