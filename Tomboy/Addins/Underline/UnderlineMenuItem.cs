using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.Underline
{
	class UnderlineMenuItem : CheckMenuItem
	{
		NoteAddin Addin;
		bool event_freeze;

		public UnderlineMenuItem (NoteAddin addin) : base ("<u>" + Catalog.GetString ("_Underline") + "</u>")
		{
			((Label) Child).UseUnderline = true;
			((Label) Child).UseMarkup = true;

			Addin = addin;
			Addin.Window.TextMenu.Shown += MenuShown;
			AddAccelerator ("activate", Addin.Window.AccelGroup,
				(uint) Gdk.Key.u, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);

			ShowAll();
		}

		protected void MenuShown (object sender, EventArgs e)
		{
			event_freeze = true;
			Active = Addin.Buffer.IsActiveTag ("underline");
			event_freeze = false;
		}

		protected override void OnActivated ()
		{
			if (!event_freeze)
				Addin.Buffer.ToggleActiveTag ("underline");

			base.OnActivated();
		}

		protected override void OnDestroyed ()
		{
			if (Addin.HasWindow)
				Addin.Window.TextMenu.Shown -= MenuShown;

			base.OnDestroyed();
		}
	}
}
