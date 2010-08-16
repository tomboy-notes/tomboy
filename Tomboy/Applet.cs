
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using Mono.Unix;

using Gnome;

namespace Tomboy
{
	public class TomboyApplet : PanelApplet
	{
		NoteManager manager;
		TomboyPanelAppletEventBox applet_event_box;

		// Keep referenced so our callbacks don't get reaped.
		static BonoboUIVerb [] menu_verbs;

		public TomboyApplet (IntPtr raw)
: base (raw)
		{
		}

		public override string IID
		{
			get {
				return "OAFIID:TomboyApplet";
			}
		}

		public override string FactoryIID
		{
			get {
				return "OAFIID:TomboyApplet_Factory";
			}
		}

		public override void Creation ()
		{
			Logger.Log ("Applet Created...");

			manager = Tomboy.DefaultNoteManager;
			applet_event_box = new TomboyPanelAppletEventBox (manager);
			// No need to keep the reference
			new TomboyPrefsKeybinder (manager, applet_event_box);

			Flags |= PanelAppletFlags.ExpandMinor;

			Add (applet_event_box);
			Tomboy.Tray = applet_event_box.Tray;
			OnChangeSize (Size);
			ShowAll ();

			if (menu_verbs == null) {
				menu_verbs = new BonoboUIVerb [] {
					new BonoboUIVerb ("Sync", SyncVerb),
					new BonoboUIVerb ("Props", ShowPreferencesVerb),
					new BonoboUIVerb ("Help", ShowHelpVerb),
					new BonoboUIVerb ("About", ShowAboutVerb)
				};
			}

			SetupMenuFromResource (null, "GNOME_TomboyApplet.xml", menu_verbs);
		}

		new void SetupMenuFromResource (Assembly asm,
		                                string resource,
		                                BonoboUIVerb [] verbs)
		{
			if (asm == null)
				asm = GetType ().Assembly;

			Stream stream = asm.GetManifestResourceStream (resource);
			if (stream != null) {
				StreamReader reader = new StreamReader (stream);
				String xml = reader.ReadToEnd ();
				reader.Close ();
				stream.Close ();

				SetupMenu (xml, verbs);
			}
		}

		void SyncVerb ()
		{
			Tomboy.ActionManager ["NoteSynchronizationAction"].Activate ();
		}

		void ShowPreferencesVerb ()
		{
			Tomboy.ActionManager ["ShowPreferencesAction"].Activate ();
		}

		void ShowHelpVerb ()
		{
			// Don't use the ActionManager in this case because
			// the handler won't know about the Screen.
			GuiUtils.ShowHelp ("tomboy", null, Screen, null);
		}

		void ShowAboutVerb ()
		{
			Tomboy.ActionManager ["ShowAboutAction"].Activate ();
		}

		protected override void OnChangeBackground (PanelAppletBackgroundType type,
		                Gdk.Color                 color,
		                Gdk.Pixmap                pixmap)
		{
			if (applet_event_box == null)
				return;

			Gtk.RcStyle rc_style = new Gtk.RcStyle ();
			applet_event_box.Style = null;
			applet_event_box.ModifyStyle (rc_style);

			switch (type) {
			case PanelAppletBackgroundType.ColorBackground:
				applet_event_box.ModifyBg (Gtk.StateType.Normal, color);
				break;
			case PanelAppletBackgroundType.NoBackground:
				break;
			case PanelAppletBackgroundType.PixmapBackground:
				Gtk.Style copy = applet_event_box.Style.Copy();
				copy.SetBgPixmap (Gtk.StateType.Normal, pixmap);
				applet_event_box.Style = copy;
				break;
			}
		}

		protected override void OnChangeSize (uint size)
		{
			if (applet_event_box == null)
				return;

			applet_event_box.SetSizeRequest ((int) size, (int) size);
		}
	}
	
	public enum PanelOrientation { Horizontal, Vertical };
	
	public class TomboyPanelAppletEventBox : Gtk.EventBox, ITomboyTray
	{
		NoteManager manager;
		TomboyTray tray;
		Gtk.Tooltips tips;
		Gtk.Image image;
		int panel_size;

		public TomboyPanelAppletEventBox (NoteManager manager)
: base ()
		{
			this.manager = manager;
			tray = new TomboyTray (manager, this);

			// Load a 16x16-sized icon to ensure we don't end up with a
			// 1x1 pixel.
			panel_size = 16;
			// Load Icon to display in the panel.
			// First we try the "tomboy-panel" icon. This icon can be replaced
			// by the user's icon theme. If the theme does not have this icon
			// then we fall back to the Tomboy Menu icon named "tomboy".
			var icon = GuiUtils.GetIcon ("tomboy-panel", panel_size) ??
				GuiUtils.GetIcon ("tomboy", panel_size);
			this.image = new Gtk.Image (icon);

			this.CanFocus = true;
			this.ButtonPressEvent += ButtonPress;
			this.Add (image);
			this.ShowAll ();

			string tip_text = TomboyTrayUtils.GetToolTipText ();

			tips = new Gtk.Tooltips ();
			tips.SetTip (this, tip_text, null);
			tips.Enable ();
			tips.Sink ();

			SetupDragAndDrop ();
		}
		
		public TomboyTray Tray
		{
			get {
				return tray;
			}
		}

		void ButtonPress (object sender, Gtk.ButtonPressEventArgs args)
		{

			Gtk.Widget parent = (Gtk.Widget) sender;

			switch (args.Event.Button) {
			case 1:
				manager.GtkInvoke (() => {
					TomboyTrayUtils.UpdateTomboyTrayMenu (tray, parent);
					GuiUtils.PopupMenu (tray.TomboyTrayMenu, args.Event);
				});
				args.RetVal = true;
				break;
			case 2:
				if ((bool) Preferences.Get (Preferences.ENABLE_ICON_PASTE)) {
					// Give some visual feedback
					Gtk.Drag.Highlight (this);
					manager.GtkInvoke (() => {
						args.RetVal = PastePrimaryClipboard ();
					});
					Gtk.Drag.Unhighlight (this);
				}
				break;
			}
		}

		void PrependTimestampedText (Note note, DateTime timestamp, string text)
		{
			NoteBuffer buffer = note.Buffer;
			StringBuilder insert_text = new StringBuilder ();

			insert_text.Append ("\n"); // initial newline
			string date_format = Catalog.GetString ("dddd, MMMM d, h:mm tt");
			insert_text.Append (timestamp.ToString (date_format));
			insert_text.Append ("\n"); // begin content
			insert_text.Append (text);
			insert_text.Append ("\n"); // trailing newline

			buffer.Undoer.FreezeUndo ();

			// Insert the date and list of links...
			Gtk.TextIter cursor = buffer.StartIter;
			cursor.ForwardLines (1); // skip title

			buffer.Insert (ref cursor, insert_text.ToString ());

			// Make the date string a small font...
			cursor = buffer.StartIter;
			cursor.ForwardLines (2); // skip title & leading newline

			Gtk.TextIter end = cursor;
			end.ForwardToLineEnd (); // end of date

			buffer.ApplyTag ("datetime", cursor, end);

			// Select the text we've inserted (avoid trailing newline)...
			end = cursor;
			end.ForwardChars (insert_text.Length - 1);

			buffer.MoveMark (buffer.SelectionBound, cursor);
			buffer.MoveMark (buffer.InsertMark, end);

			buffer.Undoer.ThawUndo ();
		}

		bool PastePrimaryClipboard ()
		{
			Gtk.Clipboard clip = GetClipboard (Gdk.Selection.Primary);
			string text = clip.WaitForText ();

			if (text == null || text.Trim() == string.Empty)
				return false;

			Note link_note = manager.FindByUri (NoteManager.StartNoteUri);
			if (link_note == null)
				return false;

			link_note.Window.Present ();
			PrependTimestampedText (link_note,
			                        DateTime.Now,
			                        text);

			return true;
		}

		// Used by TomboyApplet to modify the icon background.
		public Gtk.Image Image
		{
			get {
				return image;
			}
		}

		public void ShowMenu (bool select_first_item)
		{
			manager.GtkInvoke (() => {
				TomboyTrayUtils.UpdateTomboyTrayMenu (tray, this);
				if (select_first_item)
					tray.TomboyTrayMenu.SelectFirst (false);

				GuiUtils.PopupMenu (tray.TomboyTrayMenu, null);
			});
		}

		// Support dropping text/uri-lists and _NETSCAPE_URLs currently.
		void SetupDragAndDrop ()
		{
			Gtk.TargetEntry [] targets =
			new Gtk.TargetEntry [] {
				new Gtk.TargetEntry ("text/uri-list", 0, 0),
				new Gtk.TargetEntry ("_NETSCAPE_URL", 0, 0)
			};

			Gtk.Drag.DestSet (this,
			                  Gtk.DestDefaults.All,
			                  targets,
			                  Gdk.DragAction.Copy);

			DragDataReceived += OnDragDataReceived;
		}

		// Pop up Start Here and insert dropped links, in the form:
		// Wednesday, December 8, 6:45 AM
		// http://luna/kwiki/index.cgi?AdelaideUniThoughts
		// http://www.beatniksoftware.com/blog/
		// And select the inserted text.
		//
		// FIXME: Make undoable, make sure our date-sizing tag never "bleeds".
		//
		void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
		{
			UriList uri_list = new UriList (args.SelectionData);
			if (uri_list.Count == 0)
				return;

			StringBuilder insert_text = new StringBuilder ();
			bool more_than_one = false;

			foreach (Uri uri in uri_list) {
				if (more_than_one)
					insert_text.Append ("\n");

				if (uri.IsFile)
					insert_text.Append (uri.LocalPath);
				else
					insert_text.Append (uri.ToString ());

				more_than_one = true;
			}

			manager.GtkInvoke (() => {
				Note link_note = manager.FindByUri (NoteManager.StartNoteUri);
				if (link_note != null) {
					link_note.Window.Present ();
					PrependTimestampedText (link_note,
					                        DateTime.Now,
					                        insert_text.ToString ());
				}
			});
		}

		void InitPixbuf ()
		{
			// For some reason, the first time we ask for the allocation,
			// it's a 1x1 pixel.  Prevent against this by returning a
			// reasonable default.  Setting the icon causes OnSizeAllocated
			// to be called again anyhow.
			int icon_size = panel_size;
			if (icon_size < 16)
				icon_size = 16;


			// Control specifically which icon is used at the smaller sizes
			// so that no scaling occurs.  In the case of the panel applet,
			// add a couple extra pixels of padding so it matches the behavior
			// of the notification area tray icon.  See bug #403500 for more
			// info.
			if (Tomboy.IsPanelApplet)
				icon_size = icon_size - 2; // padding
			if (icon_size <= 21)
				icon_size = 16;
			else if (icon_size <= 31)
				icon_size = 22;
			else if (icon_size <= 47)
				icon_size = 32;

			Gdk.Pixbuf new_icon = GuiUtils.GetIcon ("tomboy-panel", icon_size) ??
				GuiUtils.GetIcon ("tomboy", icon_size);
			image.Pixbuf = new_icon;
		}

		///
		/// Determine whether the tray is inside a horizontal or vertical
		/// panel so the size of the icon can adjust correctly.
		///
		PanelOrientation GetPanelOrientation ()
		{
			if (this.ParentWindow == null) {
				return PanelOrientation.Horizontal;
			}

			Gdk.Window top_level_window = this.ParentWindow.Toplevel;

			Gdk.Rectangle rect = top_level_window.FrameExtents;
			if (rect.Width < rect.Height)
				return PanelOrientation.Vertical;

			return PanelOrientation.Horizontal;
		}

		protected override void OnSizeAllocated (Gdk.Rectangle rect)
		{
			base.OnSizeAllocated (rect);

			// Determine the orientation
			if (GetPanelOrientation () == PanelOrientation.Horizontal) {
				if (panel_size == Allocation.Height)
					return;

				panel_size = Allocation.Height;
			} else {
				if (panel_size == Allocation.Width)
					return;

				panel_size = Allocation.Width;
			}

			InitPixbuf ();
		}

		public bool MenuOpensUpward ()
		{
			bool open_upwards = false;
			int val = 0;
			Gdk.Screen screen = null;

			int x, y;
			GdkWindow.GetOrigin (out x, out y);
			val = y;
			screen = Screen;

			Gtk.Requisition menu_req = tray.TomboyTrayMenu.SizeRequest ();
			if (val + menu_req.Height >= screen.Height)
				open_upwards = true;

			return open_upwards;
		}
	}
}

