using System;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebookPreferenceTabAddin : PreferenceTabAddin
	{
		PreferencesDialog preferencesDialog;
		
		Gtk.TreeView notebooksTree;
		Gtk.TreeViewColumn notebookNameColumn;
		Gtk.Button openTemplateNoteButton;
		Gtk.Button addNotebookButton;
		Gtk.Button removeNotebookButton;
		
		public override bool GetPreferenceTabWidget (
									PreferencesDialog parent,
									out string tabLabel,
									out Gtk.Widget preferenceWidget)
		{
			try {
				preferencesDialog = parent;
				tabLabel = Catalog.GetString ("Notebooks");
				preferenceWidget = MakeNotebooksPane ();
				preferencesDialog.Hidden += OnPreferencesDialogHidden;
			} catch (Exception e) {
				Logger.Error ("Error creating the Notebooks Preference Tab widget: {0}", e.Message);
				tabLabel = string.Empty;
				preferenceWidget = null;
				return false;
			}
			
			return true;
		}

		public Gtk.Widget MakeNotebooksPane ()
		{
			Gtk.VBox vbox = new Gtk.VBox (false, 6);
			vbox.BorderWidth = 12;
			vbox.Show ();
			
			notebooksTree = new Gtk.TreeView (Notebooks.NotebookManager.Notebooks);
			notebooksTree.Selection.Changed += OnNotebooksTreeSelectionChanged;
			notebooksTree.RowActivated += OnNotebookRowActivated;
			notebooksTree.Model.RowInserted += OnNotebookAdded;
			notebooksTree.Show ();
			
			Gtk.CellRenderer renderer;
			notebookNameColumn = new Gtk.TreeViewColumn ();
			notebookNameColumn.Title = Catalog.GetString ("Name");
			notebookNameColumn.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			notebookNameColumn.Resizable = true;
			
			renderer = new Gtk.CellRendererText ();
			notebookNameColumn.PackStart (renderer, true);
			notebookNameColumn.SetCellDataFunc (renderer,
									new Gtk.TreeCellDataFunc (NotebookNameColumnDataFunc));
			notebooksTree.AppendColumn (notebookNameColumn);
			
			Gtk.ScrolledWindow sw  = new Gtk.ScrolledWindow ();
			sw.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.ShadowType = Gtk.ShadowType.In;
			sw.Add (notebooksTree);
			sw.Show ();

			vbox.PackStart (sw, true, true, 0);
			
			Gtk.HButtonBox hButtonBox = new Gtk.HButtonBox ();
			hButtonBox.Layout = Gtk.ButtonBoxStyle.Edge;
			hButtonBox.Show ();
			
			openTemplateNoteButton = new Gtk.Button (Catalog.GetString ("_Template Note"));
			openTemplateNoteButton.UseUnderline = true;
			openTemplateNoteButton.Sensitive = false;
			openTemplateNoteButton.Clicked += OnOpenTemplateNoteButtonClicked;
			openTemplateNoteButton.Show ();
			hButtonBox.PackStart (openTemplateNoteButton, false, false, 0);
			
			addNotebookButton = new Gtk.Button (Gtk.Stock.Add);
			addNotebookButton.Clicked += OnAddNotebookButtonClicked;
			addNotebookButton.Show ();
			hButtonBox.PackStart (addNotebookButton, false, false, 0);
			
			removeNotebookButton = new Gtk.Button (Gtk.Stock.Remove);
			removeNotebookButton.Clicked += OnRemoveNotebookButtonClicked;
			removeNotebookButton.Sensitive = false;
			removeNotebookButton.Show ();
			hButtonBox.PackStart (removeNotebookButton, false, false, 0);

			vbox.PackStart (hButtonBox, false, false, 0);
			
			return vbox;
		}

		void OnOpenTemplateNoteButtonClicked (object sender, EventArgs args)
		{
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			if (notebook == null)
				return;
			
			Note templateNote = notebook.GetTemplateNote ();
			if (templateNote == null)
				return; // something seriously went wrong
			
			templateNote.Window.Present ();
		}
		
		void OnAddNotebookButtonClicked (object sender, EventArgs args)
		{
			NotebookManager.PromptCreateNewNotebook (preferencesDialog);
		}
		
		// Select the specified notebook in the TreeView
//		void SelectNotebook (Notebooks.Notebook notebook)
//		{
//			Gtk.TreeIter iter;
//			if (Notebooks.NotebookManager.GetNotebookIter (notebook, out iter) == false)
//				return; // notebook not found
//			
//			notebooksTree.Selection.SelectIter (iter);
//		}

		void OnRemoveNotebookButtonClicked (object sender, EventArgs args)
		{
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			if (notebook == null)
				return;
			
			// Confirmation Dialog
			HIGMessageDialog dialog =
				new HIGMessageDialog (null,
									  Gtk.DialogFlags.Modal,
									  Gtk.MessageType.Question,
									  Gtk.ButtonsType.YesNo,
									  Catalog.GetString ("Really remove this notebook?"),
									  Catalog.GetString (
									  	"The notes that belong to this notebook will note be " +
									  	"removed, but they will no longer be associated with " +
									  	"this notebook.  This action cannot be undone."));
			Gtk.CheckButton removeTemplateNoteButton =
				new Gtk.CheckButton (Catalog.GetString ("Also _delete notebook's template note"));
			removeTemplateNoteButton.Show ();
			dialog.ExtraWidget = removeTemplateNoteButton;
			int response = dialog.Run ();
			bool removeTemplateNote = removeTemplateNoteButton.Active;
			dialog.Destroy ();
			if (response != (int) Gtk.ResponseType.Yes)
				return;
			
			Notebooks.NotebookManager.RemoveNotebook (notebook);
			if (removeTemplateNote) {
				Note templateNote = notebook.GetTemplateNote ();
				if (templateNote != null) {
					NoteManager noteManager = Tomboy.DefaultNoteManager;
					noteManager.Delete (templateNote);
				}
			}
		}

		private void OnNotebooksTreeSelectionChanged (object sender, EventArgs args)
		{
			Notebooks.Notebook notebook = GetSelectedNotebook ();
			
			if (notebook == null) {
				openTemplateNoteButton.Sensitive = false;
				removeNotebookButton.Sensitive = false;
			} else {
				openTemplateNoteButton.Sensitive = true;
				removeNotebookButton.Sensitive = true;
			}
		}
		
		// Open the notebook's note template when activated
		private void OnNotebookRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			OnOpenTemplateNoteButtonClicked (sender, EventArgs.Empty);
		}
		
		// Select the inserted notebook
		private void OnNotebookAdded (object sender, Gtk.RowInsertedArgs args)
		{
			notebooksTree.Selection.SelectIter (args.Iter);
			
			// TODO: Figure out why we have to include the following
			// lines instead of the SelectionChanged event taking care of this
			openTemplateNoteButton.Sensitive = true;
			removeNotebookButton.Sensitive = true;
		}
		
		public void OnPreferencesDialogHidden (object sender, EventArgs args)
		{
			// Unregister the RowInserted handler so that
			// it can't be called when there's no preferences
			// dialog.
			notebooksTree.Model.RowInserted -= OnNotebookAdded;
		}

		private Notebooks.Notebook GetSelectedNotebook ()
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;
			
			if (notebooksTree.Selection.GetSelected (out model, out iter) == false)
				return null; // Nothing selected
			
			return model.GetValue (iter, 0) as Notebooks.Notebook;
		}

		private void NotebookNameColumnDataFunc (Gtk.TreeViewColumn column,
												 Gtk.CellRenderer cell,
												 Gtk.TreeModel model,
												 Gtk.TreeIter iter)
		{
			Gtk.CellRendererText crt = cell as Gtk.CellRendererText;
			if (crt == null)
				return;
			
			Notebooks.Notebook notebook = model.GetValue (iter, 0) as Notebooks.Notebook;
			if (notebook == null)
				return;
			
			crt.Text = notebook.Name;
		}
	}
}
