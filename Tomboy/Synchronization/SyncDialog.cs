using System;
using System.Collections.Generic;
using Mono.Unix;

using Gtk;

namespace Tomboy.Sync
{
	public class SyncDialog : Gtk.Dialog, ISyncUI
	{
		private Gtk.Image image;
		private Gtk.Label headerLabel;
		private Gtk.Label messageLabel;
		private Gtk.ProgressBar progressBar;
		private Gtk.Label progressLabel;

		private Gtk.Expander expander;
		private Gtk.Button closeButton;
		private uint progressBarTimeoutId;

		private Gtk.TreeStore model;

		// TODO: Possible to make Tomboy not crash if quit while dialog is up?
		public SyncDialog ()
: base (string.Empty,
		        null,
		        Gtk.DialogFlags.DestroyWithParent)
		{
			progressBarTimeoutId = 0;

			SetSizeRequest (400, -1);
//			HasSeparator = false;

			// Outer box. Surrounds all of our content.
			VBox outerVBox = new VBox (false, 12);
			outerVBox.BorderWidth = 6;
			outerVBox.Show ();
			ContentArea.PackStart (outerVBox, true, true, 0);

			// Top image and label
			HBox hbox = new HBox (false, 12);
			hbox.Show ();
			outerVBox.PackStart (hbox, false, false, 0);

			image = new Image (GuiUtils.GetIcon ("tomboy", 48));
			image.Xalign = 0;
			image.Yalign = 0;
			image.Show ();
			hbox.PackStart (image, false, false, 0);

			// Label header and message
			VBox vbox = new VBox (false, 6);
			vbox.Show ();
			hbox.PackStart (vbox, true, true, 0);

			headerLabel = new Label ();
			headerLabel.UseMarkup = true;
			headerLabel.Xalign = 0;
			headerLabel.UseUnderline = false;
			headerLabel.LineWrap = true;
			headerLabel.Show ();
			vbox.PackStart (headerLabel, false, false, 0);

			messageLabel = new Label ();
			messageLabel.Xalign = 0;
			messageLabel.UseUnderline = false;
			messageLabel.LineWrap = true;
			messageLabel.SetSizeRequest (250, -1);
			messageLabel.Show ();
			vbox.PackStart (messageLabel, false, false, 0);

			progressBar = new Gtk.ProgressBar ();
			progressBar.Orientation = Gtk.Orientation.Horizontal;
//			progressBar.BarStyle = ProgressBarStyle.Continuous;
//			progressBar.ActivityBlocks = 30;
			progressBar.Show ();
			outerVBox.PackStart (progressBar, false, false, 0);

			progressLabel = new Label ();
			progressLabel.UseMarkup = true;
			progressLabel.Xalign = 0;
			progressLabel.UseUnderline = false;
			progressLabel.LineWrap = true;
			progressLabel.Wrap = true;
			progressLabel.Show ();
			outerVBox.PackStart (progressLabel, false, false, 0);

			// Expander containing TreeView
			expander = new Gtk.Expander (Catalog.GetString ("Details"));
			expander.Spacing = 6;
			expander.Activated += OnExpanderActivated;
			expander.Show ();
			outerVBox.PackStart (expander, true, true, 0);

			// Contents of expander
			Gtk.VBox expandVBox = new Gtk.VBox ();
			expandVBox.Show ();
			expander.Add (expandVBox);

			// Scrolled window around TreeView
			Gtk.ScrolledWindow scrolledWindow = new Gtk.ScrolledWindow ();
			scrolledWindow.ShadowType = Gtk.ShadowType.In;
			scrolledWindow.SetSizeRequest (-1, 200);
			scrolledWindow.Show ();
			expandVBox.PackStart (scrolledWindow, true, true, 0);

			// Create model for TreeView
			model = new Gtk.TreeStore (typeof (string), typeof (string));

			// Create TreeView, attach model
			Gtk.TreeView treeView = new Gtk.TreeView ();
			treeView.Model = model;
			treeView.RowActivated += OnRowActivated;
			treeView.Show ();
			scrolledWindow.Add (treeView);

			// Set up TreeViewColumns
			Gtk.TreeViewColumn column = new Gtk.TreeViewColumn (
			        Catalog.GetString ("Note Title"),
			        new Gtk.CellRendererText (), "text", 0);
			column.SortColumnId = 0;
			column.Resizable = true;
			treeView.AppendColumn (column);

			column = new Gtk.TreeViewColumn (
			        Catalog.GetString ("Status"),
			        new Gtk.CellRendererText (), "text", 1);
			column.SortColumnId = 1;
			column.Resizable = true;
			treeView.AppendColumn (column);

			// Button to close dialog.
			closeButton = (Gtk.Button) AddButton (Gtk.Stock.Close, Gtk.ResponseType.Close);
			closeButton.Sensitive = false;
		}

		public override void Destroy ()
		{
			base.Destroy ();
		}

		protected override void OnRealized ()
		{
			base.OnRealized ();

			SyncState state = SyncManager.State;
			if (state == SyncState.Idle) {
				// Kick off a timer to keep the progress bar going
				progressBarTimeoutId = GLib.Timeout.Add (500, OnPulseProgressBar);

				// Kick off a new synchronization
				SyncManager.PerformSynchronization (this);
			} else {
				// Adjust the GUI accordingly
				SyncStateChanged (state);
			}
		}

		private void OnExpanderActivated (object sender, EventArgs e)
		{
			if (expander.Expanded)
				this.Resizable = true;
			else
				this.Resizable = false;
		}

		void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			// TODO: Store GUID hidden in model; use instead of title
			Gtk.TreeIter iter;
			if (!model.GetIter (out iter, args.Path))
				return;

			string noteTitle = (string) model.GetValue (iter, 0 /* note title */);

			Note note = Tomboy.DefaultNoteManager.Find (noteTitle);
			if (note != null)
				note.Window.Present ();
		}

		public string HeaderText
		{
			set {
				headerLabel.Markup = string.Format (
				        "<span size=\"large\" weight=\"bold\">{0}</span>",
				        value);
			}
		}

		public string MessageText
		{
			set {
				messageLabel.Text = value;
			}
		}

		public string ProgressText
		{
			get {
				return progressLabel.Text;
			}
			set {
				progressLabel.Markup =
				        string.Format ("<span style=\"italic\">{0}</span>",
				                       value);
			}
		}

		public void AddUpdateItem (string title, string status)
		{
			model.AppendValues (title, status);
		}

		#region Private Event Handlers
		bool OnPulseProgressBar ()
		{
			if (SyncManager.State == SyncState.Idle)
				return false;

			progressBar.Pulse ();

			// Return true to keep things going well
			return true;
		}
		#endregion // Private Event Handlers

		#region ISyncUI Members
		public void SyncStateChanged (SyncState state)
		{
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			Gtk.Application.Invoke (delegate {
				// FIXME: Change these strings to be user-friendly
				switch (state) {
				case SyncState.AcquiringLock:
					ProgressText = Catalog.GetString ("Acquiring sync lock...");
					break;
				case SyncState.CommittingChanges:
					ProgressText = Catalog.GetString ("Committing changes...");
					break;
				case SyncState.Connecting:
					Title = Catalog.GetString ("Synchronizing Notes");
					HeaderText = Catalog.GetString ("Synchronizing your notes...");
					MessageText = Catalog.GetString ("This may take a while, kick back and enjoy!");
					model.Clear ();
					ProgressText = Catalog.GetString ("Connecting to the server...");
					progressBar.Fraction = 0;
					progressBar.Show ();
					progressLabel.Show ();
					break;
				case SyncState.DeleteServerNotes:
					ProgressText = Catalog.GetString ("Deleting notes off of the server...");
					progressBar.Pulse ();
					break;
				case SyncState.Downloading:
					ProgressText = Catalog.GetString ("Downloading new/updated notes...");
					progressBar.Pulse ();
					break;
				case SyncState.Idle:
					GLib.Source.Remove (progressBarTimeoutId);
					progressBarTimeoutId = 0;
					progressBar.Fraction = 0;
					progressBar.Hide ();
					progressLabel.Hide ();
					closeButton.Sensitive = true;
					break;
				case SyncState.Locked:
					Title = Catalog.GetString ("Server Locked");
					HeaderText = Catalog.GetString ("Server is locked");
					MessageText = Catalog.GetString ("One of your other computers is currently synchronizing.  Please wait 2 minutes and try again.");
					ProgressText = string.Empty;
					break;
				case SyncState.PrepareDownload:
					ProgressText = Catalog.GetString ("Preparing to download updates from server...");
					break;
				case SyncState.PrepareUpload:
					ProgressText = Catalog.GetString ("Preparing to upload updates to server...");
					break;
				case SyncState.Uploading:
					ProgressText = Catalog.GetString ("Uploading notes to server...");
					break;
				case SyncState.Failed:
					Title = Catalog.GetString ("Synchronization Failed");
					HeaderText = Catalog.GetString ("Failed to synchronize");
					MessageText = Catalog.GetString ("Could not synchronize notes.  Check the details below and try again.");
					ProgressText = string.Empty;
					break;
				case SyncState.Succeeded:
					int count = 0;
					count += model.IterNChildren ();
					Title = Catalog.GetString ("Synchronization Complete");
					HeaderText = Catalog.GetString ("Synchronization is complete");
					string numNotesUpdated =
					        string.Format (Catalog.GetPluralString ("{0} note updated.",
					                                                "{0} notes updated.",
					                                                count),
					                       count);
					MessageText = numNotesUpdated + "  " +
					              Catalog.GetString ("Your notes are now up to date.");
					ProgressText = string.Empty;
					break;
				case SyncState.UserCancelled:
					Title = Catalog.GetString ("Synchronization Canceled");
					HeaderText = Catalog.GetString ("Synchronization was canceled");
					MessageText = Catalog.GetString ("You canceled the synchronization.  You may close the window now.");
					ProgressText = string.Empty;
					break;
				case SyncState.NoConfiguredSyncService:
					Title = Catalog.GetString ("Synchronization Not Configured");
					HeaderText = Catalog.GetString ("Synchronization is not configured");
					MessageText = Catalog.GetString ("Please configure synchronization in the preferences dialog.");
					ProgressText = string.Empty;
					break;
				case SyncState.SyncServerCreationFailed:
					Title = Catalog.GetString ("Synchronization Service Error");
					HeaderText = Catalog.GetString ("Service error");
					MessageText = Catalog.GetString ("Error connecting to the synchronization service.  Please try again.");
					ProgressText = string.Empty;
					break;
				}
			});
		}

		public void NoteSynchronized (string noteTitle, NoteSyncType type)
		{
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			Gtk.Application.Invoke (delegate {
				// FIXME: Change these strings to be more user-friendly
				// TODO: Update status for a note when status changes ("Uploading" -> "Uploaded", etc)
				string statusText = string.Empty;
				switch (type) {
				case NoteSyncType.DeleteFromClient:
					statusText = Catalog.GetString ("Deleted locally");
					break;
				case NoteSyncType.DeleteFromServer:
					statusText = Catalog.GetString ("Deleted from server");
					break;
				case NoteSyncType.DownloadModified:
					statusText = Catalog.GetString ("Updated");
					break;
				case NoteSyncType.DownloadNew:
					statusText = Catalog.GetString ("Added");
					break;
				case NoteSyncType.UploadModified:
					statusText = Catalog.GetString ("Uploaded changes to server");
					break;
				case NoteSyncType.UploadNew:
					statusText = Catalog.GetString ("Uploaded new note to server");
					break;
				}
				AddUpdateItem (noteTitle, statusText);
			});
		}

		public void NoteConflictDetected (NoteManager manager,
		                             Note localConflictNote,
		                             NoteUpdate remoteNote,
		                             IList<string> noteUpdateTitles)
		{
			SyncTitleConflictResolution savedBehavior = SyncTitleConflictResolution.Cancel;
			object dlgBehaviorPref = Preferences.Get (Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR);
			if (dlgBehaviorPref != null && dlgBehaviorPref is int) // TODO: Check range of this int
				savedBehavior = (SyncTitleConflictResolution)dlgBehaviorPref;

			SyncTitleConflictResolution resolution = SyncTitleConflictResolution.OverwriteExisting;
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			// To be consistent, any exceptions in the delgate will be caught
			// and then rethrown in the synchronization thread.
			Exception mainThreadException = null;
			Gtk.Application.Invoke (delegate {
				try {
					SyncTitleConflictDialog conflictDlg =
					new SyncTitleConflictDialog (localConflictNote, noteUpdateTitles);
					Gtk.ResponseType reponse = Gtk.ResponseType.Ok;

					bool noteSyncBitsMatch =
					        SyncManager.SynchronizedNoteXmlMatches (localConflictNote.GetCompleteNoteXml (),
					                                                remoteNote.XmlContent);

					// If the synchronized note content is in conflict
					// and there is no saved conflict handling behavior, show the dialog
					if (!noteSyncBitsMatch && savedBehavior == 0)
						reponse = (Gtk.ResponseType) conflictDlg.Run ();


					if (reponse == Gtk.ResponseType.Cancel)
						resolution = SyncTitleConflictResolution.Cancel;
					else {
						if (noteSyncBitsMatch)
							resolution = SyncTitleConflictResolution.OverwriteExisting;
						else if (savedBehavior == 0)
							resolution = conflictDlg.Resolution;
						else
							resolution = savedBehavior;

						switch (resolution) {
						case SyncTitleConflictResolution.OverwriteExisting:
							if (conflictDlg.AlwaysPerformThisAction)
								savedBehavior = resolution;
							// No need to delete if sync will overwrite
							if (localConflictNote.Id != remoteNote.UUID)
								manager.Delete (localConflictNote);
							break;
						case SyncTitleConflictResolution.RenameExistingAndUpdate:
							if (conflictDlg.AlwaysPerformThisAction)
								savedBehavior = resolution;
							RenameNote (localConflictNote, conflictDlg.RenamedTitle, true);
							break;
						case SyncTitleConflictResolution.RenameExistingNoUpdate:
							if (conflictDlg.AlwaysPerformThisAction)
								savedBehavior = resolution;
							RenameNote (localConflictNote, conflictDlg.RenamedTitle, false);
							break;
						}
					}

					Preferences.Set (Preferences.SYNC_CONFIGURED_CONFLICT_BEHAVIOR,
					                 (int) savedBehavior); // TODO: Clean up

					conflictDlg.Hide ();
					conflictDlg.Destroy ();

					// Let the SyncManager continue
					SyncManager.ResolveConflict (/*localConflictNote, */resolution);
				} catch (Exception e) {
					mainThreadException = e;
				}
			});
			if (mainThreadException != null)
				throw mainThreadException;
		}
		#endregion	// ISyncUI Members

		#region Private Methods
		// TODO: This appears to add <link:internal> around the note title
		//       in the content.
		private void RenameNote (Note note, string newTitle, bool updateReferencingNotes)
		{
			string oldTitle = note.Title;
			// Rename the note (skip for now...never using updateReferencingNotes option)
			//if (updateReferencingNotes) // NOTE: This might never work, or lead to a ton of conflicts
			// note.Title = newTitle;
			//else
			// note.RenameWithoutLinkUpdate (newTitle);
			//string oldContent = note.XmlContent;
			//note.XmlContent = NoteArchiver.Instance.GetRenamedNoteXml (oldContent, oldTitle, newTitle);

			// Preserve note information
			note.Save (); // Write to file
			bool noteOpen = note.IsOpened;
			string newContent = //note.XmlContent;
			        NoteArchiver.Instance.GetRenamedNoteXml (note.XmlContent, oldTitle, newTitle);
			string newCompleteContent = //note.GetCompleteNoteXml ();
			        NoteArchiver.Instance.GetRenamedNoteXml (note.GetCompleteNoteXml (), oldTitle, newTitle);
			//Logger.Debug ("RenameNote: newContent: " + newContent);
			//Logger.Debug ("RenameNote: newCompleteContent: " + newCompleteContent);

			// We delete and recreate the note to simplify content conflict handling
			Tomboy.DefaultNoteManager.Delete (note);

			// Create note with old XmlContent just in case GetCompleteNoteXml failed
			Logger.Debug ("RenameNote: about to create " + newTitle);
			Note renamedNote = Tomboy.DefaultNoteManager.Create (newTitle, newContent);
			if (newCompleteContent != null) {// TODO: Anything to do if it is null?
				try {
					renamedNote.LoadForeignNoteXml (newCompleteContent, ChangeType.OtherDataChanged);
				} catch {} // TODO: Handle exception in case that newCompleteContent is invalid XML
			}
		if (noteOpen)
				renamedNote.Window.Present ();
		}
		#endregion // Private Methods

	}


	public class SyncTitleConflictDialog : Gtk.Dialog
	{
		private Note existingNote;
		private IList<string> noteUpdateTitles;

		private Gtk.Button continueButton;

		private Gtk.Entry renameEntry;
		private Gtk.CheckButton renameUpdateCheck;
		private Gtk.RadioButton renameRadio;
		private Gtk.RadioButton deleteExistingRadio;
		private Gtk.CheckButton alwaysDoThisCheck;

		private Gtk.Label headerLabel;
		private Gtk.Label messageLabel;

public SyncTitleConflictDialog (Note existingNote, IList<string> noteUpdateTitles) :
		base (Catalog.GetString ("Note Conflict"), null, Gtk.DialogFlags.Modal)
		{
			this.existingNote = existingNote;
			this.noteUpdateTitles = noteUpdateTitles;

			// Suggest renaming note by appending " (old)" to the existing title
			string suggestedRenameBase = existingNote.Title + Catalog.GetString (" (old)");
			string suggestedRename = suggestedRenameBase;
			for (int i = 1; !IsNoteTitleAvailable (suggestedRename); i++)
				suggestedRename = suggestedRenameBase + " " + i.ToString();

			VBox outerVBox = new VBox (false, 12);
			outerVBox.BorderWidth = 12;
			outerVBox.Spacing = 8;

			HBox hbox = new HBox (false, 8);
			Image image = new Image (GuiUtils.GetIcon (Gtk.Stock.DialogWarning, 48)); // TODO: Is this the right icon?
			image.Show ();
			hbox.PackStart (image, false, false, 0);

			VBox vbox = new VBox (false, 8);

			headerLabel = new Label ();
			headerLabel.UseMarkup = true;
			headerLabel.Xalign = 0;
			headerLabel.UseUnderline = false;
			headerLabel.Show ();
			vbox.PackStart (headerLabel, false, false, 0);

			messageLabel = new Label ();
			messageLabel.Xalign = 0;
			messageLabel.UseUnderline = false;
			messageLabel.LineWrap = true;
			messageLabel.Wrap = true;
			messageLabel.Show ();
			vbox.PackStart (messageLabel, false, false, 0);

			vbox.Show ();
			hbox.PackStart (vbox, true, true, 0);

			hbox.Show ();
			outerVBox.PackStart (hbox, false, false, 0);
			ContentArea.PackStart (outerVBox, false, false, 0);

			Gtk.HBox renameHBox = new Gtk.HBox ();
			renameRadio = new Gtk.RadioButton (Catalog.GetString ("Rename local note:"));
			renameRadio.Toggled += radio_Toggled;
			Gtk.VBox renameOptionsVBox = new Gtk.VBox ();

			renameEntry = new Gtk.Entry (suggestedRename);
			renameEntry.Changed += renameEntry_Changed;
			renameUpdateCheck = new Gtk.CheckButton (Catalog.GetString ("Update links in referencing notes"));
			renameOptionsVBox.PackStart (renameEntry, false, false, 0);
			//renameOptionsVBox.PackStart (renameUpdateCheck); // This seems like a superfluous option
			renameHBox.PackStart (renameRadio, false, false, 0);
			renameHBox.PackStart (renameOptionsVBox, false, false, 0);
			ContentArea.PackStart (renameHBox, false, false, 0);

			deleteExistingRadio = new Gtk.RadioButton (renameRadio, Catalog.GetString ("Overwrite local note"));
			deleteExistingRadio.Toggled += radio_Toggled;
			ContentArea.PackStart (deleteExistingRadio, false, false, 0);

			alwaysDoThisCheck = new Gtk.CheckButton (Catalog.GetString ("Always perform this action"));
			ContentArea.PackStart (alwaysDoThisCheck, false, false, 0);

			continueButton = (Gtk.Button) AddButton (Gtk.Stock.GoForward, Gtk.ResponseType.Accept);

			// Set initial dialog text
			HeaderText = Catalog.GetString ("Note conflict detected");
			MessageText = string.Format (Catalog.GetString ("The server version of \"{0}\" conflicts with your local note."
			                             + "  What do you want to do with your local note?"),
			                             existingNote.Title);

			ShowAll ();
		}

		private void renameEntry_Changed (object sender, System.EventArgs e)
		{
			if (renameRadio.Active &&
			                !IsNoteTitleAvailable (RenamedTitle))
				continueButton.Sensitive = false;
			else
				continueButton.Sensitive = true;
		}

		private bool IsNoteTitleAvailable (string renamedTitle)
		{
			return !noteUpdateTitles.Contains (renamedTitle) &&
			       existingNote.Manager.Find (renamedTitle) == null;
		}

		// Handler for each radio button's Toggled event
		private void radio_Toggled (object sender, System.EventArgs e)
		{
			// Make sure Continue button has the right sensitivity
			renameEntry_Changed (renameEntry, null);

			// Update sensitivity of rename-related widgets
			renameEntry.Sensitive = renameRadio.Active;
			renameUpdateCheck.Sensitive = renameRadio.Active;
		}

		public string HeaderText
		{
			set {
				headerLabel.Markup = string.Format (
				        "<span size=\"large\" weight=\"bold\">{0}</span>",
				        value);
			}
		}

		public string MessageText
		{
			set {
				messageLabel.Text = value;
			}
		}

		public string RenamedTitle
		{
			get {
				return renameEntry.Text;
			}
		}

		public bool AlwaysPerformThisAction
		{
			get {
				return alwaysDoThisCheck.Active;
			}
		}

		public SyncTitleConflictResolution Resolution
		{
			get
			{
				if (renameRadio.Active) {
					if (renameUpdateCheck.Active)
						return SyncTitleConflictResolution.RenameExistingAndUpdate;
					else
						return SyncTitleConflictResolution.RenameExistingNoUpdate;
				}
				else
					return SyncTitleConflictResolution.OverwriteExisting;
			}
		}
	}

	// NOTE: These enum int values are used to save the default behavior for this dialog
	public enum SyncTitleConflictResolution
	{
		Cancel = 0,
		OverwriteExisting = 1,
		RenameExistingNoUpdate = 2,
		RenameExistingAndUpdate = 3 // Hidden option, not exposed in UI
	}
}
