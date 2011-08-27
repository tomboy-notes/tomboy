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

using Tomboy.Sync;

namespace Tomboy.WebSync
{
	public class WebSyncServiceAddin : SyncServiceAddin
	{
		private bool initialized;
		private WebSyncPreferencesWidget prefsWidget;

		private const string serverUrlPrefPath =
			"/apps/tomboy/sync/tomboyweb/server";
		private const string accessTokenBaseUrlPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_access_token_base_url";
		private const string authorizeLocationPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_authorize_location";
		private const string consumerKeyPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_consumer_key";
		private const string consumerSecretPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_consumer_secret";
		private const string realmPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_realm";
		private const string requestTokenBaseUrlPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_request_token_base_url";
		private const string tokenPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_token";
		private const string tokenSecretPrefPath =
			"/apps/tomboy/sync/tomboyweb/oauth_token_secret";
		
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
				string serverPref;
				Api.OAuth oauth;
				return GetConfigSettings (out oauth, out serverPref);
			}
		}
		
		public override bool AreSettingsValid
		{
			get {
				return prefsWidget != null && !String.IsNullOrEmpty(prefsWidget.Server);
			}
		}

		public override bool IsSupported {
			get {
				return true; // TODO: Ever false?
			}
		}

		public override Gtk.Widget CreatePreferencesControl (EventHandler requiredPrefChanged)
		{
			string serverPref;
			Api.OAuth oauth;
			GetConfigSettings (out oauth, out serverPref);
			prefsWidget = new WebSyncPreferencesWidget (oauth, serverPref, requiredPrefChanged);
			return prefsWidget;
		}

		public override SyncServer CreateSyncServer ()
		{
			string serverPref;
			Api.OAuth oauth;
			GetConfigSettings (out oauth, out serverPref);
			return new WebSyncServer (serverPref, oauth);
		}

		public override void PostSyncCleanup ()
		{
		}

		public override void ResetConfiguration ()
		{
			SaveConfigSettings (null, null);
			prefsWidget.Auth = null;
		}

		public override bool SaveConfiguration ()
		{
			// TODO: Is this really sufficient validation?
			//       Should we try a REST API request?
			if (prefsWidget.Auth == null ||
			    !prefsWidget.Auth.IsAccessToken)
				return false;
			SaveConfigSettings (prefsWidget.Auth, prefsWidget.Server);
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

		private bool GetConfigSettings (out Api.OAuth oauthConfig, out string serverPref)
		{
			serverPref = (string)
				Preferences.Get (serverUrlPrefPath);

			oauthConfig = new Api.OAuth ();
			oauthConfig.AccessTokenBaseUrl =
				Preferences.Get (accessTokenBaseUrlPrefPath) as string;
			oauthConfig.AuthorizeLocation =
				Preferences.Get (authorizeLocationPrefPath) as string;
			oauthConfig.ConsumerKey =
				Preferences.Get (consumerKeyPrefPath) as string;
			oauthConfig.ConsumerSecret =
				Preferences.Get (consumerSecretPrefPath) as string;
			oauthConfig.Realm =
				Preferences.Get (realmPrefPath) as string;
			oauthConfig.RequestTokenBaseUrl =
				Preferences.Get (requestTokenBaseUrlPrefPath) as string;
			oauthConfig.Token =
				Preferences.Get (tokenPrefPath) as string;
			oauthConfig.TokenSecret =
				Preferences.Get (tokenSecretPrefPath) as string;

			// The fact that the configuration was saved at all
			// implies that the token is an access token.
			// TODO: Any benefit in actually storing this bool, in
			//       case of weird circumstances?
			oauthConfig.IsAccessToken =
				!String.IsNullOrEmpty (oauthConfig.Token);
			
			return !string.IsNullOrEmpty (serverPref)
				&& oauthConfig.IsAccessToken;
		}

		private void SaveConfigSettings (Api.OAuth oauthConfig, string serverPref)
		{
			Preferences.Set (serverUrlPrefPath, GetConfigString (serverPref));
			Preferences.Set (accessTokenBaseUrlPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.AccessTokenBaseUrl) :
			                 String.Empty);
			Preferences.Set (authorizeLocationPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.AuthorizeLocation) :
			                 String.Empty);
			Preferences.Set (consumerKeyPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.ConsumerKey) :
			                 String.Empty);
			Preferences.Set (consumerSecretPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.ConsumerSecret) :
			                 String.Empty);
			Preferences.Set (realmPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.Realm) :
			                 String.Empty);
			Preferences.Set (requestTokenBaseUrlPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.RequestTokenBaseUrl) :
			                 String.Empty);
			Preferences.Set (tokenPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.Token) :
			                 String.Empty);
			Preferences.Set (tokenSecretPrefPath,
			                 oauthConfig != null ?
			                 GetConfigString (oauthConfig.TokenSecret) :
			                 String.Empty);
		}

		private string GetConfigString (string val)
		{
			return val ?? String.Empty;
		}

		#endregion
	}
}
