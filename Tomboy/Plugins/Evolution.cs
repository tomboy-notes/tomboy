
using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Web;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Mono.Unix;
using Mono.Unix.Native;

using Tomboy;

class EvoUtils 
{
	// Cache of account URLs to account UIDs
	static Hashtable source_url_to_uid;

	static GConf.Client gc;
	static GConf.NotifyEventHandler changed_handler;

	static EvoUtils ()
	{
		try {
			gc = new GConf.Client ();
	    
			changed_handler = new GConf.NotifyEventHandler (OnAccountsSettingChanged);
			gc.AddNotify ("/apps/evolution/mail/accounts", changed_handler);
				
			source_url_to_uid = new Hashtable ();
			SetupAccountUrlToUidMap ();
		} 
		catch (Exception e) {
			Logger.Log ("Evolution: Error reading accounts: {0}", e);
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
			    Logger.Log (
				    "Evolution: Unable to parse account source URI \"{0}\"",
				    source_url.InnerText);
			}
		}
	}

	//
	// Duplicates evoluion/camel/camel-url.c:camel_url_encode and
	// escapes ";?" as well.
	//
	static string CamelUrlEncode (string uri_part)
	{
		// The characters Camel encodes
		const string camel_escape_chars = " \"%#<>{}|\\^[]`;?";

		StringBuilder builder = new StringBuilder (null);
		foreach (char c in uri_part) {
			if (camel_escape_chars.IndexOf (c) > -1)
				builder.Append (Uri.HexEscape (c));
			else
				builder.Append (c);
		}

		return builder.ToString ();
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
				account_uid = (string) source_url_to_uid [source_uri];
				Logger.Log ("Evolution: Matching account '{0}'...", 
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
			path = CamelUrlEncode (path);
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

		Image = new Gdk.Pixbuf (null, "stock_mail.png");
	}

	public string EmailUri
	{
		get { return (string) Attributes ["uri"]; }
		set { Attributes ["uri"] = value; }
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
			Logger.Log (message);
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

[PluginInfo(
	"Evolution Plugin", Defines.VERSION,
	"Alex Graveley <alex@beatniksoftware.com>",
	"Allows you to drag an email from Evolution into a tomboy note.  The " +
	"message subject is added as a link in the note."
	)]
public class EvolutionPlugin : NotePlugin
{
	// Used in the two-phase evolution drop handling.
	ArrayList xuid_list;
	ArrayList subject_list;

	static EvolutionPlugin ()
	{
		GMime.Global.Init();
	}

	protected override void Initialize ()
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

	protected override void Shutdown ()
	{
		if (HasWindow)
			TargetList.Remove (Gdk.Atom.Intern ("x-uid-list", false));
	}

	protected override void OnNoteOpened () 
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

		if (args.Info == 1) {
			foreach (Gdk.Atom atom in args.Context.Targets) {
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
			InsertMailLinks (xuid_list, subject_list);

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

		subject_list = new ArrayList();

		UriList uri_list = new UriList (uri_string);
		foreach (Uri uri in uri_list) {
			Logger.Log ("Evolution: Dropped URI: {0}", uri.LocalPath);

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

				string subject = GMime.Utils.HeaderDecodePhrase (message.Subject);
				subject_list.Add (subject);
				message.Dispose ();

				Logger.Log ("Evolution: Message Subject: {0}", subject);
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

		xuid_list = new ArrayList ();

		Logger.Log ("Evolution: Dropped XUid: uri = '{0}'", list [0]);

		for (int i = 1; i < list.Length; i++) {
			if (list [i] == string.Empty)
				continue;

			string launch_uri = 
				EvoUtils.EmailUriFromDropUri (list [0] /* evo account uri */,
							      list [i] /* message uid */);

			Logger.Log ("Evolution: Translating XUid uid='{0}' to uri='{1}'", 
					   list [i],
					   launch_uri);

			xuid_list.Add (launch_uri);
		}
	}

	void InsertMailLinks (ArrayList xuid_list, ArrayList subject_list)
	{
		int message_idx = 0;
		bool more_than_one = false;

		foreach (string subject in subject_list) {
			Gtk.TextIter cursor;
			int start_offset;

			if (more_than_one) {
				cursor = Buffer.GetIterAtMark (Buffer.InsertMark);

				if (cursor.LineOffset == 0) 
					Buffer.Insert (ref cursor, "\n");
				else
					Buffer.Insert (ref cursor, ", ");
			}

			string launch_uri = (string) xuid_list [message_idx++];
			
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

