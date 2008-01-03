
using System;
using System.Collections.Generic;

namespace Tomboy
{
	/// <summary>
	/// A NoteAddin extends the functionality of a note and a NoteWindow.
	/// If you wish to extend Tomboy in a more broad sense, perhaps you
	/// should create an ApplicationAddin.
	/// <summary>
	public abstract class NoteAddin : AbstractAddin
	{
		Note note;

		List<Gtk.MenuItem> tools_menu_items;
		List<Gtk.MenuItem> text_menu_items;
		Dictionary<Gtk.ToolItem, int> toolbar_items;

		public void Initialize (Note note)
		{
			this.note = note;
			this.note.Opened += OnNoteOpenedEvent;

			Initialize ();

			if (note.IsOpened)
				OnNoteOpened ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (tools_menu_items != null) {
					foreach (Gtk.Widget item in tools_menu_items)
					item.Destroy ();
				}

				if (text_menu_items != null) {
					foreach (Gtk.Widget item in text_menu_items)
					item.Destroy ();
				}
				
				if (toolbar_items != null) {
					foreach (Gtk.ToolItem item in toolbar_items.Keys)
						item.Destroy ();
				}

				Shutdown ();
			}

			note.Opened -= OnNoteOpenedEvent;
		}

		/// <summary>
		/// Called when the NoteAddin is attached to a Note
		/// </summary>
		public abstract void Initialize ();

		/// <summary>
		/// Called when a note is deleted and also when
		/// the addin is disabled.
		/// </summary>
		public abstract void Shutdown ();

		/// <summary>
		/// Called when the note is opened.
		/// </summary>
		public abstract void OnNoteOpened ();

		public Note Note
		{
			get {
				return note;
			}
		}

		public bool HasBuffer
		{
			get {
				return note.HasBuffer;
			}
		}

		public NoteBuffer Buffer
		{
			get
			{
				if (IsDisposing && !HasBuffer)
					throw new InvalidOperationException ("Plugin is disposing already");

				return note.Buffer;
			}
		}

		public bool HasWindow
		{
			get {
				return note.HasWindow;
			}
		}

		public NoteWindow Window
		{
			get
			{
				if (IsDisposing && !HasWindow)
					throw new InvalidOperationException ("Plugin is disposing already");

				return note.Window;
			}
		}

		public NoteManager Manager
		{
			get {
				return note.Manager;
			}
		}

		void OnNoteOpenedEvent (object sender, EventArgs args)
		{
			OnNoteOpened ();

			if (tools_menu_items != null) {
				foreach (Gtk.Widget item in tools_menu_items) {
					if (item.Parent == null ||
					                item.Parent != Window.PluginMenu)
						Window.PluginMenu.Add (item);
				}
			}

			if (text_menu_items != null) {
				foreach (Gtk.Widget item in text_menu_items) {
					if (item.Parent == null ||
					                item.Parent != Window.TextMenu) {
						Window.TextMenu.Add (item);
						Window.TextMenu.ReorderChild (item, 7);
					}
				}
			}
			
			if (toolbar_items != null) {
				foreach (Gtk.ToolItem item in toolbar_items.Keys) {
					if (item.Parent == null ||
									item.Parent != Window.Toolbar) {
						Window.Toolbar.Insert (item, (int) toolbar_items [item]);
					}
				}
			}
		}

		public void AddPluginMenuItem (Gtk.MenuItem item)
		{
			if (IsDisposing)
				throw new InvalidOperationException ("Plugin is disposing already");

			if (tools_menu_items == null)
				tools_menu_items = new List<Gtk.MenuItem> ();

			tools_menu_items.Add (item);

			if (note.IsOpened)
				Window.PluginMenu.Add (item);
		}
		
		public void AddToolItem (Gtk.ToolItem item, int position)
		{
			if (IsDisposing)
				throw new InvalidOperationException ("Add-in is disposing already");
				
			if (toolbar_items == null)
				toolbar_items = new Dictionary<Gtk.ToolItem, int> ();
			
			toolbar_items [item] = position;
			
			if (note.IsOpened) {
				Window.Toolbar.Insert (item, position);
			}
		}

		public void AddTextMenuItem (Gtk.MenuItem item)
		{
			if (IsDisposing)
				throw new InvalidOperationException ("Plugin is disposing already");

			if (text_menu_items == null)
				text_menu_items = new List<Gtk.MenuItem> ();

			text_menu_items.Add (item);

			if (note.IsOpened) {
				Window.TextMenu.Add (item);
				Window.TextMenu.ReorderChild (item, 7);
			}
		}
	}
}
