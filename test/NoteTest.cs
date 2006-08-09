namespace TomboyTest
{
	using NUnit.Framework;
	using Tomboy;

	[TestFixture]
	public class NoteDataTest
	{
		NoteData note;

		[SetUp]
		public void Construct ()
		{
			note = new NoteData ();
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
	public class NoteTest
	{
		[Test]
		public void Construct()
		{
			Note.CreateNewNote ("Note Title", "/tmp/note", null);
		}
	}
}
