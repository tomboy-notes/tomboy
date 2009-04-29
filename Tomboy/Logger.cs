using System;
using System.IO;
using System.Runtime.InteropServices;

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
#if WIN32
		[DllImport("kernel32.dll")]
		public static extern bool AttachConsole (uint dwProcessId);
		const uint ATTACH_PARENT_PROCESS = 0x0ffffffff;
		[DllImport("kernel32.dll")]
		public static extern bool FreeConsole ();

		public ConsoleLogger ()
		{
			AttachConsole (ATTACH_PARENT_PROCESS);
		}

		~ConsoleLogger ()
		{
			FreeConsole ();
		}
#endif

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
			console = new ConsoleLogger ();

#if WIN32
			string logfile = Path.Combine(
				Services.NativeApplication.ConfDir,
				"tomboy.log");
#else
			string logfile = Path.Combine(
				Environment.GetEnvironmentVariable ("HOME"),
				".tomboy.log");
#endif

			try {
				log = File.CreateText (logfile);
				log.Flush ();
			} catch (IOException iox) {
				console.Log(Level.WARN, 
					"Failed to create the logfile at {0}: {1}",
					logfile, iox.Message);
			} catch (UnauthorizedAccessException uax) {
				console.Log(Level.WARN,
					"Failed to create the logfile at {0}: {1}",
					logfile, uax.Message);
			}
		}

		~FileLogger ()
		{
			if (log != null)
				try {
					log.Flush ();
				} catch { }
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
			get {
				return log_level;
			}
			set {
				log_level = value;
			}
		}

		public static ILogger LogDevice
		{
			get {
				return log_dev;
			}
			set {
				log_dev = value;
			}
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
		[Obsolete("Loger.Log is deprecated and should be replaced " +
			"with calls to the level specific log methods")]
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
