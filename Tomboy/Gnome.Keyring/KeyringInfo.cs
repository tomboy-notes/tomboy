//
// Gnome.Keyring.KeyringInfo.cs
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
namespace Gnome.Keyring {
	public class KeyringInfo {
		int lock_timeout;
		DateTime mtime;
		DateTime ctime;
		bool lock_on_idle;
		bool locked;
		string name;

		internal KeyringInfo (string name, bool lock_on_idle, int lock_timeout, DateTime mtime, DateTime ctime, bool locked)
		{
			this.name = name;
			this.lock_timeout = lock_timeout;
			this.mtime = mtime;
			this.ctime = ctime;
			this.lock_on_idle = lock_on_idle;
			this.locked = locked;
		}

		public KeyringInfo ()
		{
		}

		public KeyringInfo (bool lockOnIdle) : this (lockOnIdle, 0)
		{
		}

		public KeyringInfo (bool lockOnIdle, int lockTimeout)
		{
			this.lock_on_idle = lockOnIdle;
			LockTimeoutSeconds = lockTimeout;
		}

		public override string ToString ()
		{
			return String.Format ("Keyring name: {0}\n" +
						"Locked: {2}\n" +
						"LockOnIdle: {1}\n" +
						"Lock timeout: {3}\n" +
						"Creation time: {4}\n" +
						"Modification time: {5}",
						name, lock_on_idle, locked, lock_timeout, ctime, mtime);
		}

		public string Name {
			get { return name; }
		}

		public bool LockOnIdle {
			get { return lock_on_idle; }
			set { lock_on_idle = value; }
		}

		public int LockTimeoutSeconds {
			get { return lock_timeout; }
			set {
				if (value < 0)
					throw new ArgumentOutOfRangeException ("value");
				lock_timeout = value;
			}
		}

		public DateTime ModificationTime {
			get { return mtime; }
		}

		public DateTime CreationTime {
			get { return ctime; }
		}

		public bool Locked {
			get { return locked; }
		}
	}
}

