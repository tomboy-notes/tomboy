namespace Tomboy
{
	public class MacFactory : IPlatformFactory
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
			return new MacApplication ();
		}

		public IKeybinder CreateKeybinder ()
		{
			return new NullKeybinder ();
		}
	}
}