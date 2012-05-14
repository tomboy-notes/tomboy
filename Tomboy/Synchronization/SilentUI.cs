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
// Copyright (c) 2010 Novell, Inc. (http://www.novell.com)
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 

using System;
using System.Collections.Generic;
using System.Threading;

namespace Tomboy.Sync
{
	public class SilentUI : ISyncUI
	{
		private bool uiDisabled = false;
		private NoteManager manager;

		public SilentUI (NoteManager manager)
		{
			this.manager = manager;
		}

		#region ISyncUI implementation
		public void SyncStateChanged (SyncState state)
		{
			// TODO: Update tray/applet icon
			//       D-Bus event?
			//       libnotify bubbles when appropriate
			Logger.Debug ("SilentUI: SyncStateChanged: {0}", state);
			switch (state) {
			case SyncState.Connecting:
				uiDisabled = true;
				// TODO: Disable all kinds of note editing
				//         -New notes from server should be disabled, too
				//         -Anyway we could skip this when uploading changes?
				//         -Should store original Enabled state
				GuiUtils.GtkInvokeAndWait (() => {
					manager.ReadOnly = true;
					foreach (Note note in new List<Note> (manager.Notes)) {
						note.Enabled = false;
					}
				});
				break;
			case SyncState.Idle:
				if (uiDisabled) {
					GuiUtils.GtkInvokeAndWait (() => {
						manager.ReadOnly = false;
						foreach (Note note in new List<Note> (manager.Notes)) {
							note.Enabled = true;
						}
					});
					uiDisabled = false;
				}
				break;
			default:
				break;
			}
		}

		public void NoteSynchronized (string noteTitle, NoteSyncType type)
		{
			Logger.Debug ("SilentUI: NoteSynchronized, Title: {0}, Type: {1}", noteTitle, type);
		}

		public void NoteConflictDetected (NoteManager manager, Note localConflictNote, NoteUpdate remoteNote, IList<string> noteUpdateTitles)
		{
			Logger.Debug ("SilentUI: NoteConflictDetected, overwriting without a care");
			// TODO: At least respect conflict prefs
			// TODO: Implement more useful conflict handling
			if (localConflictNote.Id != remoteNote.UUID)
				GuiUtils.GtkInvokeAndWait (() => {
					manager.Delete (localConflictNote);
				});
			SyncManager.ResolveConflict (SyncTitleConflictResolution.OverwriteExisting);
		}
		#endregion
	}
}
