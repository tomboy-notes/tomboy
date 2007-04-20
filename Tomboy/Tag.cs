
using System;
using System.Collections.Generic;

namespace Tomboy
{
	public class Tag
	{
		string name;
		string normalized_name;
		
		// <summary>
		// Used to track which notes are currently tagged by this tag.  The
		// dictionary key is the Note.Uri.
		// </summary>
		Dictionary<string, Note> notes;
		
		#region Constructors
		public Tag(string tag_name)
		{
			Name = tag_name;
			notes = new Dictionary<string,Note> ();
		}
		#endregion
		
		#region Public Methods
		// <summary>
		// Associates the specified note with this tag.
		// </summary>
		public void AddNote (Note note)
		{
			if (!notes.ContainsKey (note.Uri)) {
				notes [note.Uri] = note;
			}
		}
		
		// <summary>
		// Unassociates the specified note with this tag.
		// </summary>
		public void RemoveNote (Note note)
		{
			if (notes.ContainsKey (note.Uri)) {
				notes.Remove (note.Uri);
			}
		}
		#endregion
		
		#region Properties
		// <summary>
		// The name of the tag.  This is what the user types in as the tag and
		// what's used to show the tag to the user.
		// </summary>
		public string Name
		{
			get { return name; }
			set {
				if (value != null) {
					string trimmed_name = (value as string).Trim ();
					if (trimmed_name != String.Empty) {
						name = trimmed_name;
						normalized_name = trimmed_name.ToLower ();
					}
				}
			}
		}
		
		// <summary>
		// Use the string returned here to reference the tag in Dictionaries.
		// </summary>
		public string NormalizedName
		{
			get { return normalized_name; }
		}
		
		// <summary>
		// Returns a list of all the notes that this tag is associated with. 
		// </summary>
		public List<Note> Notes
		{
			get {
				return new List<Note> (notes.Values);
			}
		}
		
		// <summary>
		// Returns the number of notes this is currently tagging.
		// </summary>
		public int Popularity
		{
			get { return notes.Count; }
		}
		#endregion
	}
}
