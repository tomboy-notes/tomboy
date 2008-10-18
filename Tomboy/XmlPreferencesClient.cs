using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

namespace Tomboy
{
	public class XmlPreferencesClient : IPreferencesClient
	{
		#region Private Members
		
		private string fileName;
		private XmlDocument prefsDoc;
		private Dictionary<string, NotifyEventHandler> events;

		#endregion

		#region Constructor

		public XmlPreferencesClient ()
		{
			fileName = Path.Combine (
				Services.NativeApplication.ConfDir,
				"prefs.xml");
			prefsDoc = new XmlDocument ();
			if (File.Exists (fileName))
				prefsDoc.Load (fileName);
			events = new Dictionary<string, NotifyEventHandler> ();
		}
		
		#endregion
		
		#region IPreferencesClient implementation
		
		public void Set (string key, object value)
		{
			try {
				CreatePath (key);
				prefsDoc.SelectSingleNode (key).InnerText = System.Convert.ToString(value);
				prefsDoc.Save(fileName);
				foreach (string nkey in events.Keys) {
					NotifyEventHandler handler = events [nkey] as NotifyEventHandler;
					if (handler != null && key.StartsWith (nkey))
					{
						NotifyEventArgs args = new NotifyEventArgs (key, value);
						handler (this, args);
					}
				}
			}
			catch {}
		}
		
		public object Get (string key)
		{
			try {
				XmlElement element = prefsDoc.SelectSingleNode(key) as XmlElement;
				if (element != null) {
					int intVal;
					double doubleVal;
					bool boolVal;

					string innerText = element.InnerText;

					if (bool.TryParse (innerText, out boolVal))
						return boolVal;
					else if (int.TryParse (innerText, out intVal))
						return intVal;
					else if (double.TryParse (innerText, out doubleVal))
						return doubleVal;
					else
						return innerText;
				}
				
				// TODO: Ugly, fix
				throw new System.Exception();
			} catch {
				throw new NoSuchKeyException (key);
			}

		}
		
		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				if (!events.ContainsKey (dir))
					events [dir] = notify;
				else
					events [dir] += notify;
			}
		}
		
		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				if (events.ContainsKey (dir))
					events [dir] -= notify;
			}
		}
		
		public void SuggestSync ()
		{
			// TODO: Sync with file?
		}
		
		#endregion

		#region Private Methods

		private void CreatePath(string path)
		{
			if (path.Length == 0)
				return;
			if (path [0] == '/') 
				path = path.Substring (1);
			if (path.Length == 0)
				return;

			string [] parts = path.Split ('/');
			XmlNode node = prefsDoc;
			for (int i = 0; i < parts.Length; ++i)
			{
				if (node [parts [i]] == null)
				{
					node.AppendChild (prefsDoc.CreateElement (parts [i]));
				}
				node = node [parts [i]];
			}
		}

		#endregion
	}

	public class PropertyEditor : IPropertyEditor
	{
		private string key;

		public string Key
		{
			get { return key; }
		}

		public PropertyEditor (string key)
		{
			this.key = key;
		}

		public virtual void Setup ()
		{
		}

		protected object Get ()
		{
			return Services.Preferences.Get (Key);
		}

		protected void Set (object value)
		{
			Services.Preferences.Set (Key, value);
		}
	}

	public class PropertyEditorBool : PropertyEditor, IPropertyEditorBool
	{
		protected List<Gtk.Widget> guards = new List<Gtk.Widget> ();

		public PropertyEditorBool (string key) : base (key) { }

		public virtual void AddGuard (Gtk.Widget widget)
		{
			guards.Add (widget);
		}

		protected void Set (bool value)
		{
			base.Set (value);
			UpdateGuards (value);
		}

		protected void UpdateGuards ()
		{
			bool val = false;
			try {
				val = Get ();
			} catch (NoSuchKeyException) { }
			UpdateGuards (val);
		}

		private void UpdateGuards (bool value)
		{
			foreach (Gtk.Widget widget in guards)
				widget.Sensitive = value;
		}

		protected new bool Get ()
		{
			return (bool) base.Get ();
		}
	}

	public class PropertyEditorToggleButton : PropertyEditorBool
	{
		Gtk.CheckButton button;

		public PropertyEditorToggleButton (string key, Gtk.CheckButton sourceButton) :
			base (key)
		{
			button = sourceButton;
		}

		public override void Setup ()
		{
			bool active = false;
			try {
				active = Get ();
			} catch (NoSuchKeyException) { }
			button.Active = active;
			button.Clicked += new System.EventHandler (OnChanged);
			UpdateGuards ();
		}

		private void OnChanged (object sender, System.EventArgs args)
		{
			Set (button.Active);
		}
	}

	public class PropertyEditorEntry : PropertyEditor
	{
		Gtk.Entry entry;
		public PropertyEditorEntry (string key, Gtk.Entry sourceEntry) :
			base (key)
		{
			entry = sourceEntry;
		}

		public override void Setup ()
		{
			string val = string.Empty;
			try {
				val = System.Convert.ToString (base.Get ());
			} catch (NoSuchKeyException) { }
			entry.Text = val;
			entry.Changed += OnChanged;
		}

		private void OnChanged (object sender, System.EventArgs args)
		{
			Set (entry.Text);
		}
	}
}