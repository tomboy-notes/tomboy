//
// DeserializerTests.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
//
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

#if ENABLE_TESTS

using System;
using System.Reflection;
using NUnit.Framework;

using Hyena.Json;

namespace Hyena.Json.Tests
{
    [TestFixture]
    public class DeserializerTests : Hyena.Tests.TestBase
    {
        private Deserializer deserializer;
        
        [TestFixtureSetUp]
        public void Setup ()
        {
            deserializer = new Deserializer ();
        }
        
        [Test]
        public void Literal ()
        {
            Assert.AreEqual ("hello", (string)deserializer.SetInput ("\"hello\"").Deserialize ());
            Assert.AreEqual (-1.76e-3, (double)deserializer.SetInput ("-1.76e-3").Deserialize ());
            Assert.AreEqual (null, deserializer.SetInput ("null").Deserialize ());
            Assert.AreEqual (true, (bool)deserializer.SetInput ("true").Deserialize ());
            Assert.AreEqual (false, (bool)deserializer.SetInput ("false").Deserialize ());
        }
        
        [Test]
        public void Array ()
        {
            JsonArray array = (JsonArray)deserializer.SetInput ("[]").Deserialize ();
            Assert.AreEqual (0, array.Count);
            
            array = (JsonArray)deserializer.SetInput ("[[]]").Deserialize ();
            Assert.AreEqual (1, array.Count);
            Assert.AreEqual (0, ((JsonArray)array[0]).Count);
            
            array = (JsonArray)deserializer.SetInput ("[[true,[]]]").Deserialize ();
            Assert.AreEqual (1, array.Count);
            Assert.AreEqual (2, ((JsonArray)array[0]).Count);
            Assert.AreEqual (0, ((JsonArray)((JsonArray)array[0])[1]).Count);
            
            array = (JsonArray)deserializer.SetInput ("[\"a\", 1.0, true]").Deserialize ();
            Assert.AreEqual (3, array.Count);
            Assert.AreEqual ("a", (string)array[0]);
            Assert.AreEqual (1, (double)array[1]);
            Assert.AreEqual (true, (bool)array[2]);
        }
        
        [Test]
        public void Object ()
        {
            JsonObject obj = (JsonObject)deserializer.SetInput ("{}").Deserialize ();
            Assert.AreEqual (0, obj.Count);
            
            obj = (JsonObject)deserializer.SetInput ("{\"a\":{}}").Deserialize ();
            Assert.AreEqual (1, obj.Count);
            Assert.AreEqual (0, ((JsonObject)obj["a"]).Count);
            
            obj = (JsonObject)deserializer.SetInput ("{\"a\":[{\"b\":false},\"c\"]}").Deserialize ();
            Assert.AreEqual (1, obj.Count);
            JsonArray arr = (JsonArray)obj["a"];
            Assert.AreEqual (2, arr.Count);
            Assert.AreEqual (false, ((JsonObject)arr[0])["b"]);
            Assert.AreEqual ("c", (string)arr[1]);
        }
    }
}

#endif
