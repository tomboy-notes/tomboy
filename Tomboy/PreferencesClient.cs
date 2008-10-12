
namespace Tomboy
{
	public interface IPreferencesClient
	{
		void Set (string key, object val);
		object Get (string key);
		void AddNotify (string dir, NotifyEventHandler notify);
		void RemoveNotify (string dir, NotifyEventHandler notify);
		void SuggestSync ();
	}

	public interface IPropertyEditor
	{
		void Setup ();
		string Key { get; }
	}

	public interface IPropertyEditorBool : IPropertyEditor
	{
		void AddGuard (Gtk.Widget widget);
	}

	public class NullPreferencesClient : IPreferencesClient
	{
		#region IPreferencesClient implementation 
		
		public void Set (string key, object val)
		{
			// Do nothing
		}
		
		public object Get (string key)
		{
			throw new NoSuchKeyException (key);
		}
		
		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			// Do nothing
		}
		
		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			// Do nothing
		}
		
		public void SuggestSync ()
		{
			// Do nothing
		}
		
		#endregion
	}

	public delegate void NotifyEventHandler(object sender, NotifyEventArgs args);

	public class NoSuchKeyException : System.Exception
	{
		public NoSuchKeyException(string key) :
			base(string.Format("No such key: {0}", key))
		{}
	}

	public class NotifyEventArgs : System.EventArgs
	{
		private string key;
		private object val;

		public NotifyEventArgs (string key, object val)
		{
			this.key = key;
			this.val = val;
		}

		public string Key { 
			get { return key; }
		}

		public  object Value { 
			get { return val; }
		}
	}
}
