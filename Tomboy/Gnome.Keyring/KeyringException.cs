//
// Gnome.Keyring.KeyringException.cs
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
using System.Runtime.Serialization;

namespace Gnome.Keyring {
	public class KeyringException : Exception, ISerializable {
		ResultCode code;

		public KeyringException () : base ("Unknown error")
		{
		}

		internal KeyringException (ResultCode code) : base (GetMsg (code))
		{
			this.code = code;
		}

		protected KeyringException (SerializationInfo info, StreamingContext context)
			: base (info, context)
		{
			code = (ResultCode) info.GetInt32 ("code");
		}

		void ISerializable.GetObjectData (SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData (info, context);
			info.AddValue ("code", (int) code, typeof (int));
		}

		public ResultCode ResultCode {
			get { return code; }
		}

		static string GetMsg (ResultCode code)
		{
			switch (code) {
			case ResultCode.Ok:
				return "Success";
			case ResultCode.Denied:
				return "Access denied";
			case ResultCode.NoKeyringDaemon:
				return "The keyring daemon is not available";
			case ResultCode.AlreadyUnlocked:
				return "Keyring was already unlocked";
			case ResultCode.NoSuchKeyring:
				return "No such keyring";
			case ResultCode.BadArguments:
				return "Bad arguments";
			case ResultCode.IOError:
				return "I/O error";
			case ResultCode.Cancelled:
				return "Operation canceled";
			case ResultCode.AlreadyExists:
				return "Item already exists";
			default:
				return "Unknown error";
			}
		}
	}
}

