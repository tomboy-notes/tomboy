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
			return remote.AddTagToNote (uri, tag_name);
		}

		public string CreateNamedNote (string linked_title)
		{
			return remote.CreateNamedNote (linked_title);
		}

		public string CreateNamedNoteWithUri (string linked_title, string uri)
		{
			return remote.CreateNamedNoteWithUri (linked_title, uri);
		}

		public string CreateNote ()
		{
			return remote.CreateNote ();
		}

		public bool DeleteNote (string uri)
		{
			return remote.DeleteNote (uri);
		}

		public bool DisplayNote (string uri)
		{
			bool result = false;
			Gtk.Application.Invoke (delegate {
				result = remote.DisplayNote (uri);
			});
			return result;
		}

		public bool DisplayNoteWithSearch (string uri, string search)
		{
			bool result = false;
			Gtk.Application.Invoke (delegate {
				result = remote.DisplayNoteWithSearch (uri, search);
			});
			return result;
		}

		public void DisplaySearch ()
		{
			Gtk.Application.Invoke (delegate {
				remote.DisplaySearch ();
			});
		}

		public void DisplaySearchWithText (string search_text)
		{
			Gtk.Application.Invoke (delegate {
				remote.DisplaySearchWithText (search_text);
			});
		}

		public string FindNote (string linked_title)
		{
			return remote.FindNote (linked_title);
		}

		public string FindStartHereNote ()
		{
			return remote.FindStartHereNote ();
		}

		public string [] GetAllNotesWithTag (string tag_name)
		{
			return remote.GetAllNotesWithTag (tag_name);
		}

		public long GetNoteChangeDate (string uri)
		{
			return remote.GetNoteChangeDate (uri);
		}

		public string GetNoteCompleteXml (string uri)
		{
			return remote.GetNoteCompleteXml (uri);
		}

		public string GetNoteContents (string uri)
		{
			return remote.GetNoteContents (uri);
		}

		public string GetNoteContentsXml (string uri)
		{
			return remote.GetNoteContentsXml (uri);
		}

		public long GetNoteCreateDate (string uri)
		{
			return remote.GetNoteCreateDate (uri);
		}

		public string GetNoteTitle (string uri)
		{
			return remote.GetNoteTitle (uri);
		}

		public string [] GetTagsForNote (string uri)
		{
			return remote.GetTagsForNote (uri);
		}

		public bool HideNote (string uri)
		{
			bool result = false;
			Gtk.Application.Invoke (delegate {
				result = remote.HideNote (uri);
			});
			return result;
		}

		public string [] ListAllNotes ()
		{
			return remote.ListAllNotes ();
		}

		public event RemoteAddedHandler NoteAdded;

		public event RemoteDeletedHandler NoteDeleted;

		public bool NoteExists (string uri)
		{
			return remote.NoteExists (uri);
		}

		public event RemoteSavedHandler NoteSaved;

		public bool RemoveTagFromNote (string uri, string tag_name)
		{
			return remote.RemoveTagFromNote (uri, tag_name);
		}

		public string [] SearchNotes (string query, bool case_sensitive)
		{
			return remote.SearchNotes (query, case_sensitive);
		}

		public bool SetNoteCompleteXml (string uri, string xml_contents)
		{
			return remote.SetNoteCompleteXml (uri, xml_contents);
		}

		public bool SetNoteContents (string uri, string text_contents)
		{
			return remote.SetNoteContents (uri, text_contents);
		}

		public bool SetNoteContentsXml (string uri, string xml_contents)
		{
			return remote.SetNoteContentsXml (uri, xml_contents);
		}

		public string Version ()
		{
			return remote.Version ();
		}

		public string GetNotebookForNote (string uri)
		{
			return remote.GetNotebookForNote (uri);
		}

		public bool AddNoteToNotebook (string uri, string notebook_name)
		{
			return remote.AddNoteToNotebook (uri, notebook_name);
		}

		public string [] GetAllNotesInNotebook (string notebook_name)
		{
			return remote.GetAllNotesInNotebook (notebook_name);
		}

		public bool AddNotebook (string notebook_name)
		{
			return remote.AddNotebook (notebook_name);
		}

		#endregion
	}
}
