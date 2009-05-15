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

using Tomboy.Sync;

namespace Tomboy.WebSync
{
	public class WebSyncServer : SyncServer
	{
		private string serverUrl;
		private string userName;
		
		public WebSyncServer (string serverUrl, string userName)
		{
			this.serverUrl = serverUrl;
			this.userName = userName;
		}

		#region SyncServer implementation
		
		public bool BeginSyncTransaction ()
		{
			// TODO: Check connection and auth
			return true;
		}
		
		public bool CancelSyncTransaction ()
		{
			// TODO: Cancel any pending request
			return true;
		}
		
		public bool CommitSyncTransaction ()
		{
			// TODO: PUT request from info cached during
			//       UploadNotes and DeleteNotes
			return true;
		}
		
		public SyncLockInfo CurrentSyncLock {
			get {
				return null;
			}
		}
		
		public void DeleteNotes (IList<string> deletedNoteUUIDs)
		{
			// TODO: Build relevant piece of PUT request
		}
		
		public IList<string> GetAllNoteUUIDs ()
		{
			throw new System.NotImplementedException();
		}
		
		public IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision)
		{
			throw new System.NotImplementedException();
		}
		
		public string Id {
			get {
				return serverUrl;
			}
		}
		
		public int LatestRevision {
			get {
				throw new System.NotImplementedException();
			}
		}
		
		public void UploadNotes (IList<Note> notes)
		{
			// TODO: Build relevant piece of PUT request
		}
		
		#endregion
	}
}
