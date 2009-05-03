
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
		private Dictionary<string, DateTime> note_save_times;

		public override void Initialize ()
		{
			string note_path = Tomboy.DefaultNoteManager.NoteDirectoryPath;
			Tomboy.DefaultNoteManager.NoteSaved += HandleNoteSaved;

			file_change_records = new Dictionary<string, NoteFileChangeRecord> ();
			note_save_times = new Dictionary<string, DateTime> ();

			file_system_watcher = new FileSystemWatcher (note_path, "*.note");

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

		private void HandleNoteSaved (Note note)
		{
			note_save_times [note.Id] = DateTime.Now;
		}

		private void HandleFileSystemErrorEvent (Object sender, ErrorEventArgs arg) 
		{
			// TODO Rescan the local notes in case some of them have changed.
		}

		private void HandleFileSystemChangeEvent (Object sender, FileSystemEventArgs arg) 
		{
			string note_id = GetId (arg.FullPath);

			if (VERBOSE_LOGGING)
				Logger.Debug ("NoteDirectoryWatcher: {0} has {1} (note_id={2})", arg.FullPath, arg.ChangeType, note_id);
			
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
					string message = "NoteDirectoryWatcher: Unexpected WatcherChangeType " + arg.ChangeType;
					Logger.Error (message);
					return;
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
						Logger.Debug ("NoteDirectoryWatcher: Handling (timeout) {0}", pair.Key);

					// Check that Note.Saved event didn't occur within 3 seconds of last write
					if (note_save_times.ContainsKey (pair.Key) &&
					    Math.Abs (note_save_times [pair.Key].Ticks - pair.Value.last_change.Ticks) <= 3000*10000) {
						if (VERBOSE_LOGGING)
							Logger.Debug ("NoteDirectoryWatcher: Ignoring (timeout) because it was probably a Tomboy write");
						keysToRemove.Add (pair.Key);
						continue;
					}
					// TODO: Take some actions to clear note_save_times? Not a large structure...

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
			Logger.Debug ("NoteDirectoryWatcher: Deleting {0} because file deleted.", note_id);

			string note_uri = MakeUri (note_id);

			Note note_to_delete = Tomboy.DefaultNoteManager.FindByUri (note_uri);
			if (note_to_delete != null)
				Tomboy.DefaultNoteManager.Delete (note_to_delete);
			else if (VERBOSE_LOGGING)
				Logger.Debug ("NoteDirectoryWatcher: Did not delete {0} because note not found.", note_id);
		}

		private void AddOrUpdateNote (string note_id)
		{
			string note_path = Tomboy.DefaultNoteManager.NoteDirectoryPath +
						Path.DirectorySeparatorChar + note_id + ".note";
			if (!File.Exists (note_path)) {
				if (VERBOSE_LOGGING)
					Logger.Debug ("NoteDirectoryWatcher: Not processing update of {0} because file does not exist.", note_path);
				return;
			}
			
			string noteXml = null;
			try {
				using (StreamReader reader = new StreamReader (note_path)) {
					noteXml = reader.ReadToEnd ();
				}
			} catch (Exception e) {
				Logger.Error ("NoteDirectoryWatcher: Update aborted, error reading {0}: {1}", note_path, e);
				return;
			}

			if (string.IsNullOrEmpty (noteXml)) {
				if (VERBOSE_LOGGING)
					Logger.Debug ("NoteDirectoryWatcher: Update aborted, {0} had no contents.", note_path);
				return;
			}

			string note_uri = MakeUri (note_id);

			Note note = Tomboy.DefaultNoteManager.FindByUri (note_uri);

			bool is_new_note = false;

			if (note == null) {
				is_new_note = true;
				Logger.Debug ("NoteDirectoryWatcher: Adding {0} because file changed.", note_id);
				
				string title = null;
				const string title_group_name = "title";
				Match match = Regex.Match (noteXml, "<title>(?<" + title_group_name + ">[^<]+)</title>");
				if (match.Success)
					title = match.Groups [title_group_name].Value;
				else {
					Logger.Error ("NoteDirectoryWatcher: Error reading note title from {0}", note_path);
					return;
				}
				
				try {
					note = Tomboy.DefaultNoteManager.CreateWithGuid (title, note_id);
					if (note == null) {
						Logger.Error ("NoteDirectoryWatcher: Unknown error creating note from {0}", note_path);
						return;
					}
				} catch (Exception e) {
					Logger.Error ("NoteDirectoryWatcher: Error creating note from {0}: {1}", note_path, e);
					return;
				}
			}

			if (is_new_note)
				Logger.Debug ("NoteDirectoryWatcher: Updating {0} because file changed.", note_id);
			try {
				note.LoadForeignNoteXml (noteXml, ChangeType.ContentChanged);
			} catch (Exception e) {
				Logger.Error ("NoteDirectoryWatcher: Update aborted, error parsing {0}: {1}", note_path, e);
				if (is_new_note)
					Tomboy.DefaultNoteManager.Delete (note);
			}
		}

		private static string MakeUri (string note_id)
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
