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

using Mono.Unix;

using Tomboy.Sync;

namespace Tomboy.WebSync
{
	public class WebSyncServiceAddin : SyncServiceAddin
	{
		private bool initialized;
		private string serverUrl;
		private string userName;
		
		public WebSyncServiceAddin ()
		{
		}

		#region SyncServiceAddin Overrides

		public override string Id {
			get { return "tomboyweb"; }
		}

		public override string Name {
			get {
				return Catalog.GetString ("Tomboy Web");
			}
		}

		public override bool IsConfigured {
			get {
				return true; // TODO: Implement configuration
			}
		}

		public override bool IsSupported {
			get {
				return true; // TODO: Ever false?
			}
		}

		public override Gtk.Widget CreatePreferencesControl ()
		{
			return new Gtk.VBox ();
		}

		public override SyncServer CreateSyncServer ()
		{
			// TODO: What exactly do we need for connecting?
			return new WebSyncServer (serverUrl, userName);
		}

		public override void PostSyncCleanup ()
		{
		}

		public override void ResetConfiguration ()
		{
			throw new System.NotImplementedException();
		}

		public override bool SaveConfiguration ()
		{
			// TODO: Implement
			return true;
		}
		
		#endregion

		#region ApplicationAddin Overrides

		public override void Initialize ()
		{
			initialized = true;
		}

		public override void Shutdown ()
		{
		}
		
		public override bool Initialized {
			get { return initialized; }
		}

		#endregion
	}
}
