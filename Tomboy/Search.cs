
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Tomboy
{
	public class Search
	{
		private NoteManager manager;

		public Search (NoteManager manager)
		{
			this.manager = manager;
		}

		public IDictionary<Note,int> SearchNotes (string query, bool case_sensitive)
		{
			string text = query;
			string [] words = text.Split (' ', '\t', '\n');

			// Used for matching in the raw note XML
                        string [] encoded_words = XmlEncoder.Encode (text).Split (' ', '\t', '\n');
			Dictionary<Note,int> temp_matches = new Dictionary<Note,int>();

			foreach (Note note in manager.Notes) {
				// Check the note's raw XML for at least one
				// match, to avoid deserializing Buffers
				// unnecessarily.
				if (CheckNoteHasMatch (note,
					               encoded_words,
					               case_sensitive)){
					int match_count =
						FindMatchCountInNote (note.TextContent,
						                      words,
						                      case_sensitive);
					if (match_count > 0)
						// TODO: Improve note.GetHashCode()
						temp_matches.Add(note,match_count);
				}
			}
			return temp_matches;
		}

	
		bool CheckNoteHasMatch (Note note, string [] encoded_words, bool match_case)
		{
			string note_text = note.XmlContent;
			if (!match_case)
				note_text = note_text.ToLower ();

			foreach (string word in encoded_words) {
				if (note_text.IndexOf (word) > -1)
					continue;
				else
					return false;
			}

			return true;
		}

		int FindMatchCountInNote (string note_text, string [] words, bool match_case)
		{
			int matches = 0;

			if (!match_case)
				note_text = note_text.ToLower ();

			foreach (string word in words) {
				int idx = 0;
				bool this_word_found = false;

				if (word == String.Empty)
					continue;

				while (true) {
					idx = note_text.IndexOf (word, idx);

					if (idx == -1) {
						if (this_word_found)
							break;
						else
							return 0;
					}

					this_word_found = true;

					matches++;

					idx += word.Length;
				}
			}

			return matches;
		}
	}
}
