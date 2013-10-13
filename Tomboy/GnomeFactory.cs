namespace Tomboy
{
	public class GnomeFactory : IPlatformFactory
	{
		//FIXME: This needs to be properly ported to gSettings, this is just super temporary
		public IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
			////Think this should work since it references the global client
			return new PropertyEditorEntry (key, sourceEntry);
		}

		public IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
			//Think this should work since it references the global client
			return new PropertyEditorBool (key);
		}

		public IPreferencesClient CreatePreferencesClient ()
		{
			return new GSettingsPreferencesClient ();
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