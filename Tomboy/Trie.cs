
//
//  Trie.cs: An efficient exact string set matcher, using Aho-Corasick.  Used to
//  identify substrings that match Tomboy note titles as the user types.
//
//  To test, compile with:
//     mcs -g -o testtrie.exe -define:TEST Trie.cs 
//

using System;
using System.Collections;

namespace Tomboy
{
	public delegate void MatchHandler (string haystack, 
					   int    start,
					   object match);

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
		ArrayList fail_states;
		bool      case_sensitive;

		public TrieTree (bool case_sensitive)
		{
			this.case_sensitive = case_sensitive;

			root = new TrieState ();
			fail_states = new ArrayList (8);
		}

		TrieState InsertMatchAtState (int depth, TrieState q, char c)
		{
			// Create a new state with a failure at %root
			TrieState new_q = new TrieState ();
			new_q.Fail = root;

			// Insert/Replace into fail_states at %depth
			if (depth < fail_states.Count) {
				new_q.Next = (TrieState) fail_states [depth];
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
				q = (TrieState) fail_states [idx]; 

				while (q != null) {
					TrieMatch m = q.FirstMatch;

					while (m != null) {
						TrieState q1 = m.State;
						TrieState r = q.Fail;
						TrieMatch n;

						while (r != null) {
							n = FindMatchAtState (r, m.Value);
							if (n == null) 
								r = r.Fail;
							else
								break;
						}

						if (r != null) {
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
		public void FindMatches (string       haystack,
					 MatchHandler match_handler)
		{
			TrieState q = root;
			TrieMatch m;
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
						match_handler (haystack, start_idx, q.Id);
					}
				}

				last_idx = idx;
			}
		}
	}

#if TEST
	public class Tester
	{
		public static void Main (string [] args)
		{
			string src = "bazar this is some foo, bar, and baz bazbarfoofoo bazbazarbaz end bazar";
			Console.WriteLine ("Searching in '{0}':", src);

			TrieTree trie = new TrieTree (false);
			trie.AddKeyword ("foo", "foo".Length);
			trie.AddKeyword ("bar", "bar".Length);
			trie.AddKeyword ("baz", "baz".Length);
			trie.AddKeyword ("bazar", "bazar".Length);

			Console.WriteLine ("Starting search...");
			trie.FindMatches (src, new MatchHandler (MatchFound));
			Console.WriteLine ("Search finished!");
		}

		static void MatchFound (string haystack, int start, object id)
		{
			Console.WriteLine ("*** Match: start={0}, id={1}",
					   start,
					   haystack.Substring (start, (int) id));
		}
	}
#endif
}
