// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 

using System;
using System.Collections.Generic;

namespace Tomboy.WebSync.Api
{
	public class UserInfo
	{
		#region Public Static Methods
		
		public static UserInfo GetUser (string serverUrl, string userName, IAuthProvider auth)
		{
			// TODO: Clean this up
			string baseUrl = serverUrl + "/api/1.0/";
			string uri = baseUrl + userName;
			
			// TODO: Error-handling in GET and Deserialize
			WebHelper helper = new WebHelper ();
			string jsonString = helper.Get (uri, null, auth);
			UserInfo user = ParseJson (jsonString);
			user.AuthProvider = auth;
			user.BaseUrl = baseUrl;
			return user;
		}

		public static UserInfo ParseJson (string jsonString)
		{
			Hyena.Json.Deserializer deserializer =
				new Hyena.Json.Deserializer (jsonString);
			object obj = deserializer.Deserialize ();

			Hyena.Json.JsonObject jsonObj =
				obj as Hyena.Json.JsonObject;
			if (jsonObj == null)
				throw new ArgumentException ("jsonString does not contain a valid UserInfo representation");

			// TODO: Checks
			UserInfo user = new UserInfo ();
			user.FirstName = (string) jsonObj ["first-name"];
			user.LastName = (string) jsonObj ["last-name"];
			
			object val;
			if (jsonObj.TryGetValue ("latest-sync-revision", out val))
				user.LatestSyncRevision = (int) val;
			else
				user.LatestSyncRevision = -1;
			
			if (jsonObj.TryGetValue ("current-sync-guid", out val))
				user.CurrentSyncGuid = (string) val;

			Hyena.Json.JsonObject notesRefJsonObj =
				(Hyena.Json.JsonObject) jsonObj ["notes-ref"];
			user.Notes =
				ResourceReference.ParseJson (notesRefJsonObj);

			if (jsonObj.TryGetValue ("friends-ref", out val)) {
				user.Notes =
					ResourceReference.ParseJson ((Hyena.Json.JsonObject) val);
			}

			return user;
		}

		#endregion

		#region API Members
		
		public string FirstName { get; private set; }

		public string LastName { get; private set; }

		public int? LatestSyncRevision { get; private set; }

		public string CurrentSyncGuid { get; private set; }

		public ResourceReference Notes { get; private set; }

		public ResourceReference Friends { get; private set; }

		public IList<NoteInfo> GetNotes (bool includeContent, out int? latestSyncRevision)
		{
			return GetNotes (includeContent, -1, out latestSyncRevision);
		}

		public IList<NoteInfo> GetNotes (bool includeContent, int sinceRevision, out int? latestSyncRevision)
		{
			// TODO: Error-handling in GET and Deserialize
			WebHelper helper = new WebHelper ();
			string jsonString = string.Empty;

			Dictionary<string, string> parameters =
				new Dictionary<string, string> ();
			if (includeContent)
				parameters ["include_notes"] = "true";
			if (sinceRevision >= 0)
				parameters ["since"] = sinceRevision.ToString ();
			
			jsonString = helper.Get (BaseUrl + Notes.ApiRef, parameters, AuthProvider);

			return ParseJsonNotes (jsonString, out latestSyncRevision);
		}

		public int UpdateNotes (IList<NoteInfo> noteUpdates, int expectedNewRevision)
		{
			// TODO: Error-handling in PUT, Serialize, and Deserialize
			WebHelper helper = new WebHelper ();

			string jsonResponseString =
				helper.PutJson (BaseUrl + Notes.ApiRef,
				                null,
				                CreateNoteChangesJsonString (noteUpdates, expectedNewRevision),
				                AuthProvider);

			// TODO: This response object could be extremely useful
//			using (System.IO.StreamWriter writer = System.IO.File.CreateText ("/home/sandy/lastPutResp"))
//				writer.Write (jsonResponseString);
			
			int? actualNewRevision;
			ParseJsonNotes (jsonResponseString, out actualNewRevision);
			if (!actualNewRevision.HasValue)
				throw new ApplicationException ("No new sync revision provided in server response after uploading note changes");
			return actualNewRevision.Value;
		}

		#endregion

		#region Public Properties

		public IAuthProvider AuthProvider { get; set; }

		public string BaseUrl { get; set; }

		#endregion

		#region Private Methods

		private IList<NoteInfo> ParseJsonNotes (string jsonString, out int? latestSyncRevision)
		{
			Hyena.Json.Deserializer deserializer =
				new Hyena.Json.Deserializer (jsonString);
			object obj = deserializer.Deserialize ();
			Hyena.Json.JsonObject jsonObj =
				obj as Hyena.Json.JsonObject;
			Hyena.Json.JsonArray noteArray =
				(Hyena.Json.JsonArray) jsonObj [NotesElementName];

			object val;
			if (jsonObj.TryGetValue (LatestSyncRevisionElementName, out val))
				latestSyncRevision = (int) val;
			else
				latestSyncRevision = null;
				
			return ParseJsonNoteArray (noteArray);
		}

		public IList<NoteInfo> ParseJsonNoteArray (Hyena.Json.JsonArray jsonArray)
		{
			if (jsonArray == null)
				throw new ArgumentNullException ("jsonArray does not contain a valid NoteInfo array representation");

			// TODO: Checks
			List<NoteInfo> noteList = new List<NoteInfo> ();
			foreach (Hyena.Json.JsonObject jsonObj in jsonArray)
				noteList.Add (NoteInfo.ParseJson (jsonObj));
			return noteList;
		}

		private string CreateNoteChangesJsonString (IList<NoteInfo> noteUpdates, int? expectedNewRevision)
		{
			Hyena.Json.JsonObject noteChangesObj =
				new Hyena.Json.JsonObject ();
			Hyena.Json.JsonArray noteChangesArray =
				new Hyena.Json.JsonArray ();
			foreach (NoteInfo note in noteUpdates)
				noteChangesArray.Add (note.ToUpdateObject ());
			noteChangesObj [NoteChangesElementName] = noteChangesArray;
			if (expectedNewRevision != null)
				noteChangesObj [LatestSyncRevisionElementName] = expectedNewRevision;

			// TODO: Handle errors
			Hyena.Json.Serializer serializer =
				new Hyena.Json.Serializer (noteChangesObj);
			// TODO: Log this for debugging?
			return serializer.Serialize ();
		}
		
		#endregion

		#region Private Constants

		private const string LatestSyncRevisionElementName = "latest-sync-revision";
		private const string NotesElementName = "notes";
		private const string NoteChangesElementName = "note-changes";

		#endregion
	}
}
