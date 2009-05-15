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
using System.Web.Script.Serialization;

namespace Tomboy.WebSync.Api
{
	public class UserInfo
	{
		public static UserInfo GetUser (string uri)
		{
			// TODO: Error-handling in GET and Deserialize
			WebHelper helper = new WebHelper ();
			string jsonString = helper.Get (uri, null);

			JavaScriptSerializer ser = new JavaScriptSerializer ();
			return ser.Deserialize <UserInfo> (jsonString);
		}
		
		public string FirstName { get; private set; }

		public string LastName { get; private set; }

		public ResourceReference Notes { get; private set; }

		public ResourceReference Friends { get; private set; }

		public int LatestSyncRevision { get; private set; }

		public IList<NoteInfo> GetNotes (bool includeContent)
		{
			return GetNotes (includeContent, -1);
		}

		public IList<NoteInfo> GetNotes (bool includeContent, int sinceRevision)
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
			
			jsonString = helper.Get (Notes.ApiRef, parameters);

			JavaScriptSerializer ser = new JavaScriptSerializer ();
			return ser.Deserialize <List<NoteInfo>> (jsonString);
		}

		public void UpdateNotes (IList<NoteInfo> noteUpdates)
		{
			// TODO: Error-handling in PUT, Serialize, and Deserialize
			WebHelper helper = new WebHelper ();
			JavaScriptSerializer ser = new JavaScriptSerializer ();

			string jsonString =
				helper.PutJson (Notes.ApiRef, null, ser.Serialize (noteUpdates));
			
			ser.Deserialize <List<NoteInfo>> (jsonString);
		}
	}
}
