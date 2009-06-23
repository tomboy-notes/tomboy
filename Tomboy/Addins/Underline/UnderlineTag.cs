using System;
using Tomboy;

namespace Tomboy.Underline
{
	class UnderlineTag : NoteTag
	{
		public UnderlineTag () : base ("underline")
		{
		}

		public override void Initialize (string element_name)
		{
			base.Initialize (element_name);
			Underline = Pango.Underline.Single;
			CanGrow = true;
			CanUndo = true;
		}
	}
}
