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

#if ENABLE_TESTS

using System;

using Tomboy.WebSync.Api;
using Hyena.Json;

using NUnit.Framework;

namespace Tomboy.WebSync.Api.Tests
{
	[TestFixture]
	public class ResourceReferenceTests
	{
		[Test]
		public void ParseTest ()
		{
			Deserializer deserializer = new Deserializer ();
			
			string refJson =
				"{ \"api-ref\" : \"http://domain/api/1.0/sally/notes\", \"href\" : \"http://domain/sally/notes\"}";
			deserializer.SetInput (refJson);
			JsonObject refObj = (JsonObject) deserializer.Deserialize ();
			ResourceReference resourceRef = ResourceReference.ParseJson (refObj);

			Assert.AreEqual ("http://domain/api/1.0/sally/notes",
			                 resourceRef.ApiRef,
			                 "ApiRef");
			Assert.AreEqual ("http://domain/sally/notes",
			                 resourceRef.Href,
			                 "Href");

			refJson =
				"{ \"api-ref\" : \"http://domain/api/1.0/sally/notes\"}";
			deserializer.SetInput (refJson);
			refObj = (JsonObject) deserializer.Deserialize ();
			resourceRef = ResourceReference.ParseJson (refObj);

			Assert.AreEqual ("http://domain/api/1.0/sally/notes",
			                 resourceRef.ApiRef,
			                 "ApiRef");
			Assert.IsNull (resourceRef.Href, "Href should be null when none specified");

			refJson =
				"{ \"href\" : \"http://domain/sally/notes\"}";
			deserializer.SetInput (refJson);
			refObj = (JsonObject) deserializer.Deserialize ();
			resourceRef = ResourceReference.ParseJson (refObj);

			Assert.AreEqual ("http://domain/sally/notes",
			                 resourceRef.Href,
			                 "Href");
			Assert.IsNull (resourceRef.ApiRef, "ApiRef should be null when none specified");
		}

		[Test]
		public void ExceptionTest ()
		{
			bool expectedExceptionRaised = false;
			try {
				ResourceReference.ParseJson (null);
			} catch (ArgumentNullException) {
				expectedExceptionRaised = true;
			}
			Assert.IsTrue (expectedExceptionRaised,
			               "ArgumentNullException expected on null input");

			expectedExceptionRaised = false;
			
			Deserializer deserializer = new Deserializer ();
			string invalidRefJson =
				"{ \"api-ref\" : 5, \"href\" : \"http://domain/sally/notes\"}";
			deserializer.SetInput (invalidRefJson);
			JsonObject refObj = (JsonObject) deserializer.Deserialize ();
			try {
				ResourceReference.ParseJson (refObj);
			} catch (InvalidCastException) {
				expectedExceptionRaised = true;
			}
			Assert.IsTrue (expectedExceptionRaised,
			               "InvalidCastException expected on invalid input");
		}
	}
}

#endif