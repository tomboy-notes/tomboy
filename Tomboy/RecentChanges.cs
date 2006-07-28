
using System;
using Mono.Unix;
using System.Text;

namespace Tomboy
{
	public class NoteRecentChanges : Gtk.Window
	{
		NoteManager manager;

		Gtk.AccelGroup accel_group;
		Gtk.Label note_count;
		Gtk.Button close_button;
		Gtk.ScrolledWindow matches_window;
		Gtk.VBox content_vbox;

		Gtk.TreeView tree;
		Gtk.ListStore store;

		static Type [] column_types = 
			new Type [] {
				typeof (Gdk.Pixbuf), // icon
				typeof (string),     // title
				typeof (string),     // change date
				typeof (Note),       // note
			};

		static Gdk.Pixbuf note_icon;

		static NoteRecentChanges ()
		{
			note_icon = GuiUtils.GetIcon ("tomboy", 22);
		}

		public NoteRecentChanges (NoteManager manager)
			: base (Catalog.GetString ("Table of Contents"))
		{
			this.manager = manager;
			this.IconName = "tomboy";
			this.DefaultWidth = 200;

			// For Escape (Close)
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			Gtk.Image image = new Gtk.Image (Gtk.Stock.SortAscending, 
							 Gtk.IconSize.Dialog);

			Gtk.Label label = new Gtk.Label (Catalog.GetString (
				"<b>Table of Contents</b> lists all your notes.\n" +
				"Double click to open a note."));
			label.UseMarkup = true;
			label.Wrap = true;

			Gtk.HBox hbox = new Gtk.HBox (false, 2);
			hbox.BorderWidth = 8;
			hbox.PackStart (image, false, false, 4);
			hbox.PackStart (label, false, false, 0);
			hbox.ShowAll ();

			MakeRecentTree ();
			tree.Show ();

			note_count = new Gtk.Label ();
			note_count.Show ();

			// Update on changes to notes
			manager.NoteDeleted += OnNotesChanged;
			manager.NoteAdded += OnNotesChanged;
			manager.NoteRenamed += OnNoteRenamed;

			// List all the current notes
			UpdateResults ();

			matches_window = new Gtk.ScrolledWindow ();
			matches_window.ShadowType = Gtk.ShadowType.In;

			// Reign in the window size if there are notes with long
			// names, or a lot of notes...

			Gtk.Requisition tree_req = tree.SizeRequest ();
			if (tree_req.Height > 420)
				matches_window.HeightRequest = 420;
			else
				matches_window.VscrollbarPolicy = Gtk.PolicyType.Never;

			if (tree_req.Width > 480)
				matches_window.WidthRequest = 480;
			else
				matches_window.HscrollbarPolicy = Gtk.PolicyType.Never;

			matches_window.Add (tree);
			matches_window.Show ();

			close_button = new Gtk.Button (Gtk.Stock.Close);
			close_button.Clicked += CloseClicked;
			close_button.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.Escape, 
						     0,
						     Gtk.AccelFlags.Visible);
			close_button.Show ();

			Gtk.HButtonBox button_box = new Gtk.HButtonBox ();
			button_box.Layout = Gtk.ButtonBoxStyle.Edge;
			button_box.Spacing = 8;
			button_box.PackStart (note_count);
			button_box.PackStart (close_button);
			button_box.Show ();

			content_vbox = new Gtk.VBox (false, 8);
			content_vbox.BorderWidth = 6;
			content_vbox.PackStart (hbox, false, false, 0);
			content_vbox.PackStart (matches_window);
			content_vbox.PackStart (button_box, false, false, 0);
			content_vbox.Show ();

			this.Add (content_vbox);
		}

		void MakeRecentTree ()
		{
			Gtk.TargetEntry [] targets = 
				new Gtk.TargetEntry [] {
					new Gtk.TargetEntry ("STRING", 
							     Gtk.TargetFlags.App,
							     0),
					new Gtk.TargetEntry ("text/plain", 
							     Gtk.TargetFlags.App,
							     0),
					new Gtk.TargetEntry ("text/uri-list", 
							     Gtk.TargetFlags.App,
							     1),
				};

			tree = new Gtk.TreeView ();
			tree.HeadersVisible = true;
			tree.RulesHint = true;
			tree.RowActivated += OnRowActivated;
			tree.DragDataGet += OnDragDataGet;
			tree.KeyPressEvent += OnKeyPressed;

			tree.EnableModelDragSource (Gdk.ModifierType.Button1Mask,
						    targets,
						    Gdk.DragAction.Copy);

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn title = new Gtk.TreeViewColumn ();
			title.Title = Catalog.GetString ("Note");
			title.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			title.Resizable = true;
			
			renderer = new Gtk.CellRendererPixbuf ();
			title.PackStart (renderer, false);
			title.AddAttribute (renderer, "pixbuf", 0 /* icon */);

			renderer = new Gtk.CellRendererText ();
			title.PackStart (renderer, true);
			title.AddAttribute (renderer, "text", 1 /* title */);
			title.SortColumnId = 1; /* title */

			tree.AppendColumn (title);

			Gtk.TreeViewColumn change = new Gtk.TreeViewColumn ();
			change.Title = Catalog.GetString ("Last Changed");
			change.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			change.Resizable = true;

			renderer = new Gtk.CellRendererText ();
			renderer.Data ["xalign"] = 1.0;
			change.PackStart (renderer, false);
			change.AddAttribute (renderer, "text", 2 /* change date */);
			change.SortColumnId = 2; /* change date */

			tree.AppendColumn (change);
		}

		void UpdateResults ()
		{
			// FIXME: Restore the currently highlighted note

			int sort_column = 2; /* change date */
			Gtk.SortType sort_type = Gtk.SortType.Descending;
			if (store != null) {
				store.GetSortColumnId (out sort_column, out sort_type);
			}

			store = new Gtk.ListStore (column_types);
			store.SetSortFunc (2 /* change date */,
					   new Gtk.TreeIterCompareFunc (CompareDates));

			int cnt = 0;
			foreach (Note note in manager.Notes) {
				string nice_date = PrettyPrintDate (note.ChangeDate);

				store.AppendValues (note_icon,  /* icon */
						    note.Title, /* title */
						    nice_date,  /* change date */
						    note);      /* note */
				cnt++;
			}

			// Set the sort column after loading data, since we
			// don't want to resort on every append.
			store.SetSortColumnId (sort_column, sort_type);

			tree.Model = store;

			note_count.Text = string.Format (Catalog.GetPluralString("Total: {0} note",
										 "Total: {0} notes",
										 cnt),
							 cnt);
		}

		void OnNotesChanged (object sender, Note changed)
		{
			UpdateResults ();
		}

		void OnNoteRenamed (Note note, string old_title)
		{
			UpdateResults ();
		}

		void OnDragDataGet (object sender, Gtk.DragDataGetArgs args)
		{
			Note note = GetSelectedNote ();
			if (note == null)
				return;

			// FIXME: Gtk.SelectionData has no way to get the
			//        requested target.

			args.SelectionData.Set (Gdk.Atom.Intern ("text/uri-list", false),
						8,
						Encoding.UTF8.GetBytes (note.Uri));

			args.SelectionData.Text = note.Title;
		}

		Note GetSelectedNote ()
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;

			if (!tree.Selection.GetSelected (out model, out iter))
				return null;

			return (Note) model.GetValue (iter, 3 /* note */);
		}

		string PrettyPrintDate (DateTime date)
		{
			DateTime now = DateTime.Now;
			string short_time = date.ToShortTimeString ();

			if (date.Year == now.Year) {
				if (date.DayOfYear == now.DayOfYear)
					return String.Format (Catalog.GetString ("Today, {0}"), 
							      short_time);
				else if (date.DayOfYear == now.DayOfYear - 1)
					return String.Format (Catalog.GetString ("Yesterday, {0}"),
							      short_time);
				else if (date.DayOfYear > now.DayOfYear - 6)
					return String.Format (Catalog.GetString ("{0} days ago, {1}"), 
							      now.DayOfYear - date.DayOfYear,
							      short_time);
				else
					return date.ToString (Catalog.GetString ("MMMM d, h:mm tt"));
			} else
				return date.ToString (Catalog.GetString ("MMMM d yyyy, h:mm tt"));
		}

		void CloseClicked (object sender, EventArgs args)
		{
			Hide ();
			Destroy ();
		}

		int CompareDates (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			Note note_a = (Note) model.GetValue (a, 3 /* note */);
			Note note_b = (Note) model.GetValue (b, 3 /* note */);

			if (note_a == null || note_b == null)
				return -1;
			else
				return DateTime.Compare (note_a.ChangeDate, note_b.ChangeDate);
		}

		void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			Gtk.TreeIter iter;
			if (!store.GetIter (out iter, args.Path)) 
				return;

			Note note = (Note) store.GetValue (iter, 3 /* note */);

			note.Window.Present ();
		}

		void OnKeyPressed (object obj, Gtk.KeyPressEventArgs args)
		{
			Gdk.EventKey eventKey = args.Event;

			if (eventKey.Key == Gdk.Key.Delete && 
			    eventKey.State == 0 /* Gdk.ModifierType.None */) {
				// Get the selected note and prompt for deletion
				Note note = GetSelectedNote();
				if (note == null)
					return;

				NoteUtils.ShowDeletionDialog (note, this);
			}		
		}

	}
}
