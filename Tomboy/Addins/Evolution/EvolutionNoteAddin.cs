
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Web;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Mono.Unix;
using Mono.Unix.Native;

using Tomboy;

// TODO: Indent everything in this namespace in a seperate commit
namespace Tomboy.Evolution
{

	class EvoUtils
	{
		// Cache of account URLs to account UIDs
		static Dictionary<Uri, string> source_url_to_uid;

		static GConf.Client gc;
		static GConf.NotifyEventHandler changed_handler;

		static EvoUtils ()
		{
			try {
				gc = new GConf.Client ();

				changed_handler = new GConf.NotifyEventHandler (OnAccountsSettingChanged);
				gc.AddNotify ("/apps/evolution/mail/accounts", changed_handler);

				source_url_to_uid = new Dictionary<Uri, string> ();
				SetupAccountUrlToUidMap ();
			}
			catch (Exception e) {
				Logger.Error ("Evolution: Error reading accounts: {0}", e);
			}
		}

		static void OnAccountsSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			SetupAccountUrlToUidMap ();
		}

		static void SetupAccountUrlToUidMap ()
		{
			source_url_to_uid.Clear ();

			ICollection accounts = (ICollection) gc.Get ("/apps/evolution/mail/accounts");

			foreach (string xml in accounts) {
				XmlDocument xmlDoc = new XmlDocument ();

				xmlDoc.LoadXml (xml);

				XmlNode account = xmlDoc.SelectSingleNode ("//account");
				if (account == null)
					continue;

				string uid = null;

				foreach (XmlAttribute attr in account.Attributes) {
					if (attr.Name == "uid") {
						uid = attr.InnerText;
						break;
					}
				}

				if (uid == null)
					continue;

				XmlNode source_url = xmlDoc.SelectSingleNode ("//source/url");
				if (source_url == null || source_url.InnerText == null)
					continue;

				try {
					Uri uri = new Uri (source_url.InnerText);
					source_url_to_uid [uri] = uid;
				} catch (System.UriFormatException) {
					Logger.Error (
					        "Evolution: Unable to parse account source URI \"{0}\"",
					        source_url.InnerText);
				}
			}
		}

		//
		// Duplicates
		// evolution/mail/mail-config.c:mail_config_get_account_by_source_url...
		//
		static string FindAccountUidForUri (Uri uri)
		{
			if (source_url_to_uid == null)
				return null;

			string account_uid = null;

			foreach (Uri source_uri in source_url_to_uid.Keys) {
				if (uri.Scheme != source_uri.Scheme)
					continue;

				bool match = false;

				// FIXME: check "authmech" matches too
				switch (uri.Scheme) {
				case "pop":
				case "sendmail":
				case "smtp":
					match = (uri.PathAndQuery == source_uri.PathAndQuery) &&
					        (uri.Authority == source_uri.Authority) &&
					        (uri.UserInfo == source_uri.UserInfo);
					break;

				case "mh":
				case "mbox":
				case "maildir":
				case "spool":
					// FIXME: Do some path canonicalization here?
					match = (uri.PathAndQuery == source_uri.PathAndQuery) &&
					        (uri.Authority == source_uri.Authority) &&
					        (uri.UserInfo == source_uri.UserInfo);
					break;

				case "imap":
				case "imap4":
				case "imapp":
				case "groupwise":
				case "nntp":
				case "exchange":
					match = (uri.Authority == source_uri.Authority) &&
					        (uri.UserInfo == source_uri.UserInfo);
					break;
				}

				if (match) {
					account_uid = source_url_to_uid [source_uri];
					Logger.Info ("Evolution: Matching account '{0}'...",
					            account_uid);
					break;
				}
			}

			return account_uid;
		}

		//
		// Duplicates evolution/mail/em-utils.c:em_uri_from_camel...
		//
		public static string EmailUriFromDropUri (string drop_uri)
		{
			if (drop_uri.StartsWith ("email:"))
				return drop_uri;

			Uri uri = new Uri (drop_uri);
			string account_uid = null;
			string path;

			if (drop_uri.StartsWith ("vfolder:"))
				account_uid = "vfolder@local";
			else {
				account_uid = FindAccountUidForUri (uri);

				if (account_uid == null)
					account_uid = "local@local";
			}

			switch (uri.Scheme) {
			case "imap4":
			case "mh":
			case "mbox":
			case "maildir":
			case "spool":
				// These schemes keep the folder as the URI fragment
				path = uri.Fragment;
				break;
			default:
				path = uri.AbsolutePath;
				break;
			}

			if (path != string.Empty) {
				// Skip leading '/' or '#'...
				path = path.Substring (1);
				
				// Some of the special characters in path are escaped (ex: " ",
				// "%", ...) but other like ";" and "?" are not. This ensures
				// that all special characters are escaped.
				path = Uri.EscapeDataString (Uri.UnescapeDataString (path));
			}

			return string.Format ("email://{0}/{1}", account_uid, path);
		}

		public static string EmailUriFromDropUri (string drop_uri, string uid)
		{
			string email_uri = EmailUriFromDropUri (drop_uri);

			if (email_uri == null)
				return null;
			else
				return string.Format ("{0};uid={1}", email_uri, uid);
		}
	}

	public class EmailLink : DynamicNoteTag
	{
		static Gdk.Pixbuf mail_icon = null;
		
		static Gdk.Pixbuf MailIcon
		{
			get {
				if (mail_icon == null)
					mail_icon =
						GuiUtils.GetIcon (
							System.Reflection.Assembly.GetExecutingAssembly (),
							"mail",
							16);
				return mail_icon;
			}
		}
		
		public EmailLink ()
: base ()
		{
		}

		public override void Initialize (string element_name)
		{
			base.Initialize (element_name);

			Underline = Pango.Underline.Single;
			Foreground = "blue";
			CanActivate = true;

			Image = MailIcon;
		}

		public string EmailUri
		{
			get {
				return (string) Attributes ["uri"];
			}
			set {
				Attributes ["uri"] = value;
			}
		}

		protected override bool OnActivate (NoteEditor editor,
		                                    Gtk.TextIter start,
		                                    Gtk.TextIter end)
		{
			Process p = new Process ();
			p.StartInfo.FileName = "evolution";
			p.StartInfo.Arguments = EmailUri;
			p.StartInfo.UseShellExecute = false;

			try {
				p.Start ();
			} catch (Exception e) {
				string message = String.Format ("Error running Evolution: {0}",
				                                e.Message);
				Logger.Error (message);
				HIGMessageDialog dialog =
				        new HIGMessageDialog (editor.Toplevel as Gtk.Window,
				                              Gtk.DialogFlags.DestroyWithParent,
				                              Gtk.MessageType.Info,
				                              Gtk.ButtonsType.Ok,
				                              Catalog.GetString ("Cannot open email"),
				                              message);
				dialog.Run ();
				dialog.Destroy ();
			}

			return true;
		}
	}


	public class EvolutionNoteAddin : NoteAddin
	{
		// Used in the two-phase evolution drop handling.
		List<string> xuid_list;
		List<string> subject_list;

		static EvolutionNoteAddin ()
		{
			GMime.Global.Init();
		}

		public override void Initialize ()
		{
			if (!Note.TagTable.IsDynamicTagRegistered ("link:evo-mail")) {
				Note.TagTable.RegisterDynamicTag ("link:evo-mail", typeof (EmailLink));
			}
		}

		Gtk.TargetList TargetList
		{
			get {
				return Gtk.Drag.DestGetTargetList (Window.Editor);
			}
		}

		public override void Shutdown ()
		{
			if (HasWindow)
				TargetList.Remove (Gdk.Atom.Intern ("x-uid-list", false));
		}

		public override void OnNoteOpened ()
		{
			TargetList.Add (Gdk.Atom.Intern ("x-uid-list", false), 0, 99);
			Window.Editor.DragDataReceived += OnDragDataReceived;
		}

		[DllImport("libgobject-2.0.so.0")]
		static extern void g_signal_stop_emission_by_name (IntPtr raw, string name);

		//
		// DND Drop Handling
		//
		[GLib.ConnectBefore]
		void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
		{
			bool stop_emission = false;

			if (args.SelectionData.Length < 0)
				return;

			if (args.Info == 1) {
				foreach (Gdk.Atom atom in args.Context.TargetList ()) {
					if (atom.Name == "x-uid-list") {
						// Parse MIME mails in tmp files
						DropEmailUriList (args);

						Gtk.Drag.GetData (Window.Editor,
						                  args.Context,
						                  Gdk.Atom.Intern ("x-uid-list", false),
						                  args.Time);

						Gdk.Drag.Status (args.Context,
						                 Gdk.DragAction.Link,
						                 args.Time);
						stop_emission = true;
					}
				}
			} else if (args.Info == 99) {
				// Parse the evolution internal URLs and generate email:
				// URLs we can pass on the commandline.
				DropXUidList (args);

				// Insert the email: links into the note, using the
				// message subject as the display text.
				InsertMailLinks (args.X, args.Y, xuid_list, subject_list);

				Gtk.Drag.Finish (args.Context, true, false, args.Time);
				stop_emission = true;
			}

			// No return value for drag_data_received so no way to stop the
			// default TextView handler from running without resorting to
			// violence...
			if (stop_emission) {
				g_signal_stop_emission_by_name(Window.Editor.Handle,
				                               "drag_data_received");
			}
		}

		void DropEmailUriList (Gtk.DragDataReceivedArgs args)
		{
			string uri_string = Encoding.UTF8.GetString (args.SelectionData.Data);

			subject_list = new List<string>();

			UriList uri_list = new UriList (uri_string);
			foreach (Uri uri in uri_list) {
				Logger.Debug ("Evolution: Dropped URI: {0}", uri.LocalPath);

				int mail_fd = Syscall.open (uri.LocalPath, OpenFlags.O_RDONLY);
				if (mail_fd == -1)
					continue;

				GMime.Stream stream = new GMime.StreamFs (mail_fd);
				GMime.Parser parser = new GMime.Parser (stream);
				parser.ScanFrom = true;

				// Use GMime to read the RFC822 message bodies (in temp
				// files pointed to by a uri-list) in MBOX format, so we
				// can get subject/sender/date info.
				while (!parser.Eos()) {
					GMime.Message message = parser.ConstructMessage ();
					if (message == null)
						break;
					
					Logger.Debug ("Evolution: Message Subject: {0}", message.Subject);
					subject_list.Add (message.Subject);
					message.Dispose ();
				};

				parser.Dispose ();
				stream.Close ();
				stream.Dispose ();
			}
		}

		void DropXUidList (Gtk.DragDataReceivedArgs args)
		{
			// FIXME: x-uid-list is an Evolution-internal drop type.
			//        We shouldn't be using it, but there is no other way
			//        to get the info we need to be able to generate email:
			//        URIs evo will open on the command-line.
			//
			// x-uid-list Format: "uri\0uid1\0uid2\0uid3\0...\0uidn"
			//
			string source = Encoding.UTF8.GetString (args.SelectionData.Data);
			string [] list = source.Split ('\0');

			xuid_list = new List<string> ();

			Logger.Debug ("Evolution: Dropped XUid: uri = '{0}'", list [0]);

			for (int i = 1; i < list.Length; i++) {
				if (list [i] == string.Empty)
					continue;

				string launch_uri =
				        EvoUtils.EmailUriFromDropUri (list [0] /* evo account uri */,
				                                      list [i] /* message uid */);

				Logger.Debug ("Evolution: Translating XUid uid='{0}' to uri='{1}'",
				            list [i],
				            launch_uri);

				xuid_list.Add (launch_uri);
			}
		}

		void InsertMailLinks (int x, int y, List<string> xuid_list, List<string> subject_list)
		{
			int message_idx = 0;
			bool more_than_one = false;

			// Place the cursor in the position where the uri was
			// dropped, adjusting x,y by the TextView's VisibleRect.
			Gdk.Rectangle rect = Window.Editor.VisibleRect;
			x = x + rect.X;
			y = y + rect.Y;
			Gtk.TextIter cursor = Window.Editor.GetIterAtLocation (x, y);
			Buffer.PlaceCursor (cursor);

			foreach (string subject in subject_list) {
				int start_offset;

				if (more_than_one) {
					cursor = Buffer.GetIterAtMark (Buffer.InsertMark);

					if (cursor.LineOffset == 0)
						Buffer.Insert (ref cursor, "\n");
					else
						Buffer.Insert (ref cursor, ", ");
				}

				string launch_uri = xuid_list [message_idx++];

				EmailLink link_tag;
				link_tag = (EmailLink) Note.TagTable.CreateDynamicTag ("link:evo-mail");
				link_tag.EmailUri = launch_uri;

				cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
				start_offset = cursor.Offset;
				Buffer.Insert (ref cursor, subject);

				Gtk.TextIter start = Buffer.GetIterAtOffset (start_offset);
				Gtk.TextIter end = Buffer.GetIterAtMark (Buffer.InsertMark);
				Buffer.ApplyTag (link_tag, start, end);

				more_than_one = true;
			}
		}
	}

}
