using System;
using System.IO;

namespace Tomboy
{
	public enum Level { DEBUG, INFO, WARN, ERROR, FATAL };

	public interface ILogger
	{
		void Log (Level lvl, string msg, params object[] args);
	}

	class NullLogger : ILogger
	{
		public void Log (Level lvl, string msg, params object[] args)
		{
		}
	}

	class ConsoleLogger : ILogger
	{
		public void Log (Level lvl, string msg, params object[] args)
		{
			msg = string.Format ("[{0}]: {1}", Enum.GetName (typeof (Level), lvl), msg);
			Console.WriteLine (msg, args);
		}
	}

	class FileLogger : ILogger
	{
		StreamWriter log;
		ConsoleLogger console;

		public FileLogger ()
		{
			try {
				log = File.CreateText (Path.Combine (
					Environment.GetEnvironmentVariable ("HOME"), 
					".tomboy.log"));
				log.Flush ();
			} catch (IOException) {
				// FIXME: Use temp file
			}

			console = new ConsoleLogger ();
		}

		~FileLogger ()
		{
			if (log != null)
				log.Flush ();
		}

		public void Log (Level lvl, string msg, params object[] args)
		{
			console.Log (lvl, msg, args);

			if (log != null) {
				msg = string.Format ("{0} [{1}]: {2}", 
						     DateTime.Now.ToString(), 
						     Enum.GetName (typeof (Level), lvl), 
						     msg);
				log.WriteLine (msg, args);
				log.Flush();
			}
		}
	}

	// This class provides a generic logging facility. By default all
	// information is written to standard out and a log file, but other 
	// loggers are pluggable.
	public static class Logger
	{
		private static Level log_level = Level.DEBUG;

		static ILogger log_dev = new FileLogger ();

		static bool muted = false;

		public static Level LogLevel
		{
			get { return log_level; }
			set { log_level = value; }
		}

		public static ILogger LogDevice
		{
			get { return log_dev; }
			set { log_dev = value; }
		}

		public static void Debug (string msg, params object[] args)
		{
			Log (Level.DEBUG, msg, args);
		}

		public static void Info (string msg, params object[] args)
		{
			Log (Level.INFO, msg, args);
		}

		public static void Warn (string msg, params object[] args)
		{
			Log (Level.WARN, msg, args);
		}

		public static void Error (string msg, params object[] args)
		{
			Log (Level.ERROR, msg, args);
		}

		public static void Fatal (string msg, params object[] args)
		{
			Log (Level.FATAL, msg, args);
		}

		public static void Log (Level lvl, string msg, params object[] args)
		{
			if (!muted && lvl >= log_level)
				log_dev.Log (lvl, msg, args);
		}

		// This is here to support the original logging, but it should be
		// considered deprecated and old code that uses it should be upgraded to
		// call one of the level specific log methods.
		public static void Log (string msg, params object[] args)
		{
			Log (Level.DEBUG, msg, args);
		}

		public static void Mute ()
		{
			muted = true;
		}

		public static void Unmute ()
		{
			muted = false;
		}
	}
}
