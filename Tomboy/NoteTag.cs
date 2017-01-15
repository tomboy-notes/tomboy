
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;

namespace Tomboy
{
	public delegate bool TagActivatedHandler (NoteTag tag,
	                NoteEditor editor,
	                Gtk.TextIter start,
	                Gtk.TextIter end);
	
	public enum TagSaveType {
		NoSave,
		Meta,
		Content
	};

	public class NoteTag : Gtk.TextTag
	{
		string element_name;
		Gtk.TextMark widgetLocation;
		Gtk.Widget widget;
		bool allow_middle_activate = false;

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
			Logger.Info ("{0} IntPtr initializer called!", GetType());
			Logger.Info ((new System.Diagnostics.StackTrace()).ToString());
		}

		public virtual void Initialize (string element_name)
		{
			this.element_name = element_name;

			flags = TagFlags.CanSerialize | TagFlags.CanSplit;
			SaveType = TagSaveType.Content;
		}

		public string ElementName
		{
			get {
				return element_name;
			}
		}
		
		/// <summary>
		/// How the note should be saved when this tag is modified
		/// </summary>
		public TagSaveType SaveType;

		public bool CanSerialize
		{
			get {
				return (flags & TagFlags.CanSerialize) != 0;
			}
			set {
				if (value)
					flags |= TagFlags.CanSerialize;
				else
					flags &= ~TagFlags.CanSerialize;
			}
		}

		public bool CanUndo
		{
			get {
				return (flags & TagFlags.CanUndo) != 0;
			}
			set {
				if (value)
					flags |= TagFlags.CanUndo;
				else
					flags &= ~TagFlags.CanUndo;
			}
		}

		public bool CanGrow
		{
			get {
				return (flags & TagFlags.CanGrow) != 0;
			}
			set {
				if (value)
					flags |= TagFlags.CanGrow;
				else
					flags &= ~TagFlags.CanGrow;
			}
		}

		public bool CanSpellCheck
		{
			get {
				return (flags & TagFlags.CanSpellCheck) != 0;
			}
			set {
				if (value)
					flags |= TagFlags.CanSpellCheck;
				else
					flags &= ~TagFlags.CanSpellCheck;
			}
		}

		public bool CanActivate
		{
			get {
				return (flags & TagFlags.CanActivate) != 0;
			}
			set {
				if (value)
					flags |= TagFlags.CanActivate;
				else
					flags &= ~TagFlags.CanActivate;
			}
		}

		public bool CanSplit
		{
			get {
				return (flags & TagFlags.CanSplit) != 0;
			}
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

		// XmlTextWriter is required, because an XmlWriter created with
		// XmlWriter.Create considers ":" to be an invalid character
		// for an element name.
		// http://bugzilla.gnome.org/show_bug.cgi?id=559094
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

				// Do not insert selected text when activating links with
				// middle mouse button
				if (button_ev.Button == 2) {
					allow_middle_activate = true;
					return true;
				}

				return false;

			case Gdk.EventType.ButtonRelease:
				button_ev = new Gdk.EventButton (ev.Handle);

				if (button_ev.Button != 1 && button_ev.Button != 2)
					return false;

				/* Don't activate if Shift or Control is pressed */
				if ((int) (button_ev.State & (Gdk.ModifierType.ShiftMask |
				                              Gdk.ModifierType.ControlMask)) != 0)
					return false;

				// Prevent activation when selecting links with the mouse
				if (editor.Buffer.HasSelection)
					return false;

				// Don't activate if the link has just been pasted with the
				// middle mouse button (no preceding ButtonPress event)
				if (button_ev.Button == 2 && !allow_middle_activate)
					return false;
				else
					allow_middle_activate = false;

				GetExtents (iter, out start, out end);
				bool success = OnActivate (editor, start, end);

				// Hide note if link is activated with middle mouse button
				if (success && button_ev.Button == 2) {
					Gtk.Widget widget = (Gtk.Widget) sender;
					widget.Toplevel.Hide ();
				}

				return false;

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
				if (value == null) {
					Widget = null;
					return;
				}

				Gtk.Image image = new Gtk.Image (value);
				Widget = image;
			}
		}

		public virtual Gtk.Widget Widget
		{
			get {
				return widget;
			}
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
			get {
				return widgetLocation;
			}
			set {
				widgetLocation = value;
			}
		}

		Gdk.Color get_background()
		{
			/* We can't know the exact background because we're not
			   in TextView's rendering, but we can make a guess */
			if (BackgroundSet)
				return BackgroundGdk;

			Gtk.Style s = Gtk.Rc.GetStyleByPaths(Gtk.Settings.Default,
			                                     "GtkTextView", "GtkTextView", Gtk.TextView.GType);
			if (s == null) {
				Logger.Debug ("get_background: Style for GtkTextView came back null! Returning white...");
				return new Gdk.Color (0xff, 0xff, 0xff); //white, for lack of a better idea
			}
			else
				return s.Background(Gtk.StateType.Normal);
		}

		Gdk.Color render_foreground(ContrastPaletteColor symbol)
		{
			return Contrast.RenderForegroundColor(get_background(), symbol);
		}

		private ContrastPaletteColor PaletteForeground_;
		public ContrastPaletteColor PaletteForeground {
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
		Dictionary<string, string> attributes;

		public DynamicNoteTag ()
: base()
		{
		}

		public IDictionary<string, string> Attributes
		{
			get {
				if (attributes == null)
					attributes = new Dictionary<string, string> ();
				return attributes;
			}
		}

		public override void Write (XmlTextWriter xml, bool start)
		{
			if (CanSerialize) {
				base.Write (xml, start);

				if (start && attributes != null) {
					foreach (string key in attributes.Keys) {
						string val = attributes [key];
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
						Logger.Debug (
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
			get{
				return depth;
			}
		}

		public new Pango.Direction Direction
		{
			get{
				return direction;
			}
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
		Dictionary<string, Type> tag_types;
		List<Gtk.TextTag> added_tags;

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
			tag_types = new Dictionary<string, Type> ();
			added_tags = new List<Gtk.TextTag> ();

			InitCommonTags ();
		}

		public NoteTag UrlTag { get; private set; }
		public NoteTag LinkTag { get; private set; }
		public NoteTag BrokenLinkTag { get; private set; }

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
			tag.Background = "lawngreen";
			tag.CanSerialize = false;
			tag.CanSpellCheck = true;
			tag.SaveType = TagSaveType.Meta;
			Add (tag);

			tag = new NoteTag ("note-title");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
			        ContrastPaletteColor.Blue;
			tag.Scale = Pango.Scale.XXLarge;
			// FiXME: Hack around extra rewrite on open
			tag.CanSerialize = false;
			tag.SaveType = TagSaveType.Meta;
			Add (tag);

			tag = new NoteTag ("related-to");
			tag.Scale = Pango.Scale.Small;
			tag.LeftMargin = 40;
			tag.Editable = false;
			tag.SaveType = TagSaveType.Meta;
			Add (tag);

			tag = new NoteTag ("datetime");
			tag.Scale = Pango.Scale.Small;
			tag.Style = Pango.Style.Italic;
			tag.PaletteForeground =
			        ContrastPaletteColor.Grey;
			tag.CanGrow = true;
			tag.SaveType = TagSaveType.Meta;
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
			        ContrastPaletteColor.Grey;
			tag.CanActivate = true;
			tag.SaveType = TagSaveType.Meta;
			Add (tag);
			BrokenLinkTag = tag;

			tag = new NoteTag ("link:internal");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
			        ContrastPaletteColor.Blue;
			tag.CanActivate = true;
			tag.SaveType = TagSaveType.Meta;
			Add (tag);
			LinkTag = tag;

			tag = new NoteTag ("link:url");
			tag.Underline = Pango.Underline.Single;
			tag.PaletteForeground =
			        ContrastPaletteColor.Blue;
			tag.CanActivate = true;
			tag.SaveType = TagSaveType.Meta;
			Add (tag);
			UrlTag = tag;
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

		public bool HasLinkTag (Gtk.TextIter iter)
		{
			return iter.HasTag (LinkTag) || iter.HasTag (UrlTag) || iter.HasTag (BrokenLinkTag);
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
		
		/// <summary>
		/// Maps a Gtk.TextTag to ChangeType for saving notes
		/// </summary>
		/// <param name="tag">Gtk.TextTag to map</param>
		/// <returns>ChangeType to save this NoteTag</returns>
		public ChangeType GetChangeType (Gtk.TextTag tag)
		{
			ChangeType change;
			
			// Use tag Name for Gtk.TextTags
			switch (tag.Name)
			{
				// For extensibility, add Gtk.TextTag names here
				default:
					change = ChangeType.OtherDataChanged;
					break;
			}
			
			// Use SaveType for NoteTags
			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				switch (note_tag.SaveType)
				{
					case TagSaveType.Meta:
						change = ChangeType.OtherDataChanged;
						break;
					case TagSaveType.Content:
						change = ChangeType.ContentChanged;
						break;
					case TagSaveType.NoSave:
					default:
						change = ChangeType.NoChange;
						break;
				}
			}
			
			return change;
		}

		public DynamicNoteTag CreateDynamicTag (string tag_name)
		{
			Type tag_type;
			if (!tag_types.TryGetValue (tag_name, out tag_type))
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
			Type type;
			if (tag_types.TryGetValue (tag_name, out type) &&
			    type != null)
				return true;
			return false;
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
