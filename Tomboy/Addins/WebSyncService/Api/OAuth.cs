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
// Based on code from:
//      Bojan Rajkovic <bojanr@brandeis.edu>
//      Shannon Whitley <swhitley@whitleymedia.com>
//      Eran Sandler <http://eran.sandler.co.il/>
// 

using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Web;

using OAuth;
using Mono.Rocks;

namespace Tomboy.WebSync.Api
{
	// TODO: Rename to OAuthConnection ?
	public class OAuth : Base, IWebConnection
	{
		#region Constructor
		public OAuth ()
		{
			Debugging = false;
		}
		#endregion

		#region Public Authorization Methods
		public string GetAuthorizationUrl ()
		{
			var response = Post (RequestTokenBaseUrl, null, string.Empty);

			if (response.Length > 0) {
				// Response contains token and token secret.  We only need the token until we're authorized.
				var qs = HttpUtility.ParseQueryString (response);
				if (!string.IsNullOrEmpty (qs ["oauth_token"])) {
					Token = qs ["oauth_token"];
					TokenSecret = qs ["oauth_token_secret"];
					var link = string.Format ("{0}?oauth_token={1}&oauth_callback={2}", AuthorizeLocation, qs ["oauth_token"], HttpUtility.UrlEncode (CallbackUrl));
					Logger.Debug ("Response from request for auth url: {0}", response);
					return link;
				}
			}

			Logger.Error ("Asked server for preliminary token, but received nothing.");

			return string.Empty;
		}

		public bool GetAccessAfterAuthorization ()
		{
			if (string.IsNullOrEmpty (Token))
				throw new Exception ("Token");

			Logger.Debug ("Asking server for access token based on authorization token.");

			var response = Post (AccessTokenBaseUrl, null, string.Empty);

			Logger.Debug ("Received response from server: {0}", response);

			if (response.Length > 0) {
				//Store the Token and Token Secret
				var qs = HttpUtility.ParseQueryString (response);
				if (!string.IsNullOrEmpty (qs ["oauth_token"]))
					Token = qs ["oauth_token"];
				if (!string.IsNullOrEmpty (qs ["oauth_token_secret"]))
					TokenSecret = qs ["oauth_token_secret"];
				Logger.Debug ("Got access token from server");
				IsAccessToken = true;
				return true;
			} else {
				Logger.Error ("Failed to get access token from server");
				return false;
			}
		}
		#endregion

		#region IWebConnection implementation
		public string Get (string uri, IDictionary<string, string> queryParameters)
		{
			return WebRequest (RequestMethod.GET,
			                   BuildUri (uri, queryParameters),
			                   null);
		}
		
		public string Delete (string uri, IDictionary<string, string> queryParameters)
		{
			return WebRequest (RequestMethod.DELETE,
			                   BuildUri (uri, queryParameters),
			                   null);
		}
		
		public string Put (string uri, IDictionary<string, string> queryParameters, string putValue)
		{
			return WebRequest (RequestMethod.PUT,
			                   BuildUri (uri, queryParameters),
			                   putValue);
		}
		
		public string Post (string uri, IDictionary<string, string> queryParameters, string postValue)
		{
			return WebRequest (RequestMethod.POST,
			                   BuildUri (uri, queryParameters),
			                   postValue);
		}
		#endregion

		#region Public Properties
		public string Token { get; set; }

		public string TokenSecret { get; set; }

		public bool IsAccessToken { get; set; }

		public string ConsumerKey { get; set; }

		public string ConsumerSecret { get; set; }

		public string AuthorizeLocation { get; set; }

		public string RequestTokenBaseUrl { get; set; }

		public string AccessTokenBaseUrl { get; set; }

		public string Realm { get; set; }

		public string CallbackUrl { get; set; }

		public string Verifier { get; set; }
		#endregion

		#region Private Methods
//		/// <summary>
//		/// Submit a web request using OAuth, asynchronously.
//		/// </summary>
//		/// <param name="method">GET or POST.</param>
//		/// <param name="url">The full URL, including the query string.</param>
//		/// <param name="postData">Data to post (query string format), if POST methods.</param>
//		/// <param name="callback">The callback to call with the web request data when the asynchronous web request finishes.</param>
//		/// <returns>The return value of QueueUserWorkItem.</returns>
//		public bool AsyncWebRequest (RequestMethod method, string url, string postData, Action<string> callback)
//		{
//			return ThreadPool.QueueUserWorkItem (new WaitCallback (delegate {
//				callback (WebRequest (method, url, postData));
//			}));
//		}

		/// <summary>
		/// Submit a web request using OAuth.
		/// </summary>
		/// <param name="method">GET or POST.</param>
		/// <param name="url">The full URL, including the query string.</param>
		/// <param name="postData">Data to post (query string format), if POST methods.</param>
		/// <returns>The web server response.</returns>
		private string WebRequest (RequestMethod method, string url, string postData)
		{
			Uri uri = new Uri (url);

			var nonce = GenerateNonce ();
			var timeStamp = GenerateTimeStamp ();

			Logger.Debug ("Building web request for URL: {0}", url);
			if (Debugging) {
				Logger.Debug ("Generated nonce is {0}", nonce);
				Logger.Debug ("Generated time stamp is {0}", timeStamp);
			}

			var outUrl = string.Empty;
			List<IQueryParameter<string>> parameters = null;

			string callbackUrl = string.Empty;
			if (url.StartsWith (RequestTokenBaseUrl) || url.StartsWith (AccessTokenBaseUrl))
				callbackUrl = CallbackUrl;
			var sig = GenerateSignature (uri, ConsumerKey, ConsumerSecret, Token, TokenSecret, Verifier, method,
			                             timeStamp, nonce, callbackUrl, out outUrl, out parameters);

			if (Debugging)
				Logger.Debug ("Generated signature {0}", sig);

			parameters.Add (new QueryParameter<string> ("oauth_signature",
			                                            HttpUtility.UrlEncode (sig),
			                                            s => string.IsNullOrEmpty (s)));
			parameters.Sort ();

			if (Debugging)
				Logger.Debug ("Post data: {0}", postData);

			var ret = MakeWebRequest (method, url, parameters, postData);

			if (Debugging)
				Logger.Debug ("Returned value from web request: {0}", ret);

			return ret;
		}

		/// <summary>
		/// Wraps a web request into a convenient package.
		/// </summary>
		/// <param name="method">HTTP method of the request.</param>
		/// <param name="url">Full URL to the web resource.</param>
		/// <param name="postData">Data to post in query string format.</param>
		/// <returns>The web server response.</returns>
		private string MakeWebRequest (RequestMethod method,
		                               string url,
		                               List<IQueryParameter<string>> parameters,
		                               string postData)
		{
			var responseData = string.Empty;

			Logger.Debug("OAuth: SecurityProtocol before enforcement: {0}", ServicePointManager.SecurityProtocol);
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls |
												   SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			Logger.Debug("OAuth: SecurityProtocol after enforcement: {0}", ServicePointManager.SecurityProtocol);

			ServicePointManager.CertificatePolicy = new CertificateManager ();

			// TODO: Set UserAgent, Timeout, KeepAlive, Proxy?
			HttpWebRequest webRequest = ProxiedWebRequest.Create (url);
			webRequest.Method = method.ToString ();
			webRequest.ServicePoint.Expect100Continue = false;

			var headerParams =
				parameters.Implode (",", q => string.Format ("{0}=\"{1}\"", q.Name, q.Value));
			if (Debugging)
				Logger.Debug ("Generated auth header params string: " + headerParams);
			webRequest.Headers.Add ("Authorization",
			                        String.Format ("OAuth realm=\"{0}\",{1}",
			                                       Realm, headerParams));
            if (postData == null) {
                postData = string.Empty;
            }

			if (method == RequestMethod.PUT ||
			     method == RequestMethod.POST) {
				webRequest.ContentType = "application/json";
				// TODO: Error handling?
				using (var requestWriter = new StreamWriter (webRequest.GetRequestStream ()))
					requestWriter.Write (postData);
			}

			try {
				using (var responseReader = new StreamReader (webRequest.GetResponse ().GetResponseStream ())) {
			      		responseData = responseReader.ReadToEnd ();
				}
			} catch (Exception e) {
				Logger.Error ("Caught exception. Message: {0}", e.Message);
				Logger.Error ("Stack trace for previous exception: {0}", e.StackTrace);
				Logger.Error ("Rest of stack trace for above exception: {0}", System.Environment.StackTrace);
				throw;
			}

			if (Debugging)
				Logger.Debug ("Made web request, got response: {0}", responseData);

			return responseData;
		}

		private string BuildUri (string baseUri, IDictionary<string, string> queryParameters)
		{
			StringBuilder urlBuilder = new StringBuilder (baseUri);	// TODO: Capacity?
			urlBuilder.Append ("?");
			if (queryParameters != null) {
				foreach (var param in queryParameters) {
					urlBuilder.Append (param.Key);
					urlBuilder.Append ("=");
					urlBuilder.Append (param.Value);
					urlBuilder.Append ("&");
				}
			}
			// Get rid of trailing ? or &
			urlBuilder.Remove (urlBuilder.Length - 1, 1);
			return urlBuilder.ToString ();
		}
		#endregion
	}
}