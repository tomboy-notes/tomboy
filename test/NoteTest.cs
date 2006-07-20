namespace TomboyTest
{
	using NUnit.Framework;
	using Tomboy;

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
