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
using System.Text.RegularExpressions;

using Tomboy.Sync;
using Tomboy.WebSync.Api;

namespace Tomboy.WebSync
{
	public static class NoteConvert
	{
		public static NoteInfo ToNoteInfo (Note note)
		{
			NoteInfo noteInfo = new NoteInfo ();
			
			noteInfo.Guid = note.Id;
			noteInfo.Title = note.Title;
			noteInfo.OpenOnStartup = note.IsOpenOnStartup;
			noteInfo.Pinned = note.IsPinned;
			noteInfo.CreateDate = note.CreateDate;
			noteInfo.LastChangeDate = note.ChangeDate;
			noteInfo.LastMetadataChangeDate = note.MetadataChangeDate;

			noteInfo.Tags = new List<string> ();
			foreach (Tag tag in note.Tags)
				noteInfo.Tags.Add (tag.Name);

			const string noteContentRegex =
				@"^<note-content(\s+version=""(?<contentVersion>[^""]+)"")?>(?<innerContent>.*)</note-content>$";
			Match m = Regex.Match (note.XmlContent, noteContentRegex, RegexOptions.Singleline);
			Group versionGroup = m.Groups ["contentVersion"];
			Group contentGroup = m.Groups ["innerContent"];

			double contentVersion;
			if (versionGroup.Success &&
			    double.TryParse (versionGroup.Value, out contentVersion)) {
				noteInfo.NoteContentVersion = contentVersion;
			} else
				noteInfo.NoteContentVersion = 0.1;

			if (contentGroup.Success)
				noteInfo.NoteContent = contentGroup.Value;
			else
				noteInfo.NoteContent = string.Empty;

			return noteInfo;
		}

		public static NoteData ToNoteData (NoteInfo noteInfo)
		{
			// NOTE: For now, we absolutely require values for
			//       Guid, Title, NoteContent, and NoteContentVersion
			NoteData noteData = new NoteData (noteInfo.Guid);
			noteData.Title = noteInfo.Title;
			noteData.Text =
				"<note-content version=\"" + noteInfo.NoteContentVersion.ToString () + "\">" +
				noteInfo.NoteContent + "</note-content>";
			if (noteInfo.LastChangeDate.HasValue)
				noteData.ChangeDate = noteInfo.LastChangeDate.Value;
			if (noteInfo.LastMetadataChangeDate.HasValue)
				noteData.MetadataChangeDate = noteInfo.LastMetadataChangeDate.Value;
			if (noteInfo.CreateDate.HasValue)
				noteData.CreateDate = noteInfo.CreateDate.Value;
			if (noteInfo.OpenOnStartup.HasValue)
				noteData.IsOpenOnStartup = noteInfo.OpenOnStartup.Value;
			// TODO: support Pinned

			if (noteInfo.Tags != null) {
				foreach (string tagName in noteInfo.Tags) {
					Tag tag = TagManager.GetOrCreateTag (tagName);
					noteData.Tags [tag.NormalizedName] = tag;
				}
			}

			return noteData;
		}

		public static string ToNoteXml (NoteInfo noteInfo)
		{
			NoteData noteData = ToNoteData (noteInfo);
			return NoteArchiver.WriteString (noteData);
		}
	}
}
