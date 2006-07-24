
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Text;
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
			Logger.Log ("ExportToHTMLPlugin: Using user-custom {0} file.",
					   stylesheet_name);
			xsl.Load (stylesheet_file);
		} else {
			Stream resource = asm.GetManifestResourceStream (stylesheet_name);
			if (resource != null) {
				XmlTextReader reader = new XmlTextReader (resource);
				xsl.Load (reader, null, null);
				resource.Close ();
			} else {
				Logger.Log ("Unable to find HTML export template '{0}'.",
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

		Logger.Log ("Exporting Note '{0}' to '{1}'...", Note.Title, output_path);

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
			Logger.Log ("Could not export: {0}", e);
		} finally {
			if (writer != null) 
				writer.Close ();
		}
	}

	public void WriteHTMLForNote (TextWriter writer, 
			       Note note,
			       bool export_linked) 
	{
		// NOTE: Don't use the XmlDocument version, which strips
		// whitespace between elements for some reason.  Also,
		// XPathDocument is faster.
		StringWriter s_writer = new StringWriter ();
		NoteArchiver.Write (s_writer, note);
		StringReader reader = new StringReader (s_writer.ToString ());
		s_writer.Close ();
		XPathDocument doc = new XPathDocument (reader);
		reader.Close ();

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
	ArrayList already_resolved;

	public NoteNameResolver (NoteManager manager)
	{
		this.manager = manager;
		this.already_resolved = new ArrayList ();
	}

	public override System.Net.ICredentials Credentials 
	{
		set { }
	}

	public override object GetEntity (Uri absolute_uri, string role, Type of_object_to_return)
	{
		Note note = manager.FindByUri (absolute_uri.ToString ());
		if (note == null)
			return null;

		// Avoid infinite recursion.
		if (already_resolved.Contains (note))
			return null;

		StringWriter writer = new StringWriter ();
		NoteArchiver.Write (writer, note);
		Stream stream = WriterToStream (writer);
		writer.Close ();

		Logger.Log ("GetEntity: Returning Stream");
		already_resolved.Add (note);
		return stream;
	}

	// Using UTF-16 does not work - the document is not processed.
	// Also, the byte order marker (BOM in short, locate at U+FEFF,
	// 0xef 0xbb 0xbf in UTF-8) must be included, otherwise parsing fails
	// as well. This way the buffer contains an exact representation of
	// the on-disk representation of notes.
	//
	// See http://en.wikipedia.org/wiki/Byte_Order_Mark for more
	// information about the BOM.
	MemoryStream WriterToStream (TextWriter writer)
	{
		UTF8Encoding encoding = new UTF8Encoding ();
		string s = writer.ToString ();
		int bytes_required = 3 + encoding.GetByteCount (s);
		byte[] buffer = new byte [bytes_required];
		buffer[0] = 0xef;
		buffer[1] = 0xbb;
		buffer[2] = 0xbf;
		encoding.GetBytes (s, 0, s.Length, buffer, 3);
		return new MemoryStream (buffer);
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

