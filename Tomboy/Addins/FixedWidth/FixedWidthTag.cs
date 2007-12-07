using System;
using Tomboy;

namespace Tomboy.FixedWidth
{
	class FixedWidthTag : NoteTag
	{
		public FixedWidthTag ()
: base ("monospace")
		{
		}

		public override void Initialize (string element_name)
		{
			base.Initialize (element_name);
			Family = "monospace";
			CanGrow = true;
			CanUndo = true;
		}
	}
}
