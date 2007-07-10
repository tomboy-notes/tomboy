
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

//using Mono.Unix;

using Tomboy;

namespace Tomboy.Bugzilla
{
	public class BugzillaNoteAddin : NoteAddin
	{
		static int last_bug;

		static BugzillaNoteAddin ()
		{
			last_bug = -1;
		}

		public override void Initialize ()
		{
Logger.Debug ("Bugzilla.Initialize");
			if (!Note.TagTable.IsDynamicTagRegistered ("link:bugzilla")) {
				Note.TagTable.RegisterDynamicTag ("link:bugzilla", typeof (BugzillaLink));
			}
		}

		public override void Shutdown ()
		{
Logger.Debug ("Bugzilla.Shutdown");
		}

		public override void OnNoteOpened ()
		{
Logger.Debug ("Bugzilla.OnNoteOpened");
			Window.Editor.DragDataReceived += OnDragDataReceived;
		}

		[DllImport("libgobject-2.0.so.0")]
		static extern void g_signal_stop_emission_by_name (IntPtr raw, string name);

		[GLib.ConnectBefore]
		void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
		{
Logger.Debug ("Bugzilla.OnDragDataReceived");
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
Logger.Debug ("Bugzilla.DropUriList");
			if (args.SelectionData.Length > 0) {
				string uriString = Encoding.UTF8.GetString (args.SelectionData.Data);

				if (uriString.IndexOf ("show_bug.cgi?id=") != -1) {
					if (InsertBug (args.X, args.Y, uriString)) {
						Gtk.Drag.Finish (args.Context, true, false, args.Time);
						g_signal_stop_emission_by_name(Window.Editor.Handle,
						                               "drag_data_received");
					}
				}
			}
		}

		bool InsertBug (int x, int y, string uri)
		{
Logger.Debug ("Bugzilla.InsertBug");
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

				// Place the cursor in the position where the uri was
				// dropped, adjusting x,y by the TextView's VisibleRect.
				Gdk.Rectangle rect = Window.Editor.VisibleRect;
				x = x + rect.X;
				y = y + rect.Y;
				Gtk.TextIter cursor = Window.Editor.GetIterAtLocation (x, y);
				Buffer.PlaceCursor (cursor);

				Buffer.Undoer.AddUndoAction (new InsertBugAction (cursor, bug, Buffer, link_tag));

				Gtk.TextTag[] tags = {link_tag};
				Buffer.InsertWithTags (ref cursor, bug, tags);
Logger.Debug ("\tReturning true");
				return true;
			} catch {
Logger.Debug ("\tReturning false");
				return false;
			}
		}
	}
}