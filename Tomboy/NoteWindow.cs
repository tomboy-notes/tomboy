
using System;
using Mono.Posix;

namespace Tomboy
{
	public class NoteWindow : Gtk.Window 
	{
		Note note;

		Gtk.AccelGroup accel_group;
		Gtk.Toolbar toolbar;
		Gtk.Button text_button;
		NoteTextMenu text_menu;
		Gtk.TextView editor;
		Gtk.ScrolledWindow editor_window;

		GlobalKeybinder global_keys;

		static Gdk.Pixbuf stock_notes;

		static NoteWindow ()
		{
			stock_notes = new Gdk.Pixbuf (null, "stock_notes.png");
		}

		// 
		// Construct a window to display a note
		// 
		// Currently a toolbar with Link, Search, Text, Delete buttons
		// and a Gtk.TextView as the body.
		// 

		public NoteWindow (Note note) : 
			base (note.Title) 
		{
			this.note = note;
			this.Icon = stock_notes;
			this.SetDefaultSize (450, 360);

			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			toolbar = MakeToolbar ();
			toolbar.Show ();

			text_menu = new NoteTextMenu (accel_group, note.Buffer, note.Buffer.Undoer);
			text_menu.AttachToWidget (text_button, null);
			text_menu.Deactivated += ReleaseTextButton;

			editor = new Gtk.TextView (note.Buffer);
			editor.WrapMode = Gtk.WrapMode.Word;
			editor.LeftMargin = 8;
			editor.RightMargin = 8;
			editor.CanDefault = true;
			editor.PopulatePopup += OnPopulatePopup;
			editor.DragDataReceived += OnDragDataReceived;
			editor.ScrollMarkOnscreen (note.Buffer.InsertMark);
			editor.Show ();

			// Set Font from GConf preference
			if ((bool) Preferences.Get (Preferences.ENABLE_CUSTOM_FONT)) {
				string font_string = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
				editor.ModifyFont (Pango.FontDescription.FromString (font_string));
			}
			Preferences.SettingChanged += OnFontSettingChanged;

			// Set extra editor drag targets supported
			Gtk.TargetList list = Gtk.Drag.DestGetTargetList (editor);
			list.Add (Gdk.Atom.Intern ("text/uri-list", false), 0, 1);
			list.Add (Gdk.Atom.Intern ("_NETSCAPE_URL", false), 0, 1);

			// FIXME: I think it would be really nice to let the
			//        window get bigger up till it grows more than
			//        60% of the screen, and then show scrollbars. 
			editor_window = new Gtk.ScrolledWindow ();
			editor_window.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			editor_window.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			editor_window.Add (editor);
			editor_window.Show ();

			FocusChild = editor;

			Gtk.VBox box = new Gtk.VBox (false, 2);
			box.PackStart (toolbar, false, false, 0);
			box.PackStart (editor_window, true, true, 0);
			box.Show ();

			// NOTE: Since some of our keybindings are only
			// available in the context menu, and the context menu
			// is created on demand, register them with the
			// global keybinder
			global_keys = new GlobalKeybinder (accel_group);

			// Close window (Ctrl-W)
			global_keys.AddAccelerator (new EventHandler (CloseWindowHandler),
						    (uint) Gdk.Key.w, 
						    Gdk.ModifierType.ControlMask,
						    Gtk.AccelFlags.Visible);

			// Close window (Escape)
			global_keys.AddAccelerator (new EventHandler (CloseWindowHandler),
						    (uint) Gdk.Key.Escape, 
						    0,
						    Gtk.AccelFlags.Visible);

			// Open Find Dialog (Ctrl-F)
			global_keys.AddAccelerator (new EventHandler (FindActivate),
						    (uint) Gdk.Key.f, 
						    Gdk.ModifierType.ControlMask,
						    Gtk.AccelFlags.Visible);

			// Find Next (Ctrl-G)
			global_keys.AddAccelerator (new EventHandler (FindNextActivate),
						    (uint) Gdk.Key.g, 
						    Gdk.ModifierType.ControlMask,
						    Gtk.AccelFlags.Visible);

			// Find Previous (Ctrl-Shift-G)
			global_keys.AddAccelerator (new EventHandler (FindPreviousActivate),
						    (uint) Gdk.Key.g, 
						    (Gdk.ModifierType.ControlMask |
						     Gdk.ModifierType.ShiftMask),
						    Gtk.AccelFlags.Visible);

			this.Add (box);
		}

		void OnFontSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			switch (args.Key) {
			case Preferences.ENABLE_CUSTOM_FONT:
				Console.WriteLine ("Switching note font {0}...", 
						   (bool) args.Value ? "ON" : "OFF");

				if ((bool) args.Value) {
					string font_string = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
					editor.ModifyFont (Pango.FontDescription.FromString (font_string));
				} else
					editor.ModifyFont (new Pango.FontDescription ());

				break;

			case Preferences.CUSTOM_FONT_FACE:
				Console.WriteLine ("Switching note font to '{0}'...", 
						   (string) args.Value);

				editor.ModifyFont (Pango.FontDescription.FromString ((string) args.Value));
				break;
			}
		}

		protected override bool OnDeleteEvent (Gdk.Event evnt)
		{
			CloseWindowHandler (null, null);
			return true;
		}

		void CloseWindowHandler (object sender, EventArgs args)
		{
			// Unmaximize before hiding to avoid reopening
			// pseudo-maximized
			if ((GdkWindow.State & Gdk.WindowState.Maximized) > 0)
				Unmaximize ();

			Hide ();
		}

		//
		// Delete this Note.
		//

		void DeleteButtonClicked () 
		{
			HIGMessageDialog dialog = 
				new HIGMessageDialog (
					note.Window,
					Gtk.DialogFlags.DestroyWithParent,
					Gtk.MessageType.Question,
					Gtk.ButtonsType.None,
					Catalog.GetString ("Really delete this note?"),
					Catalog.GetString ("If you delete a note it is " +
							   "permanently lost."));

			Gtk.Button button;

			button = new Gtk.Button (Gtk.Stock.Cancel);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, Gtk.ResponseType.Cancel);
			dialog.DefaultResponse = Gtk.ResponseType.Cancel;

			button = new Gtk.Button (Gtk.Stock.Delete);
			button.CanDefault = true;
			button.Show ();
			dialog.AddActionWidget (button, 666);

			int result = dialog.Run ();
			if (result == 666 ) {
				// This will destroy our window...
				note.Manager.Delete (note);
			}

			dialog.Destroy();
		}

		//
		// Public Children Accessors
		//

		public Gtk.TextView Editor {
			get { return editor; }
		}

		public Gtk.Toolbar Toolbar {
			get { return toolbar; }
		}

		//
		// Right-click menu
		//
		// Add Undo, Redo, Link, Link To menu, Font menu to the start of
		// the editor's context menu.
		//
 
		void OnPopulatePopup (object sender, Gtk.PopulatePopupArgs args)
		{
			args.Menu.AccelGroup = accel_group;

			Console.WriteLine ("Populating context menu...");

			// Remove the lame-o gigantic Insert Unicode Control
			// Characters menu item.
			Gtk.Widget lame_unicode;
			lame_unicode = (Gtk.Widget) args.Menu.Children [args.Menu.Children.Length - 1];
			args.Menu.Remove (lame_unicode);

			Gtk.MenuItem spacer1 = new Gtk.SeparatorMenuItem ();
			spacer1.Show ();

			Gtk.ImageMenuItem link = 
				new Gtk.ImageMenuItem (Catalog.GetString ("_Link to New Note"));
			link.Image = new Gtk.Image (Gtk.Stock.JumpTo, Gtk.IconSize.Menu);

			link.Sensitive = (note.Buffer.Selection != null);
			link.Activated += LinkToNoteActivate;
			link.AddAccelerator ("activate",
					     accel_group,
					     (uint) Gdk.Key.l, 
					     Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);
			link.Show ();

			Gtk.ImageMenuItem text_item = 
				new Gtk.ImageMenuItem (Catalog.GetString ("Te_xt"));
			text_item.Image = new Gtk.Image (Gtk.Stock.SelectFont, Gtk.IconSize.Menu);
			text_item.Submenu = new NoteTextMenu (accel_group, 
							      note.Buffer, 
							      note.Buffer.Undoer /* don't show Undo/Redo */);
			text_item.Show ();

			Gtk.ImageMenuItem find_item = 
				new Gtk.ImageMenuItem (Catalog.GetString ("_Search"));
			find_item.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			find_item.Submenu = MakeFindMenu ();
			find_item.Show ();

			Gtk.MenuItem spacer2 = new Gtk.SeparatorMenuItem ();
			spacer2.Show ();

			args.Menu.Prepend (spacer1);
			args.Menu.Prepend (text_item);
			args.Menu.Prepend (find_item);
			args.Menu.Prepend (link);

			Gtk.ImageMenuItem close_window = 
				new Gtk.ImageMenuItem (Catalog.GetString ("_Close Window"));
			close_window.Image = new Gtk.Image (Gtk.Stock.Close, Gtk.IconSize.Menu);
			close_window.Activated += CloseWindowHandler;
			close_window.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.w, 
						     Gdk.ModifierType.ControlMask,
						     Gtk.AccelFlags.Visible);
			close_window.Show ();

			args.Menu.Append (close_window);
		}

		void UndoClicked (object sender, EventArgs args)
		{
			text_menu.UndoClicked (sender, args);
		}

		void RedoClicked (object sender, EventArgs args)
		{
			text_menu.RedoClicked (sender, args);
		}

		//
		// DND Drop handling
		//

		void OnDragDataReceived (object sender, Gtk.DragDataReceivedArgs args)
		{
			// FIXME: Need to access SelectionData's target, which
			// is unmapped in Gtk# currently, and only insert urls
			// when the target is text/uri-list, or _NETSCAPE_URL.

			UriList uri_list = new UriList (args.SelectionData);
			if (uri_list.Count == 0)
				return;

			NoteBuffer buffer = note.Buffer;
			Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);
			bool more_than_one = false;

			foreach (Uri uri in uri_list) {
				Console.WriteLine ("Got Dropped URI: {0}", uri);
				if (more_than_one)
					buffer.Insert (cursor, "\n");

				if (uri.IsFile) 
					buffer.Insert (cursor, uri.LocalPath);
				else
					buffer.Insert (cursor, uri.ToString ());

				more_than_one = true;
			}
		}

		//
		// Toolbar
		//
		// Add Link button, Font menu, Delete button to the window's
		// toolbar.
		//

		Gtk.Toolbar MakeToolbar ()
		{
			Gtk.Toolbar toolbar = new Gtk.Toolbar ();
			toolbar.IconSize = Gtk.IconSize.LargeToolbar;
			toolbar.ToolbarStyle = Gtk.ToolbarStyle.BothHoriz;
			toolbar.Tooltips = true;

			Gtk.Widget link = 
				toolbar.AppendItem (Catalog.GetString ("Link"), 
						    Catalog.GetString ("Link selected text to a " +
								       "new note"), 
						    null, 
						    new Gtk.Image (Gtk.Stock.JumpTo, 
								   Gtk.IconSize.LargeToolbar),
						    new Gtk.SignalFunc (LinkButtonClicked));
			link.AddAccelerator ("activate",
					     accel_group,
					     (uint) Gdk.Key.l, 
					     Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);

			Gtk.Widget find = 
				toolbar.AppendItem (Catalog.GetString ("Search"), 
						    Catalog.GetString ("Search your notes"),
						    null, 
						    new Gtk.Image (Gtk.Stock.Find, 
								   Gtk.IconSize.LargeToolbar),
						    new Gtk.SignalFunc (FindButtonClicked));
			find.AddAccelerator ("activate",
					     accel_group,
					     (uint) Gdk.Key.f, 
					     Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);

			text_button = new TextToolButton ();
			text_button.ButtonPressEvent += TextButtonPress;
			text_button.Clicked += TextButtonClicked;
			text_button.Show ();

			// Give it a window to receive events events
			Gtk.EventBox ev = new Gtk.EventBox ();
			ev.Add (text_button);
			ev.Show ();

			toolbar.AppendWidget (ev, 
					      Catalog.GetString ("Set properties of text"), 
					      null);

			toolbar.AppendSpace ();

			Gtk.Widget delete = 
				toolbar.AppendItem (Catalog.GetString ("Delete"), 
						    Catalog.GetString ("Delete this note"), 
						    null, 
						    new Gtk.Image (Gtk.Stock.Delete, 
								   Gtk.IconSize.LargeToolbar),
						    new Gtk.SignalFunc (DeleteButtonClicked));

			// Don't allow deleting the "Start" or "Recent Changes" notes...
			if (note.IsSpecial)
				delete.Sensitive = false;

			return toolbar;
		}

		class TextToolButton : Gtk.Button
		{
			public TextToolButton ()
			{
				this.CanFocus = false;
				this.Relief = Gtk.ReliefStyle.None;

				Gtk.Image image = new Gtk.Image (Gtk.Stock.SelectFont, 
								 Gtk.IconSize.LargeToolbar);
				Gtk.Label label = new Gtk.Label (Catalog.GetString ("_Text"));
				Gtk.Arrow arrow = new Gtk.Arrow (Gtk.ArrowType.Down, 
								 Gtk.ShadowType.In);

				Gtk.HBox box = new Gtk.HBox (false, 4);
				box.Add (image);
				box.Add (label);
				box.Add (arrow);

				Gtk.Alignment align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 0.0f);
				align.Add (box);
				align.ShowAll ();

				this.Add (align);
			}

			protected override bool OnButtonPressEvent (Gdk.EventButton ev)
			{
				base.OnButtonPressEvent (ev);
				return false;
			}
		}

		// Find context menu
		//
		// Find, Find Next, Find Previous menu items.  Next nd previous
		// are only sensitized when there are search results for this
		// buffer to iterate.

		Gtk.Menu MakeFindMenu ()
		{
			Gtk.Menu menu = new Gtk.Menu ();
			menu.AccelGroup = accel_group;

			Gtk.ImageMenuItem find = 
				new Gtk.ImageMenuItem (Catalog.GetString ("_Search..."));
			find.Image = new Gtk.Image (Gtk.Stock.Find, Gtk.IconSize.Menu);
			find.Activated += FindActivate;
			find.AddAccelerator ("activate",
					     accel_group,
					     (uint) Gdk.Key.f, 
					     Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);
			find.Show ();

			Gtk.ImageMenuItem find_next = 
				new Gtk.ImageMenuItem (Catalog.GetString ("Find _Next"));
			find_next.Image = new Gtk.Image (Gtk.Stock.GoForward, Gtk.IconSize.Menu);
			find_next.Sensitive = Find.FindNextButton.Sensitive;

			find_next.Activated += FindNextActivate;
			find_next.AddAccelerator ("activate",
						  accel_group,
						  (uint) Gdk.Key.g, 
						  Gdk.ModifierType.ControlMask,
						  Gtk.AccelFlags.Visible);
			find_next.Show ();

			Gtk.ImageMenuItem find_previous = 
				new Gtk.ImageMenuItem (Catalog.GetString ("Find _Previous"));
			find_previous.Image = new Gtk.Image (Gtk.Stock.GoBack, Gtk.IconSize.Menu);
			find_previous.Sensitive = Find.FindPreviousButton.Sensitive;

			find_previous.Activated += FindPreviousActivate;
			find_previous.AddAccelerator ("activate",
						      accel_group,
						      (uint) Gdk.Key.g, 
						      (Gdk.ModifierType.ControlMask | 
						       Gdk.ModifierType.ShiftMask),
						      Gtk.AccelFlags.Visible);
			find_previous.Show ();

			menu.Append (find);
			menu.Append (find_next);
			menu.Append (find_previous);

			return menu;
		}

		//
		// Open the find dialog, passing any currently selected text
		//

		public NoteFindDialog Find {
			get {
				return NoteFindDialog.GetInstance (note);
			}
		}

		void FindButtonClicked ()
		{
			Find.SearchText = note.Buffer.Selection;
			Find.Present ();
		}

		void FindNextActivate (object sender, EventArgs args)
		{
			Find.FindNextButton.Click ();
		}

		void FindPreviousActivate (object sender, EventArgs args)
		{
			Find.FindPreviousButton.Click ();
		}

		// 
		// Signature trampoline for editor context-menu "Find"
		//

		void FindActivate (object sender, EventArgs args)
		{
			FindButtonClicked ();
		}

		//
		// Link menu item activate
		//
		// Create a new note, names according to the buffer's selected
		// text.  Does nothing if there is no active selection.
		//

		void LinkButtonClicked () 
		{
			string select = note.Buffer.Selection;

			if (select != null) {
				Note match = note.Manager.Find (select);
				if (match == null)
					match = note.Manager.Create (select);

				match.Window.Present ();
			}
		}

		// 
		// Signature trampoline for editor context-menu "Link to Note"
		//

		void LinkToNoteActivate (object sender, EventArgs args)
		{
			LinkButtonClicked ();
		}

		//
		// Popup the Text menu.
		//
		// Called when clicking the Font toolbar button.
		//

		void TextButtonPress (object sender, Gtk.ButtonPressEventArgs args) 
		{
			text_menu.RefreshState ();
			GuiUtils.PopupMenu (text_menu, args.Event);
		}

		//
		// Call Release on text_button to reset its state when the menu
		// closes
		//

		void ReleaseTextButton (object sender, EventArgs args) 
		{
			text_button.Release ();
		}

		void TextButtonClicked (object sender, EventArgs args) 
		{
			text_menu.RefreshState ();
			GuiUtils.PopupMenu (text_menu, null);
		}
	}

	public class NoteTextMenu : Gtk.Menu
	{
		Gtk.AccelGroup accel_group;
		NoteBuffer buffer;
		UndoManager undo_manager;
		bool event_freeze;

		Gtk.ImageMenuItem undo;
		Gtk.ImageMenuItem redo;
		Gtk.CheckMenuItem bold;
		Gtk.CheckMenuItem italic;
		Gtk.CheckMenuItem strikeout;
		Gtk.RadioMenuItem normal;
		Gtk.RadioMenuItem huge;
		Gtk.RadioMenuItem large;
		Gtk.RadioMenuItem small;
		Gtk.CheckMenuItem highlight;

		// FIXME: Tags applied to a word should hold over the space
		// between the next word, as thats where you'll start typeing.
		// Tags are only active -after- a character with that tag.  This
		// is different from the way gtk-textbuffer applies tags.

		//
		// Text menu
		//
		// Menu for font style and size, and set the active radio
		// menuitem depending on the cursor poition.
		//

		public NoteTextMenu (Gtk.AccelGroup accel_group, 
				     NoteBuffer     buffer, 
				     UndoManager    undo_manager) 
			: base ()
		{
			this.accel_group = accel_group;
			this.buffer = buffer;
			this.undo_manager = undo_manager;

			if (undo_manager != null) {
				undo = new Gtk.ImageMenuItem (Gtk.Stock.Undo, accel_group);
				undo.Activated += UndoClicked;
				undo.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.z, 
						     Gdk.ModifierType.ControlMask,
						     Gtk.AccelFlags.Visible);
				undo.Show ();
				Append (undo);

				redo = new Gtk.ImageMenuItem (Gtk.Stock.Redo, accel_group);
				redo.Activated += RedoClicked;
				redo.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.z, 
						     (Gdk.ModifierType.ControlMask | 
						      Gdk.ModifierType.ShiftMask),
						     Gtk.AccelFlags.Visible);
				redo.Show ();
				Append (redo);

				Gtk.SeparatorMenuItem undo_spacer = new Gtk.SeparatorMenuItem ();
				Append (undo_spacer);

				// Listen to events so we can sensitize and
				// enable keybinding
				undo_manager.UndoChanged += UndoChanged;
			}

			bold = new Gtk.CheckMenuItem (Catalog.GetString ("<b>_Bold</b>"));
			MarkupLabel (bold);
			bold.Data ["Tag"] = "bold";
			bold.Activated += FontStyleClicked;
			bold.AddAccelerator ("activate",
					     accel_group,
					     (uint) Gdk.Key.b, 
					     Gdk.ModifierType.ControlMask,
					     Gtk.AccelFlags.Visible);

			italic = new Gtk.CheckMenuItem (Catalog.GetString ("<i>_Italic</i>"));
			MarkupLabel (italic);
			italic.Data ["Tag"] = "italic";
			italic.Activated += FontStyleClicked;
			italic.AddAccelerator ("activate",
					       accel_group,
					       (uint) Gdk.Key.i, 
					       Gdk.ModifierType.ControlMask,
					       Gtk.AccelFlags.Visible);

			strikeout = new Gtk.CheckMenuItem (Catalog.GetString ("<s>_Strikeout</s>"));
			MarkupLabel (strikeout);
			strikeout.Data ["Tag"] = "strikethrough";
			strikeout.Activated += FontStyleClicked;
			strikeout.AddAccelerator ("activate",
						  accel_group,
						  (uint) Gdk.Key.s, 
						  Gdk.ModifierType.ControlMask,
						  Gtk.AccelFlags.Visible);

			highlight = new Gtk.CheckMenuItem ("<span background='yellow'>" +
							   Catalog.GetString ("_Highlight") +
							   "</span>");
			MarkupLabel (highlight);
			highlight.Data ["Tag"] = "highlight";
			highlight.Activated += FontStyleClicked;
			highlight.AddAccelerator ("activate",
						  accel_group,
						  (uint) Gdk.Key.h, 
						  Gdk.ModifierType.ControlMask,
						  Gtk.AccelFlags.Visible);

			Gtk.SeparatorMenuItem spacer1 = new Gtk.SeparatorMenuItem ();

			Gtk.MenuItem font_size = new Gtk.MenuItem (Catalog.GetString ("Font Size"));
			font_size.Sensitive = false;

			normal = new Gtk.RadioMenuItem (Catalog.GetString ("_Normal"));
			MarkupLabel (normal);
			normal.Active = true;
			normal.Toggled += FontSizeClicked;

			huge = new Gtk.RadioMenuItem (normal.Group, 
						      "<span size=\"x-large\">" +
						      Catalog.GetString ("H_uge") +
						      "</span>");
			MarkupLabel (huge);
			huge.Data ["Tag"] = "size:huge";
			huge.Toggled += FontSizeClicked;

			large = new Gtk.RadioMenuItem (huge.Group, 
						       "<span size=\"large\">" +
						       Catalog.GetString ("_Large") +
						       "</span>");
			MarkupLabel (large);
			large.Data ["Tag"] = "size:large";
			large.Toggled += FontSizeClicked;

			small = new Gtk.RadioMenuItem (large.Group, 
						       "<span size=\"small\">" +
						       Catalog.GetString ("S_mall") +
						       "</span>");
			MarkupLabel (small);
			small.Data ["Tag"] = "size:small";
			small.Toggled += FontSizeClicked;

			RefreshState ();

			Append (bold);
			Append (italic);
			Append (strikeout);
			Append (highlight);
			Append (spacer1);
			Append (font_size);
			Append (small);
			Append (normal);
			Append (large);
			Append (huge);
			ShowAll ();
		}

		void MarkupLabel (Gtk.MenuItem item)
		{
			Gtk.Label label = (Gtk.Label) item.Child;
			label.UseMarkup = true;
			label.UseUnderline = true;
		}

		public void RefreshState ()
		{
			event_freeze = true;

			bool has_size = false;

			bold.Active = buffer.IsActiveTag ("bold");
			italic.Active = buffer.IsActiveTag ("italic");
			strikeout.Active = buffer.IsActiveTag ("strikethrough");
			highlight.Active = buffer.IsActiveTag ("highlight");

			has_size |= huge.Active = buffer.IsActiveTag ("size:huge");
			has_size |= large.Active = buffer.IsActiveTag ("size:large");
			has_size |= small.Active = buffer.IsActiveTag ("size:small");

			normal.Active = !has_size;

			if (undo_manager != null) {
				undo.Sensitive = undo_manager.CanUndo;
				redo.Sensitive = undo_manager.CanRedo;
			}

			event_freeze = false;
		}
		
		// 
		// Font-style menu item activate
		//
		// Toggle the style tag for the current text.  Style tags are
		// stored in a "Tag" member of the menuitem's Data.
		//

		void FontStyleClicked (object sender, EventArgs args) 
		{
			if (event_freeze)
				return;

			Gtk.Widget item = (Gtk.Widget) sender;
			string tag = (string) item.Data ["Tag"];

			if (tag != null)
				buffer.ToggleActiveTag (tag);
		}

		// 
		// Font-style menu item activate
		//
		// Set the font size tag for the current text.  Style tags are
		// stored in a "Tag" member of the menuitem's Data.
		//

		void FontSizeClicked (object sender, EventArgs args) 
		{
			if (event_freeze)
				return;

			Gtk.RadioMenuItem item = (Gtk.RadioMenuItem) sender;
			string tag = (string) item.Data ["Tag"];

			if (tag != null) {
				// FIXME: if we want to set the font size from
				// an accelerator, we can't rely on the state of
				// item.Active
				if (item.Active)
					buffer.SetActiveTag (tag);
				else
					buffer.RemoveActiveTag (tag);
			}
		}

		internal void UndoClicked (object sender, EventArgs args)
		{
			if (undo_manager.CanUndo) {
				Console.WriteLine ("Running undo...");
				undo_manager.Undo ();
			}
		}

		internal void RedoClicked (object sender, EventArgs args)
		{
			if (undo_manager.CanRedo) {
				Console.WriteLine ("Running redo...");
				undo_manager.Redo ();
			}
		}

		void UndoChanged (object sender, EventArgs args)
		{
			undo.Sensitive = undo_manager.CanUndo;
			redo.Sensitive = undo_manager.CanRedo;
		}
	}
}
