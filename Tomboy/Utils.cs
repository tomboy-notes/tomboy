
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.IO;
using System.Xml;

using Mono.Unix;

namespace Tomboy
{
	public class GuiUtils
	{
		static void GetMenuPosition (Gtk.Menu menu,
		                             out int  x,
		                             out int  y,
		                             out bool push_in)
		{
			if (menu.AttachWidget == null ||
			    menu.AttachWidget.GdkWindow == null) {
				// Prevent null exception in weird cases
				x = 0;
				y = 0;
				push_in = true;
				return;
			}
			
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
				menu.AttachWidget.SetStateFlags (Gtk.StateFlags.Normal, true);
		}

		// Place the menu underneath an arbitrary parent widget.  The
		// parent widget must be set using menu.AttachToWidget before
		// calling this.
		public static void PopupMenu (Gtk.Menu menu, Gdk.EventButton ev)
		{
			PopupMenu (menu, ev, new Gtk.MenuPositionFunc (GetMenuPosition));
		}

		public static void PopupMenu (Gtk.Menu menu, Gdk.EventButton ev, Gtk.MenuPositionFunc mpf)
		{
			menu.Deactivated += DeactivateMenu;
			try {
				menu.Popup (null,
				            null,
				            mpf,
				            (ev == null) ? 0 : ev.Button,
				            (ev == null) ? Gtk.Global.CurrentEventTime : ev.Time);
			} catch {
				Logger.Debug ("Menu popup failed with custom MenuPositionFunc; trying again without");
				menu.Popup (null,
				            null,
				            null,
				            (ev == null) ? 0 : ev.Button,
				            (ev == null) ? Gtk.Global.CurrentEventTime : ev.Time);
			}

			// Highlight the parent
			if (menu.AttachWidget != null)
				menu.AttachWidget.SetStateFlags (Gtk.StateFlags.Selected, true);

#if WIN32
			BringToForeground ();
#endif
		}

		public static void BringToForeground () {
			try {
				Process current_proc = Process.GetCurrentProcess ();
				int current_proc_id = current_proc.Id;
				bool set_foreground_window = true;
				IntPtr window_handle = GetForegroundWindow ();

				if (window_handle != IntPtr.Zero) {
					set_foreground_window = false;

					int window_handle_proc_id;
					GetWindowThreadProcessId (window_handle, out window_handle_proc_id);

					if (window_handle_proc_id != current_proc_id) {
						set_foreground_window = true;
					}
				}

				if (set_foreground_window) {
					window_handle = current_proc.MainWindowHandle;

					if (window_handle != IntPtr.Zero) {
						SetForegroundWindow (window_handle);
					}
				}
			} catch (Exception e) {
				Logger.Error("Error pulling Tomboy to foreground: {0}", e);
			}
		}

		[DllImport ("user32.dll", SetLastError = true)]
		static extern uint GetWindowThreadProcessId (IntPtr hWnd, out int lpdwProcessId);

		[DllImport ("user32.dll")]
		static extern IntPtr GetForegroundWindow ();

		[DllImport ("user32.dll")]
		[return: MarshalAs (UnmanagedType.Bool)]
		static extern bool SetForegroundWindow (IntPtr hWnd);

		public static Gdk.Pixbuf GetIcon (string resource_name, int size)
		{
			return GetIcon (null, resource_name, size);
		}
		
		public static Gdk.Pixbuf GetIcon (System.Reflection.Assembly asm,
										  string resource_name, int size)
		{
			try {
				return Gtk.IconTheme.Default.LoadIcon (resource_name, size, 0);
			} catch (GLib.GException) {}

			try {
				Gdk.Pixbuf ret = new Gdk.Pixbuf (asm, resource_name + ".png");
				return ret.ScaleSimple (size, size, Gdk.InterpType.Bilinear);
			} catch (ArgumentException) {}

			Logger.Debug ("Unable to load icon '{0}'.", resource_name);
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

		public static void ShowHelp (string project,
		                             string page,
		                             Gdk.Screen screen,
		                             Gtk.Window parent)
		{
			try {
				Services.NativeApplication.DisplayHelp (project, page, screen);
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
		
		public static void ShowOpeningLocationError (Gtk.Window parent, string url, string error)
		{
			string message = String.Format ("{0}: {1}", url, error);

			HIGMessageDialog dialog =
			        new HIGMessageDialog (parent,
			                              Gtk.DialogFlags.DestroyWithParent,
			                              Gtk.MessageType.Info,
			                              Gtk.ButtonsType.Ok,
			                              Catalog.GetString ("Cannot open location"),
			                              message);
			dialog.Run ();
			dialog.Destroy ();
		}

		/// <summary>
		/// Get a string that is more friendly/pretty for the specified date.
		/// For example, "Today, 3:00 PM", "4 days ago, 9:20 AM".
		/// <param name="date">The DateTime to evaluate</param>
		/// <param name="show_time">If true, output the time along with the
		/// date</param>
		/// </summary>
		public static string GetPrettyPrintDate (DateTime date, bool show_time)
		{
			string pretty_str = String.Empty;
			DateTime now = DateTime.Now;
			string short_time = date.ToShortTimeString ();

			if (date.Year == now.Year) {
				if (date.DayOfYear == now.DayOfYear)
					pretty_str = show_time ?
					             String.Format (Catalog.GetString ("Today, {0}"),
					                            short_time) :
					             Catalog.GetString ("Today");
				else if (date.DayOfYear < now.DayOfYear
				                && date.DayOfYear == now.DayOfYear - 1)
					pretty_str = show_time ?
					             String.Format (Catalog.GetString ("Yesterday, {0}"),
					                            short_time) :
					             Catalog.GetString ("Yesterday");
				else if (date.DayOfYear < now.DayOfYear
				                && date.DayOfYear > now.DayOfYear - 6)
					pretty_str = show_time ?
					             String.Format (Catalog.GetPluralString (
								"{0} day ago, {1}", "{0} days ago, {1}", 
								now.DayOfYear - date.DayOfYear),
								now.DayOfYear - date.DayOfYear, short_time) :
					             String.Format (Catalog.GetPluralString (
								"{0} day ago", "{0} days ago",
								now.DayOfYear - date.DayOfYear),
								now.DayOfYear - date.DayOfYear);
				else if (date.DayOfYear > now.DayOfYear
				                && date.DayOfYear == now.DayOfYear + 1)
					pretty_str = show_time ?
					             String.Format (Catalog.GetString ("Tomorrow, {0}"),
					                            short_time) :
					             Catalog.GetString ("Tomorrow");
				else if (date.DayOfYear > now.DayOfYear
				                && date.DayOfYear < now.DayOfYear + 6)
					pretty_str = show_time ?
					             String.Format (Catalog.GetPluralString (
								"In {0} day, {1}", "In {0} days, {1}",
								date.DayOfYear - now.DayOfYear),
								date.DayOfYear - now.DayOfYear, short_time) :
					             String.Format (Catalog.GetPluralString (
								"In {0} day", "In {0} days",
								date.DayOfYear - now.DayOfYear),
								date.DayOfYear - now.DayOfYear);
				else
					pretty_str = show_time ?
					             date.ToString (Catalog.GetString ("MMMM d, h:mm tt")) :
					             date.ToString (Catalog.GetString ("MMMM d"));
			} else if (date == DateTime.MinValue)
				pretty_str = Catalog.GetString ("No Date");
			else
				pretty_str = show_time ?
				             date.ToString (Catalog.GetString ("MMMM d yyyy, h:mm tt")) :
				             date.ToString (Catalog.GetString ("MMMM d yyyy"));

			return pretty_str;
		}

		/// <summary>
		/// Invoke a method on the GUI thread, and wait for it to
		/// return. If the method raises an exception, it will be
		/// thrown from this method.
		/// </summary>
		/// <param name="a">
		/// The action to invoke.
		/// </param>
		public static void GtkInvokeAndWait (Action a)
		{
			Exception mainThreadException = null;
			AutoResetEvent evt = new AutoResetEvent (false);
			Gtk.Application.Invoke (delegate {
				try {
					a.Invoke ();
				} catch (Exception e) {
					mainThreadException = e;
				}
				evt.Set ();
			});
			evt.WaitOne ();
			if (mainThreadException != null)
				throw mainThreadException;
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
		Gtk.VBox extra_widget_vbox;
		Gtk.Widget extra_widget;
		Gtk.Image image;

		public HIGMessageDialog (Gtk.Window parent,
		                         Gtk.DialogFlags flags,
		                         Gtk.MessageType type,
		                         Gtk.ButtonsType buttons,
		                         string          header,
		                         string          msg)
: base ()
		{
//			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = "";

			ContentArea.Spacing = 12;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;

			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			hbox.BorderWidth = 5;
			hbox.Show ();
			ContentArea.PackStart (hbox, false, false, 0);

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
			default:
				image = new Gtk.Image ();
				break;
			}

			if (image != null) {
				image.Show ();
				image.Yalign = 0;
				hbox.PackStart (image, false, false, 0);
			}

			Gtk.VBox label_vbox = new Gtk.VBox (false, 0);
			label_vbox.Show ();
			hbox.PackStart (label_vbox, true, true, 0);

			string title = String.Format ("<span weight='bold' size='larger'>{0}" +
			                              "</span>\n",
			                              header);

			Gtk.Label label;

			label = new Gtk.Label (title);
			label.UseMarkup = true;
			label.UseUnderline = false;
			label.Justify = Gtk.Justification.Left;
			label.LineWrap = true;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			label_vbox.PackStart (label, false, false, 0);

			label = new Gtk.Label (msg);
			label.UseMarkup = true;
			label.UseUnderline = false;
			label.Justify = Gtk.Justification.Left;
			label.LineWrap = true;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();
			label_vbox.PackStart (label, false, false, 0);
			
			extra_widget_vbox = new Gtk.VBox (false, 0);
			extra_widget_vbox.Show();
			label_vbox.PackStart (extra_widget_vbox, true, true, 12);

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

		protected void AddButton (string stock_id, Gtk.ResponseType response, bool is_default)
		{
			Gtk.Button button = new Gtk.Button (stock_id);
			button.CanDefault = true;
			
			AddButton (button, response, is_default);
		}
		
		protected void AddButton (Gdk.Pixbuf pixbuf, string label_text, Gtk.ResponseType response, bool is_default)
		{
			Gtk.Button button = new Gtk.Button ();
			Gtk.Image image = new Gtk.Image (pixbuf);
			// NOTE: This property is new to GTK+ 2.10, but we don't
			//       really need the line because we're just setting
			//       it to the default value anyway.
			//button.ImagePosition = Gtk.PositionType.Left;
			button.Image = image;
			button.Label = label_text;
			button.UseUnderline = true;
			button.CanDefault = true;
			
			AddButton (button, response, is_default);
		}
		
		private void AddButton (Gtk.Button button, Gtk.ResponseType response, bool is_default)
		{
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
		
		public Gtk.Widget ExtraWidget
		{
			get {
				return extra_widget;
			}
			set {
				if (extra_widget != null) {
					extra_widget_vbox.Remove (extra_widget);
				}
				
				extra_widget = value;
				extra_widget.ShowAll ();
				extra_widget_vbox.PackStart (extra_widget, true, true, 0);
			}
		}
		
		/// <value>
		/// This allows you to set the Gdk.Pixbuf for the dialog's Gtk.Image.
		/// </value>
		public Gdk.Pixbuf Pixbuf
		{
			set {
				image.Pixbuf = value;
			}
		}
	}

	public class UriList : List<Uri>
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

				Logger.Debug ("uri = {0}", s);
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
		static XmlWriterSettings documentSettings;
		static XmlWriterSettings fragmentSettings;

		static XmlEncoder ()
		{
			documentSettings = new XmlWriterSettings ();
			documentSettings.NewLineChars = "\n";
			documentSettings.Indent = true;

			fragmentSettings = new XmlWriterSettings ();
			fragmentSettings.NewLineChars = "\n";
			fragmentSettings.Indent = true;
			fragmentSettings.ConformanceLevel = ConformanceLevel.Fragment;

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

		public static XmlWriterSettings DocumentSettings
		{
			get { return documentSettings; }
		}

		public static XmlWriterSettings FragmentSettings
		{
			get { return fragmentSettings; }
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
			get {
				return buffer;
			}
		}

		public string Text
		{
			get {
				return Start.GetText (End);
			}
		}

		public int Length
		{
			get {
				return Text.Length;
			}
		}

		public Gtk.TextIter Start
		{
			get {
				return buffer.GetIterAtMark (start_mark);
			}
			set {
				buffer.MoveMark (start_mark, value);
			}
		}

		public Gtk.TextIter End
		{
			get {
				return buffer.GetIterAtMark (end_mark);
			}
			set {
				buffer.MoveMark (end_mark, value);
			}
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
			get {
				return range;
			}
		}

		// FIXME: Mutability bugs.  multiple Links on the same line
		// aren't getting renamed.
		public bool MoveNext ()
		{
			Gtk.TextIter iter = buffer.GetIterAtMark (mark);

			if (iter.Equals (buffer.EndIter)) {
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
#if !WIN32 && !MAC
		public new void Present ()
		{
			//delay Present() to play well with global key bindings
			Gtk.Application.Invoke (delegate { base.Present (); } );
		}
#endif
	}

	class ToolMenuButton : Gtk.ToggleToolButton
	{
		Gtk.Menu menu;

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
		                       Gtk.Menu menu) : base ()
		{
			this.IconWidget = image;
			Gtk.Label l = new Gtk.Label (label);
			l.UseUnderline = true;
			this.LabelWidget = l;
			this.CanFocus = true;
//			this.FocusOnClick = false; // TODO: Not supported anymore?
			this.menu = menu;
			menu.AttachToWidget (this,GuiUtils.DetachMenu);
			menu.Deactivated += ReleaseButton;

			this.ShowAll ();
		}

		protected override bool OnButtonPressEvent (Gdk.EventButton ev)
		{
			GuiUtils.PopupMenu (menu, ev);
			return true;
		}

		protected override void OnClicked ()
		{
			menu.SelectFirst (true);
			GuiUtils.PopupMenu (menu, null);
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
		}
	}

	public class Application
	{
		static INativeApplication native_app;
		static ActionManager action_manager;

		public static void Initialize (string locale_dir,
		                               string display_name,
		                               string process_name,
		                               string [] args)
		{
			native_app = Services.NativeApplication;
			native_app.Initialize (locale_dir, display_name, process_name, args);

			action_manager = new ActionManager ();
			action_manager.LoadInterface ();

			native_app.RegisterSignalHandlers ();
		}

		public static void RegisterSessionManagerRestart (string executable_path,
		                string[] args,
		                string[] environment)
		{
			native_app.RegisterSessionManagerRestart (executable_path, args, environment);
		}

		public static event EventHandler ExitingEvent
		{
		        add { native_app.ExitingEvent += value; }
		        remove { native_app.ExitingEvent -= value; }
		}

		public static void Exit (int exitcode)
		{
			native_app.Exit (exitcode);
		}

		public static void StartMainLoop ()
		{
			native_app.StartMainLoop ();
		}

		public static ActionManager ActionManager
		{
			get {
				return action_manager;
			}
		}
	}

	public static class IOUtils
	{
		/// <summary>
		/// Recursively copy the directory specified by old_path to
		/// new_path. Assumes that old_path is an existing directory
		/// and new_path does not exist.
		/// </summary>
		public static void CopyDirectory (string old_path, string new_path)
		{
			Directory.CreateDirectory (new_path);
			foreach (string file_path in Directory.GetFiles (old_path))
				File.Copy (file_path, Path.Combine (new_path, Path.GetFileName (file_path)));
			foreach (string dir_path in Directory.GetDirectories (old_path))
				CopyDirectory (dir_path, Path.Combine (new_path, Path.GetFileName (dir_path)));
		}
	}
}
