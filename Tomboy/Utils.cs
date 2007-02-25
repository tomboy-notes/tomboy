
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Xml;

using Mono.Unix;
using Mono.Unix.Native;

namespace Tomboy
{
	public class GuiUtils 
	{
		static void GetMenuPosition (Gtk.Menu menu, 
					     out int  x, 
					     out int  y, 
					     out bool push_in)
		{
			menu.AttachWidget.GdkWindow.GetOrigin (out x, out y);
			x += menu.AttachWidget.Allocation.X;

			Gtk.Requisition menu_req = menu.SizeRequest ();
			if (y + menu_req.Height >= menu.AttachWidget.Screen.Height)
				y -= menu_req.Height;
			else
				y += menu.AttachWidget.Allocation.Height;

			push_in = true;
		}

		public static void DetachMenu (Gtk.Widget attach, Gtk.Menu menu)
		{
			// Do nothing.  Callers can use this to work around a
			// Gtk#2 binding bug requiring a non-null detach
			// delegate when calling Gtk.Menu.AttachToWidget.
		}

		static void DeactivateMenu (object sender, EventArgs args) 
		{
			Gtk.Menu menu = (Gtk.Menu) sender;
			menu.Popdown ();

			// Unhighlight the parent
			if (menu.AttachWidget != null)
				menu.AttachWidget.State = Gtk.StateType.Normal;
		}

		// Place the menu underneath an arbitrary parent widget.  The
		// parent widget must be set using menu.AttachToWidget before
		// calling this.
		public static void PopupMenu (Gtk.Menu menu, Gdk.EventButton ev)
		{
			menu.Deactivated += DeactivateMenu;
			menu.Popup (null, 
				    null, 
				    new Gtk.MenuPositionFunc (GetMenuPosition), 
				    (ev == null) ? 0 : ev.Button, 
				    (ev == null) ? Gtk.Global.CurrentEventTime : ev.Time);

			// Highlight the parent
			if (menu.AttachWidget != null)
				menu.AttachWidget.State = Gtk.StateType.Selected;
		}

		public static Gdk.Pixbuf GetIcon (string resource_name, int size) 
		{
			try {
				return Gtk.IconTheme.Default.LoadIcon (resource_name, size, 0);
			} catch (GLib.GException) {}

			try {
				Gdk.Pixbuf ret = new Gdk.Pixbuf (null, resource_name + ".png");
				return ret.ScaleSimple (size, size, Gdk.InterpType.Bilinear);
			} catch (ArgumentException) {}

			Logger.Log ("Unable to load icon '{0}'.", resource_name);
			return null;
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

		public static void ShowHelp (string filename, 
					     string link_id, 
					     Gdk.Screen screen, 
					     Gtk.Window parent)
		{
			try {
				Gnome.Help.DisplayDesktopOnScreen (
					Gnome.Program.Get (),
					Defines.GNOME_HELP_DIR,
					filename, 
					link_id, 
					screen);
			} catch {
				string message = 
					Catalog.GetString ("The \"Tomboy Notes Manual\" could " +
							   "not be found.  Please verify " +
							   "that your installation has been " +
							   "completed successfully.");
				HIGMessageDialog dialog = 
					new HIGMessageDialog (parent,
							      Gtk.DialogFlags.DestroyWithParent,
							      Gtk.MessageType.Error,
							      Gtk.ButtonsType.Ok,
							      Catalog.GetString ("Help not found"),
							      message);
				dialog.Run ();
				dialog.Destroy ();
			}
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
				image = new Gtk.Image (Gtk.Stock.DialogError, 
						       Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Question:
				image = new Gtk.Image (Gtk.Stock.DialogQuestion, 
						       Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Info:
				image = new Gtk.Image (Gtk.Stock.DialogInfo, 
						       Gtk.IconSize.Dialog);
				break;
			case Gtk.MessageType.Warning:
				image = new Gtk.Image (Gtk.Stock.DialogWarning, 
						       Gtk.IconSize.Dialog);
				break;
			}

			image.Show ();
			image.Yalign = 0;
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

	public class UriList : ArrayList 
	{
		public UriList (Note [] notes) 
		{
			foreach (Note note in notes) {
				try {
					Uri uri = new Uri (note.Uri);
					Add (uri);
				} catch {
				}
			}
		}

		private void LoadFromString (string data) 
		{
			string [] items = data.Split ('\n');

			foreach (string i in items) {
				if (i.StartsWith ("#"))
					continue;

				string s = i;
				if (s.EndsWith ("\r"))
					s = s.Substring (0, s.Length - 1);

				// Handle evo's broken file urls
				if (s.StartsWith ("file:////"))
					s = s.Replace ("file:////", "file:///");

				Logger.Log ("uri = {0}", s);
				try {
					Uri uri = new Uri (s);
					if (uri != null)
						Add (uri);
				} catch {
				}
			}
		}

		public UriList (string data) 
		{
			LoadFromString (data);
		}

		public UriList (Gtk.SelectionData selection) 
		{
			// FIXME this should check the atom etc.
			if (selection.Length > 0)
				LoadFromString (Encoding.UTF8.GetString (selection.Data));
		}

		public override string ToString () 
		{
			StringBuilder list = new StringBuilder ();

			foreach (Uri uri in this) {
				list.Append (uri.ToString () + "\r\n");
			}

			return list.ToString ();
		}

		public string [] GetLocalPaths () 
		{
			int count = 0;
			foreach (Uri uri in this) {
				if (uri.IsFile)
					count++;
			}

			string [] paths = new string [count];

			count = 0;
			foreach (Uri uri in this) {
				if (uri.IsFile)
					paths [count++] = uri.LocalPath;
			}

			return paths;
		}
	}

	// Encode xml entites
	public class XmlEncoder 
	{
		static StringBuilder builder;
		static StringWriter writer;
		static XmlTextWriter xml;

		static XmlEncoder ()
		{
			builder = new StringBuilder ();
			writer = new StringWriter (builder);
			xml = new XmlTextWriter (writer);
		}

		public static string Encode (string source)
		{
			xml.WriteString (source);

			string val = builder.ToString ();
			builder.Length = 0;
			return val;
		}
	}

	// Strip xml tags
	public class XmlDecoder 
	{
		static StringBuilder builder;

		static XmlDecoder ()
		{
			builder = new StringBuilder ();
		}

		public static string Decode (string source)
		{
			StringReader reader = new StringReader (source);
			XmlTextReader xml = new XmlTextReader (reader);
			xml.Namespaces = false;

			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Text:
				case XmlNodeType.Whitespace:
					builder.Append (xml.Value);
					break;
				}
			}

			xml.Close ();

			string val = builder.ToString ();
			builder.Length = 0;
			return val;
		}
	}

	public class TextRange
	{
		Gtk.TextBuffer buffer;
		Gtk.TextMark start_mark;
		Gtk.TextMark end_mark;

		public TextRange (Gtk.TextIter start, 
				  Gtk.TextIter end)
		{
			if (start.Buffer != end.Buffer)
				throw new Exception ("Start buffer and end buffer do not match");

			buffer = start.Buffer;
			start_mark = buffer.CreateMark (null, start, true);
			end_mark = buffer.CreateMark (null, end, true);
		}

		public Gtk.TextBuffer Buffer
		{
			get { return buffer; }
		}

		public string Text
		{
			get { return Start.GetText (End); }
		}

		public int Length
		{
			get { return Text.Length; }
		}

		public Gtk.TextIter Start
		{
			get { return buffer.GetIterAtMark (start_mark); }
			set { buffer.MoveMark (start_mark, value); }
		}

		public Gtk.TextIter End
		{
			get { return buffer.GetIterAtMark (end_mark); }
			set { buffer.MoveMark (end_mark, value); }
		}

		public void Erase ()
		{
			Gtk.TextIter start_iter = Start;
			Gtk.TextIter end_iter = End;
			buffer.Delete (ref start_iter, ref end_iter);
		}

		public void Destroy ()
		{
			buffer.DeleteMark (start_mark);
			buffer.DeleteMark (end_mark);
		}

		public void RemoveTag (Gtk.TextTag tag)
		{
			buffer.RemoveTag (tag, Start, End);
		}
	}

	public class TextTagEnumerator : IEnumerator, IEnumerable
	{
		Gtk.TextBuffer buffer;
		Gtk.TextTag tag;
		Gtk.TextMark mark;
		TextRange range;

		public TextTagEnumerator (Gtk.TextBuffer buffer, string tag_name)
			: this (buffer, buffer.TagTable.Lookup (tag_name))
		{
		}

		public TextTagEnumerator (Gtk.TextBuffer buffer, Gtk.TextTag tag)
		{
			this.buffer = buffer;
			this.tag = tag;

			this.mark = buffer.CreateMark (null, buffer.StartIter, true);
			this.range = new TextRange (buffer.StartIter, buffer.StartIter);
		}

		public object Current
		{ 
			get { return range; }
		}

		// FIXME: Mutability bugs.  multiple Links on the same line
		// aren't getting renamed.
		public bool MoveNext ()
		{
			Gtk.TextIter iter = buffer.GetIterAtMark (mark);

			if (iter.Equal (buffer.EndIter)) {
				range.Destroy ();
				buffer.DeleteMark (mark);
				return false;
			}

			if (!iter.ForwardToTagToggle (tag)) {
				range.Destroy ();
				buffer.DeleteMark (mark);
				return false;
			}

			if (!iter.BeginsTag (tag)) {
				buffer.MoveMark (mark, iter);
				return MoveNext ();
			}

			range.Start = iter;

			if (!iter.ForwardToTagToggle (tag)) {
				range.Destroy ();
				buffer.DeleteMark (mark);
				return false;
			}

			if (!iter.EndsTag (tag)) {
				buffer.MoveMark (mark, iter);
				return MoveNext ();
			}

			range.End = iter;

			buffer.MoveMark (mark, iter);

			return true;
		}

		public void Reset ()
		{
			buffer.MoveMark (mark, buffer.StartIter);
		}

		public IEnumerator GetEnumerator ()
		{
			return this;
		}
	}

	public class InterruptableTimeout
	{
		uint timeout_id;
		EventArgs args;

		public InterruptableTimeout ()
		{
		}

		public void Reset (uint timeout_millis)
		{
			Reset (timeout_millis, null);
		}

		public void Reset (uint timeout_millis, EventArgs args)
		{
			Cancel ();
			this.args = args;
			timeout_id = GLib.Timeout.Add (timeout_millis, 
						       new GLib.TimeoutHandler (TimeoutExpired));
		}

		public void Cancel ()
		{
			if (timeout_id != 0) {
				GLib.Source.Remove (timeout_id);
				timeout_id = 0;
				args = null;
			}
		}

		bool TimeoutExpired ()
		{
			if (Timeout != null)
				Timeout (this, args);

			timeout_id = 0;
			return false;
		}

		public event EventHandler Timeout;
	}

	public class ForcedPresentWindow : Gtk.Window
	{
		public ForcedPresentWindow (string name)
			: base (name)
		{
		}

		[DllImport("libtomboy")]
		static extern void tomboy_window_present_hardcore (IntPtr win);

		public new void Present ()
		{
			tomboy_window_present_hardcore (this.Handle);
		}
	}

	class ToolMenuButton : Gtk.ToggleButton
	{
		bool is_important;
		Gtk.Menu menu;
		Gtk.Image image;
		Gtk.Label label_horiz;
		Gtk.Label label_vert;
		Gtk.Arrow arrow;
		Gtk.VBox box_vert;
		Gtk.HBox box_horiz;

		public ToolMenuButton (Gtk.Toolbar toolbar, 
				       string stock_image, 
				       string label, 
				       Gtk.Menu menu)
			: this (toolbar, 
				new Gtk.Image (stock_image, toolbar.IconSize), 
				label,
				menu)
		{
		}

		public ToolMenuButton (Gtk.Toolbar toolbar, 
				       Gtk.Image image, 
				       string label, 
				       Gtk.Menu menu)
		{
			this.CanFocus = true;
			this.Relief = toolbar.ReliefStyle;
			this.FocusOnClick = false;

			this.is_important = false;

			this.menu = menu;
			this.menu.AttachToWidget (this, GuiUtils.DetachMenu);
			this.menu.Deactivated += ReleaseButton;

			this.image = image;
			label_horiz = new Gtk.Label (label);
			label_vert = new Gtk.Label (label);
			arrow = new Gtk.Arrow (Gtk.ArrowType.Down, Gtk.ShadowType.In);

			box_vert = new Gtk.VBox (false, 0);
			box_vert.PackStart (image, true, true, 0);
			box_vert.PackStart (label_vert, false, true, 0);

			box_horiz = new Gtk.HBox (false, 0);
			box_horiz.PackStart (box_vert, true, true, 0);
			box_horiz.PackEnd (arrow, false, true, 0);

			this.Add (box_horiz);
			this.ShowAll ();
			ShowForToolbarStyle (toolbar.ToolbarStyle);

			toolbar.StyleChanged += OnToolbarStyleChanged;
		}

		protected override bool OnButtonPressEvent (Gdk.EventButton ev) 
		{
			GuiUtils.PopupMenu (menu, ev);
			Active = true;
			return true;
		}

		protected override void OnActivated () 
		{
			menu.SelectFirst (true);
			GuiUtils.PopupMenu (menu, null);
			Active = true;
		}

		protected override bool OnMnemonicActivated (bool group_cycling)
		{
			// ToggleButton always grabs focus away from the editor,
			// so reimplement Widget's version, which only grabs the
			// focus if we are group cycling.
			if (!group_cycling) {
				Activate ();
			} else if (CanFocus) {
				GrabFocus ();
			}

			return true;
		}

		void ReleaseButton (object sender, EventArgs args) 
		{
			// Release the state when the menu closes
			Active = false;
			Release ();
		}

		public bool IsImportant
		{
			get {
				return is_important;
			}
			set {
				if (value && !is_important) {
					box_horiz.PackStart (label_horiz, true, true, 2);
				} else if (!value && is_important) {
					box_horiz.Remove (label_horiz);
				}
				is_important = value;
			}
		}

		void ShowForToolbarStyle (Gtk.ToolbarStyle style)
		{
			switch (style) {
			case Gtk.ToolbarStyle.Icons:
				label_horiz.Hide ();
				label_vert.Hide ();
				image.Show ();
				break;
			case Gtk.ToolbarStyle.Text:
				label_vert.Hide ();
				image.Hide ();
				label_horiz.Show ();
				break;
			case Gtk.ToolbarStyle.Both:
				label_horiz.Hide ();
				image.Show ();
				label_vert.Show ();
				break;
			case Gtk.ToolbarStyle.BothHoriz:
				label_vert.Hide ();
				image.Show ();
				label_horiz.Show ();
				break;
			}
		}

		void OnToolbarStyleChanged (object sender, Gtk.StyleChangedArgs args)
		{
			ShowForToolbarStyle (args.Style);
		}
	}

	public class Application
	{
		static Gnome.Program program;
		static ActionManager action_manager;

		public static void Initialize (string locale_dir, 
					       string display_name, 
					       string process_name,  
					       string [] args)
		{
			try {
				SetProcessName (process_name);
			} catch {} // Ignore exception if fail (not needed to run)

			Gtk.Application.Init ();
			program = new Gnome.Program ("Tomboy", 
						     Defines.VERSION, 
						     Gnome.Modules.UI, 
						     args);
			
			action_manager = new ActionManager ();
			action_manager.LoadInterface ();

			RegisterSignalHandlers ();
		}

		[DllImport("libc")]
		private static extern int prctl (int option, 
						 byte [] arg2, 
						 IntPtr arg3, 
						 IntPtr arg4, 
						 IntPtr arg5);

		// From Banshee: Banshee.Base/Utilities.cs
		public static void SetProcessName (string name)
		{
			if (prctl (15 /* PR_SET_NAME */, 
				   Encoding.ASCII.GetBytes (name + "\0"),
				   IntPtr.Zero, 
				   IntPtr.Zero, 
				   IntPtr.Zero) != 0)
				throw new ApplicationException (
					"Error setting process name: " +
					Mono.Unix.Native.Stdlib.GetLastError ());
		}

		static void RegisterSignalHandlers ()
		{
			// Connect to SIGTERM and SIGINT, so we don't lose
			// unsaved notes on exit...
			Stdlib.signal (Signum.SIGTERM, OnExitSignal);
			Stdlib.signal (Signum.SIGINT, OnExitSignal);
		}

		public static void RegisterSessionManagerRestart (string executable_path,
								  string[] args, 
								  string[] environment)
		{
			if (executable_path == null)
				return;

			// Restart if we are running when the session ends or at crash...
			Gnome.Client client = Gnome.Global.MasterClient ();
			client.RestartStyle = 
				Gnome.RestartStyle.IfRunning | Gnome.RestartStyle.Immediately;
			client.Die += OnSessionManagerDie;

			foreach (string env in environment) {
				string [] split = env.Split (new char [] { '=' }, 2);
				if (split.Length == 2) {
					client.SetEnvironment (split[0], split[1]);
				}
			}

			// Get the args for session restart... 	 
			string [] restart_args = new string [args.Length + 1]; 	 
			restart_args [0] = executable_path;
			args.CopyTo (restart_args, 1);
			client.SetRestartCommand (restart_args.Length, restart_args);
		}

		static void OnSessionManagerDie (object sender, EventArgs args)
		{
			// Don't let the exit signal run, which would cancel
			// session management.
			QuitMainLoop ();
		}

		static void CancelSessionManagerRestart ()
		{
			Gnome.Client client = Gnome.Global.MasterClient ();
			client.RestartStyle = Gnome.RestartStyle.IfRunning;
			client.Flush ();
		}

		static void OnExitSignal (int signal)
		{
			// Don't auto-restart after exit/kill.
			CancelSessionManagerRestart ();

			if (ExitingEvent != null)
				ExitingEvent (null, new EventArgs ());

			if (signal >= 0)
				System.Environment.Exit (0);
		}

		public static event EventHandler ExitingEvent;

		public static void Exit (int exitcode)
		{
			OnExitSignal (-1);
			System.Environment.Exit (exitcode);
		}

		public static void StartMainLoop ()
		{
			program.Run ();
		}

		public static void QuitMainLoop ()
		{
			Gtk.Main.Quit ();
		}
		
		public static ActionManager ActionManager
		{
			get { return action_manager; }
		}
	}
}
