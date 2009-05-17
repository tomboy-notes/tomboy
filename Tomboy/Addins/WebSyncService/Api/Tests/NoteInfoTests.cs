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

#if ENABLE_TESTS

using System;

using Tomboy.WebSync.Api;
using Hyena.Json;

using NUnit.Framework;

namespace Tomboy.WebSync.Api.Tests
{
	[TestFixture]
	public class NoteInfoTests
	{
		[Test]
		public void ParseTest ()
		{
			Deserializer deserializer = new Deserializer ();

			string noteJson = "{\"guid\": \"002e91a2-2e34-4e2d-bf88-21def49a7705\"," +
				"\"title\" :\"New Note 6\"," +
				"\"note-content\" :\"New Note 6\\nDescribe youre note <b>here</b>.\"," +
				"\"note-content-version\" : 0.2," +
				"\"last-change-date\" : \"2009-04-19T21:29:23.2197340-07:00\"," +
				"\"last-metadata-change-date\" : \"2009-04-19T21:29:23.2197340-07:00\"," +
				"\"create-date\" : \"2008-03-06T13:44:46.4342680-08:00\"," +
				"\"last-sync-revision\" : 57," +
				"\"open-on-startup\" : false," +
				"\"tags\" : [\"tag1\",\"tag2\"]" +
				"}";
			deserializer.SetInput (noteJson);
			JsonObject noteObj = (JsonObject) deserializer.Deserialize ();
			//Assert.AreEqual (57, (int)noteObj["last-sync-revision"]);
			NoteInfo note = NoteInfo.ParseJson (noteObj);
			Assert.AreEqual ("002e91a2-2e34-4e2d-bf88-21def49a7705", note.Guid, "GUID");
			Assert.AreEqual ("New Note 6\nDescribe youre note <b>here</b>.", note.NoteContent, "NoteContent");
			Assert.AreEqual (0.2, note.NoteContentVersion, "NoteContentVersion");
			Assert.AreEqual ("New Note 6", note.Title, "Title");
			Assert.AreEqual (DateTime.Parse ("2009-04-19T21:29:23.2197340-07:00"),
					note.LastChangeDate, "LastChangeDate");
			Assert.AreEqual (DateTime.Parse ("2009-04-19T21:29:23.2197340-07:00"),
					note.LastMetadataChangeDate, "LastMetadataChangeDate");
			Assert.AreEqual (DateTime.Parse ("2008-03-06T13:44:46.4342680-08:00"),
					note.CreateDate, "CreateDate");
			Assert.AreEqual (57, note.LastSyncRevision, "LastSyncRevision");

			Assert.IsTrue (note.OpenOnStartup.HasValue, "OpenOnStartup should have value");
			Assert.IsFalse (note.OpenOnStartup.Value, "OpenOnStartup value");
			
			Assert.IsNotNull (note.Tags, "Tags should not be null");

			// TODO: Test resourceref,command,lack of guid,nullables,etc,
			//       ApplicationException when missing comma in string, wrong type, etc
		}
	}
}

#endif