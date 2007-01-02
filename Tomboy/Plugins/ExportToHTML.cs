
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

	Gtk.ImageMenuItem item;

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
		item = 
			new Gtk.ImageMenuItem (Catalog.GetString ("Export to HTML"));
		item.Image = new Gtk.Image (Gtk.Stock.Save, Gtk.IconSize.Menu);
		item.Activated += ExportButtonClicked;
		item.Show ();
		AddPluginMenuItem (item);
	}

	protected override void Shutdown ()
	{
		// Disconnect the event handlers so
		// there aren't any memory leaks.
		item.Activated -= ExportButtonClicked;
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

		if (response != (int) Gtk.ResponseType.Ok) {
			dialog.Destroy ();
			return;
		}

		Logger.Log ("Exporting Note '{0}' to '{1}'...", Note.Title, output_path);

		StreamWriter writer = null;
		string error_message = null;

		try {
			try {
				// FIXME: Warn about file existing.  Allow overwrite.
				File.Delete (output_path); 
			} catch {
			}

			writer = new StreamWriter (output_path);
			WriteHTMLForNote (writer, Note, dialog.ExportLinked);
			
			// Save the dialog preferences now that the note has
			// successfully been exported
			dialog.SavePreferences ();
			dialog.Destroy ();
			dialog = null;
			
			try {
				Uri output_uri = new Uri (output_path);
				Gnome.Url.Show (output_uri.AbsoluteUri);
			} catch (Exception ex) {
				Logger.Log ("Could not open exported note in a web browser: {0}", 
					    ex);

				string detail = String.Format (
					Catalog.GetString ("Your note was exported to \"{0}\"."),
					output_path);

				// Let the user know the note was saved successfully
				// even though showing the note in a web browser failed.
				HIGMessageDialog msg_dialog =
					new HIGMessageDialog (
						Window,
						Gtk.DialogFlags.DestroyWithParent,
						Gtk.MessageType.Info,
						Gtk.ButtonsType.Ok,
						Catalog.GetString ("Note exported successfully"),
						detail);
				msg_dialog.Run ();
				msg_dialog.Destroy ();
			}
		} catch (UnauthorizedAccessException) {
			error_message = Catalog.GetString ("Access denied.");
		} catch (DirectoryNotFoundException) {
			error_message = Catalog.GetString ("Folder does not exist.");
		} catch (Exception e) {
			Logger.Log ("Could not export: {0}", e);
			
			error_message = e.Message;
		} finally {
			if (writer != null) 
				writer.Close ();
		}

		if (error_message != null)
		{
			Logger.Log ("Could not export: {0}", error_message);

			string msg = String.Format (
				Catalog.GetString ("Could not save the file \"{0}\""), 
				output_path);

			HIGMessageDialog msg_dialog = 
				new HIGMessageDialog (
					dialog,
					Gtk.DialogFlags.DestroyWithParent,
					Gtk.MessageType.Error,
					Gtk.ButtonsType.Ok,
					msg,
					error_message);
			msg_dialog.Run ();
			msg_dialog.Destroy ();
		}

		if (dialog != null)
			dialog.Destroy ();
	}

	public void WriteHTMLForNote (TextWriter writer, 
			       Note note,
			       bool export_linked) 
	{
		// NOTE: Don't use the XmlDocument version, which strips
		// whitespace between elements for some reason.  Also,
		// XPathDocument is faster.
		StringWriter s_writer = new StringWriter ();
		NoteArchiver.Write (s_writer, note.Data);
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
		NoteArchiver.Write (writer, note.Data);
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

class ExportToHTMLDialog : Gtk.FileChooserDialog
{
	Gtk.CheckButton export_linked;

	public ExportToHTMLDialog (string default_file) : 
		base (Catalog.GetString ("Destination for HTML Export"),
			  null, Gtk.FileChooserAction.Save, new object[] {})
	{
		AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
		AddButton (Gtk.Stock.Save, Gtk.ResponseType.Ok);
		
		DefaultResponse = Gtk.ResponseType.Ok;
		
		export_linked = new Gtk.CheckButton (Catalog.GetString ("Export linked notes"));
		ExtraWidget = export_linked;
		
		DoOverwriteConfirmation = true;
		LocalOnly = true;

		ShowAll ();
		LoadPreferences (default_file);
	}

	public bool ExportLinked 
	{
		get { return export_linked.Active; }
		set { export_linked.Active = value; }
	}
	
	public void SavePreferences () 
	{
		string dir = System.IO.Path.GetDirectoryName (Filename);
		Preferences.Set (Preferences.EXPORTHTML_LAST_DIRECTORY, dir);

		Preferences.Set (Preferences.EXPORTHTML_EXPORT_LINKED, ExportLinked);
	}
	
	protected void LoadPreferences (string default_file) 
	{
		string last_dir = (string) Preferences.Get (Preferences.EXPORTHTML_LAST_DIRECTORY);
		if (last_dir == "")
			last_dir = Environment.GetEnvironmentVariable ("HOME");
		SetCurrentFolder (last_dir);
		CurrentName = default_file;

		ExportLinked = (bool) Preferences.Get (Preferences.EXPORTHTML_EXPORT_LINKED);
	}
}
