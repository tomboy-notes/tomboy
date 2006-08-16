using Tomboy;

namespace TomboyTest
{
	public class DummyNoteArchiver : NoteArchiver
	{
		public string Version = NoteArchiver.CURRENT_VERSION;
		public bool ReadCalled = false;
		public bool WriteCalled = false;
		public string FileWritten = null;
		public NoteData NoteWritten = null;

		public override NoteData ReadFile (string read_file, string uri)
		{
			ReadCalled = true;
			return new NoteData (uri);
		}

		public override void WriteFile (string write_file, NoteData note)
		{
			WriteCalled = true;
			FileWritten = write_file;
			NoteWritten = note;
		}
	}
}
