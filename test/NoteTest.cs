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
			new Note("Note Title", "/tmp/note", null);
		}
	}
}
