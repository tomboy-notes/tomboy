//
// Gnome.Keyring.RequestMessage.cs
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
using System.IO;
using System.Text;

namespace Gnome.Keyring {
	class RequestMessage {
		MemoryStream stream = new MemoryStream ();
		int op_start = -1;

		public MemoryStream Stream {
			get { return stream; }
		}

		public void CreateSimpleOperation (Operation op)
		{
			StartOperation (op);
			EndOperation ();
		}

		public void CreateSimpleOperation (Operation op, string str1)
		{
			StartOperation (op);
			Write (str1);
			EndOperation ();
		}

		public void CreateSimpleOperation (Operation op, string str1, string str2)
		{
			StartOperation (op);
			Write (str1);
			Write (str2);
			EndOperation ();
		}

		public void CreateSimpleOperation (Operation op, string str1, int i1)
		{
			StartOperation (op);
			Write (str1);
			Write (i1);
			EndOperation ();
		}

		public void StartOperation (Operation op)
		{
			string appname = Ring.ApplicationName;
			BinaryWriter writer = new BinaryWriter (stream);
			writer.Write (0);

			Write (appname);
			int curpos = (int) stream.Position;
			stream.Position = 0;
			writer = new BinaryWriter (stream);
			writer.Write (SwapBytes (curpos));
			stream.Position = curpos;

			op_start = (int) stream.Length;
			writer.Write (0);
			writer.Write (SwapBytes ((int) op));
		}

		public void EndOperation ()
		{
			int current = (int) stream.Length;
			int size = SwapBytes (current - op_start);
			stream.Position = op_start;
			BinaryWriter writer = new BinaryWriter (stream);
			writer.Write (size);
		}

		public void Write (string str)
		{
			WriteString (new BinaryWriter (stream), str);
		}

		static void WriteString (BinaryWriter writer, string str)
		{
			if (str == null) {
				writer.Write ((int) -1);
				return;
			}
			byte [] bytes = Encoding.UTF8.GetBytes (str);
			writer.Write (SwapBytes (bytes.Length));
			writer.Write (bytes);
		}

		public void Write (int i)
		{
			BinaryWriter writer = new BinaryWriter (stream);
			writer.Write (SwapBytes (i));
		}

		public void WriteAttributes (Hashtable atts)
		{
			Hashtable copy = new Hashtable ();
			foreach (string key in atts.Keys) {
				object o = atts [key];
				if (o != null)
					copy [key] = o;
				
			}
			BinaryWriter writer = new BinaryWriter (stream);
			writer.Write (SwapBytes (copy.Count));
			foreach (string key in copy.Keys) {
				object o = atts [key];
				if (o is string) {
					EncodeAttribute (writer, key, (string) o);
				} else if (o is int) {
					int i = (int) o;
					if (key == "port" && i == 0)
						continue;
					EncodeAttribute (writer, key, i);
				} else {
					throw new Exception ("Should not happen.");
				}
			}
		}

		static void EncodeAttribute (BinaryWriter writer, string name, string val)
		{
			WriteString (writer, name);
			writer.Write (SwapBytes ((int) AttributeType.String));
			WriteString (writer, val);
		}

		static void EncodeAttribute (BinaryWriter writer, string name, int val)
		{
			WriteString (writer, name);
			writer.Write (SwapBytes ((int) AttributeType.UInt32));
			writer.Write (SwapBytes (val));
		}

		static int SwapBytes (int i)
		{
			byte b0 = (byte) ((i >> 24) & 0xFF);
			byte b1 = (byte) ((i >> 16) & 0xFF);
			byte b2 = (byte) ((i >> 8) & 0xFF);
			byte b3 = (byte) (i & 0xFF);
			return b0 + (b1 << 8) + (b2 << 16) + (b3 << 24);
		}

	}
}

