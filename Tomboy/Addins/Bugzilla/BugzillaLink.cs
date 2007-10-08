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
			Image = new Gdk.Pixbuf(null, "stock_bug.png");
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

	}
}
