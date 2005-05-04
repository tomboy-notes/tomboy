
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;
using Mono.Posix;

using Tomboy;

public class ExportToHTMLPlugin : NotePlugin
{
	const string stylesheet_name = "ExportToHTML.xsl";
	Gtk.Widget toolbar_item;

	static XslTransform xsl;

	static ExportToHTMLPlugin ()
	{
		Assembly asm = Assembly.GetExecutingAssembly ();
		string stylesheet_file = Path.Combine (asm.Location, stylesheet_name);
		xsl = new XslTransform ();

		if (File.Exists (stylesheet_file)) {
			xsl.Load (stylesheet_file);
		} else {
			Stream resource = asm.GetManifestResourceStream (stylesheet_name);
			if (resource != null) {
				XmlTextReader reader = new XmlTextReader (resource);
				xsl.Load (reader, null, null);
			} else {
				Console.WriteLine ("Unable to find HTML export template '{0}'.",
						   stylesheet_name);
			}
		}
	}

	protected override void Initialize ()
	{
		// Do nothing.
	}

	protected override void Shutdown ()
	{
		if (toolbar_item != null) {
			Window.Toolbar.Remove (toolbar_item);
			toolbar_item = null;
		}
	}

	protected override void OnNoteOpened () 
	{
		toolbar_item = 
			Window.Toolbar.AppendItem (Catalog.GetString ("Export"), 
						   Catalog.GetString ("Export this note to HTML"), 
						   null, 
						   new Gtk.Image (Gtk.Stock.Save, 
								  Gtk.IconSize.LargeToolbar),
						   new Gtk.SignalFunc (ExportButtonClicked));
	}

	void ExportButtonClicked () 
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
			
			Gnome.Url.Show ("file://" + Uri.EscapeString(output_path));
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
		// FIXME: Make save() take an output stream.
		note.QueueSave (false);
		note.Save ();
		
		XmlDocument doc = new XmlDocument ();
		doc.PreserveWhitespace = true;
		doc.Load (note.FilePath);
		
		/* Is this needed?? */
		/*
		XmlNodeList list = doc.GetElementsByTagName ("note-content", 
							     "http://beatniksoftware.com/tomboy");
		if (list.Count != 0) {
			string innerxml = list.Item (0).InnerXml;
			// pretty kludge-y, I admit.
			list.Item(0).InnerXml = Regex.Replace (innerxml, 
							       @">(\W+)<", 
							       @"><whitespace>$1</whitespace><");
		}
		*/

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

