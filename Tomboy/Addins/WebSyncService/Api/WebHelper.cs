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
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Tomboy.WebSync.Api
{
	public class WebHelper
	{
		public string Get (string uri, IDictionary<string, string> queryParameters)
		{
			WebRequest request = BuildRequest (uri, queryParameters);
			request.Method = "GET";

			// TODO: Set ContentLength, UserAgent, Timeout, KeepAlive, Proxy, ContentType?
			//       (May only be available if we cast back to HttpWebRequest)

			return ProcessResponse (request);
		}

		public string PutJson (string uri, IDictionary<string, string> queryParameters, string postValue)
		{
			WebRequest request = BuildRequest (uri, queryParameters);
			request.Method = "POST";

			// TODO: Set ContentLength, UserAgent, Timeout, KeepAlive, Proxy, ContentType?
			//       (May only be available if we cast back to HttpWebRequest)
			request.ContentType = "application/json";

			byte [] data = Encoding.UTF8.GetBytes (postValue);
			request.ContentLength = data.Length;

			// TODO: try/finally error handling
			Stream requestStream = request.GetRequestStream ();
			requestStream.Write (data, 0, data.Length);
			requestStream.Close ();

			return ProcessResponse (request);
		}

		private string ProcessResponse (WebRequest request)
		{
			string responseString;
			
			// TODO: Error-checking
			WebResponse response = request.GetResponse ();
			
			using (StreamReader sr = new StreamReader (response.GetResponseStream ())) {
				responseString = sr.ReadToEnd ();
			}

			return responseString;
		}

		private WebRequest BuildRequest (string uri, IDictionary<string, string> queryParameters)
		{
			StringBuilder urlBuilder = new StringBuilder (uri);	// TODO: Capacity?
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

			return HttpWebRequest.Create (urlBuilder.ToString ());
		}
	}
}
