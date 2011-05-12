using System;
using System.IO;
using Tomboy;
using Mono.Unix;

namespace Tomboy.ExportToHtml
{
	/// <summary>
	/// Extends the ExportAll add-in to export to HTML.
	/// </summary>
	public class ExportToHtmlApplicationAddin : ExportAllApplicationAddin
	{

		ExportToHtmlNoteAddin exporter = new ExportToHtmlNoteAddin ();

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

			exporter.WriteHTMLForNote (writer, note, false, false);

			if (writer != null)
				writer.Close ();

			return;
		}
	}
}
