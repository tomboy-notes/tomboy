
using System;

namespace Tomboy
{
	public class NoteEditor : Gtk.TextView
	{
		//GNOME desktop default document font GConf setting path
		const string DESKTOP_GNOME_INTERFACE_PATH = "/desktop/gnome/interface";
		const string GNOME_DOCUMENT_FONT_KEY =
		        DESKTOP_GNOME_INTERFACE_PATH + "/document_font_name";

		public NoteEditor (Gtk.TextBuffer buffer)
: base (buffer)
		{
			WrapMode = Gtk.WrapMode.Word;
			LeftMargin = DefaultMargin;
			RightMargin = DefaultMargin;
			CanDefault = true;

			//FIXME: This no longer works since GNOME has moved to GSettings
			//Set up the GConf client to watch the default document font
//			Preferences.Client.AddNotify (DESKTOP_GNOME_INTERFACE_PATH,
//			                              OnFontSettingChanged);

			// Make sure the cursor position is visible
			ScrollMarkOnscreen (buffer.InsertMark);

			// Set Font from GConf preference
			if ((bool) Preferences.Get (Preferences.ENABLE_CUSTOM_FONT)) {
				string font_string = (string)
				                     Preferences.Get (Preferences.CUSTOM_FONT_FACE);
				ModifyFont (Pango.FontDescription.FromString (font_string));
			}
			else {
				ModifyFont (GetGnomeDocumentFontDescription ());
			}

			Preferences.SettingChanged += OnFontSettingChanged;

			// Set extra editor drag targets supported (in addition
			// to the default TextView's various text formats)...
			Gtk.TargetList list = Gtk.Drag.DestGetTargetList (this);
			list.Add (Gdk.Atom.Intern ("text/uri-list", false), 0, 1);
			list.Add (Gdk.Atom.Intern ("_NETSCAPE_URL", false), 0, 1);

			KeyPressEvent += KeyPressed;
			ButtonPressEvent += ButtonPressed;
		}

		public static int DefaultMargin
		{
			get {
				return 8;
			}
		}

		// Retrieve the GNOME document font setting
		Pango.FontDescription GetGnomeDocumentFontDescription ()
		{
			try {
				string doc_font_string = (string)
				                         Preferences.Client.Get (GNOME_DOCUMENT_FONT_KEY);
				return Pango.FontDescription.FromString (doc_font_string);
			} catch (NoSuchKeyException) {
			} catch (System.InvalidCastException) {
			}

			return new Pango.FontDescription ();
		}

		//
		// Update the font based on the changed Preference dialog setting.
		// Also update the font based on the changed GConf GNOME document font setting.
		//
		void OnFontSettingChanged (object sender, NotifyEventArgs args)
		{
			switch (args.Key) {
			case Preferences.ENABLE_CUSTOM_FONT:
				UpdateCustomFontSetting ();
				break;
			case Preferences.CUSTOM_FONT_FACE:
				UpdateCustomFontSetting ();
				break;

			case GNOME_DOCUMENT_FONT_KEY:
				if (!(bool) Preferences.Get (Preferences.ENABLE_CUSTOM_FONT))
					ModifyFontFromString ((string) args.Value);
				break;
			}
		}
		
		void UpdateCustomFontSetting ()
		{
			if ((bool) Preferences.Get(Preferences.ENABLE_CUSTOM_FONT)) {
				string fontString = (string) Preferences.Get(Preferences.CUSTOM_FONT_FACE);
				Logger.Debug( "Switching note font to '{0}'...", fontString);
				ModifyFontFromString (fontString);
			} else {
				Logger.Debug ("Switching back to the default font");
				ModifyFont (GetGnomeDocumentFontDescription());
			}
		}

		void ModifyFontFromString (string fontString)
		{
			Logger.Debug ("Switching note font to '{0}'...", fontString);
			ModifyFont (Pango.FontDescription.FromString (fontString));
		}

		//
		// DND Drop handling
		//
		protected override void OnDragDataReceived (Gdk.DragContext context,
		                int x,
		                int y,
		                Gtk.SelectionData selection_data,
		                uint info,
		                uint time)
		{
			bool has_url = false;

			foreach (Gdk.Atom target in context.ListTargets()) {
				if (target.Name == "text/uri-list" ||
				                target.Name == "_NETSCAPE_URL") {
					has_url = true;
					break;
				}
			}

			if (has_url) {
				UriList uri_list = new UriList (selection_data);
				bool more_than_one = false;

				// Place the cursor in the position where the uri was
				// dropped, adjusting x,y by the TextView's VisibleRect.
				Gdk.Rectangle rect = VisibleRect;
				int adjustedX = x + rect.X;
				int adjustedY = y + rect.Y;
				Gtk.TextIter cursor = GetIterAtLocation (adjustedX, adjustedY);
				Buffer.PlaceCursor (cursor);

				Gtk.TextTag link_tag = Buffer.TagTable.Lookup ("link:url");

				foreach (Uri uri in uri_list) {
					Logger.Debug ("Got Dropped URI: {0}", uri);
					string insert;
					if (uri.IsFile) {
						// URL-escape the path in case
						// there are spaces (bug #303902)
						insert = System.Uri.EscapeUriString (uri.LocalPath);
					} else {
						insert = uri.ToString ();
					}

					if (insert == null || insert.Trim () == String.Empty)
						continue;

					if (more_than_one) {
						cursor = Buffer.GetIterAtMark (Buffer.InsertMark);

						// FIXME: The space here is a hack
						// around a bug in the URL Regex which
						// matches across newlines.
						if (cursor.LineOffset == 0)
							Buffer.Insert (ref cursor, " \n");
						else
							Buffer.Insert (ref cursor, ", ");
					}

					Buffer.InsertWithTags (ref cursor, insert, link_tag);
					more_than_one = true;
				}

				Gtk.Drag.Finish (context, more_than_one, false, time);
			} else {
				base.OnDragDataReceived (context, x, y, selection_data, info, time);
			}
		}

		[GLib.ConnectBefore()]
		void KeyPressed (object sender, Gtk.KeyPressEventArgs args)
		{
			args.RetVal = true;
			bool ret_value = false;

			switch (args.Event.Key)
			{
			case Gdk.Key.KP_Enter:
			case Gdk.Key.Return:
				// Allow opening notes with Ctrl + Enter
				if ((args.Event.State & Gdk.ModifierType.ControlMask) == 0) {
					if ((int) (args.Event.State & Gdk.ModifierType.ShiftMask) != 0) {
						ret_value = ((NoteBuffer) Buffer).AddNewline (true);
					} else {
						ret_value = ((NoteBuffer) Buffer).AddNewline (false);
					}					
					ScrollMarkOnscreen (Buffer.InsertMark);
				}
				break;
			case Gdk.Key.Tab:
				ret_value = ((NoteBuffer) Buffer).AddTab ();
				ScrollMarkOnscreen (Buffer.InsertMark);
				break;
			case Gdk.Key.ISO_Left_Tab:
				ret_value = ((NoteBuffer) Buffer).RemoveTab ();
				ScrollMarkOnscreen (Buffer.InsertMark);
				break;
			case Gdk.Key.Delete:
				if (Gdk.ModifierType.ShiftMask != (args.Event.State &
				                                   Gdk.ModifierType.ShiftMask)) {
					ret_value = ((NoteBuffer) Buffer).DeleteKeyHandler ();
					ScrollMarkOnscreen (Buffer.InsertMark);
				}
				break;
			case Gdk.Key.BackSpace:
				ret_value = ((NoteBuffer) Buffer).BackspaceKeyHandler ();
				break;
			case Gdk.Key.Left:
			case Gdk.Key.Right:
			case Gdk.Key.Up:
			case Gdk.Key.Down:
				ret_value = false;
				break;
			default:
				((NoteBuffer) Buffer).CheckSelection ();
				break;
			}

			args.RetVal = ret_value;
		}

		[GLib.ConnectBefore()]
		void ButtonPressed (object sender, Gtk.ButtonPressEventArgs args)
		{
			((NoteBuffer) Buffer).CheckSelection ();
		}
	}
}
