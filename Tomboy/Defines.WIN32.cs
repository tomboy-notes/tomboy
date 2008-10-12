using System;
using System.Reflection;

// TODO: Automate this
[assembly: AssemblyInformationalVersion ("0.13.0")]

namespace Tomboy {
	public class Defines {
		public const string VERSION = "0.13.0";
		public const string DATADIR = "@datadir@";
		public const string GNOME_LOCALE_DIR = "@datadir@/locale";
		public const string GNOME_HELP_DIR = "@datadir@/gnome/help/tomboy";
		public const string PKGLIBDIR = "@pkglibdir@";
		public const string SYS_ADDINS_DIR = "@pkglibdir@/addins";
		public const string TOMBOY_WEBSITE = "http://www.gnome.org/projects/tomboy/";
	}
}


