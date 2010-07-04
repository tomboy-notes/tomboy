// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//  
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
// Copyright (c) 2010 Sandy Armstrong <sanfordarmstrong@gmail.com>
// 
// Authors:
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
//

using System;
using System.Collections.Generic;
using Mono.Unix;

using Gtk;

namespace Tomboy
{
	public enum NoteRenameBehavior
	{
		AlwaysShowDialog = 0,
		AlwaysRemoveLinks = 1,
		AlwaysRenameLinks = 2
	}

	public class NoteRenameDialog : Gtk.Dialog
	{
		private IList<Note> notes;
		private TreeStore notesModel;
		private RadioButton alwaysShowDlgRadio;
		private RadioButton neverRenameRadio;
		private RadioButton alwaysRenameRadio;

		public NoteRenameDialog (IList<Note> notes, string oldTitle, Note renamedNote) :
			base (Catalog.GetString ("Rename Note Links?"), renamedNote.Window, DialogFlags.NoSeparator)
		{
			this.DefaultResponse = ResponseType.Cancel;
			this.BorderWidth = 10;

			var renameButton = (Button)
				AddButton (Catalog.GetString ("_Rename Links"),
				           ResponseType.Yes);
			var dontRenameButton = (Button)
				AddButton (Catalog.GetString ("_Don't Rename Links"),
				           ResponseType.No);

			this.notes = notes;
			notesModel = new Gtk.TreeStore (typeof (bool), typeof (string), typeof (Note));
			foreach (var note in notes)
				notesModel.AppendValues (true, note.Title, note);

			var labelText = Catalog.GetString ("Rename links in other notes from \"<span underline=\"single\">{0}</span>\" " +
			                                   "to \"<span underline=\"single\">{1}</span>\"?\n\n" +
			                                   "If you do not rename the links, " +
			                                   "they will no longer link to anything.");
			var label = new Label ();
			label.UseMarkup = true;
			label.Markup = String.Format (labelText,
			                              GLib.Markup.EscapeText (oldTitle),
			                              GLib.Markup.EscapeText (renamedNote.Title));
			label.LineWrap = true;
			VBox.PackStart (label, false, true, 5);

			var notesView = new TreeView (notesModel);
			notesView.SetSizeRequest (-1, 200);
			var toggleCell = new CellRendererToggle ();
			toggleCell.Activatable = true;
			var column = new TreeViewColumn (Catalog.GetString ("Rename Links"),
			                                 toggleCell, "active", 0);
			column.SortColumnId = 0;
			column.Resizable = true;
			notesView.AppendColumn (column);
			toggleCell.Toggled += (o, args) => {
				TreeIter iter;
				if (!notesModel.GetIterFromString (out iter, args.Path))
					return;
				bool val = (bool) notesModel.GetValue (iter, 0);
				notesModel.SetValue (iter, 0, !val);
			};
			column = new TreeViewColumn (Catalog.GetString ("Note Title"),
			                             new CellRendererText (), "text", 1);
			column.SortColumnId = 1;
			column.Resizable = true;
			notesView.AppendColumn (column);

			notesView.RowActivated += (o, args) => {
				TreeIter iter;
				if (!notesModel.GetIter (out iter, args.Path))
					return;
				Note note = (Note) notesModel.GetValue (iter, 2);
				if (note != null) {
					note.Window.Present ();
					NoteFindBar find = note.Window.Find;
					find.ShowAll ();
					find.Visible = true;
					find.SearchText = "\"" + oldTitle + "\"";
				}
			};

			var notesBox = new VBox (false, 5);
			var selectAllButton = new Button ();
			// Translators: This button causes all notes in the list to be selected
			selectAllButton.Label = Catalog.GetString ("Select All");
			selectAllButton.Clicked += (o, e) => {
				notesModel.Foreach ((model, path, iter) => {
					notesModel.SetValue (iter, 0, true);
					return false;
				});
			};
			var selectNoneButton = new Button ();
			// Translators: This button causes all notes in the list to be unselected
			selectNoneButton.Label = Catalog.GetString ("Select None");
			selectNoneButton.Clicked += (o, e) => {
				notesModel.Foreach ((model, path, iter) => {
					notesModel.SetValue (iter, 0, false);
					return false;
				});
			};
			var notesButtonBox = new HButtonBox ();
			notesButtonBox.Add (selectNoneButton);
			notesButtonBox.Add (selectAllButton);
			notesButtonBox.Spacing = 5;
			notesButtonBox.LayoutStyle = ButtonBoxStyle.End;
			var notesScroll = new ScrolledWindow ();
			notesScroll.Add (notesView);
			notesBox.PackStart (notesScroll);
			notesBox.PackStart (notesButtonBox, false, true, 0);

			var advancedExpander = new Expander (Catalog.GetString ("Ad_vanced"));
			var expandBox = new VBox ();
			expandBox.PackStart (notesBox);
			alwaysShowDlgRadio = new RadioButton (Catalog.GetString ("Always show this _window"));
			alwaysShowDlgRadio.Clicked += (o, e) => {
				selectAllButton.Click ();
				notesBox.Sensitive = true;
				renameButton.Sensitive = true;
				dontRenameButton.Sensitive = true;
			};
			neverRenameRadio = new RadioButton (alwaysShowDlgRadio,
			                                    Catalog.GetString ("Never rename _links"));
			neverRenameRadio.Clicked += (o, e) => {
				selectNoneButton.Click ();
				notesBox.Sensitive = false;
				renameButton.Sensitive = false;
				dontRenameButton.Sensitive = true;
			};
			alwaysRenameRadio = new RadioButton (alwaysShowDlgRadio,
			                                     Catalog.GetString ("Alwa_ys rename links"));
			alwaysRenameRadio.Clicked += (o, e) => {
				selectAllButton.Click ();
				notesBox.Sensitive = false;
				renameButton.Sensitive = true;
				dontRenameButton.Sensitive = false;
			};
			expandBox.PackStart (alwaysShowDlgRadio, false, true, 0);
			expandBox.PackStart (neverRenameRadio, false, true, 0);
			expandBox.PackStart (alwaysRenameRadio, false, true, 0);
			advancedExpander.Add (expandBox);
			VBox.PackStart (advancedExpander, true, true, 5);

			advancedExpander.Activated += (o, e) =>
				this.Resizable = advancedExpander.Expanded;

			this.Focus = dontRenameButton;
			VBox.ShowAll ();
		}

		public Dictionary<Note, bool> Notes
		{
			get {
				var notes = new Dictionary<Note, bool> ();
				notesModel.Foreach ((model, path, iter) => {
					Note note = (Note) notesModel.GetValue (iter, 2);
					bool rename = (bool) notesModel.GetValue (iter, 0);
					notes [note] = rename;
					return false;
				});
				return notes;
			}
		}

		public NoteRenameBehavior SelectedBehavior
		{
			get {
				if (neverRenameRadio.Active)
					return NoteRenameBehavior.AlwaysRemoveLinks;
				else if (alwaysRenameRadio.Active)
					return NoteRenameBehavior.AlwaysRenameLinks;
				else
					return NoteRenameBehavior.AlwaysShowDialog;
			}
		}
	}
}
