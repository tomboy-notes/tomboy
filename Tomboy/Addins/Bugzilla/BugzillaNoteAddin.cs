
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

//using Mono.Unix;

using Tomboy;

namespace Tomboy.Bugzilla
{
	public class BugzillaNoteAddin : NoteAddin
	{
		public const string BugzillaLinkTagName = "link:bugzilla";

		private static string image_dir = null;

		public static string ImageDirectory {
			get {
				if (image_dir == null) {
					image_dir = Path.Combine (Services.NativeApplication.ConfigurationDirectory,
					                          "BugzillaIcons");

					// Perform migration if necessary
					if (!Directory.Exists (ImageDirectory)) {
						string old_image_dir = Path.Combine (Services.NativeApplication.PreOneDotZeroNoteDirectory,
						                                     "BugzillaIcons");
						if (Directory.Exists (old_image_dir))
							IOUtils.CopyDirectory (old_image_dir, image_dir);
					}
				}
				return image_dir;
			}
		}

		public override void Initialize ()
		{
			if (!Note.TagTable.IsDynamicTagRegistered (BugzillaLinkTagName)) {
				Note.TagTable.RegisterDynamicTag (BugzillaLinkTagName, typeof (BugzillaLink));
			}
		}

		public override void Shutdown ()
		{
		}

		public override void OnNoteOpened ()
		{
			Window.Editor.DragDataReceived += OnDragDataReceived;
		}

		[DllImport("libgobject-2.0.so.0")]
		static extern void g_signal_stop_emission_by_name (IntPtr raw, string name);

		[GLib.ConnectBefore]
		void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
		{
			Logger.Debug ("Bugzilla.OnDragDataReceived");
			foreach (Gdk.Atom atom in args.Context.ListTargets ()) {
				if (atom.Name == "text/uri-list" ||
				                atom.Name == "_NETSCAPE_URL") {
					DropUriList (args);
					return;
				}
			}
		}

		void DropUriList (Gtk.DragDataReceivedArgs args)
		{
			if (args.SelectionData.Length > 0) {
				string uriString = Encoding.UTF8.GetString (args.SelectionData.Data);

				string bugIdGroup = "bugid";
				string regexString =
				        @"show_bug\.cgi\?(\S+\&){0,1}id=(?<" + bugIdGroup + @">\d{1,})";

				Match match = Regex.Match (uriString, regexString);
				if (match.Success) {
					int bugId = int.Parse (match.Groups [bugIdGroup].Value);
					if (InsertBug (args.X, args.Y, uriString, bugId)) {
						Gtk.Drag.Finish (args.Context, true, false, args.Time);
						g_signal_stop_emission_by_name(Window.Editor.Handle,
						                               "drag_data_received");
					}
				}
			}
		}

		bool InsertBug (int x, int y, string uri, int id)
		{
			try {
				BugzillaLink link_tag = (BugzillaLink)
				                        Note.TagTable.CreateDynamicTag (BugzillaLinkTagName);
				link_tag.BugUrl = uri;

				// Place the cursor in the position where the uri was
				// dropped, adjusting x,y by the TextView's VisibleRect.
				Gdk.Rectangle rect = Window.Editor.VisibleRect;
				x = x + rect.X;
				y = y + rect.Y;
				Gtk.TextIter cursor = Window.Editor.GetIterAtLocation (x, y);
				Buffer.PlaceCursor (cursor);

				Buffer.Undoer.AddUndoAction (new InsertBugAction (cursor, id.ToString (), Buffer, link_tag));

				Gtk.TextTag[] tags = {link_tag};
				Buffer.InsertWithTags (ref cursor, id.ToString (), tags);
				return true;
			} catch {
			return false;
		}
	}
}
}
