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

namespace Tomboy.WebSync.Api
{
	public class RootInfo
	{
		#region Public Static Methods
		
		public static RootInfo GetRoot (string rootUri, IWebConnection connection)
		{
			// TODO: Error-handling in GET and Deserialize
			string jsonString = connection.Get (rootUri, null);
			RootInfo root = ParseJson (jsonString);
			return root;
		}

		public static RootInfo ParseJson (string jsonString)
		{
			Hyena.Json.Deserializer deserializer =
				new Hyena.Json.Deserializer (jsonString);
			object obj = deserializer.Deserialize ();

			Hyena.Json.JsonObject jsonObj =
				obj as Hyena.Json.JsonObject;
			if (jsonObj == null)
				throw new ArgumentException ("jsonString does not contain a valid RootInfo representation");

			// TODO: Checks
			RootInfo root = new RootInfo ();
			root.ApiVersion = (string) jsonObj ["api-version"];

			object val;
			if (jsonObj.TryGetValue ("user-ref", out val)) {
				Hyena.Json.JsonObject userRefJsonObj = (Hyena.Json.JsonObject) val;

				root.User =
					ResourceReference.ParseJson (userRefJsonObj);
			}

			root.AuthorizeUrl =  (string) jsonObj ["oauth_authorize_url"];
			root.AccessTokenUrl = (string) jsonObj ["oauth_access_token_url"];
			root.RequestTokenUrl = (string) jsonObj ["oauth_request_token_url"];

			return root;
		}

		#endregion

		#region API Members

		public ResourceReference User { get; private set; }

		public string ApiVersion { get; private set; }

		public string AuthorizeUrl { get; private set; }

		public string AccessTokenUrl { get; private set; }

		public string RequestTokenUrl { get; private set; }

		#endregion
	}
}
