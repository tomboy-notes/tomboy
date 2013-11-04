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
// Copyright (c) 2009 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 

using System;
using System.Collections.Generic;

namespace Tomboy.WebSync.Api
{
	public class NoteInfo
	{
		#region Public Static Methods

		public static NoteInfo ParseJson (string jsonString)
		{
			Hyena.Json.Deserializer deserializer =
				new Hyena.Json.Deserializer (jsonString);
			object obj = deserializer.Deserialize ();

			Hyena.Json.JsonObject jsonObj =
				obj as Hyena.Json.JsonObject;
			return ParseJson (jsonObj);
		}

		public static NoteInfo ParseJson (Hyena.Json.JsonObject jsonObj)
		{
			if (jsonObj == null)
				throw new ArgumentException ("jsonObj does not contain a valid NoteInfo representation");

			// TODO: Checks
			NoteInfo note = new NoteInfo ();
			note.Guid = (string) jsonObj ["guid"];

			// TODO: Decide how much is required
			object val = 0;
			string key = "<unknown>";
			try {
				key = TitleElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.Title = (string) val;
				key = NoteContentElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.NoteContent = (string) val;
				key = NoteContentVersionElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.NoteContentVersion = (double) val;

				key = LastChangeDateElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.LastChangeDate = DateTime.Parse ((string) val);
				key = LastMetadataChangeDateElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.LastMetadataChangeDate = DateTime.Parse ((string) val);
				key = CreateDateElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.CreateDate = DateTime.Parse ((string) val);

				key = LastSyncRevisionElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.LastSyncRevision = (int) val;
				key = OpenOnStartupElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.OpenOnStartup = (bool) val;
				key = PinnedElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.Pinned = (bool) val;

				key = TagsElementName;
				if (jsonObj.TryGetValue (key, out val)) {
					Hyena.Json.JsonArray tagsJsonArray =
						(Hyena.Json.JsonArray) val;
					note.Tags = new List<string> (tagsJsonArray.Count);
					foreach (string tag in tagsJsonArray)
						note.Tags.Add (tag);
				}

				key = ResourceReferenceElementName;
				if (jsonObj.TryGetValue (key, out val))
					note.ResourceReference =
						ResourceReference.ParseJson ((Hyena.Json.JsonObject) val);
			} catch (InvalidCastException e) {
				Logger.Error("Note '{0}': Key '{1}', value  '{2}' failed to parse due to invalid type", note.Guid, key, val);
				throw e;
			}

			return note;
		}

		#endregion

		#region Public Methods

		public Hyena.Json.JsonObject ToUpdateObject ()
		{
			Hyena.Json.JsonObject noteUpdateObj =
				new Hyena.Json.JsonObject ();

			if (string.IsNullOrEmpty (Guid))
				throw new InvalidOperationException ("Cannot create a valid JSON representation without a Guid");
			
			noteUpdateObj [GuidElementName] = Guid;
			
			if (!string.IsNullOrEmpty (Command)) {
				noteUpdateObj [CommandElementName] = Command;
				return noteUpdateObj;
			}

			if (Title != null)
				noteUpdateObj [TitleElementName] = Title;
			if (NoteContent != null)
				noteUpdateObj [NoteContentElementName] = NoteContent;
			if (NoteContentVersion.HasValue)
				noteUpdateObj [NoteContentVersionElementName] = NoteContentVersion.Value;

			if (LastChangeDate.HasValue)
				noteUpdateObj [LastChangeDateElementName] =
					LastChangeDate.Value.ToString (NoteArchiver.DATE_TIME_FORMAT);
			if (LastMetadataChangeDate.HasValue)
				noteUpdateObj [LastMetadataChangeDateElementName] =
					LastMetadataChangeDate.Value.ToString (NoteArchiver.DATE_TIME_FORMAT);
			if (CreateDate.HasValue)
				noteUpdateObj [CreateDateElementName] =
					CreateDate.Value.ToString (NoteArchiver.DATE_TIME_FORMAT);

			// TODO: Figure out what we do on client side for this
//			if (LastSyncRevision.HasValue)
//				noteUpdateObj [LastSyncRevisionElementName] = LastSyncRevision;
			if (OpenOnStartup.HasValue)
				noteUpdateObj [OpenOnStartupElementName] = OpenOnStartup.Value;
			if (Pinned.HasValue)
				noteUpdateObj [PinnedElementName] = Pinned.Value;

			if (Tags != null) {
				Hyena.Json.JsonArray tagArray =
					new Hyena.Json.JsonArray ();
				foreach (string tag in Tags)
					tagArray.Add (tag);
				noteUpdateObj [TagsElementName] = tagArray;
			}

			return noteUpdateObj;
		}

		#endregion
		
		#region API Members
		
		public string Guid { get; set; }
		
		public ResourceReference ResourceReference { get; set; }
		
		public string Title { get; set; }
		
		public string NoteContent { get; set; }

		public double? NoteContentVersion { get; set; }
		
		public DateTime? LastChangeDate { get; set; }
		
		public DateTime? LastMetadataChangeDate { get; set; }
		
		public DateTime? CreateDate { get; set; }

		public int? LastSyncRevision { get; set; }
		
		public bool? OpenOnStartup { get; set; }
		
		public bool? Pinned { get; set; }
		
		public List<string> Tags { get; set; }

		public string Command { get; set; }

		#endregion

		#region Private Constants

		private const string GuidElementName = "guid";
		private const string ResourceReferenceElementName = "ref";
		private const string TitleElementName = "title";
		private const string NoteContentElementName = "note-content";
		private const string NoteContentVersionElementName = "note-content-version";
		private const string LastChangeDateElementName = "last-change-date";
		private const string LastMetadataChangeDateElementName = "last-metadata-change-date";
		private const string CreateDateElementName = "create-date";
		private const string LastSyncRevisionElementName = "last-sync-revision";
		private const string OpenOnStartupElementName = "open-on-startup";
		private const string PinnedElementName = "pinned";
		private const string TagsElementName = "tags";
		private const string CommandElementName = "command";

		#endregion
	}
}
