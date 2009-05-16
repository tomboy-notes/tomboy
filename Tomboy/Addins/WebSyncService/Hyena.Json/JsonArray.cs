// 
// JsonArray.cs
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

using System;
using System.Collections.Generic;

namespace Hyena.Json
{
    public class JsonArray : List<object>, IJsonCollection
    {
        public void Dump ()
        {
            Dump (1);
        }
        
        public void Dump (int levels)
        {
            if (Count == 0) {
                Console.WriteLine ("[ ]");
                return;
            }
        
            Console.WriteLine ("[");
            foreach (object item in this) {
                Console.Write (String.Empty.PadLeft (levels * 2, ' '));
                if (item is IJsonCollection) {
                    ((IJsonCollection)item).Dump (levels + 1);
                } else {
                    Console.WriteLine (item);
                }
            }
            Console.WriteLine ("{0}]", String.Empty.PadLeft ((levels - 1) * 2, ' '));
        }
    }
}
