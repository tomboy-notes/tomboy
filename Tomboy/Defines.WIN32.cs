using System;
using System.Reflection;

// TODO: Automate this
[assembly: AssemblyVersion ("1.8.0")]
[assembly: AssemblyProduct("Tomboy")]
[assembly: AssemblyTitle("Tomboy Notes")]

namespace Tomboy {
	public class Defines {
		public static readonly string VERSION = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
		public static readonly string DATADIR = System.IO.Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
		public static readonly string GNOME_LOCALE_DIR = System.IO.Path.Combine (DATADIR, "locale");
		public const string GNOME_HELP_DIR = "@datadir@/gnome/help/tomboy";
		public const string PKGLIBDIR = "@pkglibdir@";
		public static readonly string SYS_ADDINS_DIR = DATADIR;
		public const string TOMBOY_WEBSITE = "http://www.gnome.org/projects/tomboy/";
	}
}


