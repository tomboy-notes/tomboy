using System;
using Tomboy;

namespace Tomboy.Bugzilla
{
	public class BugzillaLink : DynamicNoteTag
	{
		private const string UriAttributeName = "uri";
		//private const string StockIconFilename = "bug.png";
		private static Gdk.Pixbuf bug_icon;

		private static Gdk.Pixbuf BugIcon
		{
			get {
				if (bug_icon == null)
					bug_icon = GuiUtils.GetIcon (
						System.Reflection.Assembly.GetExecutingAssembly (),
						"bug",
						16);
				return bug_icon;
			}
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
			get {
				string url;
				if (Attributes.TryGetValue (UriAttributeName, out url))
					return url;
				return null;
			}
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
				Image = BugIcon;
				return;
			}

			string host = uri.Host;
			// TODO: Get this in a safer way
			string imageDir = "~/.tomboy/BugzillaIcons/";
			string imagePath = imageDir.Replace ("~", Environment.GetEnvironmentVariable ("HOME")) + host + ".png";

			try {
				Image = new Gdk.Pixbuf (imagePath);
			} catch (GLib.GException) {
				Image = BugIcon;
			}
		}

		protected override bool OnActivate (NoteEditor editor, Gtk.TextIter start, Gtk.TextIter end)
		{
			if (BugUrl != string.Empty) {
				Logger.Log ("Opening url '{0}'...", BugUrl);
				
				try {
					Services.NativeApplication.OpenUrl (BugUrl);
				} catch (Exception e) {
					GuiUtils.ShowOpeningLocationError (null, BugUrl, e.Message);
				}
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
