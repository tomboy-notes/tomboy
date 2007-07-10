using System;
using Tomboy;

namespace Tomboy.Bugzilla
{
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
}
