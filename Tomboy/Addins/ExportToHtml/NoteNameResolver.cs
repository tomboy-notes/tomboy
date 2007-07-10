using System;
using System.IO;
using System.Text;
using System.Xml;
using Tomboy;

namespace Tomboy.ExportToHtml
{
	public class NoteNameResolver : XmlResolver
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

		public override object GetEntity (Uri absolute_uri, string role, Type of_object_to_return)
		{
			Note note = manager.FindByUri (absolute_uri.ToString ());
			if (note == null)
				return null;

			StringWriter writer = new StringWriter ();
			NoteArchiver.Write (writer, note.Data);
			Stream stream = WriterToStream (writer);
			writer.Close ();

			Logger.Log ("GetEntity: Returning Stream");
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
}
