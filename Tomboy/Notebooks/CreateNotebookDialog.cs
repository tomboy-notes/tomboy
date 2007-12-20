using System;
using Mono.Unix;
using Tomboy;

namespace Tomboy.Notebooks
{
	public class CreateNotebookDialog : HIGMessageDialog
	{
		Gtk.Entry nameEntry;
		
		public CreateNotebookDialog(Gtk.Window parent,
									Gtk.DialogFlags flags)
				: base (parent, flags, Gtk.MessageType.Other,
						Gtk.ButtonsType.None,
						Catalog.GetString ("Create a new notebook"),
						Catalog.GetString ("Type the name of the notebook you'd like to create."))
		{
			Gtk.HBox hbox = new Gtk.HBox (false, 6);
			
			Gtk.Label label = new Gtk.Label (Catalog.GetString ("N_otebook name:"));
			label.Xalign = 0;
			label.UseUnderline = true;
			label.Show ();
			
			nameEntry = new Gtk.Entry ();
			nameEntry.Changed += OnNameEntryChanged;
			nameEntry.ActivatesDefault = true;
			nameEntry.Show ();
			label.MnemonicWidget = nameEntry;
			
			hbox.PackStart (label, false, false, 0);
			hbox.PackStart (nameEntry, true, true, 0);
			hbox.Show ();
			
			ExtraWidget = hbox;
			
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, false);
			AddButton (Gtk.Stock.New, Gtk.ResponseType.Ok, true);
			
			// Only let the Ok response be sensitive when
			// there's something in nameEntry
			SetResponseSensitive (Gtk.ResponseType.Ok, false);
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
		private void OnNameEntryChanged (object sender, EventArgs args)
		{
			SetResponseSensitive (
					Gtk.ResponseType.Ok,
					NotebookName == string.Empty ? false : true);
		}
	}
}
