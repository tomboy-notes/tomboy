using System;

namespace Tomboy
{
	/// <summary>
	/// Wrap the RemoteControl class methods in Gtk.Application.Invoke.
	/// </summary>
	public class RemoteControlWrapper : MarshalByRefObject, IRemoteControl
	{
		#region Static Members

		// Store the RemoteControl instance statically because:
		//	1. We only want one anyway.
		//	2. Otherwise .NET remoting will want to start
		//	   serializing RemoteControl, NoteManager, etc.
		private static RemoteControl remote;
		public static void Initialize (RemoteControl remote)
		{
			RemoteControlWrapper.remote = remote;
		}

		#endregion

		#region IRemoteControl Members

		public bool AddTagToNote (string uri, string tag_name)
		{
			throw new NotImplementedException ();
		}

		public string CreateNamedNote (string linked_title)
		{
			throw new NotImplementedException ();
		}

		public string CreateNamedNoteWithUri (string linked_title, string uri)
		{
			throw new NotImplementedException ();
		}

		public string CreateNote ()
		{
			throw new NotImplementedException ();
		}

		public bool DeleteNote (string uri)
		{
			throw new NotImplementedException ();
		}

		public bool DisplayNote (string uri)
		{
			throw new NotImplementedException ();
		}

		public bool DisplayNoteWithSearch (string uri, string search)
		{
			throw new NotImplementedException ();
		}

		public void DisplaySearch ()
		{
			Gtk.Application.Invoke (delegate {
				remote.DisplaySearch ();
			});
		}

		public void DisplaySearchWithText (string search_text)
		{
			throw new NotImplementedException ();
		}

		public string FindNote (string linked_title)
		{
			throw new NotImplementedException ();
		}

		public string FindStartHereNote ()
		{
			throw new NotImplementedException ();
		}

		public string [] GetAllNotesWithTag (string tag_name)
		{
			throw new NotImplementedException ();
		}

		public long GetNoteChangeDate (string uri)
		{
			throw new NotImplementedException ();
		}

		public string GetNoteCompleteXml (string uri)
		{
			throw new NotImplementedException ();
		}

		public string GetNoteContents (string uri)
		{
			throw new NotImplementedException ();
		}

		public string GetNoteContentsXml (string uri)
		{
			throw new NotImplementedException ();
		}

		public long GetNoteCreateDate (string uri)
		{
			throw new NotImplementedException ();
		}

		public string GetNoteTitle (string uri)
		{
			throw new NotImplementedException ();
		}

		public string [] GetTagsForNote (string uri)
		{
			throw new NotImplementedException ();
		}

		public bool HideNote (string uri)
		{
			throw new NotImplementedException ();
		}

		public string [] ListAllNotes ()
		{
			throw new NotImplementedException ();
		}

		public event RemoteAddedHandler NoteAdded;

		public event RemoteDeletedHandler NoteDeleted;

		public bool NoteExists (string uri)
		{
			throw new NotImplementedException ();
		}

		public event RemoteSavedHandler NoteSaved;

		public bool RemoveTagFromNote (string uri, string tag_name)
		{
			throw new NotImplementedException ();
		}

		public string [] SearchNotes (string query, bool case_sensitive)
		{
			throw new NotImplementedException ();
		}

		public bool SetNoteCompleteXml (string uri, string xml_contents)
		{
			throw new NotImplementedException ();
		}

		public bool SetNoteContents (string uri, string text_contents)
		{
			throw new NotImplementedException ();
		}

		public bool SetNoteContentsXml (string uri, string xml_contents)
		{
			throw new NotImplementedException ();
		}

		public string Version ()
		{
			return remote.Version ();
		}

		#endregion
	}
}
