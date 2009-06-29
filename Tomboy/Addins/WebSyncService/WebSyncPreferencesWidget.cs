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

using Mono.Unix;

namespace Tomboy.WebSync
{
	public class WebSyncPreferencesWidget : Gtk.VBox
	{
		private Gtk.Entry serverEntry;
		private Gtk.Button authButton;
		private bool authReqested;
		private Api.OAuth oauth;
		
		public WebSyncPreferencesWidget (Api.OAuth oauth, string server) : base (false, 5)
		{
			this.oauth = oauth;
			
			Gtk.Table prefsTable = new Gtk.Table (1, 2, false);
			prefsTable.RowSpacing = 5;
			prefsTable.ColumnSpacing = 10;

			serverEntry = new Gtk.Entry ();
			serverEntry.Text = server;
			AddRow (prefsTable, serverEntry, Catalog.GetString ("Se_rver:"), 0);
			
			Add (prefsTable);

			authButton = new Gtk.Button ();
			// TODO: If Auth is valid, this text should change
			if (!Auth.IsAccessToken)
				authButton.Label = Catalog.GetString ("_Connect to Server");
			else {
				authButton.Label = Catalog.GetString ("Connected");
				authButton.Sensitive = false;
			}
			authButton.Clicked += OnAuthButtonClicked;

			serverEntry.Changed += delegate {
				Auth = null;
			};

			Add (authButton);

			// TODO: Add a section that shows the user something to verify they put
			//       in the right URL...something that constructs their user URL, maybe?
			ShowAll ();
		}

		public string Server {
			get {
				return serverEntry.Text.Trim ();
			}
		}

		public Api.OAuth Auth {
			get { return oauth; }
			set {
				oauth = value;
				if (oauth == null) {
					authButton.Label =
						Catalog.GetString ("Connect to Server");
					authButton.Sensitive = true;
				}
			}
		}

		private void OnAuthButtonClicked (object sender, EventArgs args)
		{
			// TODO: Move this
			if (Auth == null) {
				Auth = new Api.OAuth ();
				Auth.AuthorizeLocation = Server + "/oauth/authenticate/";
				Auth.AccessTokenBaseUrl = Server + "/oauth/access_token/";
				Auth.RequestTokenBaseUrl = Server + "/oauth/request_token/";
				Auth.ConsumerKey = "abcdefg";
				Auth.ConsumerSecret = "1234567";
				Auth.Realm = "Snowy";
			}

			if (!Auth.IsAccessToken && !authReqested) {
				string authUrl = string.Empty;
				try {
					authUrl = Auth.GetAuthorizationUrl ();
				} catch (Exception e) {
					Logger.Error ("Failed to get auth URL from " + Server + ". Exception was: " + e.ToString ());
					authButton.Label = Catalog.GetString ("Server not responding. Try again later.");
					return;
				}
				Logger.Debug ("Launching browser to authorize web sync: " + authUrl);
				try {
					Services.NativeApplication.OpenUrl (authUrl);
					authReqested = true;
					authButton.Label = Catalog.GetString ("Click Here After Authorizing");
				} catch (Exception e) {
					Logger.Error ("Exception opening URL: " + e.Message);
					authButton.Label = Catalog.GetString ("Set the default browser and try again");
				}
			} else if (!Auth.IsAccessToken && authReqested) {
				authButton.Sensitive = false;
				authButton.Label = Catalog.GetString ("Processing...");
				try {
					if (!Auth.GetAccessAfterAuthorization ())
						throw new ApplicationException ("Unknown error getting access token");
					// TODO: Check Auth.IsAccessToken?
					Logger.Debug ("Successfully authorized web sync");
					authReqested = false;
				} catch (Exception e) {
					Logger.Error ("Failed to authorize web sync, with exception:");
					Logger.Error (e.ToString ());
					authReqested = true;
					authButton.Label = Catalog.GetString ("Authorization Failed, Try Again");
					authButton.Sensitive = true;
				}
			}

			if (Auth.IsAccessToken) {
				authButton.Sensitive = false;
				authButton.Label = Catalog.GetString ("Connected. Click Save to start synchronizing");
			}
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
	}
}
