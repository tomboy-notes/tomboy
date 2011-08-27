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
			Note template_note = null;
			Tag template_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);
			Tag notebook_tag = TagManager.GetOrCreateSystemTag (NotebookTagPrefix + Name);
			foreach (Note note in template_tag.Notes) {
				if (note.ContainsTag (notebook_tag)) {
					template_note = note;
					break;
				}
			}
			
			if (template_note == null) {
				template_note =
					noteManager.Create (templateNoteTitle,
							NoteManager.GetNoteTemplateContent (templateNoteTitle));
					
				// Select the initial text
				NoteBuffer buffer = template_note.Buffer;
				Gtk.TextIter iter = buffer.GetIterAtLineOffset (2, 0);
				buffer.MoveMark (buffer.SelectionBound, iter);
				buffer.MoveMark (buffer.InsertMark, buffer.EndIter);

				// Flag this as a template note
				template_note.AddTag (template_tag);

				// Add on the notebook system tag so Tomboy
				// will persist the tag/notebook across sessions
				// if no other notes are added to the notebook.
				template_note.AddTag (notebook_tag);
				
				template_note.QueueSave (ChangeType.ContentChanged);
			}
			
			return template_note;
		}
		
		public Note CreateNotebookNote ()
		{
			string temp_title;
			Note template = GetTemplateNote ();
			NoteManager note_manager = Tomboy.DefaultNoteManager;
			
			temp_title = note_manager.GetUniqueName (Catalog.GetString ("New Note"), note_manager.Notes.Count);
			Note note = note_manager.CreateNoteFromTemplate (temp_title, template);
			
			// Add the notebook tag
			note.AddTag (tag);
			
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
			return note.ContainsTag (tag);
		}
		#endregion // Public Methods
		
		#region Private Methods
		#endregion // Private Methods
	}

	/// <summary>
	/// A notebook of this type is special in the sense that it
	/// will not normally be displayed to the user as a notebook
	/// but it's used in the Search All Notes Window for special
	/// filtering of the notes.
	/// </summary>
	public abstract class SpecialNotebook : Notebook
	{
	}
	
	/// <summary>
	/// A special notebook that represents really "no notebook" as
	/// being selected.  This notebook is used in the Search All
	/// Notes Window to allow users to select it at the top of the
	/// list so that all notes are shown.
	/// </summary>
	public class AllNotesNotebook : SpecialNotebook
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
	
	/// <summary>
	/// A special notebook that represents a notebook with notes
	/// that are not filed.  This is used in the Search All Notes
	/// Window to filter notes that are not placed in any notebook.
	/// </summary>
	public class UnfiledNotesNotebook : SpecialNotebook
	{
		public UnfiledNotesNotebook () : base ()
		{
		}
		
		public override string Name
		{
			get { return Catalog.GetString ("Unfiled Notes"); }
		}
		
		public override string NormalizedName
		{
			get { return "___NotebookManager___UnfiledNotes__Notebook___"; }
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