using System;
using Tomboy;

namespace Tomboy.Bugzilla
{
	public class BugzillaLink : DynamicNoteTag
	{
                private const string UriAttributeName = "uri";
                private const string StockIconFilename = "stock_bug.png";
                
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
			get { return (string) Attributes [UriAttributeName]; }
			set {
                                Attributes [UriAttributeName] = value;
                                SetImage ();
                        }
		}
                
                private void SetImage()
                {
                        System.Uri uri = null;
                        try {
                                uri = new System.Uri(BugUrl);
                        } catch {}

                        if (uri == null) {
                                Image = new Gdk.Pixbuf(null, StockIconFilename);
                                return;
                        }

                        string host = uri.Host;
                        // TODO: Get this in a safer way
                        string imageDir = "~/.tomboy/BugzillaIcons/";
                        string imagePath = imageDir.Replace ("~", Environment.GetEnvironmentVariable ("HOME")) + host + ".png";

                        try {
                                Image = new Gdk.Pixbuf (imagePath);
                        } catch (GLib.GException) {
                                Image = new Gdk.Pixbuf(null, StockIconFilename);
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

                protected override void OnAttributeRead (string attributeName)
                {
                        base.OnAttributeRead (attributeName);
                        
                        if (attributeName == UriAttributeName)
                                SetImage ();
                }

	}
}
