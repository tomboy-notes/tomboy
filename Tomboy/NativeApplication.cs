using System;

namespace Tomboy
{
	public interface INativeApplication
	{
		void Initialize (string locale_dir,
		                 string display_name,
		                 string process_name,
		                 string [] args);

		void RegisterSessionManagerRestart (string executable_path,
		                                    string[] args,
		                                    string[] environment);
		void RegisterSignalHandlers ();
		event EventHandler ExitingEvent;

		void Exit (int exitcode);
		void StartMainLoop ();

		string ConfDir { get; }

		void OpenUrl (string url, Gdk.Screen screen);

		void DisplayHelp (string help_uri, Gdk.Screen screen);
	}
}
