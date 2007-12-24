using System;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	/// <summary>
	/// An object that represents a notebook in Tomboy
	/// </summary>
	public class Notebook
	{
		public static string NotebookTagPrefix = "notebook:";
		
		#region Fields
		string name;
		string normalizedName;
		string templateNoteTitle;
		Tag tag;
		#endregion // Fields
		
		#region Constructors
		/// <summary>
		/// Construct a new Notebook with a given name
		/// </summary>
		/// <param name="name">
		/// A <see cref="System.String"/>.  This is the name that will be used
		/// to identify the notebook.
		/// </param>
		public Notebook (string name)
		{
			Name = name;
			tag = TagManager.GetOrCreateSystemTag (NotebookTagPrefix + name);
		}
		
		/// <summary>
		/// Construct a new Notebook with the specified notebook system tag.
		/// </summary>
		/// <param name="notebookTag">
		/// A <see cref="Tag"/>.  This must be a system notebook tag.
		/// </param>
		public Notebook (Tag notebookTag)
		{
			// Parse the notebook name from the tag name
			string systemNotebookPrefix = Tag.SYSTEM_TAG_PREFIX + NotebookTagPrefix;
			string notebookName = notebookTag.Name.Substring (systemNotebookPrefix.Length);
			Name = notebookName;
			tag = notebookTag;
		}
		
		/// <summary>
		/// Default constructor not used
		/// </summary>
		protected Notebook ()
		{
		}
		
		#endregion // Constructors
		
		#region Properties
		public virtual string Name
		{
			get {
				return name;
			}
			set {
				if (value != null) {
					string trimmedName = (value as string).Trim ();
					if (trimmedName != String.Empty) {
						name = trimmedName;
						normalizedName = trimmedName.ToLower ();

						// The templateNoteTite should show the name of the
						// notebook.  For example, if the name of the notebooks
						// "Meetings", the templateNoteTitle should be "Meetings
						// Notebook Template".  Translators should place the
						// name of the notebook accordingly using "{0}".
						// TODO: Figure out how to make this note for
						// translators appear properly.
						string format = Catalog.GetString ("{0} Notebook Template");
						templateNoteTitle = string.Format (format, Name);
					}
				}
			}
		}
		
		public virtual string NormalizedName
		{
			get {
				return normalizedName;
			}
		}
		
		public virtual Tag Tag
		{
			get {
				return tag;
			}
		}
		#endregion // Properties
		
		#region Public Methods
		/// <summary>
		/// Return the template Tomboy Note that corresponds with
		/// this Notebook.
		/// </summary>
		/// <returns>
		/// A <see cref="Note"/>
		/// </returns>
		public virtual Note GetTemplateNote ()
		{
			NoteManager noteManager = Tomboy.DefaultNoteManager;
			Note note = noteManager.Find (templateNoteTitle);
			if (note == null) {
				note =
					noteManager.Create (templateNoteTitle,
							NoteManager.GetNoteTemplateContent (templateNoteTitle));
					
				// Select the initial text
				NoteBuffer buffer = note.Buffer;
				Gtk.TextIter iter = buffer.GetIterAtLineOffset (2, 0);
				buffer.MoveMark (buffer.SelectionBound, iter);
				buffer.MoveMark (buffer.InsertMark, buffer.EndIter);

				// Add on the notebook system tag so Tomboy
				// will persist the tag/notebook across sessions
				// if no other notes are added to the notebook.
				Tag tag = TagManager.GetOrCreateSystemTag (NotebookTagPrefix + Name);
				note.AddTag (tag);

				note.QueueSave (true);
			}
			
			return note;
		}
		
		/// <summary>
		/// Returns true when the specified note exists in the notebook
		/// </summary>
		/// <param name="note">
		/// A <see cref="Note"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Boolean"/>
		/// </returns>
		public bool ContainsNote (Note note)
		{
			// Check the specified note to see if it contains the notebook tag
			foreach (Tag noteTag in note.Tags) {
				if (noteTag == tag)
					return true;
			}
			
			return false;
		}
		#endregion // Public Methods
		
		#region Private Methods
		#endregion // Private Methods
	}

	/// <summary>
	/// A special notebook that represents really "no notebook" as
	/// being selected.  This notebook is used in the Search All
	/// Notes Window to allow users to select it at the top of the
	/// list so that all notes are shown.
	/// </summary>
	public class AllNotesNotebook : Notebook
	{
		public AllNotesNotebook () : base ()
		{
		}
		
		public override string Name
		{
			get { return Catalog.GetString ("All Notes"); }
		}
		
		public override string NormalizedName
		{
			get { return "___NotebookManager___AllNotes__Notebook___"; }
		}
		
		public override Tag Tag
		{
			get { return null; }
		}
		
		public override Note GetTemplateNote ()
		{
			return Tomboy.DefaultNoteManager.GetOrCreateTemplateNote ();
		}
	}
}