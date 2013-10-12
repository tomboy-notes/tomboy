namespace Tomboy
{
	public class GnomeFactory : IPlatformFactory
	{
		//FIXME: This needs to be properly ported to gSettings, this is just super temporary
		public IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
//			return new GConfPropertyEditorEntry (key, sourceEntry);
			return new PropertyEditorEntry (key, sourceEntry);
		}

		public IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
//			return new GConfPropertyEditorToggleButton (key, sourceButton);
			return new PropertyEditorBool (key);
		}

		public IPreferencesClient CreatePreferencesClient ()
		{
//			return new GConfPreferencesClient ();
			return new XmlPreferencesClient ();
		}

		public INativeApplication CreateNativeApplication ()
		{
			return new GnomeApplication ();
		}

		public IKeybinder CreateKeybinder ()
		{
			return new XKeybinder ();
		}
	}
}