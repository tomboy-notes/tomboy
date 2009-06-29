//
// QueryParameter.cs
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

using System;
using System.Collections.Generic;

namespace OAuth
{
	/// <summary>
	/// A common interface for query parameters to create lists.
	/// </summary>
	public interface IQueryParameter<T> : IComparable<IQueryParameter<T>>, IEquatable<IQueryParameter<T>>
	{
		string Name
		{
			get;
		}

		T Value
		{
			get;
		}
	}

	public interface IQueryParameter
	{
		string Name
		{
			get;
		}

		object Value
		{
			get;
		}
	}

	/// <summary>
	/// Provides a structure to hold query parameters for easier query string building.
	/// </summary>
	public class QueryParameter<T> : IQueryParameter, IQueryParameter<T>, IEquatable<QueryParameter<T>>, IComparable<QueryParameter<T>>
		where T : IComparable<T>, IEquatable<T>
	{
		private T _value;

		/// <summary>
		/// Creates an instance of the QueryParameter object.
		/// </summary>
		/// <param name="name">The parameter name.</param>
		/// <param name="value">The parameter value.</param>
		/// <param name="omitCondition">The condition under which to omit the parameter.</param>
		public QueryParameter (string name, T value, Predicate<T> omitCondition)
		{
			if (string.IsNullOrEmpty (name)) throw new ArgumentNullException ("name");

			Name = name;
			_value = value;
			Omit = omitCondition;
		}

		/// <summary>
		/// The condition under which to omit the QueryParameter.
		/// </summary>
		public Predicate<T> Omit
		{
			get;
			private set;
		}

		object IQueryParameter.Value
		{
			get { return _value; }
		}

		/// <summary>
		/// The parameter name.
		/// </summary>
		public string Name
		{
			get;
			private set;
		}

		/// <summary>
		/// The parameter value.
		/// </summary>
		public T Value
		{
			get { return _value; }
			private set { _value = value; }
		}

		/// <summary>
		/// Creates a string that represents the query parameter (or omits it if the omit condition is met).
		/// </summary>
		/// <returns></returns>
		public override string ToString ()
		{
			if (Omit (_value)) return null;
			else return string.Format ("{0}={1}", Name, _value);
		}

		public bool Equals (IQueryParameter<T> other)
		{
			return Equals ((QueryParameter<T>) other);
		}

		public int CompareTo (IQueryParameter<T> other)
		{
			return CompareTo ((QueryParameter<T>) other);
		}

		/// <summary>
		/// Check if this QueryParameter instance is equal to another.
		/// </summary>
		/// <param name="other">The other query parameter instance.</param>
		/// <returns>True if the objects are equal, false otherwise.</returns>
		public bool Equals (QueryParameter<T> other)
		{
			return string.Equals (other.Name, Name, StringComparison.OrdinalIgnoreCase)
				? EqualityComparer<T>.Default.Equals (other.Value, _value)
				: false;
		}

		/// <summary>
		/// Compares this QueryParameter instance to another.
		/// </summary>
		/// <param name="other">The other QueryParameter.</param>
		/// <returns>-1 if this instance is to be sorted after, 0 if they are equal, 1 if this one is to be sorted before.</returns>
		public int CompareTo (QueryParameter<T> other)
		{
			var value = string.Compare (Name, other.Name, StringComparison.OrdinalIgnoreCase);
			return value == 0 ? Comparer<T>.Default.Compare (_value, other.Value) : value;
		}

		/// <summary>
		/// Compares this object to another for equality.
		/// </summary>
		/// <param name="obj">The other object.</param>
		/// <returns>True if they are the same, false otherwise.</returns>
		public override bool Equals (object obj)
		{
			if (obj == null)
				throw new ArgumentNullException ("obj");
			if (obj is QueryParameter<T>)
				return Equals ((QueryParameter<T>) obj);
			else throw new ArgumentException ("obj is not a QueryParameter<T>.", "obj");
		}

		/// <summary>
		/// Gets the hash code for this QueryParameter instance.
		/// </summary>
		/// <returns>The hash code.</returns>
		public override int GetHashCode ()
		{
			return Name.GetHashCode () ^ _value.GetHashCode ();
		}

		/// <summary>
		/// Checks if two instances of <see cref="QueryParameter{T}">QueryParameter&lt;T&gt;</see> are equal.
		/// </summary>
		/// <param name="lhs">One instance.</param>
		/// <param name="rhs">The other instance.</param>
		/// <returns>True if they are same, false otherwise.</returns>
		public static bool operator == (QueryParameter<T> lhs, QueryParameter<T> rhs)
		{
			return lhs.Equals (rhs);
		}

		/// <summary>
		/// Checks if two instances of <see cref="QueryParameter{T}">QueryParameter&lt;T&gt;</see> are not equal.
		/// </summary>
		/// <param name="lhs">One instance.</param>
		/// <param name="rhs">The other instance.</param>
		/// <returns>True if they are different, false otherwise.</returns>
		public static bool operator != (QueryParameter<T> lhs, QueryParameter<T> rhs)
		{
			return !(lhs == rhs);
		}
	}
}
