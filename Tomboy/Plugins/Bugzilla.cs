
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

using Tomboy;

public class InsertBugAction : SplitterAction
{
	BugzillaLink Tag;
	int Offset;
	string Id;

	public InsertBugAction (Gtk.TextIter start,
	                        string id,
	                        Gtk.TextBuffer buffer,
				BugzillaLink tag)
	{
		Tag = tag;
		Id = id;

		Offset = start.Offset;
	}

	public override void Undo (Gtk.TextBuffer buffer)
	{
		// Tag images change the offset by one, but only when deleting.
		Gtk.TextIter start_iter = buffer.GetIterAtOffset (Offset);
		Gtk.TextIter end_iter = buffer.GetIterAtOffset (Offset + chop.Length + 1);
		buffer.Delete (ref start_iter, ref end_iter);
		buffer.MoveMark (buffer.InsertMark, buffer.GetIterAtOffset (Offset));
		buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (Offset));

		Tag.ImageLocation = null;

		ApplySplitTags (buffer);
	}

	public override void Redo (Gtk.TextBuffer buffer)
	{
		RemoveSplitTags (buffer);

		Gtk.TextIter cursor = buffer.GetIterAtOffset (Offset);

		Gtk.TextTag[] tags = {Tag};
		buffer.InsertWithTags (ref cursor, Id, tags);

		buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (Offset));
		buffer.MoveMark (buffer.InsertMark,
		                 buffer.GetIterAtOffset (Offset + chop.Length));

	}

	public override void Merge (EditAction action)
	{
		SplitterAction splitter = action as SplitterAction;
		this.splitTags = splitter.SplitTags;
		this.chop = splitter.Chop;
	}

	/*
	 * The internal listeners will create an InsertAction when the text
	 * is inserted.  Since it's ugly to have the bug insertion appear
	 * to the user as two items in the undo stack, have this item eat
	 * the other one.
	 */
	public override bool CanMerge (EditAction action)
	{
		InsertAction insert = action as InsertAction;
		if (insert == null) {
			return false;
		}

		if (String.Compare(Id, insert.Chop.Text) == 0) {
			return true;
		}

		return false;
	}

	public override void Destroy ()
	{
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
		CanSplit = false;
	}

	public string BugUrl
	{
		get { return (string) Attributes ["uri"]; }
		set { Attributes ["uri"] = value; }
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
	}

	public override Gdk.Pixbuf Image
	{
		get
		{
			if (Icon != null)
				return Icon;

			System.Uri uri = new System.Uri(BugUrl);
			if (uri == null)
				return null;

			string host = uri.Host;
			string imageDir = "~/.tomboy/BugzillaIcons/";

			string imagePath = imageDir.Replace ("~", Environment.GetEnvironmentVariable ("HOME")) + host + ".png";

			try {
				Icon = new Gdk.Pixbuf (imagePath);
			} catch (GLib.GException) {
				Icon = new Gdk.Pixbuf(null, "stock_bug.png");
			}

			return Icon;
		}
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

			Buffer.Undoer.AddUndoAction (new InsertBugAction (cursor, bug, Buffer, link_tag));

			Gtk.TextTag[] tags = {link_tag};
			Buffer.InsertWithTags (ref cursor, bug, tags);
			return true;
		} catch {
			return false;
		}
	}
}
