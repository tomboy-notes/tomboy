//
// OAuthBase.cs
//  
// Author:
//       Bojan Rajkovic <bojanr@brandeis.edu>
//       Shannon Whitley <swhitley@whitleymedia.com>
//       Eran Sandler <http://eran.sandler.co.il/>
//       Sandy Armstrong <sanfordarmstrong@gmail.com>
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
using OAuth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace OAuth
{
	/// <summary>
	/// Provides a base class for OAuth authentication and signing.
	/// </summary>
	public class Base
	{
		class LoggerAdapter
		{
			public void LogDebug (string format, params object[] objects)
			{
				if (Debugging)
					Tomboy.Logger.Debug (format, objects);
			}
	
			public bool Debugging { get; set; }
		}

		private readonly LoggerAdapter log = new LoggerAdapter ();
		private bool debugging;

		public bool Debugging
		{
			get { return debugging; }
			set { debugging = value; log.Debugging = value; }
		}

		private const string OAuthVersion = "1.0";

		//
		// List of know and used oauth parameters' names
		//
		private const string OAuthConsumerKeyKey = "oauth_consumer_key";
		private const string OAuthCallbackKey = "oauth_callback";
		private const string OAuthVersionKey = "oauth_version";
		private const string OAuthSignatureMethodKey = "oauth_signature_method";
		private const string OAuthSignatureKey = "oauth_signature";
		private const string OAuthTimestampKey = "oauth_timestamp";
		private const string OAuthNonceKey = "oauth_nonce";
		private const string OAuthTokenKey = "oauth_token";
		private const string OAuthTokenSecretKey = "oauth_token_secret";
		private const string OAuthVerifierKey = "oauth_verifier";

		private const string HMACSHA1SignatureType = "HMAC-SHA1";
		private const string PlainTextSignatureType = "PLAINTEXT";
		private const string RSASHA1SignatureType = "RSA-SHA1";

		private Random random = new Random ();

		private string unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

		/// <summary>
		/// Helper function to compute a hash value.
		/// </summary>
		/// <param name="hashAlgorithm">
		/// 	The hashing algoirhtm used. If that algorithm needs some initialization, like HMAC and its derivatives,
		/// 	they should be initialized prior to passing it to this function.
		/// </param>
		/// <param name="data">The data to hash.</param>
		/// <returns>A Base64 string of the hash value.</returns>
		private string ComputeHash (HashAlgorithm hashAlgorithm, string data)
		{
			log.LogDebug ("Computing hash for data {0}", data);

			if (hashAlgorithm == null) throw new ArgumentNullException ("hashAlgorithm");
			if (string.IsNullOrEmpty (data)) throw new ArgumentNullException ("data");

			byte[] dataBuffer = System.Text.Encoding.ASCII.GetBytes (data);
			byte[] hashBytes = hashAlgorithm.ComputeHash (dataBuffer);

			return Convert.ToBase64String (hashBytes);
		}

		/// <summary>
		/// URL encodes a string using OAuth's encoding scheme (slightly different from HttpUtility's UrlEncode).
		/// </summary>
		/// <param name="value">The string to URL encode.</param>
		/// <returns>An URL encoded string.</returns>
		private string UrlEncode (string value)
		{
			log.LogDebug ("URL encoding value.");
			var result = new StringBuilder ();

			foreach (char symbol in value) {
				if (unreservedChars.IndexOf(symbol) != -1) result.Append(symbol);
				else result.Append('%' + String.Format("{0:X2}", (int)symbol));
			}

			return result.ToString();
		}

		/// <summary>
		/// Internal function to cut out all non oauth query string parameters.
		/// </summary>
		/// <param name="parameters">The query string part of the URL.</param>
		/// <returns>A list of QueryParameter each containing the parameter name and value.</returns>
		private IEnumerable<IQueryParameter<string>> GetQueryParameters (string parameters)
		{
			log.LogDebug ("Creating list of parameters from parameter string {0}", parameters);

			return CreateQueryParametersIterator (parameters);
		}

		private IEnumerable<IQueryParameter<string>> CreateQueryParametersIterator (string parameters)
		{
			if (parameters == null) throw new ArgumentNullException ("parameters");
			var parameterDictionary = HttpUtility.ParseQueryString (parameters).ToDictionary ();

			foreach (var kvp in parameterDictionary)
				yield return new QueryParameter<string> (kvp.Key, kvp.Value, s => string.IsNullOrEmpty (s));
		}

		/// <summary>
		/// Generate the signature base that is used to produce the signature
		/// </summary>
		/// <param name="url">The full URL that needs to be signed including its non OAuth URL parameters.</param>
		/// <param name="consumerKey">The consumer key.</param>
		/// <param name="token">The token, if available. If not available pass null or an empty string.</param>
		/// <param name="tokenSecret">The token secret, if available. If not available pass null or an empty string.</param>
		/// <param name="verifier">The callback verifier, if available. If not available pass null or an empty string.</param>
		/// <param name="httpMethod">The HTTP method used. Must be a valid HTTP method verb (POST,GET,PUT, etc)</param>
		/// <param name="signatureType">The signature type. To use the default values use <see cref="SignatureType">SignatureType</see>.</param>
		/// <returns>The signature base.</returns>
		private string GenerateSignatureBase (Uri url, string consumerKey, string token, string tokenSecret, string verifier,
			RequestMethod method, TimeSpan timeStamp, string nonce, SignatureType signatureType, out string normalizedUrl,
			out List<IQueryParameter<string>> parameters)
		{
			log.LogDebug ("Generating signature base for OAuth request.");

			token = token ?? string.Empty;
			tokenSecret = tokenSecret ?? string.Empty;
			verifier = verifier ?? String.Empty;

			if (consumerKey == null) throw new ArgumentNullException ("consumerKey");

			log.LogDebug ("URL: {0}", url.Query);

			var signatureString = string.Empty;

			switch (signatureType) {
				case SignatureType.HMACSHA1:
					signatureString = "HMAC-SHA1";
					break;
				case SignatureType.RSASHA1:
					signatureString = "RSA-SHA1";
					break;
				case SignatureType.PLAINTEXT:
					signatureString = SignatureType.PLAINTEXT.ToString ();
					break;
			}

			parameters = GetQueryParameters (url.Query).Concat (new List<IQueryParameter<string>> {
				new QueryParameter<string> (OAuthVersionKey, OAuthVersion, s => string.IsNullOrEmpty (s)),
				new QueryParameter<string> (OAuthTimestampKey, ((long)timeStamp.TotalSeconds).ToString (), s => string.IsNullOrEmpty (s)),
				new QueryParameter<string> (OAuthSignatureMethodKey, signatureString, s => string.IsNullOrEmpty (s)),
				new QueryParameter<string> (OAuthNonceKey, nonce, s => string.IsNullOrEmpty (s)),
				new QueryParameter<string> (OAuthConsumerKeyKey, consumerKey, s => string.IsNullOrEmpty (s))
			}).ToList ();

			if (!string.IsNullOrEmpty (token)) parameters.Add (new QueryParameter<string> (OAuthTokenKey, token, s => string.IsNullOrEmpty (s)));
			if (!string.IsNullOrEmpty (verifier)) parameters.Add (new QueryParameter<string> (OAuthVerifierKey, verifier, s => string.IsNullOrEmpty (s)));

			log.LogDebug ("Normalizing URL for signature.");

			normalizedUrl = string.Format ("{0}://{1}", url.Scheme, url.Host);
			if (!((url.Scheme == "http" && url.Port == 80) || (url.Scheme == "https" && url.Port == 443))) normalizedUrl += ":" + url.Port;
			normalizedUrl += url.AbsolutePath;

			log.LogDebug ("Generated normalized URL: {0}", normalizedUrl);
			log.LogDebug ("Normalizing request parameters.");

			parameters.Sort ();
			string normalizedRequestParameters = parameters.NormalizeRequestParameters ();

			log.LogDebug ("Normalized request parameters {0}.", normalizedRequestParameters);
			log.LogDebug ("Generating signature base from normalized URL and request parameters.");

			var signatureBase = new StringBuilder ();
			signatureBase.AppendFormat("{0}&", method.ToString ());
			signatureBase.AppendFormat("{0}&", UrlEncode (normalizedUrl));
			signatureBase.AppendFormat("{0}", UrlEncode (normalizedRequestParameters));

			log.LogDebug ("Signature base: {0}", signatureBase.ToString ());

			return signatureBase.ToString ();
		}

		/// <summary>
		/// Generate the signature value based on the given signature base and hash algorithm.
		/// </summary>
		/// <param name="signatureBase">
		/// 	The signature based as produced by the GenerateSignatureBase method or by any other means.
		/// </param>
		/// <param name="hash">
		/// 	The hash algorithm used to perform the hashing. If the hashing algorithm requires
		/// 	initialization or a key it should be set prior to calling this method.
		/// </param>
		/// <returns>A Base64 string of the hash value.</returns>
		private string GenerateSignatureUsingHash (string signatureBase, HashAlgorithm hash)
		{
			log.LogDebug ("Generating hashed signature.");
			return ComputeHash (hash, signatureBase);
		}

		/// <summary>
		/// Generates a signature using the HMAC-SHA1 algorithm
		/// </summary>
		/// <param name="url">The full URL that needs to be signed including its non-OAuth URL parameters.</param>
		/// <param name="consumerKey">The consumer key.</param>
		/// <param name="consumerSecret">The consumer seceret.</param>
		/// <param name="token">The token, if available. If not available pass null or an empty string.</param>
		/// <param name="tokenSecret">The token secret, if available. If not, pass null or an empty string.</param>
		/// <param name="verifier">The callback verifier, if available. If not, pass null or an empty string.</param>
		/// <param name="httpMethod">The HTTP method used. Must be valid HTTP method verb (POST, GET, PUT, etc).</param>
		/// <returns>A Base64 string of the hash value.</returns>
		protected string GenerateSignature (Uri url, string consumerKey, string consumerSecret, string token,
			string tokenSecret, string verifier, RequestMethod method, TimeSpan timeStamp, string nonce, out string normalizedUrl,
			out List<IQueryParameter<string>> parameters)
		{
			log.LogDebug ("Generating signature using HMAC-SHA1 algorithm.");
			return GenerateSignature (url, consumerKey, consumerSecret, token, tokenSecret, verifier, method, timeStamp, nonce,
				SignatureType.HMACSHA1, out normalizedUrl, out parameters);
		}

		/// <summary>
		/// Generates a signature using the specified signature type.
		/// </summary>
		/// <param name="url">The full URL that needs to be signed including its non-OAuth URL parameters.</param>
		/// <param name="consumerKey">The consumer key.</param>
		/// <param name="consumerSecret">The consumer seceret.</param>
		/// <param name="token">The token, if available. If not available pass null or an empty string.</param>
		/// <param name="tokenSecret">The token secret, if available. If not, pass null or an empty string.</param>
		/// <param name="verifier">The callback verifier, if available. If not, pass null or an empty string.</param>
		/// <param name="httpMethod">The HTTP method used. Must be a valid HTTP method verb (POST,GET,PUT, etc).</param>
		/// <param name="signatureType">The type of signature to use.</param>
		/// <returns>A Base64 string of the hash value.</returns>
		private string GenerateSignature (Uri url, string consumerKey, string consumerSecret, string token,
			string tokenSecret, string verifier, RequestMethod method, TimeSpan timeStamp, string nonce, SignatureType signatureType,
			out string normalizedUrl, out List<IQueryParameter<string>> parameters)
		{
			log.LogDebug ("Generating signature using signature type {0}", signatureType);

			normalizedUrl = null;
			parameters = null;

			switch (signatureType)
			{
				case SignatureType.PLAINTEXT:
					var signature = UrlEncode (string.Format ("{0}&{1}", consumerSecret, tokenSecret));
					log.LogDebug ("Plaintext encoding signature {0} of consumer secret and token secret.", signature);
					return signature;
				case SignatureType.HMACSHA1:
					string signatureBase = GenerateSignatureBase (url, consumerKey, token, tokenSecret, verifier, method,
						timeStamp, nonce, SignatureType.HMACSHA1, out normalizedUrl, out parameters);

					var hmacsha1 = new HMACSHA1 ();
					hmacsha1.Key = Encoding.ASCII.GetBytes (string.Format ("{0}&{1}",
						UrlEncode (consumerSecret),
						string.IsNullOrEmpty (tokenSecret) ? "" : UrlEncode(tokenSecret)));

					var hashedSignature = GenerateSignatureUsingHash (signatureBase, hmacsha1);

					log.LogDebug ("HMAC-SHA1 encoded signature {0} of consumer secret and token secret.", hashedSignature);
					return hashedSignature;
				case SignatureType.RSASHA1:
					throw new NotImplementedException ();
				default:
					throw new ArgumentException ("Unknown signature type", "signatureType");
			}
		}

		/// <summary>
		/// Generate the timestamp for the signature.
		/// </summary>
		/// <returns>A string timestamp.</returns>
		protected TimeSpan GenerateTimeStamp ()
		{
			log.LogDebug ("Generating time stamp.");
			// Default implementation of UNIX time of the current UTC time
			return DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
		}

		/// <summary>
		/// Generate a nonce.
		/// </summary>
		/// <returns>A random nonce string.</returns>
		protected virtual string GenerateNonce()
		{
			log.LogDebug ("Generating nonce.");
			// Just a simple implementation of a random number between 123400 and 9999999
			return random.Next (123400, 9999999).ToString ();
		}
	}
}