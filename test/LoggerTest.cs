namespace TomboyTest
{
	using System;
	using System.IO;
	using NUnit.Framework;
	using Tomboy;

	class DebugLogger : ILogger
	{
		public string Message = null;
		public object[] Arguments = null;

		public void Log (string msg, params object[] args)
		{
			Message = msg;
			Arguments = args;
		}
	}

	[TestFixture]
	public class LoggerTest
	{
		DebugLogger logger = null;

		[SetUp]
		public void Setup ()
		{
			logger = new DebugLogger ();
			Logger.Instance = logger;
		}

		[Test]
		public void LogSimple ()
		{
			Logger.Log ("Foo");
			Assert.AreEqual ("Foo", logger.Message);
			Assert.AreEqual (0, logger.Arguments.Length);
		}

		[Test]
		public void LogOneArgument ()
		{
			Logger.Log ("Foo", "arg");
			Assert.AreEqual ("Foo", logger.Message);
			Assert.AreEqual (1, logger.Arguments.Length);
			Assert.AreEqual ("arg", logger.Arguments[0]);
		}

		[Test]
		public void LogTwoArguments ()
		{
			Logger.Log ("Foo", 1, 2);
			Assert.AreEqual ("Foo", logger.Message);
			Assert.AreEqual (2, logger.Arguments.Length);
			Assert.AreEqual (1, logger.Arguments[0]);
			Assert.AreEqual (2, logger.Arguments[1]);
		}
	}
}
