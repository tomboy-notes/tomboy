using System;
using Tomboy;
using Mono.Unix;

namespace Tomboy.ExportToHtml
{
	public class ExportToHtmlDialog : Gtk.FileChooserDialog
	{
		Gtk.CheckButton export_linked;
		Gtk.CheckButton export_linked_all;

public ExportToHtmlDialog (string default_file) :
		base (Catalog.GetString ("Destination for HTML Export"),
		      null, Gtk.FileChooserAction.Save, new object[] {})
		{
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			AddButton (Gtk.Stock.Save, Gtk.ResponseType.Ok);

			DefaultResponse = Gtk.ResponseType.Ok;

			Gtk.Table table = new Gtk.Table (2, 2, false);

			export_linked = new Gtk.CheckButton (Catalog.GetString ("Export linked notes"));
			export_linked.Toggled += OnExportLinkedToggled;
			table.Attach (export_linked, 0, 2, 0, 1, Gtk.AttachOptions.Fill, 0, 0, 0);

			export_linked_all =
			        new Gtk.CheckButton (Catalog.GetString ("Include all other linked notes"));
			table.Attach (export_linked_all,
			              1, 2, 1, 2, Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill, 0, 20, 0);

			ExtraWidget = table;

			DoOverwriteConfirmation = true;
			LocalOnly = true;

			ShowAll ();
			LoadPreferences (default_file);
			SetExportLinkedAllSensitivity ();
		}

		public bool ExportLinked
		{
			get {
				return export_linked.Active;
			}
			set {
				export_linked.Active = value;
			}
		}

		public bool ExportLinkedAll
		{
			get {
				return export_linked_all.Active;
			}
			set {
				export_linked_all.Active = value;
			}
		}

		public void SavePreferences ()
		{
			string dir = System.IO.Path.GetDirectoryName (Filename);
			Preferences.Set (Preferences.EXPORTHTML_LAST_DIRECTORY, dir);

			Preferences.Set (Preferences.EXPORTHTML_EXPORT_LINKED, ExportLinked);
			Preferences.Set (Preferences.EXPORTHTML_EXPORT_LINKED_ALL, ExportLinkedAll);
		}

		protected void LoadPreferences (string default_file)
		{
			string last_dir = (string) Preferences.Get (Preferences.EXPORTHTML_LAST_DIRECTORY);
			if (last_dir == "")
				last_dir = Environment.GetEnvironmentVariable ("HOME");
			SetCurrentFolder (last_dir);
			CurrentName = default_file;

			ExportLinked = (bool) Preferences.Get (Preferences.EXPORTHTML_EXPORT_LINKED);
			ExportLinkedAll = (bool) Preferences.Get (Preferences.EXPORTHTML_EXPORT_LINKED_ALL);
		}

		protected void OnExportLinkedToggled (object sender, EventArgs args)
		{
			SetExportLinkedAllSensitivity ();
		}

		protected void SetExportLinkedAllSensitivity ()
		{
			if (export_linked.Active)
				export_linked_all.Sensitive = true;
			else
				export_linked_all.Sensitive = false;
		}
	}
}
