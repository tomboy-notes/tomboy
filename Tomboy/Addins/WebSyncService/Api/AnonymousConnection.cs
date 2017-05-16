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
// Copyright (c) 2009 Canonical, Ltd (http://www.canonical.com)
// 
// Authors: 
//      Rodrigo Moya <rodrigo.moya@canonical.com>
// 

using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Web;

using Mono.Rocks;

namespace Tomboy.WebSync.Api
{

	public class AnonymousConnection : IWebConnection
	{

		#region IWebConnection implementation
		public string Get (string uri, IDictionary<string, string> parameters)
		{
			return WebRequest ("GET", BuildUri (uri, parameters));
		}

		public string Delete (string uri, IDictionary<string, string> parameters)
		{
			return null;
		}

		public string Put (string uri, IDictionary<string, string> parameters, string putValue)
		{
			return null;
		}

		public string Post (string uri, IDictionary<string, string> parameters, string postValue)
		{
			return null;
		}
		#endregion

		#region Private Methods
		private string WebRequest (string method, string uri)
		{
			string responseData = string.Empty;
			HttpWebRequest webRequest;

			Logger.Debug ("AnonymousConnection: SecurityProtocol before enforcement: {0}", ServicePointManager.SecurityProtocol);
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls |
												   SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
			Logger.Debug ("AnonymousConnection: SecurityProtocol after enforcement: {0}", ServicePointManager.SecurityProtocol);

			ServicePointManager.CertificatePolicy = new CertificateManager ();

			try {
				webRequest = ProxiedWebRequest.Create (uri);

				webRequest.Method = method;
				webRequest.ServicePoint.Expect100Continue = false;

				using (var responseReader = new StreamReader (webRequest.GetResponse ().GetResponseStream ())) {
					responseData = responseReader.ReadToEnd ();
				}
			} catch (Exception e) {
				Logger.Error ("Caught exception. Message: {0}", e.Message);
				Logger.Error ("Stack trace for previous exception: {0}", e.StackTrace);
				throw;
			}

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
