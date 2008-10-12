namespace Tomboy
{
	public class GnomeFactory : IPlatformFactory
	{
		public IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
			return new GConfPropertyEditorEntry (key, sourceEntry);
		}

		public IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
			return new GConfPropertyEditorToggleButton (key, sourceButton);
		}

		public IPreferencesClient CreatePreferencesClient ()
		{
			return new GConfPreferencesClient ();
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