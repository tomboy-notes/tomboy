
using System;
using System.IO;
using System.Xml;
using Mono.Unix;

using Tomboy.Sync;

namespace Tomboy
{
	public class Tomboy : Application
	{
		static bool debugging;
		static bool uninstalled;
		static NoteManager manager;
		static TomboyTrayIcon tray_icon;
		static TomboyTray tray = null;
		static bool tray_icon_showing = false;
		static bool is_panel_applet = false;
		static PreferencesDialog prefs_dlg;
		static SyncDialog sync_dlg;
		static RemoteControl remote_control;
		static Gtk.IconTheme icon_theme = null;

		[STAThread]
		public static void Main (string [] args)
		{
			// TODO: Extract to a PreInit in Application, or something
#if WIN32
			string tomboy_path =
				Environment.GetEnvironmentVariable ("TOMBOY_PATH_PREFIX");
			string tomboy_gtk_basepath =
				Environment.GetEnvironmentVariable ("TOMBOY_GTK_BASEPATH");
			Environment.SetEnvironmentVariable ("GTK_BASEPATH",
				tomboy_gtk_basepath ?? string.Empty);
			if (string.IsNullOrEmpty (tomboy_path)) {
				string gtk_lib_path = null;
				try {
					gtk_lib_path = (string)
						Microsoft.Win32.Registry.GetValue (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\AssemblyFolders\GtkSharp",
						                                   string.Empty,
						                                   string.Empty);
				} catch (Exception e) {
					Console.WriteLine ("Exception while trying to get GTK# install path: " +
					                   e.ToString ());
				}
				if (!string.IsNullOrEmpty (gtk_lib_path))
					tomboy_path =
						gtk_lib_path.Replace ("lib\\gtk-sharp-2.0", "bin");
			}
			if (!string.IsNullOrEmpty (tomboy_path))
				Environment.SetEnvironmentVariable ("PATH",
				                                    tomboy_path +
				                                    Path.PathSeparator +
				                                    Environment.GetEnvironmentVariable ("PATH"));
#endif
			Catalog.Init ("tomboy", Defines.GNOME_LOCALE_DIR);

			TomboyCommandLine cmd_line = new TomboyCommandLine (args);
			debugging = cmd_line.Debug;
			uninstalled = cmd_line.Uninstalled;

			if (!RemoteControlProxy.FirstInstance) {
				if (!cmd_line.NeedsExecute)
					cmd_line = new TomboyCommandLine (new string [] {"--search"});
				// Execute args at an existing tomboy instance...
				cmd_line.Execute ();
				Console.WriteLine ("Tomboy is already running.  Exiting...");
				return;
			}

			Logger.LogLevel = debugging ? Level.DEBUG : Level.INFO;
#if PANEL_APPLET
			is_panel_applet = cmd_line.UsePanelApplet;
#else
			is_panel_applet = false;
#endif

			// NOTE: It is important not to use the Preferences
			//       class before this call.
			Initialize ("tomboy", "Tomboy", "tomboy", args);

			// Add private icon dir to search path
			icon_theme = Gtk.IconTheme.Default;
			icon_theme.AppendSearchPath (Path.Combine (Path.Combine (Defines.DATADIR, "tomboy"), "icons"));

			// Create the default note manager instance.
			string note_path = GetNotePath (cmd_line.NotePath);
			manager = new NoteManager (note_path);

			SetupGlobalActions ();
			ActionManager am = Tomboy.ActionManager;

			// TODO: Instead of just delaying, lazy-load
			//       (only an issue for add-ins that need to be
			//       available at Tomboy startup, and restoring
			//       previously-opened notes)
			GLib.Timeout.Add (500, () => {
				manager.Initialize ();
				SyncManager.Initialize ();

				ApplicationAddin [] addins =
				        manager.AddinManager.GetApplicationAddins ();
				foreach (ApplicationAddin addin in addins) {
					addin.Initialize ();
				}

				// Register the manager to handle remote requests.
				RegisterRemoteControl (manager);
				if (cmd_line.NeedsExecute) {
					// Execute args on this instance
					cmd_line.Execute ();
				}
#if WIN32
				if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
					var os_version = Environment.OSVersion.Version;
					if (( os_version.Major == 6 && os_version.Minor > 0 ) || os_version.Major > 6) {
						JumpListManager.CreateJumpList (manager);

						manager.NoteAdded += delegate (object sender, Note changed) {
							JumpListManager.CreateJumpList (manager);
						};

						manager.NoteRenamed += delegate (Note sender, string old_title) {
							JumpListManager.CreateJumpList (manager);
						};

						manager.NoteDeleted += delegate (object sender, Note changed) {
							JumpListManager.CreateJumpList (manager);
						};
					}
				}
#endif
				return false;
			});

#if PANEL_APPLET
			if (is_panel_applet) {
				tray_icon_showing = true;

				// Show the Close item and hide the Quit item
				am ["CloseWindowAction"].Visible = true;
				am ["QuitTomboyAction"].Visible = false;

				RegisterPanelAppletFactory ();
				Logger.Debug ("All done.  Ciao!");
				Exit (0);
			}
#endif
			RegisterSessionManagerRestart (
			        Environment.GetEnvironmentVariable ("TOMBOY_WRAPPER_PATH"),
			        args,
			        new string [] { "TOMBOY_PATH=" + note_path  }); // TODO: Pass along XDG_*?
			StartTrayIcon ();

			Logger.Debug ("All done.  Ciao!");
		}

		public static bool Debugging
		{
			get { return debugging; }
		}

		public static bool Uninstalled
		{
			get { return uninstalled; }
		}

		static string GetNotePath (string override_path)
		{
			// Default note location, as specified in --note-path or $TOMBOY_PATH
			string note_path = (override_path != null) ?
			        override_path :
			        Environment.GetEnvironmentVariable ("TOMBOY_PATH");
			if (note_path == null)
				note_path = Services.NativeApplication.DataDirectory;

			// Tilde expand
			return note_path.Replace ("~", Environment.GetEnvironmentVariable ("HOME")); // TODO: Wasted work
		}

		static void RegisterPanelAppletFactory ()
		{
			// This will block if there is no existing instance running
#if !WIN32 && !MAC
#if PANEL_APPLET
			Gnome.PanelAppletFactory.Register (typeof (TomboyApplet));
#endif
#endif
		}

		static void StartTrayIcon ()
		{
			// Create the tray icon and run the main loop
			tray_icon = new TomboyTrayIcon (manager);
			tray = tray_icon.Tray;
			StartMainLoop ();
		}

		static void RegisterRemoteControl (NoteManager manager)
		{
			try {
				remote_control = RemoteControlProxy.Register (manager);
				if (remote_control != null) {
					Logger.Debug ("Tomboy remote control active.");
				} else {
					// If Tomboy is already running, open the search window
					// so the user gets some sort of feedback when they
					// attempt to run Tomboy again.
					IRemoteControl remote = null;
					try {
						remote = RemoteControlProxy.GetInstance ();
						remote.DisplaySearch ();
					} catch {}

					Logger.Error ("Tomboy is already running.  Exiting...");
					System.Environment.Exit (-1);
				}
			} catch (Exception e) {
				Logger.Warn ("Tomboy remote control disabled (DBus exception): {0}",
				            e.Message);
			}
		}

		// These actions can be called from anywhere in Tomboy
		static void SetupGlobalActions ()
		{
			ActionManager am = Tomboy.ActionManager;
			am ["NewNoteAction"].Activated += OnNewNoteAction;
			am ["QuitTomboyAction"].Activated += OnQuitTomboyAction;
			am ["ShowPreferencesAction"].Activated += OnShowPreferencesAction;
			am ["ShowHelpAction"].Activated += OnShowHelpAction;
			am ["ShowAboutAction"].Activated += OnShowAboutAction;
			am ["TrayNewNoteAction"].Activated += OnNewNoteAction;
			am ["ShowSearchAllNotesAction"].Activated += OpenSearchAll;
			am ["NoteSynchronizationAction"].Activated += OpenNoteSyncWindow;
		}

		static void OnNewNoteAction (object sender, EventArgs args)
		{
			try {
				Note new_note = manager.Create ();
				new_note.Window.Show ();
			} catch (Exception e) {
				HIGMessageDialog dialog =
				        new HIGMessageDialog (
				        null,
				        0,
				        Gtk.MessageType.Error,
				        Gtk.ButtonsType.Ok,
				        Catalog.GetString ("Cannot create new note"),
				        e.Message);
				dialog.Run ();
				dialog.Destroy ();
			}
		}

		static void OpenNoteSyncWindow (object sender, EventArgs args)
		{
			if (sync_dlg == null) {
				sync_dlg = new SyncDialog ();
				sync_dlg.Response += OnSyncDialogResponse;
			}

			sync_dlg.Present ();
		}

		static void OnSyncDialogResponse (object sender, Gtk.ResponseArgs args)
		{
			((Gtk.Widget) sender).Destroy ();
			sync_dlg = null;
		}

		static void OnQuitTomboyAction (object sender, EventArgs args)
		{
			if (Tomboy.IsPanelApplet)
				return; // Ignore the quit action

			Logger.Debug ("Quitting Tomboy.  Ciao!");
			Exit (0);
		}

		static void OnShowPreferencesAction (object sender, EventArgs args)
		{
			if (prefs_dlg == null) {
				prefs_dlg = new PreferencesDialog (manager.AddinManager);
				prefs_dlg.Response += OnPreferencesResponse;
			}
			prefs_dlg.Present ();
		}

		static void OnPreferencesResponse (object sender, Gtk.ResponseArgs args)
		{
			((Gtk.Widget) sender).Destroy ();
			prefs_dlg = null;
		}

		static void OnShowHelpAction (object sender, EventArgs args)
		{
			Gdk.Screen screen = null;
			if (tray_icon != null) {
#if WIN32 || MAC
				screen = tray_icon.Tray.TomboyTrayMenu.Screen;
#else
				Gdk.Rectangle area;
				Gtk.Orientation orientation;
				tray_icon.GetGeometry (out screen, out area, out orientation);
#endif
			}
			GuiUtils.ShowHelp ("tomboy", null, screen, null);

		}

		static void OnShowAboutAction (object sender, EventArgs args)
		{
			string [] authors = new string [] {
				Catalog.GetString ("Primary Development:"),
				"\tAlex Graveley (original author)",
				"\tAaron Borden (maintainer)",
				"\t\t<adborden@live.com>",
				"\tBenjamin Podszun (maintainer)",
				"\t\t<benjamin.podszun@gmail.com>",
				"\tGreg Poirier (maintainer)",
				"\t\t<grep@binary-snobbery.com>",
				"\tJared Jennings (maintainer)",
				"\t\t<jaredljennings@gmail.com>",
				"\tSandy Armstrong (retired maintainer)",
				"\tBoyd Timothy (retired maintainer)",
				"",
				Catalog.GetString ("Contributors:"),
				"\tAaron Bockover",
				"\tAbhinav Upadhyay",
				"\tAlejandro Cura",
				"\tAlexey Nedilko",
				"\tAlex Kloss",
				"\tAlex Tereschenko",
				"\tAnders Petersson",
				"\tAndrew Fister",
				"\tBrian Mattern",
				"\tBrion Vibber",
				"\tBuchner Johannes",
				"\tCarlos Arenas",
				"\tChris Scobell",
				"\tClemens N. Buss",
				"\tCory Thomas",
				"\tDave Foster",
				"\tDavid Trowbridge",
				"\tDoug Johnston",
				"\tEveraldo Canuto",
				"\tFrederic Crozat",
				"\tGabriel Burt",
				"\tGabriel de Perthuis",
				"\tIain Lane",
				"\tJakub Steiner",
				"\tJames Westby",
				"\tJamin Philip Gray",
				"\tJan Rüegg",
				"\tJavier Jardón",
				"\tJay R. Wren",
				"\tJeffrey Stedfast",
				"\tJeff Stoner",
				"\tJeff Tickle",
				"\tJerome Haltom",
				"\tJoe Shaw",
				"\tJohn Anderson",
				"\tJohn Carr",
				"\tJon Lund Steffensen",
				"\tJP Rosevear",
				"\tKevin Kubasik",
				"\tLaurent Bedubourg",
				"\tLukas Vacek",
				"\tŁukasz Jernaś",
				"\tMark Wakim",
				"\tMathias Hasselmann",
				"\tMatthew Pirocchi",
				"\tMatt Johnston",
				"\tMatt Jones",
				"\tMatt Rajca",
				"\tMax Lin",
				"\tMichael Fletcher",
				"\tMike Mazur",
				"\tNathaniel Smith",
				"\tOlav Vitters",
				"\tOlivier Le Thanh Duong",
				"\tOwen Williams",
				"\tPaul Cutler",
				"\tPavol Klačanský",
				"\tPrzemysław Grzegorczyk",
				"\tRobert Buchholz",
				"\tRobert Nordan",
				"\tRobin Sonefors",
				"\tRodrigo Moya",
				"\tRomain Tartiere",
				"\tRyan Lortie",
				"\tSebastian Dröge",
				"\tSebastian Rittau",
				"\tStefan Cosma",
				"\tStefan Schweizer",
				"\tTobias Abenius",
				"\tTommi Asiala",
				"\tWouter Bolsterlee",
				"\tYonatan Oren"
			};

			string [] documenters = new string [] {
				"Alex Graveley <alex@beatniksoftware.com>",
				"Boyd Timothy <btimothy@gmail.com>",
				"Brent Smith <gnome@nextreality.net>",
				"Laurent Codeur <laurentc@iol.ie>",
				"Paul Cutler <pcutler@foresightlinux.org>",
				"Sandy Armstrong <sanfordarmstrong@gmail.com>",
				"Stefan Schweizer <steve.schweizer@gmail.com>"
			};

			string translators = Catalog.GetString ("translator-credits");
			if (translators == "translator-credits")
				translators = null;

			Gtk.AboutDialog about = new Gtk.AboutDialog ();
			about.Name = "Tomboy";
			about.Version = Defines.VERSION;
			about.Logo = GuiUtils.GetIcon ("tomboy", 48);
			about.Copyright =
			        Catalog.GetString ("Copyright \xa9 2004-2007 Alex Graveley\n" +
				                   "Copyright \xa9 2004-2011 Others\n");
			about.Comments = Catalog.GetString ("A simple and easy to use desktop " +
			                                    "note-taking application.");
			Gtk.AboutDialog.SetUrlHook (delegate (Gtk.AboutDialog dialog, string link) {
				try {
					Services.NativeApplication.OpenUrl (link, null);
				} catch (Exception e) {
					GuiUtils.ShowOpeningLocationError (dialog, link, e.Message);
				}
			}); 
			about.Website = Defines.TOMBOY_WEBSITE;
			about.WebsiteLabel = Catalog.GetString("Homepage");
			about.Authors = authors;
			about.Documenters = documenters;
			about.TranslatorCredits = translators;
			about.IconName = "tomboy";
			about.Response += delegate {
				about.Destroy ();
			};
			about.Present ();
		}

		static void OpenSearchAll (object sender, EventArgs args)
		{
			NoteRecentChanges.GetInstance (manager).Present ();
		}

		public static NoteManager DefaultNoteManager
		{
			get {
				return manager;
			}
		}

		public static bool TrayIconShowing
		{
			get {
				tray_icon_showing = !is_panel_applet && tray_icon != null &&
					tray_icon.IsEmbedded && tray_icon.Visible;
				return tray_icon_showing;
			}
		}

		public static bool IsPanelApplet
		{
			get {
				return is_panel_applet;
			}
		}

		public static TomboyTray Tray
		{
			get {
				return tray;
			} set {
				tray = value;
			}
		}

		public static SyncDialog SyncDialog
		{
			get {
				return sync_dlg;
			}
		}
	}

	public class TomboyCommandLine
	{
		bool debug;
		bool new_note;
		bool panel_applet;
		string new_note_name;
		bool open_start_here;
		string open_note_uri;
		string open_note_name;
		string open_external_note_path;
		string highlight_search;
		string note_path;
		string search_text;
		bool open_search;

		public TomboyCommandLine (string [] args)
		{
			Parse (args);
		}

		// TODO: Document this option
		public bool Debug
		{
			get { return debug; }
		}

		// TODO: Document this option
		public bool Uninstalled
		{
			get; private set;
		}

		public bool UsePanelApplet
		{
			get {
				return panel_applet;
			}
		}

		public bool NeedsExecute
		{
			get {
				return new_note ||
				open_note_name != null ||
				open_note_uri != null ||
				open_search ||
				open_start_here ||
				open_external_note_path != null;
			}
		}

		public string NotePath
		{
			get {
				return note_path;
			}
		}

		public static void PrintAbout ()
		{
			string about =
			        Catalog.GetString (
			                "Tomboy: A simple, easy to use desktop note-taking " +
			                "application.\n" +
			                "Copyright \xa9 2004-2007 Alex Graveley\n" +
							"<alex@beatniksoftware.com>\n\n" +		
							"Copyright \xa9 2004-2011 Others\n"
							);

			Console.Write (about);
		}

		public static void PrintUsage ()
		{
			string usage =
			        Catalog.GetString (
			                "Usage:\n" +
			                "  --version\t\t\tPrint version information.\n" +
			                "  --help\t\t\tPrint this usage message.\n" +
			                "  --note-path [path]\t\tLoad/store note data in this " +
			                "directory.\n" +
			                "  --search [text]\t\tOpen the search all notes window with " +
			                "the search text.\n");
			// This odd concatenation preserved to avoid wasting time retranslating these strings
			usage +=
			        Catalog.GetString (
			                "  --new-note\t\t\tCreate and display a new note.\n" +
			                "  --new-note [title]\t\tCreate and display a new note, " +
			                "with a title.\n" +
			                "  --open-note [title/url]\tDisplay the existing note " +
			                "matching title.\n" +
			                "  --start-here\t\t\tDisplay the 'Start Here' note.\n" +
			                "  --highlight-search [text]\tSearch and highlight text " +
			                "in the opened note.\n");

			Console.WriteLine (usage);
		}

		public static void PrintVersion()
		{
			Console.WriteLine (Catalog.GetString ("Version {0}"), Defines.VERSION);
		}

		public void Parse (string [] args)
		{
			for (int idx = 0; idx < args.Length; idx++) {
				bool quit = false;

				switch (args [idx]) {
				case "--debug":
					debug = true;
					break;
				case "--uninstalled":
					Uninstalled = true;
					break;
				case "--new-note":
					// Get optional name for new note...
					if (idx + 1 < args.Length
					                && args [idx + 1] != null
					                && args [idx + 1] != String.Empty
					                && args [idx + 1][0] != '-') {
						new_note_name = args [++idx];
					}

					new_note = true;
					break;

				case "--open-note":
					// Get required name for note to open...
					if (idx + 1 >= args.Length ||
					                (args [idx + 1] != null
					                 && args [idx + 1] != String.Empty
					                 && args [idx + 1][0] == '-')) {
						PrintUsage ();
						quit = true;
					}

					++idx;

					// If the argument looks like a Uri, treat it like a Uri.
					if (args [idx].StartsWith ("note://tomboy/"))
						open_note_uri = args [idx];
					else if (File.Exists (args [idx])) {
						// This is potentially a note file
						open_external_note_path = args [idx];
					} else
						open_note_name = args [idx];

					break;

				case "--start-here":
					// Open the Start Here note
					open_start_here = true;
					break;

				case "--highlight-search":
					// Get required search string to highlight
					if (idx + 1 >= args.Length ||
					                (args [idx + 1] != null
					                 && args [idx + 1] != String.Empty
					                 && args [idx + 1][0] == '-')) {
						PrintUsage ();
						quit = true;
					}

					++idx;
					highlight_search = args [idx];
					break;

				case "--panel-applet":
					panel_applet = true;
					break;

				case "--note-path":
					if (idx + 1 >= args.Length ||
					                (args [idx + 1] != null
					                 && args [idx + 1] != String.Empty
					                 && args [idx + 1][0] == '-')) {
						PrintUsage ();
						quit = true;
					}

					note_path = args [++idx];

					if (!Directory.Exists (note_path)) {
						Console.WriteLine (
						        "Tomboy: Invalid note path: " +
						        "\"{0}\" does not exist.",
						        note_path);
						quit = true;
					}

					break;

				case "--search":
					// Get optional search text...
					if (idx + 1 < args.Length
					                && args [idx + 1] != null
					                && args [idx + 1] != String.Empty
					                && args [idx + 1][0] != '-') {
						search_text = args [++idx];
					}

					open_search = true;
					break;

				case "--version":
					PrintAbout ();
					PrintVersion();
					quit = true;
					break;

				case "--help":
				case "--usage":
					PrintAbout ();
					PrintUsage ();
					quit = true;
					break;

				default:
					break;
				}

				if (quit == true)
					System.Environment.Exit (1);
			}
		}

		public void Execute ()
		{
			IRemoteControl remote = null;
			try {
				remote = RemoteControlProxy.GetInstance ();
			} catch (Exception e) {
				Logger.Error ("Unable to connect to Tomboy remote control: {0}",
				            e.Message);
			}

			if (remote == null)
				return;

			if (new_note) {
				string new_uri;

				if (new_note_name != null) {
					new_uri = remote.FindNote (new_note_name);

					if (new_uri == null || new_uri == string.Empty)
						new_uri = remote.CreateNamedNote (new_note_name);
				} else
					new_uri = remote.CreateNote ();

				if (new_uri != null)
					remote.DisplayNote (new_uri);
			}

			if (open_start_here)
				open_note_uri = remote.FindStartHereNote ();

			if (open_note_name != null)
				open_note_uri = remote.FindNote (open_note_name);

			if (open_note_uri != null) {
				if (highlight_search != null)
					remote.DisplayNoteWithSearch (open_note_uri,
					                              highlight_search);
				else
					remote.DisplayNote (open_note_uri);
			}

			if (open_external_note_path != null) {
				string note_id = Path.GetFileNameWithoutExtension (open_external_note_path);
				if (note_id != null && note_id != string.Empty) {
					// Attempt to load the note, assuming it might already
					// be part of our notes list.
					if (remote.DisplayNote (
					                        string.Format ("note://tomboy/{0}", note_id)) == false) {

						StreamReader sr = File.OpenText (open_external_note_path);
						if (sr != null) {
							string noteTitle = null;
							string noteXml = sr.ReadToEnd ();

							// Make sure noteXml is parseable
							XmlDocument xmlDoc = new XmlDocument ();
							try {
								xmlDoc.LoadXml (noteXml);
							} catch {
							noteXml = null;
						}

						if (noteXml != null) {
								noteTitle = NoteArchiver.Instance.GetTitleFromNoteXml (noteXml);
								if (noteTitle != null) {
									// Check for conflicting titles
									string baseTitle = (string)noteTitle.Clone ();
									for (int i = 1; remote.FindNote (noteTitle) != string.Empty; i++)
										noteTitle = baseTitle + " (" + i.ToString() + ")";

									string note_uri = remote.CreateNamedNote (noteTitle);

									// Update title in the note XML
									noteXml = NoteArchiver.Instance.GetRenamedNoteXml (noteXml, baseTitle, noteTitle);

									if (note_uri != null) {
										// Load in the XML contents of the note file
										if (remote.SetNoteCompleteXml (note_uri, noteXml))
											remote.DisplayNote (note_uri);
									}
								}
							}
						}
					}
				}
			}

			if (open_search) {
				if (search_text != null)
					remote.DisplaySearchWithText (search_text);
				else
					remote.DisplaySearch ();
			}
		}
	}
}
