namespace Tomboy
{
	public interface IPlatformFactory
	{
		IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry);

		IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton);

		IPreferencesClient CreatePreferencesClient ();

		INativeApplication CreateNativeApplication ();

		IKeybinder CreateKeybinder ();
	}
}