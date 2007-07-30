//
// Gnome.Keyring.ResponseMessage.cs
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

using Mono.Unix.Native;

namespace Gnome.Keyring {
	class ResponseMessage {
		byte [] buffer;
		MemoryStream stream;

		public ResponseMessage (byte [] buffer)
		{
			this.buffer = buffer;
			stream = new MemoryStream (buffer);
		}

		public bool DataAvailable {
			get { return (stream.Position < stream.Length); }
		}

		public string [] GetStringList ()
		{
			int nstrings = GetInt32 ();
			string [] list = new string [nstrings];
			for (int i = 0; i < nstrings; i++) {
				list [i] = GetString ();
			}

			return list;
		}

		public string GetString ()
		{
			int len = GetInt32 ();
			if (len == -1) {
				return null;
			}
			int offset = (int) stream.Position;
			string result =  Encoding.UTF8.GetString (buffer, offset, len);
			stream.Position += len;
			return result;
		}

		public int GetInt32 ()
		{
			byte b3 = (byte) stream.ReadByte ();
			byte b2 = (byte) stream.ReadByte ();
			byte b1 = (byte) stream.ReadByte ();
			byte b0 = (byte) stream.ReadByte ();
			return (b0 + (b1 << 8) + (b2 << 16) + (b3 << 24));
		}

		public DateTime GetDateTime ()
		{
			return NativeConvert.FromTimeT ((GetInt32 () << 32) + GetInt32 ());
		}

		public void ReadAttributes (Hashtable tbl)
		{
			int natts = GetInt32 ();
			for (int i = 0; i < natts; i++) {
				object val;
				string name = GetString ();
				AttributeType type = (AttributeType) GetInt32 ();
				if (AttributeType.String == type) {
					val = GetString ();
				} else if (type == AttributeType.UInt32) {
					val = GetInt32 ();
				} else {
					throw new Exception ("This should not happen: "  + type);
				}
				tbl [name] = val;
			}
		}
	}
}

