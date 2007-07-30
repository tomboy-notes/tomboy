//
// Gnome.Keyring.NetItemData.cs
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

using System.Collections;

namespace Gnome.Keyring {
	public class NetItemData : ItemData {
		public string User;
		public string Domain;
		public string Server;
		public string Obj;
		public string Protocol;
		public string AuthType;
		public int Port;

		public override ItemType Type {
			get { return ItemType.NetworkPassword; }
		}

		internal override void SetValuesFromAttributes ()
		{
			Hashtable tbl = Attributes;
			if (tbl == null)
				return;

			User = (string) tbl ["user"];
			Domain = (string) tbl ["domain"];
			Server = (string) tbl ["server"];
			Obj = (string) tbl ["object"];
			Protocol = (string) tbl ["protocol"];
			AuthType = (string) tbl ["authtype"];
			if (tbl ["port"] != null)
				Port = (int) tbl ["port"];

			base.SetValuesFromAttributes ();
		}
	}
}

