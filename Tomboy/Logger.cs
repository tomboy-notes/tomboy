using System;

namespace Tomboy
{
	public interface ILogger
	{
		void Log (string msg, params object[] args);
	}

	class NullLogger : ILogger
	{
		public void Log (string msg, params object[] args)
		{
		}
	}

	class ConsoleLogger : ILogger
	{
		public void Log (string msg, params object[] args)
		{
			Console.WriteLine (msg, args);
		}
	}

	// This class provides a generic logging facility. By default all
	// information is written to stdout, but other loggers are pluggable.
	// TODO: The generic Log method should probably be replaced by methods
	// like Debug, Message, and Error.
	public static class Logger
	{
		static ILogger instance = new ConsoleLogger ();

		public static ILogger Instance
		{
			get { return instance; }
			set { instance = value; }
		}

		public static void Log (string msg, params object[] args)
		{
			instance.Log (msg, args);
		}

		public static void Mute ()
		{
			instance = new NullLogger ();
		}

		public static void Unmute ()
		{
			instance = new ConsoleLogger ();
		}
	}
}
