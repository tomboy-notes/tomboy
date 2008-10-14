namespace Tomboy
{
	public class WindowsFactory : IPlatformFactory
	{
		public IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
			return new PropertyEditorEntry (key, sourceEntry);
		}

		public IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
			return new PropertyEditorToggleButton (key, sourceButton);
		}

		public IPreferencesClient CreatePreferencesClient ()
		{
			return new XmlPreferencesClient ();
		}

		public INativeApplication CreateNativeApplication ()
		{
			return new WindowsApplication ();
		}

		public IKeybinder CreateKeybinder ()
		{
			return new WindowsKeybinder ();
		}
	}
}