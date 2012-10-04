
using System;
using System.Net;
using LibProxy;

namespace Tomboy.WebSync
{
	public static class ProxiedWebRequest
	{
		private static ProxyFactory proxyFactory = null;
		private const string useProxyAuthentication =
			"/system/http_proxy/use_authentication";
		private const string proxyAuthenticationUser =
			"/system/http_proxy/authentication_user";
		private const string proxyAuthenticationPassword =
			"/system/http_proxy/authentication_password";

		public static bool useLibProxy = true;
		public static HttpWebRequest Create (string uri)
		{
			HttpWebRequest webRequest = WebRequest.Create (uri) as HttpWebRequest;
			if (useLibProxy) {
				try {
					ApplyProxy (webRequest, uri);
				} catch (System.DllNotFoundException) {
					Logger.Warn ("libproxy not installed");
					useLibProxy = false;
				}
			}
			
			return webRequest;
		}
		
		private static void ApplyProxy (HttpWebRequest webRequest, string uri)
		{
			if (proxyFactory == null) {
				proxyFactory = new LibProxy.ProxyFactory ();
			}

			string[] proxies = proxyFactory.GetProxies (uri);
			foreach (string proxy in proxies) {
				Uri proxyUri = new Uri (proxy);
				string scheme = proxyUri.Scheme;
				if (scheme == "direct") {
					break;
				} else if (scheme == "http" || scheme == "https") {
					WebProxy webProxy = new WebProxy ();
					
					if (UseAuthentication ()) {
						ICredentials credentials = 
							new NetworkCredential (GetAuthUser (), GetAuthPass ());
						webProxy.Credentials = credentials;
					}
					
					webProxy.Address = proxyUri;
					webRequest.Proxy = webProxy;
					break;
				}
			}
		}
		
		// this settings are taken from GConf/xml until libproxy supports
		// returning the user/password to use for the proxy
		// TODO: fix when libproxy release 0.5 is out
		public static bool UseAuthentication ()
		{
			object useProxyAuth = Preferences.Get (useProxyAuthentication);
			
			if (useProxyAuth == null) {
				return false;
			}
			return (bool) useProxyAuth;
		}

		public static string GetAuthUser ()
		{
			return Preferences.Get (proxyAuthenticationUser) as string;
		}
		
		public static string GetAuthPass ()
		{
			return Preferences.Get (proxyAuthenticationPassword) as string;
		}
	}
}
