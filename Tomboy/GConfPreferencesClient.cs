using System;
using System.Collections.Generic;

namespace Tomboy
{
	public class GConfPreferencesClient : IPreferencesClient
	{
		private GConf.Client client;
		private List<NotifyWrapper> wrapper_list;

		public GConfPreferencesClient ()
		{
			client = new GConf.Client ();
			wrapper_list = new List<NotifyWrapper> ();
		}

		public void Set (string key, object val)
		{
			try {
				client.Set (key, val);
			} catch {	// TODO: what kind?
			        throw new Exception ("Error setting key: " + key);	// TODO: can do better than this
			}
		}

		public object Get (string key)
		{
			try {
				return client.Get (key);
			} catch (GConf.NoSuchKeyException) {
				throw new NoSuchKeyException (key);
			}
		}

		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			if (dir == null)
				throw new NullReferenceException("dir");
			if (notify == null)
				throw new NullReferenceException("notify");

			NotifyWrapper wrapper = new NotifyWrapper (notify, dir);
			client.AddNotify (dir, wrapper.HandleNotify);
			wrapper_list.Add (wrapper);
		}

		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			if (dir == null)
				throw new NullReferenceException("dir");
			if (notify == null)
				throw new NullReferenceException("notify");

			NotifyWrapper wrapper_to_remove = null;
			foreach (NotifyWrapper wrapper in wrapper_list)
				if (wrapper.dir.Equals (dir) && wrapper.notify.Equals (notify)) {
					wrapper_to_remove = wrapper;
					break;
				}

			// NOTE: For some unknown reason, the RemoveNotify call does not
			//		 work here.  That is why we explicitly disable the wrapper,
			//		 since it will unfortunately continue to exist and get
			//		 inappropriately notified.
			if (wrapper_to_remove != null) {
				client.RemoveNotify (dir, wrapper_to_remove.HandleNotify);
				wrapper_to_remove.enabled = false;
				wrapper_list.Remove (wrapper_to_remove);
			}
		}

		public void SuggestSync ()
		{
			client.SuggestSync ();
		}

		class NotifyWrapper
		{
			public NotifyEventHandler notify;
			public string dir;
			public bool enabled = true;

			public NotifyWrapper (NotifyEventHandler notify, string dir)
			{
				this.notify = notify;
				this.dir = dir;
			}

			public void HandleNotify (object sender, GConf.NotifyEventArgs args)
			{
				if (!enabled)
					return;
				
				NotifyEventArgs newArgs = new NotifyEventArgs (args.Key, args.Value);
				notify (sender, newArgs);
			}
		}
	}


	public class GConfPropertyEditorToggleButton : GConf.PropertyEditors.PropertyEditorToggleButton, IPropertyEditorBool
	{
		public GConfPropertyEditorToggleButton (string key, Gtk.CheckButton sourceButton) : base (key, sourceButton) {}
	}

	public class GConfPropertyEditorEntry : GConf.PropertyEditors.PropertyEditorEntry, IPropertyEditor
	{
		public GConfPropertyEditorEntry (string key, Gtk.Entry sourceEntry) : base (key, sourceEntry) { }
	}
}
