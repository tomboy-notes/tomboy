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

		private const string serverUrlPrefPath =
			"/apps/tomboy/sync/tomboyweb/server";
		private const string usernamePrefPath =
			"/apps/tomboy/sync/tomboyweb/username";
		// TODO: Migrate to keyring or hash it or something!
		private const string passwordPrefPath =
			"/apps/tomboy/sync/tomboyweb/password";

		private Gtk.Entry serverEntry;
		private Gtk.Entry userEntry;
		private Gtk.Entry passwordEntry;
		
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
				string serverPref, userPref, passPref;
				GetConfigSettings (out serverPref, out userPref, out passPref);
				return !string.IsNullOrEmpty (serverPref) &&
					!string.IsNullOrEmpty (userPref) &&
					!string.IsNullOrEmpty (passPref);
			}
		}

		public override bool IsSupported {
			get {
				return true; // TODO: Ever false?
			}
		}

		public override Gtk.Widget CreatePreferencesControl ()
		{
			Gtk.Table prefsTable = new Gtk.Table (3, 2, false);
			prefsTable.RowSpacing = 5;
			prefsTable.ColumnSpacing = 10;

			serverEntry = new Gtk.Entry ();
			userEntry = new Gtk.Entry ();
			passwordEntry = new Gtk.Entry ();

			string serverPref, userPref, passPref;
			GetConfigSettings (out serverPref, out userPref, out passPref);

			serverEntry.Text = serverPref;
			userEntry.Text = userPref;
			passwordEntry.Text = passPref;
			passwordEntry.Visibility = false;
			
			AddRow (prefsTable, serverEntry, Catalog.GetString ("Se_rver:"), 0);
			AddRow (prefsTable, userEntry, Catalog.GetString ("User_name:"), 1);
			AddRow (prefsTable, passwordEntry, Catalog.GetString ("_Password:"), 2);

			prefsTable.Show ();

			// TODO: Add a section that shows the user something to verify they put
			//       in the right URL...something that constructs their user URL, maybe?
			
			return prefsTable;
		}

		public override SyncServer CreateSyncServer ()
		{
			// TODO: What exactly do we need for connecting?
			string serverPref, userPref, passPref;
			GetConfigSettings (out serverPref, out userPref, out passPref);
			return new WebSyncServer (serverPref, userPref, passPref);
		}

		public override void PostSyncCleanup ()
		{
		}

		public override void ResetConfiguration ()
		{
			SaveConfigSettings (null, null, null);
		}

		public override bool SaveConfiguration ()
		{
			string serverPref, userPref, passPref;
			GetPrefWidgetSettings (out serverPref, out userPref, out passPref);
			SaveConfigSettings (serverPref, userPref, passPref);
			// TODO: Validate config
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
			initialized = false;
		}
		
		public override bool Initialized {
			get { return initialized; }
		}

		#endregion

		#region Private Members

		private void GetPrefWidgetSettings (out string serverPref, out string userPref, out string passPref)
		{
			serverPref = serverEntry.Text.Trim ();
			userPref = userEntry.Text.Trim ();
			passPref = passwordEntry.Text.Trim ();
		}

		private void GetConfigSettings (out string serverPref, out string userPref, out string passPref)
		{
			serverPref = (string)
				Preferences.Get (serverUrlPrefPath);
			userPref = (string)
				Preferences.Get (usernamePrefPath);
			passPref = (string)
				Preferences.Get (passwordPrefPath);
		}

		private void SaveConfigSettings (string serverPref, string userPref, string passPref)
		{
			Preferences.Set (serverUrlPrefPath, serverPref);
			Preferences.Set (usernamePrefPath, userPref);
			Preferences.Set (passwordPrefPath, passPref);
		}
		
		private void AddRow (Gtk.Table table, Gtk.Widget widget, string labelText, uint row)
		{
			Gtk.Label l = new Gtk.Label (labelText);
			l.UseUnderline = true;
			l.Xalign = 0.0f;
			l.Show ();
			table.Attach (l, 0, 1, row, row + 1,
			              Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);

			widget.Show ();
			table.Attach (widget, 1, 2, row, row + 1,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill,
			              0, 0);

			l.MnemonicWidget = widget;

			// TODO: Tooltips
		}

		#endregion
	}
}
