
//
//  Trie.cs: An efficient exact string set matcher.  Used to identify substrings
//  that match Tomboy note titles as the user types.
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
					   int    end);

	public class TrieTree 
	{
		class TrieNode 
		{
			public char     Value;
			public TrieNode Next;
			public TrieNode Fail;

			public TrieNode (char     value, 
					 TrieNode next, 
					 TrieNode fail)
			{
				Value = value;
				Next = next;
				Fail = fail;
			}
		}

		class StringLengthComparer : IComparer
		{
			// Sort strings according to length.  Shortest first...
			public int Compare (object one, object two)
			{
				string s_one = (string) one;
				string s_two = (string) two;

				return s_one.Length.CompareTo (s_two.Length);
			}
		}

		TrieNode root;
		bool case_sensitive;

		static TrieNode SuccessTerminal;

		static TrieTree ()
		{
			SuccessTerminal = new TrieNode ('\0', null, null);
		}

		TrieTree (bool is_case_sensitive)
		{
			root = new TrieNode ('\0', null, null);
			case_sensitive = is_case_sensitive;
		}

		public TrieTree (ICollection keywords, bool is_case_sensitive)
			: this (is_case_sensitive)
		{
			// Sort strings by length to ensure matches are greedy...
			ArrayList sorted_keywords = new ArrayList (keywords);
			sorted_keywords.Sort (new StringLengthComparer ());

			foreach (string word in sorted_keywords) {
				AddKeyword (word);
			}
		}

		[System.Diagnostics.Conditional ("TEST")]
		static void Trace (string format, params object [] args)
		{
			Console.WriteLine (format, args);
		}

		char CaseConvert (char c)
		{
			if (!case_sensitive)
				return Char.ToLower (c);
			else
				return c;
		}

		void AddKeyword (string needle)
		{
			Trace ("Adding keyword '{0}'", needle);

			int idx = 0;
			TrieNode iter = root.Next;
			TrieNode last_match = root;

			while (iter != null && idx < needle.Length) {
				if (CaseConvert (needle [idx]) == iter.Value) {
					last_match = iter;
					iter = iter.Next;
					idx++;
				} else {
					iter = iter.Fail;
				}
			}

			TrieNode new_next = SuccessTerminal;

			for (int i = needle.Length; i != idx; i--) {
				Trace ("Appending '{0}' node", CaseConvert (needle [i - 1]));

				new_next = new TrieNode (CaseConvert (needle [i - 1]), 
							 new_next,
							 last_match.Next);
			}

			last_match.Next = new_next;
		}

		public void FindMatches (string       haystack,
					 MatchHandler match_handler)
		{
			int outer_idx = 0;

			while (outer_idx < haystack.Length) {
				int idx = outer_idx;
				TrieNode iter = root.Next;

				while (iter != null && 
				       iter != SuccessTerminal && 
				       idx < haystack.Length) {
					if (CaseConvert (haystack [idx]) == iter.Value) {
						Trace ("Got match for '{0}' == '{1}', moving to next ('{2}')",
						       CaseConvert (haystack [idx]), 
						       iter.Value,
						       (iter.Next == null) ? null : iter.Next.Value);

						iter = iter.Next;
						idx++;
					} else {
						Trace ("Got fail  for '{0}' != '{1}', moving to fail ('{2}')",
						       CaseConvert (haystack [idx]), 
						       iter.Value,
						       (iter.Fail == null) ? null : iter.Fail.Value);

						iter = iter.Fail;
					}
				}

				if (iter == SuccessTerminal) {
					// Success!  Call match handler with substring of haystack..
					match_handler (haystack, outer_idx, idx);
					// Skip text we've matched..
					outer_idx = idx;
				} else {
					// Failed!  Move on to next haystack character..
					outer_idx++;
				}
			}
		}
	}

#if TEST
	public class Tester
	{
		public static void Main (string [] args)
		{
			ArrayList keywords = new ArrayList ();
			keywords.Add ("foo");
			keywords.Add ("bar");
			keywords.Add ("baz");
			keywords.Add ("bazar");

			string src = "bazar this is some foo, bar, and baz bazbarfoofoo bazbazarbaz end";
			Console.WriteLine ("Searching in '{0}':", src);

			TrieTree trie = new TrieTree (keywords, false);

			Console.WriteLine ("Starting search...");
			trie.FindMatches (src, new MatchHandler (MatchFound));
			Console.WriteLine ("Search finished!");
		}

		static void MatchFound (string haystack, int start, int end)
		{
			Console.WriteLine ("*** Match: start={0}, end={1}, value='{2}'",
					   start,
					   end,
					   haystack.Substring (start, end - start));
		}
	}
#endif
}
