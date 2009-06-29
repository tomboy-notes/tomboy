//
// Extensions.cs
//  
// Author:
//       Bojan Rajkovic <bojanr@brandeis.edu>
// 
// Copyright (c) 2009 Bojan Rajkovic
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using Mono.Rocks;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace OAuth
{
	/// <summary>
	/// Extensions to help work with some BCL classes.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Normalizes the request parameters according to the query string specification.
		/// </summary>
		/// <param name="parameters">The list of parameters already sorted.</param>
		/// <returns>A string representing the normalized parameters.</returns>
		public static string NormalizeRequestParameters<T> (this IEnumerable<IQueryParameter<T>> parameters)
		{
			return parameters.Implode ("&");
		}

		/// <summary>
		/// Normalizes the request parameters according to the query string specification.
		/// </summary>
		/// <param name="parameters">The list of parameters already sorted.</param>
		/// <returns>A string representing the normalized parameters.</returns>
		public static string NormalizeRequestParameters (this IEnumerable<IQueryParameter> parameters)
		{
			return parameters.Implode ("&");
		}
		
		public static string NormalizeRequestParameters<T> (this IEnumerable<IQueryParameter<T>> parameters, Func<IQueryParameter<T>, string> selector)
		{
			return parameters.Implode ("&", selector);
		}

		/// <summary>
		/// Converts a NameValueCollection to an *actual* <see cref="Dictionary{T, T}">Dictionary&lt;T, T&gt;</see>.
		/// </summary>
		/// <param name="self">The NameValueCollection to convert.</param>
		/// <returns>A <see cref="Dictionary{T, T}">Dictionary&lt;T, T&gt;</see>.</returns>
		public static IDictionary<string, string> ToDictionary (this NameValueCollection self)
		{
			SortedDictionary<string, string> dict = new SortedDictionary<string, string> ();
			foreach (var key in self.AllKeys) dict[key] = self[key];
			return dict;
		}
	}
}
