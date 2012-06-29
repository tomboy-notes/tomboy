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
using System.Web;

using Mono.Unix;
using Tomboy.WebSync.Api;
#if !WIN32
using HL = System.Net;
#else
using HL = MonoHttp;
#endif 

namespace Tomboy.WebSync
{
	public class WebSyncPreferencesWidget : Gtk.VBox
	{
		private Gtk.Entry serverEntry;
		private Gtk.Button authButton;
		private Api.OAuth oauth;
		private HL.HttpListener listener;
		
		private const string callbackHtmlTemplate =
				@"<html><head><meta http-equiv=""content-type"" content=""text/html; charset=utf-8""><title>{0}</title></head><body><div><h1>{0}</h1>{1}</div></body></html>";
		
		public WebSyncPreferencesWidget (Api.OAuth oauth, string server, EventHandler requiredPrefChanged) : base (false, 5)
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
				authButton.Label = Catalog.GetString ("Connect to Server");
			else {
				authButton.Label = Catalog.GetString ("Connected");
				authButton.Sensitive = false;
			}
			authButton.Clicked += OnAuthButtonClicked;

			serverEntry.Changed += delegate {
				Auth = null;
			};
			serverEntry.Changed += requiredPrefChanged;

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
			if (listener != null && listener.IsListening) {
				listener.Stop ();
				listener.Close ();
			}

			// TODO: Move this
			if (Auth == null)
				Auth = new Api.OAuth ();

			string rootUri = Server.TrimEnd('/') + "/api/1.0";
			try {
				RootInfo root = RootInfo.GetRoot (rootUri, new Api.AnonymousConnection ());

				Auth.AuthorizeLocation = root.AuthorizeUrl;
				Auth.AccessTokenBaseUrl = root.AccessTokenUrl;
				Auth.RequestTokenBaseUrl = root.RequestTokenUrl;
				Auth.ConsumerKey = "anyone";
				Auth.ConsumerSecret = "anyone";
				Auth.Realm = "Snowy";
			} catch (Exception e) {
				Logger.Error ("Failed to get Root resource " + rootUri + ". Exception was: " + e.ToString());
				authButton.Label = Catalog.GetString ("Server not responding. Try again later.");
				oauth = null;
				return;
			}

			if (!Auth.IsAccessToken) {
				listener = new HL.HttpListener ();
				int portToTry = 8000;
				string callbackUrl = string.Empty;
				while (!listener.IsListening && portToTry < 9000) {
					callbackUrl = String.Format ("http://localhost:{0}/tomboy-web-sync/",
					                            portToTry);
					try {
						listener.Prefixes.Add (callbackUrl);
					} catch (Exception e) {
						Logger.Error ("Exception while trying to add {0} as an HttpListener Prefix",
							callbackUrl);
						Logger.Error (e.ToString ());
						break;
					}
					try {
						listener.Start ();
						Auth.CallbackUrl = callbackUrl;
					} catch {
						listener.Prefixes.Clear ();
						portToTry++;
					}
				}

				if (!listener.IsListening) {
					Logger.Error ("Unable to start HttpListener on any port between 8000-8999");
					authButton.Label = Catalog.GetString ("Server not responding. Try again later.");
					oauth = null;
					return;
				}

				Logger.Debug ("Listening on {0} for OAuth callback", callbackUrl);
				string authUrl = string.Empty;
				try {
					authUrl = Auth.GetAuthorizationUrl ();
				} catch (Exception e) {
					listener.Stop ();
					listener.Close ();
					Logger.Error ("Failed to get auth URL from " + Server + ". Exception was: " + e.ToString ());
					// Translators: The web service supporting Tomboy WebSync is not responding as expected
					authButton.Label = Catalog.GetString ("Server not responding. Try again later.");
					oauth = null;
					return;
				}

				IAsyncResult result = listener.BeginGetContext (delegate (IAsyncResult localResult) {
					HL.HttpListenerContext context;
					try {
						context = listener.EndGetContext (localResult);
					} catch (Exception e) {
						// TODO: Figure out why this error occurs
						Logger.Error ("Error processing OAuth callback. Could be a sign that you pressed the button to reset the connection. Exception details:");
						Logger.Error (e.ToString ());
						return;
					}
					// Assuming if we got here user clicked Allow
					Logger.Debug ("Context request uri query section: " + context.Request.Url.Query);
					// oauth_verifier is required in OAuth 1.0a, not 1.0
					var qs = HttpUtility.ParseQueryString (context.Request.Url.Query);
					if (!String.IsNullOrEmpty (qs ["oauth_verifier"]))
						Auth.Verifier = qs ["oauth_verifier"];
					try {
						if (!Auth.GetAccessAfterAuthorization ())
							throw new ApplicationException ("Unknown error getting access token");
						Logger.Debug ("Successfully authorized web sync");
					} catch (Exception e) {
						listener.Stop ();
						listener.Close ();
						Logger.Error ("Failed to authorize web sync, with exception:");
						Logger.Error (e.ToString ());
						Gtk.Application.Invoke (delegate {
							authButton.Label = Catalog.GetString ("Authorization Failed, Try Again");
							authButton.Sensitive = true;
						});
						oauth = null;
						return;
					}
					string htmlResponse =
						String.Format (callbackHtmlTemplate,
						               // Translators: Title of web page presented to user after they authorized Tomboy for sync
						               Catalog.GetString ("Tomboy Web Authorization Successful"),
						               // Translators: Body of web page presented to user after they authorized Tomboy for sync
						               Catalog.GetString ("Please return to the Tomboy Preferences window and press Save to start synchronizing."));
					using (var writer = new System.IO.StreamWriter (context.Response.OutputStream))
						writer.Write (htmlResponse);
					listener.Stop ();
					listener.Close ();

					if (Auth.IsAccessToken) {
						Gtk.Application.Invoke (delegate {
							authButton.Sensitive = false;
							authButton.Label = Catalog.GetString ("Connected. Press Save to start synchronizing");
						});
					}
				}, null);

				Logger.Debug ("Launching browser to authorize web sync: " + authUrl);
				authButton.Label = Catalog.GetString ("Authorizing in browser (Press to reset connection)");
				try {
					Services.NativeApplication.OpenUrl (authUrl, Screen);
				} catch (Exception e) {
					listener.Stop ();
					listener.Close ();
					Logger.Error ("Exception opening URL: " + e.Message);
					// Translators: Sometimes a user's default browser is not set, so we recommend setting it and trying again
					authButton.Label = Catalog.GetString ("Set the default browser and try again");
					return;
				}
				// Translators: The user must take action in their web browser to continue the authorization process
				authButton.Label = Catalog.GetString ("Authorizing in browser (Press to reset connection)");
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
