

namespace Tomboy
{
	[DBus.Interface ("com.beatniksoftware.Tomboy.RemoteControl")]
	public abstract class RemoteControlProxy
	{
		public const string Path = "/com/beatniksoftware/Tomboy/RemoteControl";
		public const string Namespace = "com.beatniksoftware.Tomboy";

		// Displays the note with the specified Uri.
		// Returns true on success, false if there is no note
		// corresponding to that Uri.
		[DBus.Method]
		public abstract bool DisplayNote (string uri);

	        // Displays the note with the specified Uri,
	        // highlighting all occurences of the text 'search'.
		// Returns true on success, false if there is no note
		// corresponding to that Uri.
		[DBus.Method]
		public abstract bool DisplayNoteWithSearch (string uri, string search);

		// Finds a note with the specified title and returns
		// its Uri.  If no note with that title exists,
		// the empty string is returned.
		[DBus.Method]
		public abstract string FindNote (string linked_title);

		// Creates a new note and returns its Uri.
		[DBus.Method]
		public abstract string CreateNote ();

		// Creates a new note with the specified title and returns
		// the Uri.  If another note with the same title already
		// exists, the empty string is returned.
		[DBus.Method]
		public abstract string CreateNamedNote (string linked_title);

		// Deletes a note with the specified Uri.
		// Returns true on success, false if there is no note
		// corresponding to that Uri.
		[DBus.Method]
		public abstract bool DeleteNote (string uri);
	}
}
