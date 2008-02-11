using System;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class CreateNotebookDialog : HIGMessageDialog
	{
		Gtk.Entry nameEntry;
		Gtk.Label errorLabel;
		static Gdk.Pixbuf newNotebookIcon;
		static Gdk.Pixbuf newNotebookIconDialog;

		static CreateNotebookDialog ()
		{
			newNotebookIcon = GuiUtils.GetIcon ("notebook-new", 16);
			newNotebookIconDialog = GuiUtils.GetIcon ("notebook-new", 48);
		}
		
		public CreateNotebookDialog(Gtk.Window parent,
									Gtk.DialogFlags flags)
				: base (parent, flags, Gtk.MessageType.Info,
						Gtk.ButtonsType.None,
						Catalog.GetString ("Create a new notebook"),
						Catalog.GetString ("Type the name of the notebook you'd like to create."))
		{
			this.Pixbuf = newNotebookIconDialog;
			
			Gtk.Table table = new Gtk.Table (2, 2, false);
			
			Gtk.Label label = new Gtk.Label (Catalog.GetString ("N_otebook name:"));
			label.Xalign = 0;
			label.UseUnderline = true;
			label.Show ();
			
			nameEntry = new Gtk.Entry ();
			nameEntry.Changed += OnNameEntryChanged;
			nameEntry.ActivatesDefault = true;
			nameEntry.Show ();
			label.MnemonicWidget = nameEntry;
			
			errorLabel = new Gtk.Label ();
			errorLabel.Xalign = 0;
			errorLabel.Markup = string.Format("<span foreground='red' style='italic'>{0}</span>",
			                                  Catalog.GetString ("Name already taken"));
			
			table.Attach (label, 0, 1, 0, 1);
			table.Attach (nameEntry, 1, 2, 0, 1);
			table.Attach (errorLabel, 1, 2, 1, 2);
			table.Show ();
			
			ExtraWidget = table;
			
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, false);
			AddButton (
				newNotebookIcon,
				// Translation note: This is the Create button in the Create
				// New Note Dialog.
				Catalog.GetString ("C_reate"),
				Gtk.ResponseType.Ok,
				true);
			
			// Only let the Ok response be sensitive when
			// there's something in nameEntry
			SetResponseSensitive (Gtk.ResponseType.Ok, false);
			errorLabel.Hide ();
		}
		
		public string NotebookName
		{
			get {
				string name = nameEntry.Text;
				return name.Trim ();
			}
			set {
				if (value == null || value.Trim () == string.Empty) {
					nameEntry.Text = string.Empty;
				} else {
					nameEntry.Text = value.Trim ();
				}
			}
		}
		
		// Enable the Ok response only if there's text in the nameEntry
		// and the Notebook's name hasn't already been taken
		private void OnNameEntryChanged (object sender, EventArgs args)
		{
			bool nameTaken = false;
			if (Notebooks.NotebookManager.NotebookExists (NotebookName)) {
				errorLabel.Show ();
				nameTaken = true;
			} else {
				errorLabel.Hide ();
			}
			
			SetResponseSensitive (
					Gtk.ResponseType.Ok,
					NotebookName == string.Empty || nameTaken ? false : true);
		}
	}
}
