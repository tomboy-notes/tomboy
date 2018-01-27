
// (C) 2018 Piotr Wiœniowski <contact.wisniowskipiotr@gmail.com>, LGPL 2.1 or later.
using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.PassEncrypt
{
	class PassEncryptMenuItem : CheckMenuItem
	{
		NoteAddin Addin;
		bool event_freeze;

		public PassEncryptMenuItem (NoteAddin addin) : base ( Catalog.GetString ("_PassEncrypt"))
		{
			((Label) Child).UseMarkup = true;

            Addin = addin;
			Addin.Window.TextMenu.Shown += MenuShown;
			AddAccelerator ("activate", Addin.Window.AccelGroup,
				(uint) Gdk.Key.e, Gdk.ModifierType.ControlMask,
				Gtk.AccelFlags.Visible);

			ShowAll();
		}

		protected void MenuShown (object sender, EventArgs e)
		{
			event_freeze = true;
			Active = Addin.Buffer.IsActiveTag ("encpass");
			event_freeze = false;
		}

		protected async override void OnActivated ()
		{
            
            string tagName = PassEncryptTag.TagName;
            if (!event_freeze && Addin.Buffer.HasSelection && !Addin.Buffer.IsActiveTag(tagName))
            {
                string selection = Addin.Buffer.Selection.Trim();
                TextIter start, end;
                Addin.Note.Buffer.GetSelectionBounds(out start, out end);
                if (string.IsNullOrWhiteSpace(selection))
                    return;

                PasswordWindow passwordWindow = new PasswordWindow(true);
                passwordWindow.ShowAll();
                string passPhrase = await passwordWindow.GetPassword();
                if (string.IsNullOrWhiteSpace(passPhrase))
                    return;
                string encPassword = Encrypter.Encrypt(selection, passPhrase);
                PassEncryptTag encPassTag = (PassEncryptTag) Addin.Note.TagTable.CreateDynamicTag(PassEncryptTag.TagName);
                encPassTag.SetPassword(encPassword);
                Gtk.TextTag[] tags = { encPassTag };
                Addin.Note.Buffer.DeleteInteractive(ref start, ref end, true);
                Addin.Note.Buffer.InsertWithTags(ref start, Catalog.GetString(" -Encoded Password- "), tags);
                //PassEncryptTag encPassTag = new PassEncryptTag(encPassword);
                //encPassTag.Initialize(PassEncryptTag.TagName);
                //if (Addin.Note.TagTable.Lookup(encPassTag.ElementName) != null)
                //{
                //    // error same passwort hashcode
                //    PasswordWindow showException= new PasswordWindow(false);
                //    showException.ShowAll();
                //    showException.ShowNonEditableText(Catalog.GetString(" Error - same password Id detected: " + encPassTag.Name));
                //    return;
                //}
                //Addin.Note.TagTable.Add(encPassTag);
                //Addin.Note.Buffer.DeleteInteractive(ref start, ref end, true);
                //Addin.Note.Buffer.InsertInteractive(ref start, Catalog.GetString(" -Encoded Password- "), true);
                //Addin.Note.Buffer.InsertWithTagsByName(ref start, Catalog.GetString(" -Encoded Password- "), encPassTag.ElementName);
            }
        }

        protected override void OnDestroyed ()
		{
			if (Addin.HasWindow)
				Addin.Window.TextMenu.Shown -= MenuShown;

			base.OnDestroyed();
		}
	}
}
