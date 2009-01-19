
//
//  Trie.cs: An efficient exact string set matcher, using Aho-Corasick.  Used to
//  identify substrings that match Tomboy note titles as the user types.
//
//  To test, compile with:
//     mcs -g -o testtrie.exe -define:TEST Trie.cs
//

using System;
using System.Collections.Generic;

namespace Tomboy
{
	public class TrieHit
	{
		public int    Start;
		public int    End;
		public string Key;
		public object Value;

		public TrieHit (int start, int end, string key, object value)
		{
			Start = start;
			End = end;
			Key = key;
			Value = value;
		}
	}

	public class TrieTree
	{
		class TrieState
		{
			public TrieState Next;
			public TrieState Fail;
			public TrieMatch FirstMatch;
			public int       Final;
			public object    Id;
		}

		class TrieMatch
		{
			public TrieMatch Next;
			public TrieState State;
			public char      Value;
		}

		TrieState root;
		List<TrieState> fail_states;
		bool      case_sensitive;
		int       max_length;

		public TrieTree (bool case_sensitive)
		{
			this.case_sensitive = case_sensitive;

			root = new TrieState ();
			fail_states = new List<TrieState> (8);
		}

		TrieState InsertMatchAtState (int depth, TrieState q, char c)
		{
			// Create a new state with a failure at %root
			TrieState new_q = new TrieState ();
			new_q.Fail = root;

			// Insert/Replace into fail_states at %depth
			if (depth < fail_states.Count) {
				new_q.Next = fail_states [depth];
				fail_states [depth] = new_q;
			} else {
				fail_states.Insert (depth, new_q);
			}

			// New match points to the newly created state for value %c
			TrieMatch m = new TrieMatch ();
			m.Next = q.FirstMatch;
			m.State = new_q;
			m.Value = c;

			// Insert the new match into existin state's match list
			q.FirstMatch = m;

			return new_q;
		}

		// Iterate the matches at state %s looking for the first match
		// containing %c.
		TrieMatch FindMatchAtState (TrieState s, char c)
		{
			TrieMatch m = s.FirstMatch;

			while (m != null && m.Value != c)
				m = m.Next;

			return m;
		}

		/*
		 * final = empty set
		 * FOR p = 1 TO #pat
		 *   q = root
		 *   FOR j = 1 TO m[p]
		 *     IF g(q, pat[p][j]) == null
		 *       insert(q, pat[p][j])
		 *     ENDIF
		 *     q = g(q, pat[p][j])
		 *   ENDFOR
		 *   final = union(final, q)
		 * ENDFOR
		 */
		public void AddKeyword (string needle, object pattern_id)
		{
			TrieState q = root;
			int depth = 0;

			// Step 1: add the pattern to the trie...

			for (int idx = 0; idx < needle.Length; idx++) {
				char c = needle [idx];
				if (!case_sensitive)
					c = Char.ToLower (c);

				TrieMatch m = FindMatchAtState (q, c);
				if (m == null)
					q = InsertMatchAtState (depth, q, c);
				else
					q = m.State;

				depth++;
			}

			q.Final = depth;
			q.Id = pattern_id;

			// Step 2: compute failure graph...

			for (int idx = 0; idx < fail_states.Count; idx++) {
				q = fail_states [idx];

				while (q != null) {
					TrieMatch m = q.FirstMatch;

					while (m != null) {
						TrieState q1 = m.State;
						TrieState r = q.Fail;
						TrieMatch n = null;

						while (r != null) {
							n = FindMatchAtState (r, m.Value);
							if (n == null)
								r = r.Fail;
							else
								break;
						}

						if (r != null && n != null) {
							q1.Fail = n.State;

							if (q1.Fail.Final > q1.Final)
								q1.Final = q1.Fail.Final;
						} else {
							n = FindMatchAtState (root, m.Value);
							if (n == null)
								q1.Fail = root;
							else
								q1.Fail = n.State;
						}

						m = m.Next;
					}

					q = q.Next;
				}
			}

			// Update max_length
			max_length = Math.Max (max_length, needle.Length);
		}

		/*
		 * Aho-Corasick
		 *
		 * q = root
		 * FOR i = 1 TO n
		 *   WHILE q != fail AND g(q, text[i]) == fail
		 *     q = h(q)
		 *   ENDWHILE
		 *   IF q == fail
		 *     q = root
		 *   ELSE
		 *     q = g(q, text[i])
		 *   ENDIF
		 *   IF isElement(q, final)
		 *     RETURN TRUE
		 *   ENDIF
		 * ENDFOR
		 * RETURN FALSE
		 */
		public IList<TrieHit> FindMatches (string haystack)
		{
			List<TrieHit> matches = new List<TrieHit> ();
			TrieState q = root;
			TrieMatch m = null;
			int idx = 0, start_idx = 0, last_idx = 0;

			while (idx < haystack.Length) {
				char c = haystack [idx++];
				if (!case_sensitive)
					c = Char.ToLower (c);

				while (q != null) {
					m = FindMatchAtState (q, c);
					if (m == null)
						q = q.Fail;
					else
						break;
				}

				if (q == root)
					start_idx = last_idx;

				if (q == null) {
					q = root;
					start_idx = idx;
				} else if (m != null) {
					q = m.State;

					// Got a match!
					if (q.Final != 0) {
						string key = haystack.Substring (start_idx,
						                                 idx - start_idx);
						TrieHit hit =
						        new TrieHit (start_idx, idx, key, q.Id);
						matches.Add (hit);
					}
				}

				last_idx = idx;
			}

			return matches;
		}

		public object Lookup (string key)
		{
			foreach (TrieHit hit in FindMatches (key)) {
				if (hit.Key.Length == key.Length)
					return hit.Value;
			}
			return null;
		}

		public int MaxLength
		{
			get {
				return max_length;
			}
		}
	}

	#if TEST
	public class Tester
	{
		public static void Main (string [] args)
		{
			string src = "bazar this is some foo, bar, and baz bazbarfoofoo bazbazarbaz end bazar";
			Logger.Log ("Searching in '{0}':", src);

			TrieTree trie = new TrieTree (false);
			trie.AddKeyword ("foo", "foo");
			trie.AddKeyword ("bar", "bar");
			trie.AddKeyword ("baz", "baz");
			trie.AddKeyword ("bazar", "bazar");

			Logger.Log ("Starting search...");
			foreach (TrieHit hit in trie.FindMatches (src)) {
				Logger.Log ("*** Match: '{0}' at {1}-{2}",
				            hit.Key,
				            hit.Start,
				            hit.End);
			}
			Logger.Log ("Search finished!");
		}
	}
	#endif
}
