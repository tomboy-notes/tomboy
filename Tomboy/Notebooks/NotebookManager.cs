using System;
using System.Collections.Generic;
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
		static Dictionary<string, Gtk.TreeIter> notebookMap;
		static object locker = new object ();
		#endregion // Fields

		#region Constructors
		static NotebookManager ()
		{
			notebooks = new Gtk.ListStore (typeof (Notebook));

			sortedNotebooks = new Gtk.TreeModelSort (notebooks);
			sortedNotebooks.SetSortFunc (0, new Gtk.TreeIterCompareFunc (CompareNotebooksSortFunc));
			sortedNotebooks.SetSortColumnId (0, Gtk.SortType.Ascending);

			// <summary>
			// The key for this dictionary is Notebook.Name.ToLower ().
			// </summary>
			notebookMap = new Dictionary<string, Gtk.TreeIter> ();
			
			LoadNotebooks ();
		}
		#endregion // Constructors
		
		#region Properties
		public static Gtk.TreeModel Notebooks
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
				
				notebook = new Notebook (notebookName);
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
			}

			return notebook;
		}
		
		/// <summary>
		/// Remove the specified notebook from the system
		/// </summary>
		/// <param name="notebook">
		/// A <see cref="Notebook"/>
		/// </param>
		public static void RemoveNotebook (Notebook notebook)
		{
			if (notebook == null)
				throw new ArgumentNullException ("NotebookManager.RemoveNotebook () called with a null argument.");
			
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
			if (notebookMap.ContainsKey (notebook.NormalizedName) == true) {
				iter = notebookMap [notebook.NormalizedName];
				return true;
			}
			
			iter = Gtk.TreeIter.Zero;
			return false;
		}
		#endregion // Public Methods
		
		#region Private Methods
		static int CompareNotebooksSortFunc (Gtk.TreeModel model,
											 Gtk.TreeIter a,
											 Gtk.TreeIter b)
		{
			Notebook notebook_a = model.GetValue (a, 0) as Notebook;
			Notebook notebook_b = model.GetValue (b, 0) as Notebook;

			if (notebook_a == null || notebook_b == null)
				return 0;

			return string.Compare (notebook_a.Name, notebook_b.Name);
		}
		
		/// <summary>
		/// Loop through the system tags looking for notebooks
		/// </summary>
		private static void LoadNotebooks ()
		{
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
		#endregion // Private Methods
	}
}
