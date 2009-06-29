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
	public class ResourceReference
	{
		#region API Members
		
		public string Href { get; private set; }
		
		public string ApiRef { get; private set; }

		#endregion

		#region Public Static Members

		public static ResourceReference ParseJson (Hyena.Json.JsonObject jsonObj)
		{
			if (jsonObj == null)
				throw new ArgumentNullException ("jsonObj");

			// TODO: Casting checks?
			ResourceReference resourceRef = new ResourceReference ();
			object uri;
			if (jsonObj.TryGetValue ("api-ref", out uri))
				resourceRef.ApiRef = (string) uri;
			if (jsonObj.TryGetValue ("href", out uri))
				resourceRef.Href = (string) uri;
			
			return resourceRef;
		}

		#endregion
	}
}
