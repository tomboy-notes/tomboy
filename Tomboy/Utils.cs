
using System;

namespace Tomboy
{
	public class GuiUtils 
	{
		public static void GetMenuPosition (Gtk.Menu menu, 
						    out int  x, 
						    out int  y, 
						    out bool push_in)
		{
			Gtk.Requisition menu_req = menu.SizeRequest ();

			menu.AttachWidget.GdkWindow.GetOrigin (out x, out y);

			if (y + menu_req.Height >= menu.AttachWidget.Screen.Height)
				y -= menu_req.Height;
			else
				y += menu.AttachWidget.Allocation.Height;

			push_in = true;
		}

		static void DeactivateMenu (object sender, EventArgs args) 
		{
			Gtk.Menu menu = (Gtk.Menu) sender;
			menu.Popdown ();
		}

		// Place the menu underneath an arbitrary parent widget.  The
		// parent widget must be set using menu.AttachToWidget before
		// calling this
		public static void PopupMenu (Gtk.Menu menu, Gdk.EventButton ev)
		{
			menu.Deactivated += DeactivateMenu;
			menu.Popup (null, 
				    null, 
				    new Gtk.MenuPositionFunc (GetMenuPosition), 
				    IntPtr.Zero, 
				    (ev == null) ? 0 : ev.Button, 
				    (ev == null) ? Gtk.Global.CurrentEventTime : ev.Time);
		}

		public static Gdk.Pixbuf GetMiniIcon (string resource_name) 
		{
			Gdk.Pixbuf noicon = new Gdk.Pixbuf (null, resource_name);
			return noicon.ScaleSimple (24, 24, Gdk.InterpType.Nearest);
		}

		public static Gtk.Button MakeImageButton (Gtk.Image image, string label)
		{
			Gtk.HBox box = new Gtk.HBox (false, 2);
			box.PackStart (image, false, false, 0);
			box.PackEnd (new Gtk.Label (label), false, false, 0);
			box.ShowAll ();

			Gtk.Button button = new Gtk.Button ();

			Gtk.Alignment align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 0.0f);
			align.Add (box);
			align.Show ();

			button.Add (align);
			return button;
		}			

		public static Gtk.Button MakeImageButton (string stock_id, string label)
		{
			Gtk.Image image = new Gtk.Image (stock_id, Gtk.IconSize.Button);
			return MakeImageButton (image, label);
		}
	}

	public class GlobalKeybinder 
	{
		Gtk.AccelGroup accel_group;
		Gtk.Menu fake_menu;

		public GlobalKeybinder (Gtk.AccelGroup accel_group) 
		{
			this.accel_group = accel_group;

			fake_menu = new Gtk.Menu ();
			fake_menu.AccelGroup = accel_group;
		}

		public void AddAccelerator (EventHandler handler,
					    uint key,
					    Gdk.ModifierType modifiers,
					    Gtk.AccelFlags flags)
		{
			Gtk.MenuItem foo = new Gtk.MenuItem ();
			foo.Activated += handler;
			foo.AddAccelerator ("activate",
					    accel_group,
					    key, 
					    modifiers,
					    flags);
			foo.Show ();

			fake_menu.Append (foo);
		}
	}

	public class HIGMessageDialog : Gtk.Dialog
	{
		Gtk.AccelGroup accel_group;

		public HIGMessageDialog (Gtk.Window parent,
					 Gtk.DialogFlags flags,
					 Gtk.MessageType type,
					 Gtk.ButtonsType buttons,
					 string          header,
					 string          msg)
			: base ()
		{
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = "";

			VBox.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;

			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			hbox.BorderWidth = 5;
			hbox.Show ();
			VBox.PackStart (hbox, false, false, 0);

			Gtk.Image image = null;

			switch (type) {
			case Gtk.MessageType.Error:
				image = new Gtk.Image ("gtk-dialog-error", Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Question:
				image = new Gtk.Image ("gtk-dialog-question", Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Info:
				image = new Gtk.Image ("gtk-dialog-info", Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Warning:
				image = new Gtk.Image ("gtk-dialog-warning", Gtk.IconSize.Dialog);
				break;
			}

			image.Show ();
			hbox.PackStart (image, false, false, 0);
			
			Gtk.VBox label_vbox = new Gtk.VBox (false, 0);
			label_vbox.Show ();
			hbox.PackStart (label_vbox, true, true, 0);

			string title = String.Format ("<span weight='bold' size='larger'>{0}" +
						      "</span>\n",
						      header);

			Gtk.Label label;

			label = new Gtk.Label (title);
			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.LineWrap = true;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			label_vbox.PackStart (label, false, false, 0);

			label = new Gtk.Label (msg);
			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.LineWrap = true;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			label_vbox.PackStart (label, false, false, 0);
			
			switch (buttons) {
			case Gtk.ButtonsType.None:
				break;
			case Gtk.ButtonsType.Ok:
				AddButton (Gtk.Stock.Ok, Gtk.ResponseType.Ok, true);
				break;
			case Gtk.ButtonsType.Close:
				AddButton (Gtk.Stock.Close, Gtk.ResponseType.Close, true);
				break;
			case Gtk.ButtonsType.Cancel:
				AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, true);
				break;
			case Gtk.ButtonsType.YesNo:
				AddButton (Gtk.Stock.No, Gtk.ResponseType.No, false);
				AddButton (Gtk.Stock.Yes, Gtk.ResponseType.Yes, true);
				break;
			case Gtk.ButtonsType.OkCancel:
				AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel, false);
				AddButton (Gtk.Stock.Ok, Gtk.ResponseType.Ok, true);
				break;
			}

			if (parent != null)
				TransientFor = parent;

			if ((int) (flags & Gtk.DialogFlags.Modal) != 0)
				Modal = true;

			if ((int) (flags & Gtk.DialogFlags.DestroyWithParent) != 0)
				DestroyWithParent = true;
		}

		void AddButton (string stock_id, Gtk.ResponseType response, bool is_default)
		{
			Gtk.Button button = new Gtk.Button (stock_id);
			button.CanDefault = true;
			button.Show ();

			AddActionWidget (button, response);

			if (is_default) {
				DefaultResponse = response;
				button.AddAccelerator ("activate",
						       accel_group,
						       (uint) Gdk.Key.Escape, 
						       0,
						       Gtk.AccelFlags.Visible);
			}
		}
	}
}
