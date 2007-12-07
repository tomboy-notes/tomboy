namespace TomboyTest
{
	using System;
	using System.IO;
	using NUnit.Framework;
	using Tomboy;

	class MyNoteManager : NoteManager
	{
		public string LastDirCreated = null;
		public bool CreatedStartNote = false;
		public bool LoadedNotes = false;

public MyNoteManager () :
		base ("/tmp/notes-dir")
		{
		}

		protected override bool DirectoryExists (string directory)
		{
			return true;
		}

		protected override DirectoryInfo CreateDirectory (string directory)
		{
			LastDirCreated = directory;
			return null;
		}

		protected override PluginManager CreatePluginManager ()
		{
			return null;
		}

		protected override void CreateStartNote ()
		{
			Assert.IsFalse(CreatedStartNote,
			               "CreateStartNote called twice");
			CreatedStartNote = true;
		}

		protected override void LoadNotes ()
		{
			Assert.IsFalse(LoadedNotes, "LoadNotes called twice");
			LoadedNotes = true;
		}
	}

	class MyNoteManagerFirstRun : MyNoteManager
	{
		protected override bool DirectoryExists (string directory)
		{
			return !(directory == "/tmp/notes-dir");
		}
	}

	[TestFixture]
	public class NoteManagerTest
	{
		[Test]
		public void Construct()
		{
			MyNoteManager manager = new MyNoteManager ();
			Assert.IsNull (manager.LastDirCreated);
			Assert.IsTrue (manager.LoadedNotes);
		}

		[Test]
		public void ConstructFirstRun()
		{
			MyNoteManager manager = new MyNoteManagerFirstRun ();
			Assert.AreEqual ("/tmp/notes-dir",
			                 manager.LastDirCreated);
			Assert.IsTrue (manager.CreatedStartNote);
		}
	}
}
