namespace TomboyTest
{
	using NUnit.Framework;
	using Gtk;
	using Tomboy;

	[TestFixture]
	public class NoteDataTest
	{
		NoteData note;

		[SetUp]
		public void Construct ()
		{
			note = new NoteData ("tomboy://www.example.com/note");
		}

		[Test]
		public void HasPositionAfterConstruction ()
		{
			Assert.IsFalse (note.HasPosition ());
		}

		[Test]
		public void HasPositionAfterSettingX ()
		{
			note.X = 5;
			Assert.IsFalse (note.HasPosition ());
		}

		[Test]
		public void HasPositionAfterSettingY ()
		{
			note.Y = 5;
			Assert.IsFalse (note.HasPosition ());
		}

		[Test]
		public void HasPositionAfterSettingXY ()
		{
			note.X = 5;
			note.Y = 5;
			Assert.IsTrue (note.HasPosition ());
		}

		[Test]
		public void HasPositionAfterSetPositionExtent ()
		{
			note.SetPositionExtent (0, 0, 5, 5);
			Assert.IsTrue (note.HasPosition ());
		}

		[Test]
		public void HasExtentAfterConstruction ()
		{
			Assert.IsFalse (note.HasExtent ());
		}

		[Test]
		public void HasExtentAfterSettingWidth ()
		{
			note.Width = 5;
			Assert.IsFalse (note.HasExtent ());
		}

		[Test]
		public void HasExtentAfterSettingHeight ()
		{
			note.Height = 5;
			Assert.IsFalse (note.HasExtent ());
		}

		[Test]
		public void HasExtentAfterSettingWidthHeight ()
		{
			note.Width = 5;
			note.Height = 5;
			Assert.IsTrue (note.HasExtent ());
		}

		[Test]
		public void HasExtentAfterSetPositionExtent ()
		{
			note.SetPositionExtent (0, 0, 5, 5);
			Assert.IsTrue (note.HasExtent ());
		}
	}

	[TestFixture]
	public class NoteDataBufferSynchronizerTest
	{
		NoteData data;
		NoteDataBufferSynchronizer note;

		[SetUp]
		public void Construct ()
		{
			data = new NoteData ("http://www.example.com/note");
			note = new NoteDataBufferSynchronizer (data);
		}

		[Test]
		public void Text ()
		{
			Assert.AreEqual ("", note.Text);
		}

		[Test]
		public void ValidText ()
		{
			data.Text = "<note-content>Foo</note-content>";
			Assert.AreEqual ("<note-content>Foo</note-content>", note.Text);
		}
	}

	[TestFixture]
	public class NoteDataBufferSynchronizerTestWithBuffer
	{
		NoteData data;
		NoteDataBufferSynchronizer note;
		NoteBuffer buffer;

		[SetUp]
		public void Construct ()
		{
			data = new NoteData ("http://www.example.com/note");
			data.Text = "<note-content>Foo</note-content>";
			note = new NoteDataBufferSynchronizer (data);
			buffer = new NoteBuffer (new TextTagTable ());
		}

		[Test]
		public void TextAfterAddingBuffer ()
		{
			buffer.Text = "Bar";
			note.Buffer = buffer;
			Assert.AreEqual ("<note-content version=\"0.1\">FooBar</note-content>", note.Text);
		}

		[Test]
		public void TextAfterChangingBufferText ()
		{
			note.Buffer = buffer;
			buffer.Text = "Bar";
			Assert.AreEqual ("<note-content version=\"0.1\">Bar</note-content>", note.Text);
		}
	}

	[TestFixture]
	public class NoteTest
	{
		[Test]
		public void Construct()
		{
			Note.CreateNewNote ("Note Title", "/tmp/note", null);
		}
	}
}
