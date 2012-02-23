using System;
using System.Collections.Generic;
using System.IO;
using Mono.Unix;
using Tomboy;
using Tomboy.Notebooks;

namespace Tomboy
{
	/// <summary>
	/// An abstract class which handles all the details of "export all",
	/// to be subclassed with a method that defines what to do with a
	/// single note.
	/// </summary>
	public abstract class ExportAllApplicationAddin : ApplicationAddin
	{

		/// <summary>
		/// The filename suffix of the export type, e.g. "html" or "txt".
		/// Don't put a punctuation mark in front!
		/// </summary>
		protected string export_file_suffix;

		/// <summary>
		/// The full name to be used in the menu. Can include spaces, should use Catalog.GetString.
		/// </summary>
		protected string export_type_pretty_name;

		private Gtk.ActionGroup action_group;
		private uint action_group_id;
		private ActionManager am = Tomboy.ActionManager;
		private bool initialized = false;

		/// <summary>
		/// Used to inform the path resolver if all notes are being exported or just one notebook.
		/// </summary>
		private bool exporting_single_notebook = false;

		/// <summary>
		/// Called when Tomboy has started up and is nearly 100% initialized.
		/// </summary>
		public override void Initialize ()
		{
			// Gets names from subclass.
			SetNames ();

			/*Adds "Export All Notes/Notebook To ***" to Tomboy's Main Menu */

			if (am.FindActionByName ("NoteExportAll"+export_file_suffix+"Action") == null) {

				am.MainWindowActions.Add (new Gtk.ActionEntry [] {
					new Gtk.ActionEntry ("NoteExportAll"+export_file_suffix+"Action", null,
					String.Format (Catalog.GetString ("Export All Notes to {0}"), export_type_pretty_name), null,
					String.Format (Catalog.GetString ("Start exporting notes to {0}"), export_type_pretty_name), null)
				});
				am.MainWindowActions.Add (new Gtk.ActionEntry [] {
					new Gtk.ActionEntry ("NoteExportNotebook"+export_file_suffix+"Action", null,
					String.Format (Catalog.GetString ("Export selected notebook to {0}"), export_type_pretty_name), null,
					String.Format (Catalog.GetString ("Start exporting notebook to {0}"), export_type_pretty_name), null)
				});

				action_group = new Gtk.ActionGroup ("Export");
				action_group.Add (new Gtk.ActionEntry [] {
					new Gtk.ActionEntry ("ToolsMenuAction", null,
						Catalog.GetString ("_Tools"), null, null, null),

					new Gtk.ActionEntry ("ExportMenuAction", Gtk.Stock.New,
					Catalog.GetString ("Export"), null,
					Catalog.GetString ("Export your notes."), null),

					new Gtk.ActionEntry ("ExportAllNotes"+export_file_suffix+"Action", null,
						String.Format (Catalog.GetString ("Export All Notes To {0}"), export_type_pretty_name), null, null,
						delegate {
							am ["NoteExportAll"+export_file_suffix+"Action"].Activate ();
					}),
					new Gtk.ActionEntry ("ExportNotebook"+export_file_suffix+"Action", null,
						String.Format (Catalog.GetString ("Export Selected Notebook To {0}"), export_type_pretty_name), null, null,
						delegate {
							am ["NoteExportNotebook"+export_file_suffix+"Action"].Activate ();
					})
				});

				action_group_id = am.UI.AddUiFromString (String.Format (@"
				                <ui>
				                <menubar name='MainWindowMenubar'>
				                <placeholder name='MainWindowMenuPlaceholder'>
				                <menu name='ToolsMenu' action='ToolsMenuAction'>
				                <menu name='ExportMenu' action='ExportMenuAction'>
				                <menuitem name='ExportAllNotes{0}' action='ExportAllNotes{0}Action' />
				                <menuitem name='ExportNotebook{0}' action='ExportNotebook{0}Action' />
				                </menu>
				                </menu>
				                </placeholder>
				                </menubar>
				                </ui>
				                ", export_file_suffix)
				                 );

				am.UI.InsertActionGroup (action_group, 0);

				am ["NoteExportAll"+export_file_suffix+"Action"].Activated += ExportAllButtonClicked;
				am ["NoteExportNotebook"+export_file_suffix+"Action"].Activated += ExportNotebookButtonClicked;

				initialized = true;
			}
		}

		/// <summary>
		/// Must be overridden in order to set names for internal menu use
		/// and file naming (export_file_suffix) and what the user sees
		/// (export_type_pretty_name).
		/// </summary>
		protected abstract void SetNames ();

		/// <summary>
		/// Called just before Tomboy shuts down for good.
		/// </summary>
		public override void Shutdown ()
		{
			// Disconnect the event handlers and global menu entries so
			// there aren't any memory leaks.

			if (action_group != null) {
				am ["NoteExportAll"+export_file_suffix+"Action"].Activated -= ExportAllButtonClicked;
				am ["NoteExportNotebook"+export_file_suffix+"Action"].Activated -= ExportAllButtonClicked;
				am.UI.RemoveUi (action_group_id);
				am.UI.RemoveActionGroup (action_group);
				am.MainWindowActions.Remove
				    (Tomboy.ActionManager.FindActionByName ("NoteExportAll"+export_file_suffix+"Action"));
				am.MainWindowActions.Remove
				    (Tomboy.ActionManager.FindActionByName ("NoteExportNotebook"+export_file_suffix+"Action"));

				action_group = null;
			}
		}

		void ExportAllButtonClicked (object sender, EventArgs args)
		{
			ExportAllNotes ();
		}

		/// <summary>
		/// Called when the user chooses "Export All"
		/// </summary>
		/// <param name="sender">
		void ExportAllNotes ()
		{
			Logger.Info ("Activated export all to " + export_type_pretty_name);
			exporting_single_notebook = false;

			//Opens the folder selection dialog
			ExportMultipleDialog dialog =
			    new ExportMultipleDialog (String.Format (Catalog.GetString ("All Notes {0} Export"), export_type_pretty_name), export_type_pretty_name);
			int response = dialog.Run ();
			if (response != (int) Gtk.ResponseType.Ok) {
				Logger.Debug("User clicked cancel.");
				dialog.Destroy ();
				return;
			}
			string output_folder = SanitizePath (dialog.Filename);

			try {
				Logger.Debug ("Creating an export folder in: " + output_folder);
				System.IO.Directory.CreateDirectory (output_folder);

				//Iterate through notebooks
				Notebooks.Notebook notebook;
				string notebook_folder;

				foreach (Tag tag in TagManager.AllTags) {
					// Skip over tags that aren't notebooks
					notebook = NotebookManager.GetNotebookFromTag (tag);
					if (notebook == null)
						continue;

					Logger.Debug ("Exporting notebook " + notebook.Name);
					notebook_folder = SanitizePath (output_folder + System.IO.Path.DirectorySeparatorChar
					                  + notebook.NormalizedName);
					System.IO.Directory.CreateDirectory (notebook_folder);
					ExportNotesInList (notebook.Tag.Notes, notebook_folder);

				}

				//Finally we have to export all unfiled notes.
				Logger.Debug ("Exporting Unfiled Notes");
				ExportNotesInList (ListUnfiledNotes (), output_folder);

				//Successful export: clean up and inform.
				dialog.SavePreferences ();
				ShowSuccessDialog (output_folder);

			} catch (UnauthorizedAccessException) {
				Logger.Error (Catalog.GetString ("Could not export, access denied."));
				ShowErrorDialog (output_folder, dialog,
				                 Catalog.GetString ("Access denied."));
				return;
			} catch (DirectoryNotFoundException) {
				Logger.Error (Catalog.GetString ("Could not export, folder does not exist."));
				ShowErrorDialog (output_folder, dialog,
				                 Catalog.GetString ("Folder does not exist."));
				return;
			} catch (Exception ex) {
				Logger.Error (Catalog.GetString ("Could not export: {0}"), ex);
				ShowErrorDialog (output_folder, dialog,
				                 Catalog.GetString ("Unknown error."));
				return;
			} finally {
				if (dialog != null) {
					dialog.Destroy ();
					dialog = null;
				}
			}
		}

		/// <summary>
		/// Called when the user chooses "Export Notebook"
		/// (Even when "All Notes or "Unfiled Notes" are
		/// selected.)
		/// </summary>
		void ExportNotebookButtonClicked (object sender, EventArgs args)
		{
			string output_folder = null;
			ExportMultipleDialog dialog = null;
			Logger.Info ("Activated export notebook to " + export_file_suffix);

			Notebook notebook = NoteRecentChanges.GetInstance (Tomboy.DefaultNoteManager).GetSelectedNotebook ();

			try {
				//Handling the two special notebooks
				string notebook_name = notebook.NormalizedName;
				if (notebook_name == "___NotebookManager___AllNotes__Notebook___") {
					Logger.Info ("This notebook includes all notes, activating Export All");
					ExportAllNotes ();
					return;
				} else if (notebook_name == "___NotebookManager___UnfiledNotes__Notebook___") {
					dialog = new ExportMultipleDialog (Catalog.GetString ("Unfiled Notes"), export_type_pretty_name);
					int response = dialog.Run ();
					output_folder = SanitizePath (dialog.Filename);
					if (response != (int) Gtk.ResponseType.Ok) {
						Logger.Debug("User clicked cancel.");
						dialog.Destroy ();
						return;
					}

					exporting_single_notebook = true;
					Logger.Debug ("Creating an export folder in: " + output_folder);
					System.IO.Directory.CreateDirectory (output_folder);
					ExportNotesInList (ListUnfiledNotes (), output_folder);
				} else {
					//Ordinary notebooks
					dialog = new ExportMultipleDialog (notebook_name, export_type_pretty_name);
					int response = dialog.Run ();
					output_folder = SanitizePath (dialog.Filename);
					if (response != (int) Gtk.ResponseType.Ok) {
						Logger.Debug("User clicked cancel.");
						dialog.Destroy ();
						return;
					}

					exporting_single_notebook = true;
					Logger.Debug ("Creating an export folder in: " + output_folder);
					System.IO.Directory.CreateDirectory (output_folder);
					ExportNotesInList (notebook.Tag.Notes, output_folder);
				}

				//Successful export: clean up and inform.
				dialog.SavePreferences ();
				ShowSuccessDialog (output_folder);

			} catch (UnauthorizedAccessException) {
				Logger.Error (Catalog.GetString ("Could not export, access denied."));
				ShowErrorDialog (output_folder, dialog,
				                 Catalog.GetString ("Access denied."));
				return;
			} catch (DirectoryNotFoundException) {
				Logger.Error (Catalog.GetString ("Could not export, folder does not exist."));
				ShowErrorDialog (output_folder, dialog,
				                 Catalog.GetString ("Folder does not exist."));
				return;
			} catch (Exception ex) {
				Logger.Error (Catalog.GetString ("Could not export: {0}"), ex);
				ShowErrorDialog (output_folder, dialog,
				                 Catalog.GetString ("Unknown error."));
				return;
			} finally {
				if (dialog != null) {
					dialog.Destroy ();
					dialog = null;
				}
			}
		}

		/// <summary>
		/// Exports the specified list of notes to *** files in the given folder,
		/// excludes template notes.
		/// </summary>
		public void ExportNotesInList (List<Note> note_list, string output_folder)
		{
			output_folder = output_folder + System.IO.Path.DirectorySeparatorChar;
			bool save;

			foreach (Note note in note_list) {
				save = true;
				//Checks all tags on note to see if it's a template.
				foreach (Tag tag in note.Tags) {
					if (tag.Name.StartsWith (Tag.SYSTEM_TAG_PREFIX + TagManager.TemplateNoteSystemTag))
						save = false;
				}

				if (save) {
					ExportSingleNote (note, output_folder);
				}
			}
			return;
		}

		/// <summary>
		/// Finds all notes without a notebook tag and returns them in a list.
		/// </summary>
		public List<Note> ListUnfiledNotes ()
		{
			List<Note> unfiled_notes = new List<Note> ();
			//Checks all notes
			foreach (Note note in Tomboy.DefaultNoteManager.Notes) {
				if (NotebookManager.GetNotebookFromNote (note) == null)
					unfiled_notes.Add (note);
			}
			return unfiled_notes;
		}

		/// <summary>
		/// Exports a single Note to the chosen format and saves it in the specified folder.
		/// To be implemented in a subclass where the subclass implementation takes care
		/// of conversion and saving.
		/// <param name="output_folder">
		/// The folder which the note is to be saved to. For an all notes export the top level
		/// folder is chosen by the user and sublevel folders are automatically created for
		/// each notebook and passed to this method.
		/// </summary>
		public abstract void ExportSingleNote (Note note, string output_folder);

		/// <summary>
		/// Removes elements from the note title that might be problematic in a file name.
		/// </summary>
		public string SanitizeNoteTitle (string note_title)
		{
			note_title = SanitizePath (note_title);

			//Clearing common folder and file chars
			note_title = note_title.Replace ('/', '_');
			note_title = note_title.Replace ('\\', '_');
			note_title = note_title.Replace ('.', '_');
			note_title = note_title.Replace ('?', '_');

			return note_title;
		}

		/// <summary>
		/// Makes sure a path doesn't have any illegal characters.
		/// </summary>
		private string SanitizePath (string path)
		{
			char[] invalid_path_chars = Path.GetInvalidPathChars ();

			foreach (char x in invalid_path_chars) {
				path = path.Replace (x, '_');
			}

			return path;
		}

		/// <summary>
		/// Return true if the addin is initialized
		/// </summary>
		public override bool Initialized
		{
			get
			{
				return initialized;
			}
		}

		/// <summary>
		/// Shows a success dialog when export is complete
		/// </summary>
		/// <param name="detail"> A string with details of the export folder.</param>
		private static void ShowSuccessDialog (string output_folder)
		{
			string detail = String.Format (
			                            Catalog.GetString ("Your notes were exported to \"{0}\"."),
			                            output_folder);

			HIGMessageDialog msg_dialog =
			        new HIGMessageDialog (
			        null,
			        Gtk.DialogFlags.DestroyWithParent,
			        Gtk.MessageType.Info,
			        Gtk.ButtonsType.Ok,
			        Catalog.GetString ("Notes exported successfully"),
			        detail);
			msg_dialog.Run ();
			msg_dialog.Destroy ();
		}

		/// <summary>
		/// Shows an error dialog if things go wrong.
		/// </summary>
		/// <param name="output_folder">
		/// A <see cref="System.String"/> with the name of the folder
		/// that couldn't be exported to.
		/// </param>
		/// <param name="dialog">
		/// The parent <see cref="ExportMultipleDialog"/>.
		/// </param>
		/// <param name="error_message">
		/// A <see cref="System.String"/> with an error description.
		/// </param>
		private static void ShowErrorDialog (string output_folder, ExportMultipleDialog dialog,
		                                     string error_message)
		{
			string msg = String.Format (
			                     Catalog.GetString ("Could not save the files in \"{0}\""),
			                     output_folder);
				HIGMessageDialog msg_dialog =
				        new HIGMessageDialog (
				        dialog,
				        Gtk.DialogFlags.DestroyWithParent,
				        Gtk.MessageType.Error,
				        Gtk.ButtonsType.Ok,
				        msg,
				        error_message);
				msg_dialog.Run ();
				msg_dialog.Destroy ();
			dialog.Destroy ();
			Logger.Error (error_message);
		}

		/// <summary>
		/// Determines the relative path between two exported files, can optionally be used
		/// by the subclass.
		/// </summary>
		/// <param name="title_from">
		/// The note we're finding the relative path from.
		/// </param>
		/// <param name="title_to">
		/// The title of the note we're finding the relative path to.
		/// </param>
		/// <returns>
		/// A <see cref="System.String"/>
		/// </returns>
		public string ResolveRelativePath (Note note_from, string title_to)
		{
			NoteManager manager = Tomboy.DefaultNoteManager;
			Note note_to = manager.Find (title_to);
			string title_from = SanitizeNoteTitle (note_from.Title);
			title_to = SanitizeNoteTitle (note_to.Title);

			if (exporting_single_notebook) {
				//If there is only one notebook being exported
				if (NotebookManager.GetNotebookFromNote (note_from) == NotebookManager.GetNotebookFromNote (note_to)) {
					return title_to + "." + export_file_suffix;
				} else {
					return "";
				}
			} else {
				//If all notebooks are available
				if (NotebookManager.GetNotebookFromNote (note_from) == NotebookManager.GetNotebookFromNote (note_to)) {
					//Both notes are in the same notebook
					return title_to + "." + export_file_suffix;
				} else {
					//Unfiled notes are a special case because they're in the root directory and will
					// throw an exception from the notebookmanager
					string notebook_from;
					string notebook_to;
					try {
						notebook_from = NotebookManager.GetNotebookFromNote (note_from).NormalizedName;
					} catch (Exception ex) {
						notebook_from = "___NotebookManager___UnfiledNotes__Notebook___"; //TODO: Ugly!
					}
					try {
						notebook_to = NotebookManager.GetNotebookFromNote (note_to).NormalizedName;
					} catch (Exception ex) {
						notebook_to = "___NotebookManager___UnfiledNotes__Notebook___";
					}

					if (notebook_to == "___NotebookManager___UnfiledNotes__Notebook___") {
						return ".." + System.IO.Path.DirectorySeparatorChar + title_to + "." + export_file_suffix;
					} else if (notebook_from == "___NotebookManager___UnfiledNotes__Notebook___") {
						return SanitizePath (notebook_to) + System.IO.Path.DirectorySeparatorChar
							+ title_to + "." + export_file_suffix;
					} else {
						return ".." + System.IO.Path.DirectorySeparatorChar + SanitizePath (notebook_to)
							+ System.IO.Path.DirectorySeparatorChar + title_to + "." + export_file_suffix;
					}
				}
			}
		}
	}

	/// <summary>
	/// A utility class for choosing where to export to.
	/// </summary>
	public class ExportMultipleDialog : Gtk.FileChooserDialog
	{

		public ExportMultipleDialog (string default_folder, string export_type_name) :
			// Translators: {0} is the export type, e.g. HTML
			base (String.Format (Catalog.GetString ("Create destination folder for {0} export"), export_type_name),
				null, Gtk.FileChooserAction.Save, new object[] {})
		//Using action Save insted of CreateFolder because of Win32 issue
		{
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			AddButton (Gtk.Stock.Save, Gtk.ResponseType.Ok);

			DefaultResponse = Gtk.ResponseType.Ok;
			DoOverwriteConfirmation = true;
			LocalOnly = true;

			ShowAll ();

			LoadPreferences (default_folder);
		}

		//Using the same directory prefs as a single note export.
		public void SavePreferences ()
		{
			string dir = System.IO.Path.GetDirectoryName (Filename);
			Preferences.Set (Preferences.EXPORTHTML_LAST_DIRECTORY, dir);
		}

		protected void LoadPreferences (string default_folder)
		{
			string last_dir = (string) Preferences.Get (Preferences.EXPORTHTML_LAST_DIRECTORY);
			if (last_dir == "")
				last_dir = Environment.GetEnvironmentVariable ("HOME");
			SetCurrentFolder (last_dir);
			CurrentName = default_folder;
		}
	}
}
