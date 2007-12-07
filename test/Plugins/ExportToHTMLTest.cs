using System;
using System.IO;

using NUnit.Framework;
using Tomboy;

namespace TomboyTest
{
	[TestFixture]
	public class ExportToHTMLPluginTest
	{
		static string html_text =
		        "<html xmlns:tomboy=\"http://beatniksoftware.com/tomboy\" xmlns:link=\"http://beatniksoftware.com/tomboy/link\" xmlns:size=\"http://beatniksoftware.com/tomboy/size\"><head><META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-16\"><title>Test Title</title><style type=\"text/css\">\n" +
		        "        \n" +
		        "\tbody {  }\n" +
		        "\th1 { font-size: xx-large;\n" +
		        "     \t     font-weight: bold;\n" +
		        "     \t     color: red;\n" +
		        "     \t     text-decoration: underline; }\n" +
		        "\tdiv.note { overflow: auto;\n" +
		        "\t\t   position: relative;\n" +
		        "\t\t   border: 1px solid black;\n" +
		        "\t\t   display: block;\n" +
		        "\t\t   padding: 5pt;\n" +
		        "\t\t   margin: 5pt; \n" +
		        "\t\t   white-space: -moz-pre-wrap; /* Mozilla */\n" +
		        " \t      \t   white-space: -pre-wrap;     /* Opera 4 - 6 */\n" +
		        " \t      \t   white-space: -o-pre-wrap;   /* Opera 7 */\n" +
		        " \t      \t   white-space: pre-wrap;      /* CSS3 */\n" +
		        " \t      \t   word-wrap: break-word;      /* IE 5.5+ */ }\n" +
		        "\t</style></head><body><div class=\"note\" id=\"Test Title\" style=\"width:0;\"><a name=\"#Test Title\"></a><h1>Test Title</h1>\n" +
		        "Some text</div></body></html>";

		static string html_non_ascii_text =
		        "<html xmlns:tomboy=\"http://beatniksoftware.com/tomboy\" xmlns:link=\"http://beatniksoftware.com/tomboy/link\" xmlns:size=\"http://beatniksoftware.com/tomboy/size\"><head><META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-16\"><title>Test Title</title><style type=\"text/css\">\n" +
		        "        \n" +
		        "\tbody {  }\n" +
		        "\th1 { font-size: xx-large;\n" +
		        "     \t     font-weight: bold;\n" +
		        "     \t     color: red;\n" +
		        "     \t     text-decoration: underline; }\n" +
		        "\tdiv.note { overflow: auto;\n" +
		        "\t\t   position: relative;\n" +
		        "\t\t   border: 1px solid black;\n" +
		        "\t\t   display: block;\n" +
		        "\t\t   padding: 5pt;\n" +
		        "\t\t   margin: 5pt; \n" +
		        "\t\t   white-space: -moz-pre-wrap; /* Mozilla */\n" +
		        " \t      \t   white-space: -pre-wrap;     /* Opera 4 - 6 */\n" +
		        " \t      \t   white-space: -o-pre-wrap;   /* Opera 7 */\n" +
		        " \t      \t   white-space: pre-wrap;      /* CSS3 */\n" +
		        " \t      \t   word-wrap: break-word;      /* IE 5.5+ */ }\n" +
		        "\t</style></head><body><div class=\"note\" id=\"Test Title\" style=\"width:0;\"><a name=\"#Test Title\"></a><h1>Test \u00c4 Title</h1>\n" +
		        "Some text with arabian characters: \u062b\u0642\u06cd.</div></body></html>";
		static string html_link_text =
		        "<html xmlns:tomboy=\"http://beatniksoftware.com/tomboy\" xmlns:link=\"http://beatniksoftware.com/tomboy/link\" xmlns:size=\"http://beatniksoftware.com/tomboy/size\"><head><META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-16\"><title>Test Title</title><style type=\"text/css\">\n" +
		        "        \n" +
		        "\tbody {  }\n" +
		        "\th1 { font-size: xx-large;\n" +
		        "     \t     font-weight: bold;\n" +
		        "     \t     color: red;\n" +
		        "     \t     text-decoration: underline; }\n" +
		        "\tdiv.note { overflow: auto;\n" +
		        "\t\t   position: relative;\n" +
		        "\t\t   border: 1px solid black;\n" +
		        "\t\t   display: block;\n" +
		        "\t\t   padding: 5pt;\n" +
		        "\t\t   margin: 5pt; \n" +
		        "\t\t   white-space: -moz-pre-wrap; /* Mozilla */\n" +
		        " \t      \t   white-space: -pre-wrap;     /* Opera 4 - 6 */\n" +
		        " \t      \t   white-space: -o-pre-wrap;   /* Opera 7 */\n" +
		        " \t      \t   white-space: pre-wrap;      /* CSS3 */\n" +
		        " \t      \t   word-wrap: break-word;      /* IE 5.5+ */ }\n" +
		        "\t</style></head><body><div class=\"note\" id=\"Test Title\" style=\"width:0;\"><a name=\"#Test Title\"></a><h1>Test Title</h1>\n" +
		        "Link here: <a style=\"color:red\" href=\"#Other Note\">Other Note</a>.</div>\n" +
		        "<div class=\"note\" id=\"Other Note\" style=\"width:0;\"><a name=\"#Other Note\"></a><h1>Other Note</h1>\n" +
		        "Some text.</div></body></html>";

		[SetUp]
		public void SetupNoteArchiver ()
		{
			NoteArchiver.Instance = new DummyNoteArchiver ();
			Logger.Mute ();
		}

		[TearDown]
		public void TearDownNoteArchiver ()
		{
			NoteArchiver.Instance = null;
			Logger.Unmute ();
		}

		[Test]
		public void Construct ()
		{
			new ExportToHTMLPlugin ();
		}

		[Test]
		public void WriteHTMLForNote ()
		{
			Note note = Note.CreateNewNote ("Test Title", "note://tomboy/foo", null);
			note.XmlContent = "<note-content>Test Title\n\nSome text</note-content>";
			ExportToHTMLPlugin plugin = new ExportToHTMLPlugin ();
			StringWriter writer = new StringWriter ();
			plugin.WriteHTMLForNote (writer, note, false);
			Assert.AreEqual (html_text, writer.ToString ());
		}

		[Test]
		public void WriteHTMLForNoteWithNonAsciiCharacters ()
		{
			// Test with non-ASCII characters as well to make sure
			// all buffers are large enough to process multi-byte
			// characters.
			Note note = Note.CreateNewNote ("Test Title", "note://tomboy/foo", null);
			note.XmlContent = "<note-content>Test \u00c4 Title\n\nSome text with arabian characters: \u062b\u0642\u06cd.</note-content>";
			ExportToHTMLPlugin plugin = new ExportToHTMLPlugin ();
			StringWriter writer = new StringWriter ();
			plugin.WriteHTMLForNote (writer, note, false);
			Assert.AreEqual (html_non_ascii_text, writer.ToString ());
		}

		/* FIXME: Disabled for now.
		[Test]
		public void WriteHTMLForNoteLinked ()
		{
		 DummyNoteManager manager = new DummyNoteManager ();
		 manager.Create ("Other Note", "<note-content>Other Note\n\nSome text.</note-content>");
		 Note linking_note = manager.Create ("Test Title", "<note-content>Test Title\n\nLink here: <link:internal>Other Note</link:internal>.</note-content>");

		 ExportToHTMLPlugin plugin = new ExportToHTMLPlugin ();
		 StringWriter writer = new StringWriter ();
		 plugin.WriteHTMLForNote (writer, linking_note, true);
		 Assert.AreEqual (html_link_text, writer.ToString ());
		}
		*/
	}
}
