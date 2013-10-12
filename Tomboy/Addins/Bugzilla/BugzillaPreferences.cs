using System;
using System.IO;
using Mono.Unix;

namespace Tomboy.Bugzilla
{
	public class BugzillaPreferences : Gtk.VBox
	{
		Gtk.TreeView icon_tree;
		Gtk.ListStore icon_store;

		Gtk.Button add_button;
		Gtk.Button remove_button;

		string last_opened_dir;

		public BugzillaPreferences ()
: base (false, 12)
		{
			last_opened_dir = Environment.GetEnvironmentVariable ("HOME");

			Gtk.Label l = new Gtk.Label (Catalog.GetString (
			                                     "You can use any bugzilla just by dragging links " +
			                                     "into notes.  If you want a special icon for " +
			                                     "certain hosts, add them here."));
			l.Wrap = true;
			l.Xalign = 0;

			PackStart (l, false, false, 0);

			icon_store = CreateIconStore ();

			icon_tree = new Gtk.TreeView (icon_store);
			icon_tree.HeadersVisible = true;
			icon_tree.Selection.Mode = Gtk.SelectionMode.Single;
			icon_tree.Selection.Changed += SelectionChanged;

			Gtk.CellRenderer renderer;

			Gtk.TreeViewColumn host_col = new Gtk.TreeViewColumn ();
			host_col.Title = Catalog.GetString ("Host Name");
			host_col.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			host_col.Resizable = true;
			host_col.Expand = true;
			host_col.MinWidth = 200;

			renderer = new Gtk.CellRendererText ();
			host_col.PackStart (renderer, true);
			host_col.AddAttribute (renderer, "text", 1 /* host name */);
			host_col.SortColumnId = 1; /* host name */
			host_col.SortIndicator = false;
			host_col.Reorderable = false;
			host_col.SortOrder = Gtk.SortType.Ascending;

			icon_tree.AppendColumn (host_col);

			Gtk.TreeViewColumn icon_col = new Gtk.TreeViewColumn ();
			icon_col.Title = Catalog.GetString ("Icon");
			icon_col.Sizing = Gtk.TreeViewColumnSizing.Fixed;
			icon_col.MaxWidth = 50;
			icon_col.MinWidth = 50;
			icon_col.Resizable = false;

			renderer = new Gtk.CellRendererPixbuf ();
			icon_col.PackStart (renderer, false);
			icon_col.AddAttribute (renderer, "pixbuf", 0 /* icon */);

			icon_tree.AppendColumn (icon_col);

			Gtk.ScrolledWindow sw = new Gtk.ScrolledWindow ();
			sw.ShadowType = Gtk.ShadowType.In;
			sw.HeightRequest = 200;
			sw.WidthRequest = 300;
			sw.SetPolicy (Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
			sw.Add (icon_tree);

			PackStart (sw, true, true, 0);

			add_button = new Gtk.Button (Gtk.Stock.Add);
			add_button.Clicked += AddClicked;

			remove_button = new Gtk.Button (Gtk.Stock.Remove);
			remove_button.Sensitive = false;
			remove_button.Clicked += RemoveClicked;

			Gtk.HButtonBox hbutton_box = new Gtk.HButtonBox ();
			hbutton_box.Layout = Gtk.ButtonBoxStyle.Start;
			hbutton_box.Spacing = 6;

			hbutton_box.PackStart (add_button, false, false, 0);
			hbutton_box.PackStart (remove_button, false, false, 0);
			PackStart (hbutton_box, false, false, 0);

			ShowAll ();
		}

		Gtk.ListStore CreateIconStore ()
		{
			Gtk.ListStore store = new Gtk.ListStore (
			        typeof (Gdk.Pixbuf), // icon
			        typeof (string),     // host
			        typeof (string));    // file path
			store.SetSortColumnId (1, Gtk.SortType.Ascending);

			return store;
		}

		void UpdateIconStore ()
		{
			// Read ~/.config/tomboy/BugzillaIcons/"

			if (!Directory.Exists (BugzillaNoteAddin.ImageDirectory))
				return;

			icon_store.Clear (); // clear out the old entries

			string [] icon_files = Directory.GetFiles (BugzillaNoteAddin.ImageDirectory);
			foreach (string icon_file in icon_files) {
				FileInfo file_info = new FileInfo (icon_file);

				Gdk.Pixbuf pixbuf = null;
				try {
					pixbuf = new Gdk.Pixbuf (icon_file);
				} catch (Exception e) {
					Logger.Warn ("Error loading Bugzilla Icon {0}: {1}", icon_file, e.Message);
				}

				if (pixbuf == null)
					continue;

				string host = ParseHost (file_info);
				if (host != null) {
					Gtk.TreeIter iter = icon_store.Append ();
					icon_store.SetValue (iter, 0, pixbuf);
					icon_store.SetValue (iter, 1, host);
					icon_store.SetValue (iter, 2, icon_file);
				}
			}
		}

		string ParseHost (FileInfo file_info)
		{
			string name = file_info.Name;
			string ext = file_info.Extension;

			if (ext == null || ext == String.Empty)
				return null;

			int ext_pos = name.IndexOf (ext);
			if (ext_pos <= 0)
				return null;

			string host = name.Substring (0, ext_pos);
			if (host == null || host == String.Empty)
				return null;

			return host;
		}

		protected override void OnRealized ()
		{
			base.OnRealized ();

			UpdateIconStore ();
		}

		void SelectionChanged (object sender, EventArgs args)
		{
			remove_button.Sensitive =
			        icon_tree.Selection.CountSelectedRows() > 0;
		}

		void AddClicked (object sender, EventArgs args)
		{
			Gtk.FileChooserDialog dialog = new Gtk.FileChooserDialog (
			        Catalog.GetString ("Select an icon..."),
			        null, Gtk.FileChooserAction.Open, new object[] {});
			dialog.AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			dialog.AddButton (Gtk.Stock.Open, Gtk.ResponseType.Ok);

			dialog.DefaultResponse = Gtk.ResponseType.Ok;
			dialog.LocalOnly = true;
			dialog.SetCurrentFolder (last_opened_dir);

			Gtk.FileFilter filter = new Gtk.FileFilter ();
			filter.AddPixbufFormats ();

			dialog.Filter = filter;

			// Extra Widget
			Gtk.Label l = new Gtk.Label (Catalog.GetString ("_Host name:"));
			Gtk.Entry host_entry = new Gtk.Entry ();
			l.MnemonicWidget = host_entry;
			Gtk.HBox hbox = new Gtk.HBox (false, 6);
			hbox.PackStart (l, false, false, 0);
			hbox.PackStart (host_entry, true, true, 0);
			hbox.ShowAll ();

			dialog.ExtraWidget = hbox;

			int response;
			string icon_file;
			string host;

run_add_dialog:
			response = dialog.Run ();
			icon_file = dialog.Filename;
			host = host_entry.Text.Trim ();

			if (response == (int) Gtk.ResponseType.Ok
			                && host == String.Empty) {
				// Let the user know that they
				// have to specify a host name.
				HIGMessageDialog warn =
				        new HIGMessageDialog (
				        null,
				        Gtk.DialogFlags.DestroyWithParent,
				        Gtk.MessageType.Warning,
				        Gtk.ButtonsType.Ok,
				        Catalog.GetString ("No host name specified"),
				        Catalog.GetString ("You must specify the Bugzilla " +
				                           "host name to use with this icon."));
				warn.Run ();
				warn.Destroy ();

				host_entry.GrabFocus ();
				goto run_add_dialog;
			} else if (response != (int) Gtk.ResponseType.Ok) {
				dialog.Destroy ();
				return;
			}

			// Keep track of the last directory the user had open
			last_opened_dir = dialog.CurrentFolder;

			dialog.Destroy ();

			// Copy the file to the BugzillaIcons directory
			string err_msg;
			if (!CopyToBugizllaIconsDir (icon_file, host, out err_msg)) {
				HIGMessageDialog err =
				        new HIGMessageDialog (
				        null,
				        Gtk.DialogFlags.DestroyWithParent,
				        Gtk.MessageType.Error,
				        Gtk.ButtonsType.Ok,
				        Catalog.GetString ("Error saving icon"),
				        Catalog.GetString ("Could not save the icon file.") +
					                   "  " +
					                   err_msg);
				err.Run ();
				err.Destroy ();
			}

			UpdateIconStore ();
		}

		bool CopyToBugizllaIconsDir (string file_path,
		                             string host,
		                             out string err_msg)
		{
			err_msg = null;

			FileInfo file_info = new FileInfo (file_path);
			string ext = file_info.Extension;
			string saved_path = System.IO.Path.Combine (BugzillaNoteAddin.ImageDirectory, host + ext);
			try {
				if (!Directory.Exists (BugzillaNoteAddin.ImageDirectory)) {
					Directory.CreateDirectory (BugzillaNoteAddin.ImageDirectory);
				}

				File.Copy (file_path, saved_path);
			} catch (Exception e) {
				err_msg = e.Message;
				return false;
			}

			ResizeIfNeeded (saved_path);
			return true;
		}

		void ResizeIfNeeded (string file_path)
		{
			// FIXME: Resize the icon to not be larger than 16x16
		}

		void RemoveClicked (object sender, EventArgs args)
		{
			// Remove the icon file and call UpdateIconStore ().
			Gtk.TreeIter iter;
			if (!icon_tree.Selection.GetSelected (out iter))
				return;

			string icon_path = icon_store.GetValue (iter, 2) as string;

			HIGMessageDialog dialog =
			        new HIGMessageDialog (
			        null,
			        Gtk.DialogFlags.DestroyWithParent,
			        Gtk.MessageType.Question,
			        Gtk.ButtonsType.None,
			        Catalog.GetString ("Really remove this icon?"),
			        Catalog.GetString ("If you remove an icon it is " +
			                           "permanently lost."));

			Gtk.Button button;

			button = new Gtk.Button (Gtk.Stock.Cancel);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, Gtk.ResponseType.Cancel);
			dialog.DefaultResponse = Gtk.ResponseType.Cancel;

			button = new Gtk.Button (Gtk.Stock.Delete);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, 666);

			int result = dialog.Run ();
			if (result == 666) {
				try {
					File.Delete (icon_path);
					UpdateIconStore ();
				} catch (Exception e) {
					Logger.Error ("Error removing icon {0}: {1}",
					              icon_path,
					              e.Message);
				}
			}

			dialog.Destroy();
		}
	}
}
