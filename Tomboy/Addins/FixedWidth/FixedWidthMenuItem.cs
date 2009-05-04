using System;
using Mono.Unix;

namespace Tomboy.FixedWidth
{
	class FixedWidthMenuItem : Gtk.CheckMenuItem
	{
		NoteAddin Addin;
		bool event_freeze;

		public FixedWidthMenuItem (NoteAddin addin)
: base ("<span font_family='monospace'>" +
		        Catalog.GetString ("Fixed Wid_th") +
		        "</span>")
		{
			((Gtk.Label) Child).UseUnderline = true;
			((Gtk.Label) Child).UseMarkup = true;

			Addin = addin;
			Addin.Window.TextMenu.Shown += MenuShown;
			AddAccelerator ("activate", Addin.Window.AccelGroup,
                                (uint) Gdk.Key.t, Gdk.ModifierType.ControlMask,
                                Gtk.AccelFlags.Visible);

			ShowAll();
		}

		protected void MenuShown (object sender, EventArgs e)
		{
			event_freeze = true;
			Active = Addin.Buffer.IsActiveTag ("monospace");
			event_freeze = false;
		}

		protected override void OnActivated ()
		{
			if (!event_freeze)
				Addin.Buffer.ToggleActiveTag ("monospace");

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
