using System;
using System.Collections.Generic;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	/// <summary>
	/// A convenience class for dealing with Notebooks in Tomboy
	/// </summary>
	public class NotebookManager
	{
		#region Fields
		static Gtk.ListStore notebooks;
		static Gtk.TreeModelSort sortedNotebooks;
		static Gtk.TreeModelFilter filteredNotebooks;
		static Dictionary<string, Gtk.TreeIter> notebookMap;
		static object locker = new object ();		
		public static bool AddingNotebook = false;
		#endregion // Fields
		
		public delegate void NotebookEventHandler (Note note, Notebook notebook);
		
		#region Events
		public static event NotebookEventHandler NoteAddedToNotebook;
		public static event NotebookEventHandler NoteRemovedFromNotebook;
		#endregion // Events

		#region Constructors
		static NotebookManager ()
		{
			notebooks = new Gtk.ListStore (typeof (Notebook));

			sortedNotebooks = new Gtk.TreeModelSort (notebooks);
			sortedNotebooks.SetSortFunc (0, new Gtk.TreeIterCompareFunc (CompareNotebooksSortFunc));
			sortedNotebooks.SetSortColumnId (0, Gtk.SortType.Ascending);
			
			filteredNotebooks = new Gtk.TreeModelFilter (sortedNotebooks, null);
			filteredNotebooks.VisibleFunc = FilterNotebooks;
			
			AllNotesNotebook allNotesNotebook = new AllNotesNotebook ();
			Gtk.TreeIter iter = notebooks.Append ();
			notebooks.SetValue (iter, 0, allNotesNotebook);
			
			UnfiledNotesNotebook unfiledNotesNotebook = new UnfiledNotesNotebook ();
			iter = notebooks.Append ();
			notebooks.SetValue (iter, 0, unfiledNotesNotebook);

			// <summary>
			// The key for this dictionary is Notebook.Name.ToLower ().
			// </summary>
			notebookMap = new Dictionary<string, Gtk.TreeIter> ();
			
			// Load the notebooks now if the notes have already been loaded
			// or wait for the NotesLoaded event otherwise.
			if (Tomboy.DefaultNoteManager.Initialized)
				LoadNotebooks ();
			else
				Tomboy.DefaultNoteManager.NotesLoaded += OnNotesLoaded;
		}
		#endregion // Constructors
		
		#region Properties
		public static Gtk.ITreeModel Notebooks
		{
			get {
				return filteredNotebooks;
			}
		}
		
		/// <summary>
		/// A Gtk.ITreeModel that contains all of the items in the
		/// NotebookManager TreeStore including SpecialNotebooks
		/// which are used in the "Search All Notes" window.
		/// </summary>
		/// <param name="notebookName">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="Notebook"/>
		/// </returns>
		public static Gtk.ITreeModel NotebooksWithSpecialItems
		{
			get {
				return sortedNotebooks;
			}
		}
		#endregion // Properties
		
		#region Public Methods
		public static Notebook GetNotebook (string notebookName)
		{
			if (notebookName == null)
				throw new ArgumentNullException ("NotebookManager.GetNotebook () called with a null name.");
			
			string normalizedName = notebookName.Trim ().ToLower ();
			if (normalizedName == String.Empty)
				throw new ArgumentException ("NotebookManager.GetNotebook () called with an empty name.");
			
			if (notebookMap.ContainsKey (normalizedName)) {
				Gtk.TreeIter iter = notebookMap [normalizedName];
				return notebooks.GetValue (iter, 0) as Notebook;
			}
			
			return null;
		}
		
		public static bool NotebookExists (string notebookName)
		{
			string normalizedName = notebookName.Trim ().ToLower ();
			if (notebookMap.ContainsKey (normalizedName)) {
				return true;
			}
			return false;
		}
		
		public static Notebook GetOrCreateNotebook (string notebookName)
		{
			if (notebookName == null)
				throw new ArgumentNullException ("NotebookManager.GetNotebook () called with a null name.");
			
			Notebook notebook = GetNotebook (notebookName);
			if (notebook != null)
				return notebook;
			
			Gtk.TreeIter iter = Gtk.TreeIter.Zero;
			lock (locker) {
				notebook = GetNotebook (notebookName);
				if (notebook != null)
					return notebook;
				
				try {
					AddingNotebook = true;
					notebook = new Notebook (notebookName);
				} finally {
					AddingNotebook = false;
				}
				iter = notebooks.Append ();
				notebooks.SetValue (iter, 0, notebook);
				notebookMap [notebook.NormalizedName] = iter;
				
				// Create the template note so the system tag
				// that represents the notebook actually gets
				// saved to a note (and persisted after Tomboy
				// is shut down).
				Note templateNote = notebook.GetTemplateNote ();
				
				// Make sure the template note has the notebook tag.
				// Since it's possible for the template note to already
				// exist, we need to make sure it gets tagged.
				templateNote.AddTag (notebook.Tag);
				if (NoteAddedToNotebook != null)
					NoteAddedToNotebook (templateNote, notebook);
			}

			return notebook;
		}
		
		/// <summary>
		/// Delete the specified notebook from the system
		/// </summary>
		/// <param name="notebook">
		/// A <see cref="Notebook"/>
		/// </param>
		public static void DeleteNotebook (Notebook notebook)
		{
			if (notebook == null)
				throw new ArgumentNullException ("NotebookManager.DeleteNotebook () called with a null argument.");
			
			if (notebookMap.ContainsKey (notebook.NormalizedName) == false)
				return;
			
			lock (locker) {
				if (notebookMap.ContainsKey (notebook.NormalizedName) == false)
					return;
				
				Gtk.TreeIter iter = notebookMap [notebook.NormalizedName];
				if (notebooks.Remove (ref iter) == true) {
					Logger.Debug ("NotebookManager: Removed notebook: {0}", notebook.NormalizedName);
				} else {
					Logger.Warn ("NotebookManager: Call to remove notebook failed: {0}", notebook.NormalizedName);
				}
				
				notebookMap.Remove (notebook.NormalizedName);
				
				// Remove the notebook tag from every note that's in the notebook
				foreach (Note note in notebook.Tag.Notes) {
					note.RemoveTag (notebook.Tag);
					if (NoteRemovedFromNotebook != null)
						NoteRemovedFromNotebook (note, notebook);
				}
			}
		}
		
		/// <summary>
		/// Returns the Gtk.TreeIter that points to the specified Notebook.
		/// </summary>
		/// <param name="notebook">
		/// A <see cref="Notebook"/>
		/// </param>
		/// <param name="iter">
		/// A <see cref="Gtk.TreeIter"/>.  Will be set to a valid iter if
		/// the specified notebook is found.
		/// </param>
		/// <returns>
		/// A <see cref="System.Boolean"/>.  True if the specified notebook
		/// was found, false otherwise.
		/// </returns>
		public static bool GetNotebookIter (Notebook notebook, out Gtk.TreeIter iter)
		{
			Gtk.TreeIter current_iter;
			if (sortedNotebooks.GetIterFirst (out current_iter)) {
				do {
					Notebook current_notebook = (Notebook)sortedNotebooks.GetValue (current_iter, 0);
					if (notebook == current_notebook) {
						iter = current_iter;
						return true;
					}
				} while (sortedNotebooks.IterNext (ref current_iter));
			}
			
			iter = Gtk.TreeIter.Zero;
			return false;
		}
		
		/// <summary>
		/// Returns the Notebook associated with this note or null
		/// if no notebook exists.
		/// </summary>
		/// <param name="note">
		/// A <see cref="Note"/>
		/// </param>
		/// <returns>
		/// A <see cref="Notebook"/>
		/// </returns>
		public static Notebook GetNotebookFromNote (Note note)
		{
			foreach (Tag tag in note.Tags) {
				Notebook notebook = GetNotebookFromTag (tag);
				if (notebook != null)
					return notebook;
			}
			
			return null;
		}
		
		/// <summary>
		/// Returns the Notebook associated with the specified tag
		/// or null if the Tag does not represent a notebook.
		/// </summary>
		/// <param name="tag">
		/// A <see cref="Tag"/>
		/// </param>
		/// <returns>
		/// A <see cref="Notebook"/>
		/// </returns>
		public static Notebook GetNotebookFromTag (Tag tag)
		{
			if (IsNotebookTag (tag) == false)
				return null;
			
			// Parse off the system and notebook prefix to get
			// the name of the notebook and then look it up.
			string systemNotebookPrefix = Tag.SYSTEM_TAG_PREFIX + Notebook.NotebookTagPrefix;
			string notebookName = tag.Name.Substring (systemNotebookPrefix.Length);
			
			return GetNotebook (notebookName);
		}
		
		/// <summary>
		/// Evaluates the specified tag and returns <value>true</value>
		/// if it's a tag which represents a notebook.
		/// </summary>
		/// <param name="tag">
		/// A <see cref="Tag"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.Boolean"/>
		/// </returns>
		public static bool IsNotebookTag (Tag tag)
		{
			string fullTagName = tag.Name;
			if (fullTagName.StartsWith (Tag.SYSTEM_TAG_PREFIX + Notebook.NotebookTagPrefix) == true)
				return true;
			
			return false;
		}
		
		/// <summary>
		/// Prompt the user to create a new notebook
		/// </summary>
		/// <param name="parent">
		/// A <see cref="Gtk.Window"/> that will be used in the child dialog or
		/// null if none is available.
		/// </param>
		/// <returns>If successful, returns the newly created notebook.</returns>
		public static Notebook PromptCreateNewNotebook (Gtk.Window parent)
		{
			return PromptCreateNewNotebook (parent, null);
		}
		
		/// <summary>
		/// Prompt the user to create a new notebook and if successful, move
		/// the notes specified in the notesToAdd list into the new notebook.
		/// </summary>
		/// <param name="parent">
		/// A <see cref="Gtk.Window"/>
		/// </param>
		/// <param name="notesToAdd">
		/// A <see cref="List`1"/> of notes that should be added to the new
		/// notebook.
		/// </param>
		/// <returns>
		/// The newly created <see cref="Notebook"/> if successful or null
		/// if there was a problem.
		/// </returns>
		public static Notebook PromptCreateNewNotebook (Gtk.Window parent, List<Note> notesToAdd)
		{
			// Prompt the user for the name of a new notebook
			Notebooks.CreateNotebookDialog dialog =
				new Notebooks.CreateNotebookDialog (parent,
							Gtk.DialogFlags.Modal
								| Gtk.DialogFlags.DestroyWithParent
								| Gtk.DialogFlags.NoSeparator);
			
			
			int response = dialog.Run ();
			string notebookName = dialog.NotebookName;
			dialog.Destroy ();
			if (response != (int) Gtk.ResponseType.Ok)
				return null;
			
			Notebooks.Notebook notebook = GetOrCreateNotebook (notebookName);
			if (notebook == null) {
				Logger.Warn ("Could not create notebook: {0}", notebookName);
			} else {
				Logger.Debug ("Created the notebook: {0} ({1})", notebook.Name, notebook.NormalizedName);
				
				if (notesToAdd != null) {
					// Move all the specified notesToAdd into the new notebook
					foreach (Note note in notesToAdd) {
						NotebookManager.MoveNoteToNotebook (note, notebook);
					}
				}
			}
			
			return notebook;
		}
		
		/// <summary>
		/// Prompt the user and delete the notebok (if they say so).
		/// </summary>
		/// <param name="parent">
		/// A <see cref="Gtk.Window"/>
		/// </param>
		/// <param name="notebook">
		/// A <see cref="Notebook"/>
		/// </param>
		public static void PromptDeleteNotebook (Gtk.Window parent, Notebook notebook)
		{
			// Confirmation Dialog
			HIGMessageDialog dialog =
				new HIGMessageDialog (parent,
									  Gtk.DialogFlags.Modal,
									  Gtk.MessageType.Question,
									  Gtk.ButtonsType.YesNo,
									  Catalog.GetString ("Really delete this notebook?"),
									  Catalog.GetString (
									  	"The notes that belong to this notebook will not be " +
									  	"deleted, but they will no longer be associated with " +
									  	"this notebook.  This action cannot be undone."));
			dialog.DefaultResponse = Gtk.ResponseType.No;
			int response = dialog.Run ();
			dialog.Destroy ();
			if (response != (int) Gtk.ResponseType.Yes)
				return;
			
			// Grab the template note before removing all the notebook tags
			Note templateNote = notebook.GetTemplateNote ();
			
			DeleteNotebook (notebook);

			// Delete the template note
			if (templateNote != null) {
				NoteManager noteManager = Tomboy.DefaultNoteManager;
				noteManager.Delete (templateNote);
			}
		}
		
		/// <summary>
		/// Place the specified note into the specified notebook.  If the
		/// note already belongs to a notebook, it will be removed from that
		/// notebook first.
		/// </summary>
		/// <param name="note">
		/// A <see cref="Note"/>
		/// </param>
		/// <param name="notebook">
		/// A <see cref="Notebook"/>.  If Notebook is null, the note will
		/// be removed from its current notebook.
		/// </param>
		/// <returns>True if the note was successfully moved.</returns>
		public static bool MoveNoteToNotebook (Note note, Notebook notebook)
		{
			if (note == null)
				return false;
			
			// NOTE: In the future we may want to allow notes
			// to exist in multiple notebooks.  For now, to
			// alleviate the confusion, only allow a note to
			// exist in one notebook at a time.
			
			Notebook currentNotebook = GetNotebookFromNote (note);
			if (currentNotebook == notebook)
				return true; // It's already there.
			
			if (currentNotebook != null) {
				note.RemoveTag (currentNotebook.Tag);
				if (NoteRemovedFromNotebook != null)
					NoteRemovedFromNotebook (note, currentNotebook);
			}
			
			// Only attempt to add the notebook tag when this
			// menu item is not the "No notebook" menu item.
			if (notebook != null && (notebook is SpecialNotebook) == false) {
				note.AddTag (notebook.Tag);
				if (NoteAddedToNotebook != null)
					NoteAddedToNotebook (note, notebook);
			}
			
			return true;
		}
		
		public static void FireNoteAddedToNoteBook (Note note, Notebook notebook)
		{
			if (NoteAddedToNotebook != null)
				NoteAddedToNotebook (note, notebook);
		}
		
		public static void FireNoteRemovedFromNoteBook (Note note, Notebook notebook)
		{
			if (NoteRemovedFromNotebook != null)
				NoteRemovedFromNotebook (note, notebook);
		}
		
		#endregion // Public Methods
		
		#region Private Methods
		static int CompareNotebooksSortFunc (Gtk.ITreeModel model,
											 Gtk.TreeIter a,
											 Gtk.TreeIter b)
		{
			Notebook notebook_a = model.GetValue (a, 0) as Notebook;
			Notebook notebook_b = model.GetValue (b, 0) as Notebook;

			if (notebook_a == null || notebook_b == null)
				return 0;
			
			if (notebook_a is SpecialNotebook && notebook_b is SpecialNotebook) {
				if (notebook_a is AllNotesNotebook)
					return -1;
				else
					return 1;
			} else if (notebook_a is SpecialNotebook)
				return -1;
			else if (notebook_b is SpecialNotebook)
				return 1;

			return string.Compare (notebook_a.Name, notebook_b.Name);
		}
		
		static void OnNotesLoaded(object sender, EventArgs args)
		{
			LoadNotebooks ();
		}

		/// <summary>
		/// Loop through the system tags looking for notebooks
		/// </summary>
		private static void LoadNotebooks ()
		{
			Logger.Debug ("Loading notebooks");
			Gtk.TreeIter iter = Gtk.TreeIter.Zero;
			foreach (Tag tag in TagManager.AllTags) {
				// Skip over tags that aren't notebooks
				if (tag.IsSystem == false
						|| tag.Name.StartsWith (Tag.SYSTEM_TAG_PREFIX + Notebook.NotebookTagPrefix) == false) {
					continue;
				}
				Notebook notebook = new Notebook (tag);
				iter = notebooks.Append ();
				notebooks.SetValue (iter, 0, notebook);
				notebookMap [notebook.NormalizedName] = iter;
			}
		}

	        /// <summary>
	        /// Filter out SpecialNotebooks from the model
	        /// </summary>
	        static bool FilterNotebooks (Gtk.ITreeModel model, Gtk.TreeIter iter)
	        {
	        	Notebook notebook = model.GetValue (iter, 0) as Notebook;
	        	if (notebook == null || notebook is SpecialNotebook)
	        		return false;
	        	
	        	return true;
	        }
		#endregion // Private Methods
	}
}
