
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Mono.Unix;

using Tomboy;

public class ExportToHTMLPlugin : NotePlugin
{
	const string stylesheet_name = "ExportToHTML.xsl";
	static XslTransform xsl;

	static ExportToHTMLPlugin ()
	{
		Assembly asm = Assembly.GetExecutingAssembly ();
		string asm_dir = System.IO.Path.GetDirectoryName (asm.Location);
		string stylesheet_file = Path.Combine (asm_dir, stylesheet_name);

		xsl = new XslTransform ();

		if (File.Exists (stylesheet_file)) {
			Console.WriteLine ("ExportToHTMLPlugin: Using user-custom {0} file.",
					   stylesheet_name);
			xsl.Load (stylesheet_file);
		} else {
			Stream resource = asm.GetManifestResourceStream (stylesheet_name);
			if (resource != null) {
				XmlTextReader reader = new XmlTextReader (resource);
				xsl.Load (reader, null, null);
				resource.Close ();
			} else {
				Console.WriteLine ("Unable to find HTML export template '{0}'.",
						   stylesheet_name);
			}
		}
	}

	protected override void Initialize ()
	{
		Gtk.ImageMenuItem item = 
			new Gtk.ImageMenuItem (Catalog.GetString ("Export to HTML"));
		item.Image = new Gtk.Image (Gtk.Stock.Save, Gtk.IconSize.Menu);
		item.Activated += ExportButtonClicked;
		item.Show ();
		AddPluginMenuItem (item);
	}

	protected override void Shutdown ()
	{
		// Do nothing.
	}

	protected override void OnNoteOpened () 
	{
		// Do nothing.
	}

	void ExportButtonClicked (object sender, EventArgs args)
	{
		ExportToHTMLDialog dialog = new ExportToHTMLDialog (Note.Title + ".html");
		int response = dialog.Run();
		string output_path = dialog.Filename;
		dialog.Destroy ();

		if (response != (int) Gtk.ResponseType.Ok) 
			return;

		Console.WriteLine ("Exporting Note '{0}' to '{1}'...", Note.Title, output_path);

		StreamWriter writer = null;
		try {
			try {
				// FIXME: Warn about file existing.  Allow overwrite.
				File.Delete (output_path); 
			} catch {
			}

			writer = new StreamWriter (output_path);
			WriteHTMLForNote (writer, Note, dialog.ExportLinked);

			Uri output_uri = new Uri (output_path);
			Gnome.Url.Show (output_uri.AbsoluteUri);
		} catch (Exception e) {
			System.Console.WriteLine ("Could not export: {0}", e);
		} finally {
			if (writer != null) 
				writer.Close ();
		}
	}

	void WriteHTMLForNote (TextWriter writer, 
			       Note note,
			       bool export_linked) 
	{
		// NOTE: Don't use the XmlDocument version, which strips
		// whitespace between elements for some reason.  Also,
		// XPathDocument is faster.
		XPathDocument doc = new XPathDocument (note.FilePath);

		XsltArgumentList args = new XsltArgumentList ();
		args.AddParam ("export-linked", "", export_linked);
		args.AddParam ("root-note", "", note.Title);

		if ((bool) Preferences.Get (Preferences.ENABLE_CUSTOM_FONT)) {
			string font_face = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
			Pango.FontDescription font_desc = 
				Pango.FontDescription.FromString (font_face);
			string font = String.Format ("font-family:'{0}';", font_desc.Family);

			args.AddParam ("font", "", font);
		}

		NoteNameResolver resolver = new NoteNameResolver (note.Manager);
		xsl.Transform (doc, args, writer, resolver);
	}
}

class NoteNameResolver : XmlResolver
{
	NoteManager manager;

	public NoteNameResolver (NoteManager manager)
	{
		this.manager = manager;
	}

	public override System.Net.ICredentials Credentials 
	{
		set { }
	}

	public override object GetEntity (Uri absoluteUri, string role, Type ofObjectToReturn)
	{		
		Note note = manager.FindByUri (absoluteUri.ToString());
		if (note != null) {
			FileStream stream = File.OpenRead (note.FilePath);
			Console.WriteLine ("GetEntity: Returning Stream");
			return stream;
		}

		return null;
	}

	public override Uri ResolveUri (Uri baseUri, string relativeUri)
	{
		Note note = manager.Find (relativeUri);
		if (note != null)
			return new Uri (note.Uri);

		return null;
	}
}

class ExportToHTMLDialog : Gtk.FileSelection
{
	Gtk.CheckButton export_linked;

	public ExportToHTMLDialog (string default_file) : 
		base (Catalog.GetString ("Destination for HTML Export")) 
	{
		Response += OnResponseCb;

		HideFileopButtons ();
		export_linked = new Gtk.CheckButton (Catalog.GetString ("Export linked notes"));
		VBox.Add (export_linked);

		ShowAll ();
		LoadPreferences (default_file);
	}

	public bool ExportLinked 
	{
		get { return export_linked.Active; }
		set { export_linked.Active = value; }
	}

	protected void OnResponseCb (object sender, Gtk.ResponseArgs args) 
	{
		if (args.ResponseId == Gtk.ResponseType.Ok) 
			SavePreferences ();
	}

	protected void LoadPreferences (string default_file) 
	{
		string last_dir = (string) Preferences.Get (Preferences.EXPORTHTML_LAST_DIRECTORY);
		if (last_dir == "")
			last_dir = Environment.GetEnvironmentVariable ("HOME");
		last_dir = System.IO.Path.Combine (last_dir, default_file);
		Filename = last_dir;

		ExportLinked = (bool) Preferences.Get (Preferences.EXPORTHTML_EXPORT_LINKED);
	}

	protected void SavePreferences () 
	{
		string dir = System.IO.Path.GetDirectoryName (Filename);
		Preferences.Set (Preferences.EXPORTHTML_LAST_DIRECTORY, dir);

		Preferences.Set (Preferences.EXPORTHTML_EXPORT_LINKED, ExportLinked);
	}
}

