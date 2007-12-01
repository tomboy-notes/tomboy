
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;

namespace Tomboy
{
	public delegate bool TagActivatedHandler (NoteTag tag,
						  NoteEditor editor,
						  Gtk.TextIter start, 
						  Gtk.TextIter end);

	public class NoteTag : Gtk.TextTag
	{
		string element_name;
		Gtk.TextMark widgetLocation;
		Gtk.Widget widget;

		[Flags]
		enum TagFlags {
			CanSerialize = 1,
			CanUndo = 2,
			CanGrow = 4,
			CanSpellCheck = 8,
			CanActivate = 16,
			CanSplit = 32
		};
		
		TagFlags flags;
		
		public NoteTag (string tag_name)
			: base(tag_name)
		{
			if (tag_name == null || tag_name == "") {
				throw new Exception ("NoteTags must have a tag name.  Use " +
						     "DynamicNoteTag for constructing " +
						     "anonymous tags.");
			}

			Initialize (tag_name);
		}

		internal NoteTag ()
			: base (null)
		{
			// Constructor used (only) by DynamicNoteTag
			// Initialize() is called by NoteTagTable.Create().
		}

		public NoteTag (IntPtr raw)
			: base (raw)
		{
			Logger.Log ("{0} IntPtr initializer called!", GetType());
			Logger.Log ((new System.Diagnostics.StackTrace()).ToString());
		}

		public virtual void Initialize (string element_name)
		{
			this.element_name = element_name;

			flags = TagFlags.CanSerialize | TagFlags.CanSplit;
		}

		public string ElementName
		{
			get { return element_name; }
		}

		public bool CanSerialize 
		{
			get { return (flags & TagFlags.CanSerialize) != 0; }
			set {
				if (value)
					flags |= TagFlags.CanSerialize;
				else 
					flags &= ~TagFlags.CanSerialize;
			}
		}

		public bool CanUndo 
		{
			get { return (flags & TagFlags.CanUndo) != 0; }
			set {
				if (value)
					flags |= TagFlags.CanUndo;
				else 
					flags &= ~TagFlags.CanUndo;
			}
		}

		public bool CanGrow
		{
			get { return (flags & TagFlags.CanGrow) != 0; }
			set {
				if (value)
					flags |= TagFlags.CanGrow;
				else 
					flags &= ~TagFlags.CanGrow;
			}
		}

		public bool CanSpellCheck
		{
			get { return (flags & TagFlags.CanSpellCheck) != 0; }
			set {
				if (value)
					flags |= TagFlags.CanSpellCheck;
				else 
					flags &= ~TagFlags.CanSpellCheck;
			}
		}

		public bool CanActivate
		{
			get { return (flags & TagFlags.CanActivate) != 0; }
			set {
				if (value)
					flags |= TagFlags.CanActivate;
				else 
					flags &= ~TagFlags.CanActivate;
			}
		}

		public bool CanSplit
		{
			get { return (flags & TagFlags.CanSplit) != 0; }
			set {
				if (value)
					flags |= TagFlags.CanSplit;
				else
					flags &= ~TagFlags.CanSplit;
			}
		}

		public void GetExtents (Gtk.TextIter iter, 
					out Gtk.TextIter start, 
					out Gtk.TextIter end)
		{
			start = iter;
			if (!start.BeginsTag (this))
				start.BackwardToTagToggle (this);

			end = iter;
			end.ForwardToTagToggle (this);
		}
		
		public virtual void Write (XmlTextWriter xml, bool start)
		{
			if (CanSerialize) {
				if (start) {
					xml.WriteStartElement (null, element_name, null);
				} else {
					xml.WriteEndElement();
				}
			}
		}

		public virtual void Read (XmlTextReader xml, bool start)
		{
			if (CanSerialize) {
				if (start) {
					element_name = xml.Name;
				}
			}
		}

		protected override bool OnTextEvent (GLib.Object  sender, 
						     Gdk.Event    ev, 
						     Gtk.TextIter iter)
		{
			NoteEditor editor = (NoteEditor) sender;
			Gtk.TextIter start, end;

			if (!CanActivate)
				return false;

			switch (ev.Type) {
			case Gdk.EventType.ButtonPress:
				Gdk.EventButton button_ev = new Gdk.EventButton (ev.Handle);

				if (button_ev.Button != 1 && button_ev.Button != 2)
					return false;

				/* Don't activate if Shift or Control is pressed */
				if ((int) (button_ev.State & (Gdk.ModifierType.ShiftMask |
							      Gdk.ModifierType.ControlMask)) != 0)
					return false;

				GetExtents (iter, out start, out end);
				bool success = OnActivate (editor, start, end);

				if (success && button_ev.Button == 2) {
					Gtk.Widget widget = (Gtk.Widget) sender;
					widget.Toplevel.Hide ();
				}

				return success;

			case Gdk.EventType.KeyPress:
				Gdk.EventKey key_ev = new Gdk.EventKey (ev.Handle);

				// Control-Enter activates the link at point...
				if ((int) (key_ev.State & Gdk.ModifierType.ControlMask) == 0)
					return false;

				if (key_ev.Key != Gdk.Key.Return &&
				    key_ev.Key != Gdk.Key.KP_Enter)
					return false;

				GetExtents (iter, out start, out end);
				return OnActivate (editor, start, end);
			}

			return false;
		}

		protected virtual bool OnActivate (NoteEditor editor, 
						   Gtk.TextIter start, 
						   Gtk.TextIter end)
		{
			bool retval = false;

			if (Activated != null) {
				foreach (Delegate d in Activated.GetInvocationList()) {
					TagActivatedHandler handler = (TagActivatedHandler) d;
					retval |= handler (this, editor, start, end);
				}
			}

			return retval;
		}

		public event TagActivatedHandler Activated;

		public virtual Gdk.Pixbuf Image
		{
			get {
				Gtk.Image image = widget as Gtk.Image;
				if (image == null) return null;

				return image.Pixbuf;
			}
			set {
				if(value == null) {
					Widget = null;
					return;
				}

				Gtk.Image image = new Gtk.Image (value);
				Widget = image;
			}
		}

		public virtual Gtk.Widget Widget
		{
			get { return widget; }
			set {
				if (value == null && widget != null) {
					widget.Destroy ();
					widget = null;
				}

				widget = value;

				if (Changed != null) {
					Gtk.TagChangedArgs args = new Gtk.TagChangedArgs ();
					args.Args = new object [2];
					args.Args [0] = false; // SizeChanged
					args.Args [1] = this;  // Tag
					try {
						Changed (this, args);
					} catch (Exception e) {
						Logger.Warn ("Exception calling TagChanged from NoteTag.set_Widget: {0}", e.Message);
					}
				}
			}
		}

		public virtual Gtk.TextMark WidgetLocation
		{
			get { return widgetLocation; }
			set { widgetLocation = value; }
		}

		// From contrast.h
		public enum PaletteColor {
			CONTRAST_COLOR_AQUA        =  0,
			CONTRAST_COLOR_BLACK       =  1,
			CONTRAST_COLOR_BLUE        =  2,
			CONTRAST_COLOR_BROWN       =  3,
			CONTRAST_COLOR_CYAN        =  4,
			CONTRAST_COLOR_DARK_BLUE   =  5,
			CONTRAST_COLOR_DARK_GREEN  =  6,
			CONTRAST_COLOR_DARK_GREY   =  7,
			CONTRAST_COLOR_DARK_RED    =  8,
			CONTRAST_COLOR_GREEN       =  9,
			CONTRAST_COLOR_GREY        = 10,
			CONTRAST_COLOR_LIGHT_BLUE  = 11,
			CONTRAST_COLOR_LIGHT_BROWN = 12,
			CONTRAST_COLOR_LIGHT_GREEN = 13,
			CONTRAST_COLOR_LIGHT_GREY  = 14,
			CONTRAST_COLOR_LIGHT_RED   = 15,
			CONTRAST_COLOR_MAGENTA     = 16,
			CONTRAST_COLOR_ORANGE      = 17,
			CONTRAST_COLOR_PURPLE      = 18,
			CONTRAST_COLOR_RED         = 19,
			CONTRAST_COLOR_VIOLET      = 20,
			CONTRAST_COLOR_WHITE       = 21,
			CONTRAST_COLOR_YELLOW      = 22,
			CONTRAST_COLOR_LAST        = 23,
		};

		[DllImport("libtomboy")]
		static extern Gdk.Color contrast_render_foreground_color(
					    Gdk.Color background,
					    PaletteColor symbol);

		Gdk.Color get_background()
		{
			/* We can't know the exact background because we're not
			   in TextView's rendering, but we can make a guess */
			if (BackgroundSet)
				return BackgroundGdk;

			Gtk.Style s = Gtk.Rc.GetStyleByPaths(Gtk.Settings.Default,
			    "GtkTextView", "GtkTextView", Gtk.TextView.GType);
			return s.Background(Gtk.StateType.Normal);
		}

		Gdk.Color render_foreground(PaletteColor symbol)
		{
			return contrast_render_foreground_color(get_background(), symbol);
		}

		private PaletteColor PaletteForeground_;
		public PaletteColor PaletteForeground {
			set {
				PaletteForeground_ = value;
				// XXX We should also watch theme changes.
				ForegroundGdk = render_foreground(value);
			}
			get {
				return PaletteForeground_;
			}
		}

		public event Gtk.TagChangedHandler Changed;
	}

	public class DynamicNoteTag : NoteTag
	{
		Hashtable attributes;

		public DynamicNoteTag ()
			: base()
		{
		}

		public Hashtable Attributes 
		{
			get { 
				if (attributes == null)
					attributes = new Hashtable ();
				return attributes; 
			}
		}

		public override void Write (XmlTextWriter xml, bool start)
		{
			if (CanSerialize) {
				base.Write (xml, start);

				if (start && attributes != null) {
					foreach (string key in attributes.Keys) {
						string val = (string) attributes [key];
						xml.WriteAttributeString (null, key, null, val);
					}
				}
			}
		}

		public override void Read (XmlTextReader xml, bool start)
		{
			if (CanSerialize) {
				base.Read (xml, start);

				if (start) {
					while (xml.MoveToNextAttribute()) {
						string name = xml.Name;

						xml.ReadAttributeValue();
						Attributes [name] = xml.Value;

                                                OnAttributeRead (name);
						Logger.Log (
							"NoteTag: {0} read attribute {1}='{2}'",
							ElementName,
							name,
							xml.Value);
					}
				}
			}
		}
                
                /// <summary>
                /// Derived classes should override this if they desire
                /// to be notified when a tag attribute is read in.
                /// </summary>
                /// <param name="attributeName">
                /// A <see cref="System.String"/> that is the name of the
                /// newly read attribute.
                /// </param>
                protected virtual void OnAttributeRead (string attributeName) {}
	}
	
	public class DepthNoteTag : NoteTag
	{
		int depth = -1;
		Pango.Direction direction = Pango.Direction.Ltr;
		
		public int Depth
		{
			get{ return depth; }
		}
		
		public Pango.Direction Direction
		{
			get{ return direction; }
		}

		public DepthNoteTag (int depth, Pango.Direction direction)
			: base("depth:" + depth + ":" + direction)
		{
			this.depth = depth;
			this.direction = direction;
		}

		public override void Write (XmlTextWriter xml, bool start)
		{
			if (CanSerialize) {
				if (start) {
					xml.WriteStartElement (null, "list-item", null);
					
					// Write the list items writing direction
					xml.WriteStartAttribute (null, "dir", null);
					if (Direction == Pango.Direction.Rtl)
						xml.WriteString ("rtl");
					else
						xml.WriteString ("ltr");
					xml.WriteEndAttribute ();
				} else {
					xml.WriteEndElement ();
				}
			}
		}
	}	

	public class NoteTagTable : Gtk.TextTagTable
	{
		static NoteTagTable instance;
		Hashtable tag_types;
		ArrayList added_tags;

		public static NoteTagTable Instance 
		{
			get {
				if (instance == null) 
					instance = new NoteTagTable ();
				return instance;
			}
		}

		public NoteTagTable () 
			: base ()
		{
			tag_types = new Hashtable ();
			added_tags = new ArrayList ();

			InitCommonTags ();
		}
		
		void InitCommonTags () 
		{
			NoteTag tag;

			// Font stylings

			tag = new NoteTag ("centered");
			tag.Justification = Gtk.Justification.Center;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("bold");
			tag.Weight = Pango.Weight.Bold;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("italic");
			tag.Style = Pango.Style.Italic;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("strikethrough");
			tag.Strikethrough = true;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("highlight");
			tag.Background = "yellow";
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("find-match");
			tag.Background = "green";
			tag.CanSerialize = false;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("note-title");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
				NoteTag.PaletteColor.CONTRAST_COLOR_BLUE;
			tag.Scale = Pango.Scale.XXLarge;
			// FiXME: Hack around extra rewrite on open
			tag.CanSerialize = false;
			Add (tag);

			tag = new NoteTag ("related-to");
			tag.Scale = Pango.Scale.Small;
			tag.LeftMargin = 40;
			tag.Editable = false;
			Add (tag);

			// Used when inserting dropped URLs/text to Start Here
			tag = new NoteTag ("datetime");
			tag.Scale = Pango.Scale.Small;
			tag.Style = Pango.Style.Italic;
			tag.PaletteForeground =
				NoteTag.PaletteColor.CONTRAST_COLOR_GREY;
			Add (tag);

			// Font sizes

			tag = new NoteTag ("size:huge");
			tag.Scale = Pango.Scale.XXLarge;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("size:large");
			tag.Scale = Pango.Scale.XLarge;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("size:normal");
			tag.Scale = Pango.Scale.Medium;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			tag = new NoteTag ("size:small");
			tag.Scale = Pango.Scale.Small;
			tag.CanUndo = true;
			tag.CanGrow = true;
			tag.CanSpellCheck = true;
			Add (tag);

			// Links

			tag = new NoteTag ("link:broken");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
				NoteTag.PaletteColor.CONTRAST_COLOR_GREY;
			tag.CanActivate = true;
			Add (tag);

			tag = new NoteTag ("link:internal");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
				NoteTag.PaletteColor.CONTRAST_COLOR_BLUE;
			tag.CanActivate = true;
			Add (tag);

			tag = new NoteTag ("link:url");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
				NoteTag.PaletteColor.CONTRAST_COLOR_BLUE;
			tag.CanActivate = true;
			Add (tag);
		}

		public static bool TagIsSerializable (Gtk.TextTag tag)
		{
			if (tag is NoteTag)
				return ((NoteTag) tag).CanSerialize;
			return false;
		}

		public static bool TagIsGrowable (Gtk.TextTag tag)
		{
			if (tag is NoteTag)
				return ((NoteTag) tag).CanGrow;
			return false;
		}

		public static bool TagIsUndoable (Gtk.TextTag tag)
		{
			if (tag is NoteTag)
				return ((NoteTag) tag).CanUndo;
			return false;
		}

		public static bool TagIsSpellCheckable (Gtk.TextTag tag)
		{
			if (tag is NoteTag)
				return ((NoteTag) tag).CanSpellCheck;
			return false;
		}

		public static bool TagIsActivatable (Gtk.TextTag tag)
		{
			if (tag is NoteTag)
				return ((NoteTag) tag).CanActivate;
			return false;
		}
		
		public static bool TagHasDepth (Gtk.TextTag tag)
		{
			if (tag is DepthNoteTag)
				return true;
			
			return false;
		}

		public DepthNoteTag GetDepthTag(int depth, Pango.Direction direction)
		{
			string name = "depth:" + depth + ":" + direction;
			
			DepthNoteTag tag = Lookup (name) as DepthNoteTag;

			if (tag == null) {
				tag = new DepthNoteTag (depth, direction);
				tag.Indent = -14;
				
				if (direction == Pango.Direction.Rtl)
					tag.RightMargin = (depth+1) * 25;
				else
					tag.LeftMargin = (depth+1) * 25;
				
				tag.PixelsBelowLines = 4;
				tag.Scale = Pango.Scale.Medium;
				Add (tag);
			}

			return tag;
		}

		public DynamicNoteTag CreateDynamicTag (string tag_name)
		{
			Type tag_type = tag_types [tag_name] as Type;
			if (tag_type == null) 
				return null;

			DynamicNoteTag tag = (DynamicNoteTag) Activator.CreateInstance(tag_type);
			tag.Initialize (tag_name);
			Add (tag);
			return tag;
		}

		public void RegisterDynamicTag (string tag_name, Type type)
		{
			if (!type.IsSubclassOf (typeof (DynamicNoteTag)))
				throw new Exception ("Must register only DynamicNoteTag types.");

			tag_types [tag_name] = type;
		}

		public bool IsDynamicTagRegistered (string tag_name)
		{
			return tag_types [tag_name] != null;
		}

		protected override void OnTagAdded (Gtk.TextTag tag)
		{
			added_tags.Add (tag);

			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				note_tag.Changed += OnTagChanged;
			}
		}

		protected override void OnTagRemoved (Gtk.TextTag tag)
		{
			added_tags.Remove (tag);

			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				note_tag.Changed -= OnTagChanged;
			}
		}

		void OnTagChanged (object sender, Gtk.TagChangedArgs args)
		{
			if (TagChanged != null) {
				TagChanged (this, args);
			}
		}

		public new event Gtk.TagChangedHandler TagChanged;
	}
}
