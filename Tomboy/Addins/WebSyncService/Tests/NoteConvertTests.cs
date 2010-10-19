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

#if ENABLE_TESTS

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Tomboy.WebSync;
using Tomboy.WebSync.Api;

using NUnit.Framework;

namespace Tomboy.WebSync.Tests
{
	[TestFixture]
	public class NoteConvertTests
	{
		[TestFixtureSetUp]
		public void FixtureSetUp ()
		{
			InMemoryPreferencesClient memPrefsClient = new InMemoryPreferencesClient ();

			Type prefsType = typeof (Preferences);
			FieldInfo prefsClientField =
				prefsType.GetField ("client", BindingFlags.NonPublic
				                              | BindingFlags.Static);
			prefsClientField.SetValue (null, memPrefsClient);

			Type servicesType = typeof (Services);
			FieldInfo servicesClientField =
				servicesType.GetField ("prefs", BindingFlags.NonPublic
				                                | BindingFlags.Static);
			servicesClientField.SetValue (null, memPrefsClient);
		}

		[TestFixtureTearDown]
		public void FixtureTearDown ()
		{
			Type prefsType = typeof (Preferences);
			FieldInfo prefsClientField =
				prefsType.GetField ("client", BindingFlags.NonPublic
				                                        | BindingFlags.Static);
			prefsClientField.SetValue (null, null);

			Type servicesType = typeof (Services);
			FieldInfo servicesClientField =
				servicesType.GetField ("client", BindingFlags.NonPublic
				                                        | BindingFlags.Static);
			servicesClientField.SetValue (null, null);
		}
		
		[Test]
		public void ToNoteInfoTest ()
		{
			// Note content stress tests
			string [] titles = new string [18];
			string [] contents = new string [18];
			string [] expectedInfoContents = new string [18];
			
			titles [0] = "(Untitled 238)";
			contents [0] = @"<note-content version=""0.1"">

Title Actually on Third Line

(edited before moving to third line)

<link:internal>new note 322</link:internal>

title on second lin</note-content>";
			expectedInfoContents [0] = @"Title Actually on Third Line

(edited before moving to third line)

<link:internal>new note 322</link:internal>

title on second lin";

			titles [1] = "Title on Fourth Line";
			contents [1] = @"<note-content version=""0.1"">


Title on Fourth Line

Describe your new note here.</note-content>";
			expectedInfoContents [1] = @"
Title on Fourth Line

Describe your new note here.";

			titles [2] = "New Note 322";
			contents [2] = @"<note-content version=""0.1"">
Title on second lin

(edited after moving to second line)</note-content>";
			expectedInfoContents [2] = @"Title on second lin

(edited after moving to second line)";

			titles [3] = "New Note 326";
			contents [3] = @"<note-content version=""0.1"">







New Note 326

Describe your new note here.</note-content>";
			expectedInfoContents [3] = @"





New Note 326

Describe your new note here.";

			titles [4] = "(Untitled 331)";
			contents [4] = @"<note-content version=""0.1"" />";
			expectedInfoContents [4] = string.Empty;

			titles [5] = "(Untitled 329)";
			contents [5] = @"<note-content version=""0.1"">
Text on second line added after first line totally deleted
Describe your new note here.</note-content>";
			expectedInfoContents [5] = @"Text on second line added after first line totally deleted
Describe your new note here.";

			titles [6] = "Seventy Six trombones in the big parade blah blah blah blah blah blah blah blah hlkjsfdijsdflksjf lsajfsdlj lskjf sljk lsjf sljflsjf lsjkf sljfsl slfj sljfslkjf lsjf lsjf lsj fsdlj fsdlj fsljkf sljkf slfjk slkfj slfj sldfj sljkf lsakjfslajf sljf lsfjk sl dfjlsf j";
			contents [6] = @"<note-content version=""0.1"">Seventy Six trombones in the big parade blah blah blah blah blah blah blah blah hlkjsfdijsdflksjf lsajfsdlj lskjf sljk lsjf sljflsjf lsjkf sljfsl slfj sljfslkjf lsjf lsjf lsj fsdlj fsdlj fsljkf sljkf slfjk slkfj slfj sldfj sljkf lsakjfslajf sljf lsfjk sl dfjlsf j
Title on Third Lne yeah?

(edited before moving to second line...didn't mean third)</note-content>";
			expectedInfoContents [6] = @"Title on Third Lne yeah?

(edited before moving to second line...didn't mean third)";

			titles [7] = "New Note 329";
			contents [7] = @"<note-content version=""0.1"">New Note 329

Describe your new note here.</note-content>";
			expectedInfoContents [7] = @"Describe your new note here.";

			titles [8] = "New Note 329";
			contents [8] = @"<note-content version=""0.1""><note-title>New Note 329</note-title>

Describe your new note here.</note-content>";
			expectedInfoContents [8] = @"<note-title></note-title>Describe your new note here.";

			titles [9] = "New Note 329";
			contents [9] = @"<note-content version=""0.1""><note-title><b>New Note 329</b></note-title>

Describe your new note here.</note-content>";
			expectedInfoContents [9] = @"<note-title><b></b></note-title>Describe your new note here.";

			titles [10] = "New Note 329";
			contents [10] = @"<note-content version=""0.1""><size:huge><note-title>New Note 329</note-title>

Describe your new note here.</size:huge></note-content>";
			expectedInfoContents [10] = @"<size:huge><note-title></note-title>Describe your new note here.</size:huge>";

			titles [11] = "New Note 329";
			contents [11] = @"<note-content version=""0.1""><size:huge>New Note 329

Describe your new note here.</size:huge></note-content>";
			expectedInfoContents [11] = @"<size:huge>Describe your new note here.</size:huge>";

			titles [12] = "New Note 330";
			contents [12] = @"<note-content version=""0.1"">New Note 330
Describe your new note here.</note-content>";
			expectedInfoContents [12] = @"Describe your new note here.";

			titles [13] = "New Note 331";
			contents [13] = @"<note-content version=""0.1"">New Note 331


Describe your new note here.</note-content>";
			expectedInfoContents [13] = @"
Describe your new note here.";

			titles [14] = "New Note 331";
			contents [14] = @"<note-content version="""">New Note 331

Describe your new note here.</note-content>";
			expectedInfoContents [14] = @"Describe your new note here.";

			titles [15] = "New Note 331";
			contents [15] = @"<note-content xmlns:link=""http://beatniksoftware.com/tomboy/link"" xmlns:size=""http://beatniksoftware.com/tomboy/size"" version=""0.1"">New Note 331

Describe your new note here.</note-content>";
			expectedInfoContents [15] = @"Describe your new note here.";

			titles [16] = "New Note 331";
			contents [16] = @"<note-content xmlns:link=""http://beatniksoftware.com/tomboy/link"" version=""0.1"" xmlns:size=""http://beatniksoftware.com/tomboy/size"">New Note 331

Describe your new note here.</note-content>";
			expectedInfoContents [16] = @"Describe your new note here.";

			titles [17] = "New Note 331";
			contents [17] = @"<note-content version=""0.1"" xmlns:size=""http://beatniksoftware.com/tomboy/size"">New Note 331

Describe your new note here.</note-content>";
			expectedInfoContents [17] = @"Describe your new note here.";
			

			for (int i = 0; i < titles.Length; i++) {
				NoteData data = new NoteData ("note://tomboy/12345");
				data.Title = titles [i];
				data.Text = contents [i];
				string tmpFileName = "ToNoteInfoTest.tmp";
				File.Create (tmpFileName).Close ();
				File.SetCreationTime (tmpFileName, new DateTime (2009, 1, 5));
				File.SetLastWriteTime (tmpFileName, new DateTime (2009, 1, 6));
	
				Note note = Note.CreateExistingNote (data, tmpFileName, null);
				
				NoteInfo info = NoteConvert.ToNoteInfo (note);
				Assert.AreEqual (titles [i], info.Title, "Title " + i.ToString ());
				Assert.AreEqual (expectedInfoContents [i], info.NoteContent, "NoteContent " + i.ToString ());
				Assert.AreEqual (0.1, info.NoteContentVersion.Value, "NoteContentVersion " + i.ToString ());
			}
		}
	}

	public class InMemoryPreferencesClient : IPreferencesClient
	{
		private Dictionary<string, object> prefs =
			new Dictionary<string, object> ();
		private Dictionary<string, NotifyEventHandler> events =
			new Dictionary<string, NotifyEventHandler> ();

		#region IPreferencesClient implementation
		public void Set (string key, object val)
		{
			prefs [key] = val;
			foreach (string nkey in events.Keys) {
				NotifyEventHandler handler = events [nkey] as NotifyEventHandler;
				if (handler != null && key.StartsWith (nkey)) {
					NotifyEventArgs args = new NotifyEventArgs (key, val);
					handler (this, args);
				}
			}
		}
		
		public object Get (string key)
		{
			object val;
			if (prefs.TryGetValue (key, out val))
				return val;
			throw new NoSuchKeyException (key);
		}
		
		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				if (!events.ContainsKey (dir))
					events [dir] = notify;
				else
					events [dir] += notify;
			}
		}
		
		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				if (events.ContainsKey (dir))
					events [dir] -= notify;
			}
		}
		
		public void SuggestSync ()
		{
		}
		#endregion
	}
}

#endif