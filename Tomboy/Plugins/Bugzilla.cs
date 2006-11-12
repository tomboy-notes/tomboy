
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

using Tomboy;

/*
 * This object watches open NoteBuffers and reacts to changes.  This makes
 * it easier to do complex, "undoable" operations to the buffer without
 * dealing with TextIter agony.
 */
public class BugzillaWatcher
{
	NoteBuffer Buffer;

	public BugzillaWatcher (NoteBuffer buffer)
	{
		Buffer = buffer;

		Buffer.TagApplied  += OnTagApplied;
		Buffer.TagRemoved  += OnTagRemoved;
		Buffer.DeleteRange += OnDeleteRange;
		Buffer.InsertText  += OnInsertText;
	}

	void OnTagApplied (object sender, Gtk.TagAppliedArgs args)
	{
		// XXX: insert image.
	}

	void OnTagRemoved (object sender, Gtk.TagRemovedArgs args)
	{
		// XXX: remove image.
	}

	/*
	 * This cycles backward through the buffer, to find the start
	 * of a BugzillaLink tag which occurs before or at the position
	 * of start.  If it finds one, it then tries to find the end of
	 * that tag at or after the starting position.
	 *
	 * start is an in/out parameter, end is an out parameter
	 */
	protected Gtk.TextTag FindEnclosingTag (ref Gtk.TextIter start, out Gtk.TextIter end) {
		int Offset = start.Offset;
		end = start;

		Gtk.TextTag tag = null;
		do {
			foreach (Gtk.TextTag i in start.GetToggledTags (true)) {
				if (i is BugzillaLink) {
					tag = i;
					break;
				}
			}
		} while (tag == null && start.BackwardToTagToggle (null));

		if (tag != null) {
			end = Buffer.GetIterAtOffset (Offset);
			if (end.EndsTag (tag) || end.ForwardToTagToggle (tag)) {
				return tag;
			}
		}

		return null;
	}

	void OnInsertText (object sender, Gtk.InsertTextArgs args)
	{
		Gtk.TextIter start = args.Pos;
		Gtk.TextIter end;

		Gtk.TextTag tag = FindEnclosingTag (ref start, out end);
		if (tag != null && !args.Pos.BeginsTag(tag)) {
			Buffer.RemoveTag (tag, start, end);
		}
	}

	void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
	{
		Gtk.TextIter start = args.Start;
		Gtk.TextIter end = args.End;

		Gtk.TextTag tag = FindEnclosingTag (ref start, out end);
		if (tag != null && !args.End.BeginsTag(tag) && !args.Start.EndsTag(tag)) {
			Buffer.RemoveTag (tag, start, end);
		}
	}
}

public class BugzillaLink : DynamicNoteTag
{
	Gdk.Pixbuf Icon;

	public BugzillaLink ()
		: base ()
	{
	}

	public override void Initialize (string element_name)
	{
		base.Initialize (element_name);

		Underline = Pango.Underline.Single;
		Foreground = "blue";
		CanActivate = true;
		CanGrow = true;
		CanSpellCheck = false;
	}

	public string BugUrl
	{
		get { return (string) Attributes ["uri"]; }
		set {
			Attributes ["uri"] = value;
			GLib.Idle.Add (new GLib.IdleHandler (ReloadImage));
		}
	}

	protected override bool OnActivate (NoteEditor editor, Gtk.TextIter start, Gtk.TextIter end)
	{
		if (BugUrl != string.Empty) {
			Logger.Log ("Opening url '{0}'...", BugUrl);
			Gnome.Url.Show (BugUrl);
		}
		return true;
	}

	public override void Read (XmlTextReader xml, bool start)
	{
		base.Read (xml, start);
		GLib.Idle.Add (new GLib.IdleHandler (ReloadImage));
	}

	public Gdk.Pixbuf GetIcon ()
	{
		if (Icon != null) {
			return Icon;
		}

		System.Uri uri = Attributes["uri"] as System.Uri;
		if (uri == null) {
			return null;
		}

		string host = uri.Host;
		string imageDir = "~/.tomboy/BugzillaIcons/";

		string imagePath = 
			imageDir.Replace ("~", Environment.GetEnvironmentVariable ("HOME")) + 
			host + ".png";

		Icon = new Gdk.Pixbuf (imagePath);

		return Icon;
	}

	protected bool ReloadImage ()
	{
		Icon = null;
		// XXX: Emit changed
		return false;
	}
}

public class BugzillaPlugin : NotePlugin
{
	static int last_bug;

	static BugzillaPlugin ()
	{
		last_bug = -1;
	}

	protected override void Initialize ()
	{
		if (!Note.TagTable.IsDynamicTagRegistered ("link:bugzilla")) {
			Note.TagTable.RegisterDynamicTag ("link:bugzilla", typeof (BugzillaLink));
		}
	}

	protected override void Shutdown ()
	{
	}

	protected override void OnNoteOpened ()
	{
		Window.Editor.DragDataReceived += OnDragDataReceived;

		new BugzillaWatcher(Buffer);
	}

	[DllImport("libgobject-2.0.so.0")]
	static extern void g_signal_stop_emission_by_name (IntPtr raw, string name);

	[GLib.ConnectBefore]
	void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
	{
		foreach (Gdk.Atom atom in args.Context.Targets) {
			if (atom.Name == "text/uri-list" ||
			    atom.Name == "_NETSCAPE_URL") {
				DropUriList (args);
				return;
			}
		}
	}

	void DropUriList (Gtk.DragDataReceivedArgs args)
	{
		string uriString = Encoding.UTF8.GetString (args.SelectionData.Data);

		if (uriString.IndexOf ("show_bug.cgi?id=") != -1) {
			if (InsertBug (uriString)) {
				Gtk.Drag.Finish (args.Context, true, false, args.Time);
				g_signal_stop_emission_by_name(Window.Editor.Handle, 
							       "drag_data_received");
			}
		}
	}

	bool InsertBug(string uri)
	{
		try {
			string bug = uri.Substring (uri.IndexOf ("show_bug.cgi?id=") + 16);
			int id = int.Parse (bug);
			// Debounce.  I'm not sure why this is necessary :(
			if (id == last_bug) {
				last_bug = -1;
				return true;
			}
			last_bug = id;

			BugzillaLink link_tag = (BugzillaLink) 
				Note.TagTable.CreateDynamicTag ("link:bugzilla");
			link_tag.BugUrl = uri;

			Gtk.TextIter cursor = Buffer.GetIterAtMark (Buffer.InsertMark);
			int start_offset = cursor.Offset;
			Buffer.Insert (ref cursor, bug);

			Gtk.TextIter start = Buffer.GetIterAtOffset (start_offset);
			Gtk.TextIter end = Buffer.GetIterAtMark (Buffer.InsertMark);
			Buffer.ApplyTag (link_tag, start, end);
			return true;
		} catch {
			return false;
		}
	}
}
