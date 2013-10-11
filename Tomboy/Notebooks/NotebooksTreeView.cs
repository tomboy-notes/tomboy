using System;
//using Mono.Unix; // TODO: Add this file to the POTFILES.in if Catalog.GetString is used here
using Tomboy;

namespace Tomboy.Notebooks
{
	public class NotebooksTreeView : Gtk.TreeView
	{
		NoteManager noteManager;
		
		public NotebooksTreeView(Gtk.ITreeModel model) : base (model)
		{
			noteManager = Tomboy.DefaultNoteManager;
			
			// Set up the notebooksTree as a drag target so that notes
			// can be dragged into the notebook.
			Gtk.TargetEntry [] targets =
				new Gtk.TargetEntry [] {
					new Gtk.TargetEntry ("text/uri-list",
					Gtk.TargetFlags.App,
					1)
				};

			Gtk.Drag.DestSet (this,
							  Gtk.DestDefaults.All,
							  targets,
							  Gdk.DragAction.Move);
		}
		
		protected override void OnDragDataReceived (Gdk.DragContext context,
													int x, int y,
													Gtk.SelectionData selectionData,
													uint info, uint time_)
		{
			UriList uriList = new UriList (selectionData);
			if (uriList.Count == 0) {
				Gtk.Drag.Finish (context, false, false, time_);
				return;
			}
			
			Gtk.TreePath path;
			Gtk.TreeViewDropPosition pos;
			if (GetDestRowAtPos (x, y, out path, out pos) == false) {
				Gtk.Drag.Finish (context, false, false, time_);
				return;
			}
			
			Gtk.TreeIter iter;
			if (Model.GetIter (out iter, path) == false) {
				Gtk.Drag.Finish (context, false, false, time_);
				return;
			}
			
			Notebook destNotebook = Model.GetValue (iter, 0) as Notebook;
			if (destNotebook is AllNotesNotebook) {
				Gtk.Drag.Finish (context, false, false, time_);
				return;
			}
			
			foreach (Uri uri in uriList) {
				Note note = noteManager.FindByUri (uri.ToString ());
				if (note == null)
					continue;
				
				Logger.Debug ("Dropped into notebook: {0}", note.Title);
				
				// TODO: If we ever support selecting multiple notes,
				// we may want to double-check to see if there will be
				// any notes are already inside of a notebook.  Do we
				// want to prompt the user to confirm this choice?
				NotebookManager.MoveNoteToNotebook (note, destNotebook);
			}

			Gtk.Drag.Finish (context, true, false, time_);
		}
		
		protected override bool OnDragMotion(Gdk.DragContext context, int x, int y, uint time)
		{
			Gtk.TreePath path;
			Gtk.TreeViewDropPosition pos;
			if (GetDestRowAtPos (x, y, out path, out pos) == false) {
				SetDragDestRow (null, Gtk.TreeViewDropPosition.IntoOrAfter);
				return false;
			}
			
			Gtk.TreeIter iter;
			if (Model.GetIter (out iter, path) == false) {
				SetDragDestRow (null, Gtk.TreeViewDropPosition.IntoOrAfter);
				return false;
			}
			
			Notebook destNotebook = Model.GetValue (iter, 0) as Notebook;
			if (destNotebook is AllNotesNotebook) {
				SetDragDestRow (null, Gtk.TreeViewDropPosition.IntoOrAfter);
				return true;
			}
			
			SetDragDestRow (path, Gtk.TreeViewDropPosition.IntoOrAfter);
			
			return true;
		}
		
		protected override void OnDragLeave (Gdk.DragContext context, uint time_)
		{
			SetDragDestRow (null, Gtk.TreeViewDropPosition.IntoOrAfter);
		}
	}
}
