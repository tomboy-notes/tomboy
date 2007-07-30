//
// Gnome.Keyring.ItemData.cs
//
// Authors:
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//
// (C) Copyright 2006 Novell, Inc. (http://www.novell.com)
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
using System.Collections;
using System.Text;

namespace Gnome.Keyring {
	public abstract class ItemData {
		public string Keyring;
		public int ItemID;
		public string Secret;
		public Hashtable Attributes;

		public abstract ItemType Type { get; }

		public override string ToString ()
		{
			StringBuilder sb = new StringBuilder ();
			foreach (string key in Attributes.Keys)
				sb.AppendFormat ("{0}: {1}\n", key, Attributes [key]);
			return String.Format ("Keyring: {0} ItemID: {1} Secret: {2}\n{3}", Keyring, ItemID, Secret, sb.ToString ());
		}

		internal virtual void SetValuesFromAttributes ()
		{
		}

		internal static ItemData GetInstanceFromItemType (ItemType type)
		{
			if (type == ItemType.GenericSecret)
				return new GenericItemData ();

			if (type == ItemType.NetworkPassword)
				return new NetItemData ();

			if (type == ItemType.Note)
				return new NoteItemData ();

			throw new ArgumentException ("Unknown type: " + type, "type");
		}
	}
}

