
using System;
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
		
		/// <summary>
		/// Search the notes! A match number of
		/// <see cref="int.MaxValue"/> indicates that the note
		/// title contains the search term.
		/// </summary>
		/// <param name="query">
		/// A <see cref="System.String"/>
		/// </param>
		/// <param name="case_sensitive">
		/// A <see cref="System.Boolean"/>
		/// </param>
		/// <param name="selected_notebook">
		/// A <see cref="Notebooks.Notebook"/>.  If this is not
		/// null, only the notes of the specified notebook will
		/// be searched.
		/// </param>
		/// <returns>
		/// A <see cref="IDictionary`2"/> with the relevant Notes
		/// and a match number. If the search term is in the title,
		/// number will be <see cref="int.MaxValue"/>.
		/// </returns>
		public IDictionary<Note,int> SearchNotes (
				string query,
				bool case_sensitive,
				Notebooks.Notebook selected_notebook)
		{
			string [] words = Search.SplitWatchingQuotes (query);

			// Used for matching in the raw note XML
			string [] encoded_words = SplitWatchingQuotes (XmlEncoder.Encode (query));
			Dictionary<Note,int> temp_matches = new Dictionary<Note,int>();
			
			// Skip over notes that are template notes
			Tag template_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);

			foreach (Note note in manager.Notes) {
				// Skip template notes
				if (note.ContainsTag (template_tag))
					continue;
				
				// Skip notes that are not in the
				// selected notebook
				if (selected_notebook != null
						&& selected_notebook.ContainsNote (note) == false)
					continue;
				
				// First check the note's title for a match,
				// if there is no match check the note's raw
				// XML for at least one match, to avoid
				// deserializing Buffers unnecessarily.

				if (0 < FindMatchCountInNote (note.Title,
						                      words,
						                      case_sensitive))
					temp_matches.Add(note,int.MaxValue);
				else if (CheckNoteHasMatch (note,
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
		
		static public string [] SplitWatchingQuotes (string text)
		{
			string [] phrases = text.Split ('\"');

			//Make it possible to search for "whole phrases"
			List<string> wordsList = new List<string> (phrases);
			int count = wordsList.Count;
			for (int i = 0; i < count; i += 1) {
				string part = wordsList[i];
				foreach (string s in part.Split (' ', '\t', '\n'))
					if (s.Length > 0)
						wordsList.Add (s);
				wordsList.RemoveAt (i);
				count--;
			}

			return wordsList.ToArray ();
		}

		bool CheckNoteHasMatch (Note note, string [] encoded_words, bool match_case)
		{
			string note_text = note.XmlContent;
			if (!match_case)
				note_text = note_text.ToLower ();

			foreach (string word in encoded_words) {
				if (note_text.Contains (word) )
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
