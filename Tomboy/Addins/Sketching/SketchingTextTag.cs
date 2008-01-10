using System;
using Tomboy;

namespace Tomboy.Sketching
{
	public class SketchingTextTag : DynamicNoteTag
	{
		Gdk.Pixbuf Icon;

		public SketchingTextTag ()
			: base ()
		{
		}

		public override void Initialize (string element_name)
		{
			base.Initialize (element_name);

			Foreground = "black";
			CanActivate = true;
			CanGrow = true;
			CanSpellCheck = false;
			CanSplit = false;
		}

		public string Uri
		{
			get { return (string) Attributes ["uri"]; }
			set { Attributes ["uri"] = value; }
		}
	}
}
