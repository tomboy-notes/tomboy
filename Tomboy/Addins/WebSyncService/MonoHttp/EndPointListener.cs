#define EMBEDDED_IN_1_0

//
// System.Net.EndPointListener
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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

#if !NET_2_0

using System.IO;
using System.Net.Sockets;
using System.Collections;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
//using Mono.Security.Authenticode;
using System;
using System.Net;
using System.Web;

namespace MonoHttp {
	
	sealed class EndPointListener : IHttpListenerContextBinder
	{
		IPEndPoint endpoint;
		Socket sock;
		Hashtable prefixes;  // Dictionary <ListenerPrefix, HttpListener>
		ArrayList unhandled; // List<ListenerPrefix> unhandled; host = '*'
		ArrayList all;       // List<ListenerPrefix> all;  host = '+'
		
		AsymmetricAlgorithm key;
		bool secure;

		public EndPointListener (IPAddress addr, int port, bool secure)
		{
#if !EMBEDDED_IN_1_0
			if (secure) {
				this.secure = secure;
				LoadCertificateAndKey (addr, port);
			}
#endif

			endpoint = new IPEndPoint (addr, port);
			sock = new Socket (addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			sock.Bind (endpoint);
			sock.Listen (500);
			sock.BeginAccept (OnAccept, this);
			prefixes = new Hashtable ();
		}
#if !EMBEDDED_IN_1_0
		void LoadCertificateAndKey (IPAddress addr, int port)
		{
			// Actually load the certificate
			try {
				string dirname = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
				string path = Path.Combine (dirname, ".mono");
				path = Path.Combine (path, "httplistener");
				string cert_file = Path.Combine (path, String.Format ("{0}.cer", port));
				string pvk_file = Path.Combine (path, String.Format ("{0}.pvk", port));
				cert = new X509Certificate2 (cert_file);
				key = PrivateKey.CreateFromFile (pvk_file).RSA;
			} catch {
				// ignore errors
			}
		}
#endif
		static void OnAccept (IAsyncResult ares)
		{
			EndPointListener epl = (EndPointListener) ares.AsyncState;
			Socket accepted = null;
			try {
				accepted = epl.sock.EndAccept (ares);
			} catch {
				// Anything to do here?
			} finally {
				try {
					epl.sock.BeginAccept (OnAccept, epl);
				} catch {
					if (accepted != null) {
						try {
							accepted.Close ();
						} catch {}
						accepted = null;
					}
				} 
			}

			if (accepted == null)
				return;

			if (epl.secure && (epl.key == null)) {
				accepted.Close ();
				return;
			}
			HttpConnection conn = new HttpConnection (accepted, epl);
			conn.BeginReadRequest ();
		}

		public bool BindContext (HttpListenerContext context)
		{
			HttpListenerRequest req = context.Request;
			ListenerPrefix prefix;
			HttpListener listener = SearchListener (req.UserHostName, req.RawUrl, out prefix);
			if (listener == null)
				return false;

			context.Listener = listener;
			context.Connection.Prefix = prefix;
			listener.RegisterContext (context);
			return true;
		}

		public void UnbindContext (HttpListenerContext context)
		{
			if (context == null || context.Request == null)
				return;

			HttpListenerRequest req = context.Request;
			ListenerPrefix prefix;
			HttpListener listener = SearchListener (req.UserHostName, req.RawUrl, out prefix);
			if (listener != null)
				listener.UnregisterContext (context);
		}

		HttpListener SearchListener (string host, string raw_url, out ListenerPrefix prefix)
		{
			prefix = null;
			if (raw_url == null)
				return null;

			//TODO: We should use a ReaderWriterLock between this and the add/remove operations.
			if (host != null) {
				int colon = host.IndexOf (':');
				if (colon >= 0)
					host = host.Substring (0, colon);
			}

			string path;
			Uri raw_uri;
			#if NET_2_0
if (MonoHttp.Utility.MaybeUri (raw_url) && Uri.TryCreate (raw_url, UriKind.Absolute, out raw_uri))
#else
try { raw_uri = new Uri (raw_url); } catch { raw_uri = null; } if (raw_uri != null)
#endif

				path = raw_uri.PathAndQuery;
			else
				path = HttpUtility.UrlDecode (raw_url);
			
			string path_slash = path [path.Length - 1] == '/' ? path : path + "/";
			
			HttpListener best_match = null;
			int best_length = -1;

			lock (prefixes) {
				if (host != null && host != "") {
					foreach (ListenerPrefix p in prefixes.Keys) {
						string ppath = p.Path;
						if (ppath.Length < best_length)
							continue;

						if (p.Host == host && (path.StartsWith (ppath) || path_slash.StartsWith (ppath))) {
							best_length = ppath.Length;
							best_match = (HttpListener) prefixes [p];
							prefix = p;
						}
					}
					if (best_length != -1)
						return best_match;
				}

				best_match = MatchFromList (host, path, unhandled, out prefix);
				if (best_match != null)
					return best_match;

				best_match = MatchFromList (host, path, all, out prefix);
				if (best_match != null)
					return best_match;
			}
			return null;
		}

		HttpListener MatchFromList (string host, string path, ArrayList list, out ListenerPrefix prefix)
		{
			prefix = null;
			if (list == null)
				return null;

			HttpListener best_match = null;
			int best_length = -1;
			
			foreach (ListenerPrefix p in list) {
				string ppath = p.Path;
				if (ppath.Length < best_length)
					continue;

				if (path.StartsWith (ppath)) {
					best_length = ppath.Length;
					best_match = p.Listener;
					prefix = p;
				}
			}

			return best_match;
		}

		void AddSpecial (ArrayList coll, ListenerPrefix prefix)
		{
			if (coll == null)
				return;

			foreach (ListenerPrefix p in coll) {
				if (p.Path == prefix.Path) //TODO: code
					throw new HttpListenerException (400, "Prefix already in use.");
			}

			coll.Add (prefix);
		}

		void RemoveSpecial (ArrayList coll, ListenerPrefix prefix)
		{
			if (coll == null)
				return;

			int c = coll.Count;
			for (int i = 0; i < c; i++) {
				ListenerPrefix p = (ListenerPrefix) coll [i];
				if (p.Path == prefix.Path) {
					coll.RemoveAt (i);
					CheckIfRemove ();
					return;
				}
			}
		}

		void CheckIfRemove ()
		{
			if (prefixes.Count > 0)
				return;

			if (unhandled != null && unhandled.Count > 0)
				return;

			if (all != null && all.Count > 0)
				return;

			EndPointManager.RemoveEndPoint (this, endpoint);
		}

		public void Close ()
		{
			sock.Close ();
		}

		public void AddPrefix (ListenerPrefix prefix, HttpListener listener)
		{
			lock (prefixes) {
				if (prefix.Host == "*") {
					if (unhandled == null)
						unhandled = new ArrayList ();

					prefix.Listener = listener;
					AddSpecial (unhandled, prefix);
					return;
				}

				if (prefix.Host == "+") {
					if (all == null)
						all = new ArrayList ();
					prefix.Listener = listener;
					AddSpecial (all, prefix);
					return;
				}

				if (prefixes.ContainsKey (prefix)) {
					HttpListener other = (HttpListener) prefixes [prefix];
					if (other != listener) // TODO: code.
						throw new HttpListenerException (400, "There's another listener for " + prefix);
					return;
				}

				prefixes [prefix] = listener;
			}
		}

		public void RemovePrefix (ListenerPrefix prefix, HttpListener listener)
		{
			lock (prefixes) {
				if (prefix.Host == "*") {
					RemoveSpecial (unhandled, prefix);
					return;
				}

				if (prefix.Host == "+") {
					RemoveSpecial (all, prefix);
					return;
				}

				if (prefixes.ContainsKey (prefix)) {
					prefixes.Remove (prefix);
				}
			}
		}
	}
}
#endif

