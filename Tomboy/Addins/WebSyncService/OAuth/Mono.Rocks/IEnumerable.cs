//
// IEnumerableRocks.cs
//
// Author:
//   Jb Evain (jbevain@novell.com)
//   Jonathan Pryor  <jpryor@novell.com>
//
// Copyright (c) 2007-2009 Novell, Inc. (http://www.novell.com)
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
using System.Linq;

namespace Mono.Rocks {

	public static class IEnumerableRocks {

		public static string Implode<TSource> (this IEnumerable<TSource> self, string separator)
		{
			return Implode (self, separator, e => e.ToString ());
		}

		public static string Implode<TSource> (this IEnumerable<TSource> self)
		{
			return Implode (self, null);
		}

		public static string Implode<TSource> (this IEnumerable<TSource> self, string separator, Func<TSource, string> selector)
		{
			Check.Self (self);
			Check.Selector (selector);

			var c = self as ICollection<TSource>;
			string[] values = new string [c != null ? c.Count : 10];
			int i = 0;
			foreach (var e in self) {
				if (values.Length == i)
					Array.Resize (ref values, i*2);
				values [i++] = selector (e);
			}
			if (i < values.Length)
				Array.Resize (ref values, i);
			return string.Join (separator, values);
		}
	}
}
