using Gtk;
using System;
using System.Xml;
using Tomboy;

namespace Tomboy.PassEncrypt
{
	class PassEncryptTag : NoteTag
	{
        public static string TagName = "encpass";
        protected static string AtrName = "password";

        public PassEncryptTag (string encryptedPass) : base (TagName + encryptedPass.GetHashCode().ToString())
		{
            this.Editable = false;
            this.CanSplit = false;
            this.CanSpellCheck = false;
            this.CanGrow = false;
            this.CanActivate = true;
            this.Activated += PassEncryptTag_Activated;
            this.SaveType = TagSaveType.Content;
            EncryptedPass = encryptedPass;
        }

        public string EncryptedPass;

        protected bool PassEncryptTag_Activated(NoteTag tag, NoteEditor editor, Gtk.TextIter start, Gtk.TextIter end)
        {
            string encPass = (tag as PassEncryptTag).EncryptedPass;
              //  .Ens editor.Buffer.GetText(start, end, false);
            if (string.IsNullOrWhiteSpace(encPass))
                return false;
            DecryptPassAsync(encPass);
            return true;
        }
        protected async void DecryptPassAsync(string encPass)
        {
            PasswordWindow passwordWindow = new PasswordWindow(false);
            passwordWindow.ShowAll();
            string passPhrase = await passwordWindow.GetPassword();
            if (string.IsNullOrEmpty(passPhrase))
                return;

            string decrypted = Encrypter.Decrypt(encPass, passPhrase);
            passwordWindow.ShowNonEditableText(decrypted);
        }

        //// XmlTextWriter is required, because an XmlWriter created with
        //// XmlWriter.Create considers ":" to be an invalid character
        //// for an element name.
        //// http://bugzilla.gnome.org/show_bug.cgi?id=559094
        public override void Write(XmlTextWriter xml, bool start)
        {
            if (CanSerialize && !string.IsNullOrWhiteSpace(EncryptedPass))
            {
                if (start)
                {
                    xml.WriteStartElement(null, ElementName, null);
                    xml.WriteAttributeString(AtrName, EncryptedPass);

                }
                else
                {
                    xml.WriteEndElement();
                }
            }
        }
        public override void Read(XmlTextReader xml, bool start)
        {
            if (CanSerialize)
            {
                if (start)
                {
                    EncryptedPass = xml.GetAttribute(TagName);
                }
            }
            base.Read(xml, start);
        }
    }
}
