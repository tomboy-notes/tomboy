using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Tomboy;
using Mono.Unix;

namespace Tomboy.ExportToHtml
{
	/// <summary>
	/// Extends the ExportAll add-in to export to HTML.
	/// </summary>
	public class ExportToHtmlApplicationAddin : ExportAllApplicationAddin
	{

		const string stylesheet_name = "ExportToHtml.xsl";
		static XslTransform xsl;

		Gtk.ImageMenuItem item;

		static XslTransform NoteXsl
		{
			get {
				if (xsl == null) {
					Assembly asm = Assembly.GetExecutingAssembly ();
					string asm_dir = System.IO.Path.GetDirectoryName (asm.Location);
					string stylesheet_file = Path.Combine (asm_dir, stylesheet_name);

					xsl = new XslTransform ();

					if (File.Exists (stylesheet_file)) {
						Logger.Info ("ExportToHTML: Using user-custom {0} file.",
						            stylesheet_name);
						xsl.Load (stylesheet_file);
					} else {
						Stream resource = asm.GetManifestResourceStream (stylesheet_name);
						if (resource != null) {
							XmlTextReader reader = new XmlTextReader (resource);
							xsl.Load (reader, null, null);
							resource.Close ();
						} else {
							Logger.Error ("Unable to find HTML export template '{0}'.",
							            stylesheet_name);
						}
					}
				}
				return xsl;
			}
		}

		/// <summary>
		/// Sets the names of the export type.
		/// </summary>
		protected override void SetNames ()
		{
			export_file_suffix = "html";
			export_type_pretty_name = Catalog.GetString ("HTML");
		}

		/// <summary>
		/// Exports a single Note to HTML in a specified location.
		/// </summary>
		public override void ExportSingleNote (Note note,
		                                string output_folder)
		{
			string output_path = output_folder + SanitizeNoteTitle (note.Title)
				+ "." + export_file_suffix;

			Logger.Debug ("Exporting Note '{0}' to '{1}'...", note.Title, output_path);

			StreamWriter writer = null;

			try {
				// FIXME: Warn about file existing.  Allow overwrite.
				File.Delete (output_path);
			} catch {
			}

			writer = new StreamWriter (output_path);

			WriteHTMLForNote (writer, note);

			if (writer != null)
				writer.Close ();

			return;
		}

		public void WriteHTMLForNote (TextWriter writer,
		                              Note note)
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
			args.AddParam ("exporting-multiple", "", true);
			args.AddParam ("export-linked", "", false);
			args.AddParam ("export-linked-all", "", false);
			args.AddParam ("root-note", "", note.Title);
			args.AddExtensionObject ("http://beatniksoftware.com/tomboy",
				new ExportAllTransformExtension (note, this));

			if ((bool) Preferences.Get (Preferences.ENABLE_CUSTOM_FONT)) {
				string font_face = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
				Pango.FontDescription font_desc =
				        Pango.FontDescription.FromString (font_face);
				string font = String.Format ("font-family:'{0}';", font_desc.Family);

				args.AddParam ("font", "", font);
			}

			NoteXsl.Transform (doc, args, writer);
		}
	}

		/// <summary>
	/// Makes <see cref="System.String.ToLower"/> available in the
	/// XSL stylesheet and resolves relative paths between notes.
	/// </summary>
	public class ExportAllTransformExtension
	{
		private Note note;
		private ExportToHtmlApplicationAddin parent;

		public ExportAllTransformExtension (Note note, ExportToHtmlApplicationAddin parent)
		{
			this.note = note;
			this.parent = parent;
		}

		public String ToLower (string s)
		{
			return s.ToLower ();
		}

		public string GetRelativePath (string title_to)
		{
			if (string.IsNullOrEmpty (title_to))
				return string.Empty;

			//Get the the value from the exportAll superclass, changing from platform
			//dependent to URL drectory seperators since we're making HTML.
			string system_relative_path = parent.ResolveRelativePath (note, title_to);
			return system_relative_path.Replace (System.IO.Path.DirectorySeparatorChar, '/');
		}
	}
}
