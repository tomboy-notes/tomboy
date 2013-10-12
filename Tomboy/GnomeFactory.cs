namespace Tomboy
{
	public class GnomeFactory : IPlatformFactory
	{
		public IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
//			return new GConfPropertyEditorEntry (key, sourceEntry);
			return null;
		}

		public IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
//			return new GConfPropertyEditorToggleButton (key, sourceButton);
			return null;
		}

		public IPreferencesClient CreatePreferencesClient ()
		{
//			return new GConfPreferencesClient ();
			return new NullPreferencesClient ();
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