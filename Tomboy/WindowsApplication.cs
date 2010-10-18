// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 


using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Tomboy
{
	public class WindowsApplication : INativeApplication
	{
		private static string confDir;
		private static string dataDir;
		private static string cacheDir;
		private static string logDir;
		private const string tomboyDirName = "Tomboy";

		static WindowsApplication ()
		{
			string appDataPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
			                                   tomboyDirName);
			string localAppDataPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData),
			                                        tomboyDirName);
			dataDir = Path.Combine (appDataPath, "notes");
			confDir = Path.Combine (appDataPath, "config");
			cacheDir = Path.Combine (localAppDataPath, "cache");
			logDir = localAppDataPath;

			// NOTE: Other directories created on demand
			//       (non-existence is an indicator that migration is needed)
			if (!Directory.Exists (cacheDir))
				Directory.CreateDirectory (cacheDir);
		}

		#region INativeApplication implementation 
		
		public event EventHandler ExitingEvent;
		
		public virtual void Initialize (string locale_dir, string display_name, string process_name, string[] args)
		{
			Gtk.Application.Init ();
		}
		
		public virtual void RegisterSessionManagerRestart (string executable_path, string[] args, string[] environment)
		{
			// Do nothing
		}
		
		public virtual void RegisterSignalHandlers ()
		{
			// Nothing yet, but need to register for native exit signals?
		}
		
		public virtual void Exit (int exitcode)
		{
			if (ExitingEvent != null)
				ExitingEvent (null, new EventArgs ());
			System.Environment.Exit (exitcode);
		}
		
		public virtual void StartMainLoop ()
		{
			Gtk.Application.Run ();
		}

		public string DataDirectory {
			get { return dataDir; }
		}

		public string ConfigurationDirectory {
			get { return confDir; }
		}

		public string CacheDirectory {
			get { return cacheDir; }
		}

		public string LogDirectory {
			get { return logDir; }
		}

		public string PreOneDotZeroNoteDirectory {
			get {
				return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
				                     "tomboy");
			}
		}

		public virtual void OpenUrl (string url, Gdk.Screen screen)
		{
			try {
				System.Diagnostics.Process.Start (url);
			} catch (Exception e) {
				Logger.Error ("Error opening url [{0}]:\n{1}", url, e.ToString ());
			}
		}

		public virtual void DisplayHelp (string project, string page, Gdk.Screen screen)
		{
			string version = Defines.VERSION.Remove (Defines.VERSION.LastIndexOf ('.'));
			OpenUrl (string.Format ("http://library.gnome.org/users/{0}/{1}", project, version), screen);
		}

		#endregion
	}
}
