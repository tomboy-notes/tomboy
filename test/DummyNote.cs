using Tomboy;

namespace TomboyTest
{
	public class DummyNoteArchiver : NoteArchiver
	{
		public string Version = NoteArchiver.CURRENT_VERSION;
		public bool ReadCalled = false;
		public bool WriteCalled = false;
		public string FileWritten = null;
		public Note NoteWritten = null;

		public override void ReadFile (string read_file, Note note)
		{
			ReadCalled = true;
		}

		public override void WriteFile (string write_file, Note note)
		{
			WriteCalled = true;
			FileWritten = write_file;
			NoteWritten = note;
		}
	}
}
