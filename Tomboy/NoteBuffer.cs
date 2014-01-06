
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Tomboy
{
	// Provides the concept of active tags, which are applied on text
	// insert.  Exposes the UndoManager for this buffer.  And adds a
	// InsertTextWithTags event which is fired after inserted text has all
	// the active tags applied.
	public class NoteBuffer : Gtk.TextBuffer
	{
		UndoManager undo_manager;
		char[] indent_bullets = {
			'\u2022',
#if !MAC
			'\u2218', // Not available on Mac, need to pick something else
#endif
			'\u2023'};

		// GODDAMN Gtk.TextBuffer. I hate you. Hate Hate Hate.
		struct WidgetInsertData
		{
			public bool adding;
			public Gtk.TextBuffer buffer;
			public Gtk.TextMark position;
			public Gtk.Widget widget;
			public NoteTag tag;
		};
		Queue <WidgetInsertData> widgetQueue;
		uint widgetQueueTimeout;
		// HATE.

		// list of Gtk.TextTags to apply on insert
		List<Gtk.TextTag> active_tags;

		// The note that owns this buffer
		private Note note;

		public bool EnableAutoBulletedLists
		{
			get
			{
				string key = Preferences.ENABLE_AUTO_BULLETED_LISTS;
				return Convert.ToBoolean (Preferences.Get (key));
			}
		}

		private static bool text_buffer_serialize_func_fixed = typeof(Gtk.TextBufferSerializeFunc).GetMethod ("Invoke").ReturnType == typeof(byte[]);

		public NoteBuffer (Gtk.TextTagTable tags, Note note)
: base (tags)
		{
			// Ensure Gtk# has the fix for BNC #555495
			if (text_buffer_serialize_func_fixed) {
				RegisterSerializeFormat ("text/html", SerializeToHtml);
			}

			active_tags = new List<Gtk.TextTag> ();
			undo_manager = new UndoManager (this);

			InsertText += TextInsertedEvent;
			DeleteRange += RangeDeletedEvent;
			MarkSet += MarkSetEvent;

			TagApplied += OnTagApplied;

			tags.TagChanged += OnTagChanged;

			widgetQueue = new Queue <WidgetInsertData> ();
			widgetQueueTimeout = 0;

			this.note = note;
		}

		private static XslTransform html_transform;
		private static XslTransform HtmlTransform {
			get {
				if (html_transform == null) {
					html_transform = new XslTransform ();
					var resource = typeof(NoteBuffer).Assembly.GetManifestResourceStream ("tomboy-note-clipboard-html.xsl");
					var reader = new XmlTextReader (resource);
					html_transform.Load (reader, null, null);
					reader.Close ();
				}
				return html_transform;
			}
		}

		private static byte [] SerializeToHtml (Gtk.TextBuffer register_buffer, Gtk.TextBuffer content_buffer, Gtk.TextIter start, Gtk.TextIter end)
		{
			if (start.Equals (end) || start.Equals (Gtk.TextIter.Zero) || end.Equals (Gtk.TextIter.Zero) || HtmlTransform == null) {
				return new byte [0];
			}

			Logger.Debug ("Handling text/html Clipboard copy/cut request");
			var xsl = HtmlTransform;

			string xml = String.Format (
				"<note version=\"0.3\" xmlns:link=\"http://beatniksoftware.com/tomboy/link\" xmlns:size=\"http://beatniksoftware.com/tomboy/size\">{0}</note>",
				NoteBufferArchiver.Serialize (register_buffer, start, end)
			);

			var reader = new StringReader (xml);
			var doc = new XPathDocument (reader);
			var args = new XsltArgumentList ();

			var writer = new StringWriter ();
			xsl.Transform (doc, args, writer);

			string html = writer.ToString ();
			byte [] bytes = System.Text.Encoding.UTF8.GetBytes (html);
			return bytes;
		}

		// Signal that text has been inserted, and any active tags have
		// been applied to the text.  This allows undo to pull any
		// active tags from the inserted text.
		public event Gtk.InsertTextHandler InsertTextWithTags;

		public event ChangeDepthHandler ChangeTextDepth;

		public event NewBulletHandler NewBulletInserted;

		public void ToggleActiveTag (string tag_name)
		{
			Logger.Debug ("ToggleTag called for '{0}'", tag_name);

			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter select_start, select_end;

			if (GetSelectionBounds (out select_start, out select_end)) {
				// Ignore the bullet character
				if (FindDepthTag (select_start) != null)
					select_start.LineOffset = 2;

				if (select_start.BeginsTag (tag) || select_start.HasTag (tag))
					RemoveTag (tag, select_start, select_end);
				else
					ApplyTag (tag, select_start, select_end);
			} else {
				if (active_tags.Contains (tag))
					active_tags.Remove (tag);
				else
					active_tags.Add (tag);
			}
		}

		public void SetActiveTag (string tag_name)
		{
			Logger.Debug ("SetTag called for '{0}'", tag_name);

			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter select_start, select_end;

			if (GetSelectionBounds (out select_start, out select_end)) {
				ApplyTag (tag, select_start, select_end);
			} else {
				active_tags.Add (tag);
			}
		}

		public void RemoveActiveTag (string tag_name)
		{
			Logger.Debug ("RemoveTag called for '{0}'", tag_name);

			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter select_start, select_end;

			if (GetSelectionBounds (out select_start, out select_end)) {
				RemoveTag (tag, select_start, select_end);
			} else {
				active_tags.Remove (tag);
			}
		}

		/// <summary>
		/// Returns the specified DynamicNoteTag if one exists on the TextIter
		/// or null if none was found.
		/// </summary>
		public DynamicNoteTag GetDynamicTag (string tag_name, Gtk.TextIter iter)
		{
			// TODO: Is this variables used, or do we just need to
			// access iter.Tags to work around a bug?
			Gtk.TextTag [] tags = iter.Tags;
			foreach (Gtk.TextTag tag in iter.Tags) {
				DynamicNoteTag dynamic_tag = tag as DynamicNoteTag;
				if (dynamic_tag != null &&
				                dynamic_tag.ElementName.CompareTo (tag_name) == 0)
					return dynamic_tag;
			}

			return null;
		}

		public void OnTagApplied (object o, Gtk.TagAppliedArgs args)
		{
			if (!(args.Tag is DepthNoteTag)) {
				// Remove the tag from any bullets in the selection
				Undoer.FreezeUndo ();
				Gtk.TextIter iter;
				for (int i = args.Start.Line; i <= args.End.Line; i++) {
					iter = GetIterAtLine(i);

					if (FindDepthTag (iter) != null) {
						Gtk.TextIter next = iter;
						next.ForwardChars (2);
						RemoveTag (args.Tag, iter, next);
					}
				}
				Undoer.ThawUndo ();
			} else {
				// Remove any existing tags when a depth tag is applied
				Undoer.FreezeUndo ();
				foreach (Gtk.TextTag tag in args.Start.Tags) {
					if (!(tag is DepthNoteTag)) {
						RemoveTag (tag, args.Start, args.End);
					}
				}
				Undoer.ThawUndo ();
			}
		}

		public bool IsActiveTag (string tag_name)
		{
			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter iter, select_end;

			if (GetSelectionBounds (out iter, out select_end)) {
				// Ignore the bullet character and look at the
				// first character of the list item
				if (FindDepthTag (iter) != null)
					iter.ForwardChars (2);
				return iter.BeginsTag (tag) || iter.HasTag (tag);
			} else {
				return active_tags.Contains (tag);
			}
		}

		// Returns true if the cursor is inside of a bulleted list
		public bool IsBulletedListActive ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;

			DepthNoteTag depth = FindDepthTag (iter);

			if (depth == null)
				return false;

			return true;
		}

		// Returns true if the cursor is at a position that can
		// be made into a bulleted list
		public bool CanMakeBulletedList ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);

			if (iter.Line == 0)
				return false;

			return true;
		}

		// Apply active_tags to inserted text
		void TextInsertedEvent (object sender, Gtk.InsertTextArgs args)
		{
			// Only apply active tags when typing, not on paste.
			if (args.NewTextLength == 1) {
				Gtk.TextIter insert_start = args.Pos;
				insert_start.BackwardChars (args.NewTextLength);

				Undoer.FreezeUndo ();
				foreach (Gtk.TextTag tag in insert_start.Tags) {
					RemoveTag (tag, insert_start, args.Pos);
				}

				foreach (Gtk.TextTag tag in active_tags) {
					ApplyTag (tag, insert_start, args.Pos);
				}
				Undoer.ThawUndo ();
			}

			// See if we want to change the direction of the bullet
			Gtk.TextIter line_start = args.Pos;
			line_start.LineOffset = 0;

			if (args.Pos.LineOffset - args.NewTextLength == 2 &&
			                FindDepthTag (line_start) != null) {
				Pango.Direction direction = Pango.Direction.Ltr;

				if (args.NewTextLength > 0)
					direction = Pango.Global.UnicharDirection (args.NewText[0]);

				ChangeBulletDirection (args.Pos, direction);
			}

			if (InsertTextWithTags != null)
				InsertTextWithTags (sender, args);
		}

		// Change the direction of a bulleted line to match the new
		// first character after the previous character is deleted.
		void RangeDeletedEvent (object sender, Gtk.DeleteRangeArgs args)
		{
			Gtk.TextIter[] iters = {args.Start, args.End};
			foreach (Gtk.TextIter iter in iters) {
				Gtk.TextIter line_start = iter;
				line_start.LineOffset = 0;

				if ((iter.LineOffset == 3 || iter.LineOffset == 2) &&
				                FindDepthTag (line_start) != null) {

					Gtk.TextIter first_char = iter;
					first_char.LineOffset = 2;

					Pango.Direction direction = Pango.Direction.Ltr;

					if (first_char.Char.Length > 0)
						direction = Pango.Global.UnicharDirection (first_char.Char[0]);

					ChangeBulletDirection (first_char, direction);
				}
			}
		}

		public bool AddNewline(bool soft_break)
		{
			if (!CanMakeBulletedList() || !EnableAutoBulletedLists)
				return false;

			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;

			DepthNoteTag prev_depth = FindDepthTag (iter);
			
			Gtk.TextIter insert = GetIterAtMark (insert_mark);
 
			// Insert a LINE SEPARATOR character which allows us
			// to have multiple lines in a single bullet point
			if (prev_depth != null && soft_break) {
				bool at_end_of_line = insert.EndsLine ();
				Insert (ref insert, "\u2028");
				
				// Hack so that the user sees that what they type
				// next will appear on a new line, otherwise the
				// cursor stays at the end of the previous line.
				if (at_end_of_line) {
					Insert (ref insert, " ");
					Gtk.TextIter bound = insert;
					bound.BackwardChar ();
					MoveMark (SelectionBound, bound);
				}
				
				return true;			

			// If the previous line has a bullet point on it we add a bullet
			// to the new line, unless the previous line was blank (apart from
			// the bullet), in which case we clear the bullet/indent from the
			// previous line.
			} else if (prev_depth != null) {
				iter.ForwardChar ();

				// See if the line was left contentless and remove the bullet
				// if so.
				if (iter.EndsLine () || insert.LineOffset < 3 ) {
					Gtk.TextIter start = GetIterAtLine (iter.Line);
					Gtk.TextIter end = start;
					end.ForwardToLineEnd ();

					if (end.LineOffset < 2) {
						end = start;
					} else {
						end = GetIterAtLineOffset (iter.Line, 2);
					}

					Delete (ref start, ref end);

					iter = GetIterAtMark (insert_mark);
					Insert (ref iter, "\n");
				} else {
					iter = GetIterAtMark (insert_mark);
					Gtk.TextIter prev = iter;
					prev.BackwardChar ();
					
					// Remove soft breaks
					if (prev.Char == "\u2028") {
						Delete (ref prev, ref iter);
					}
					
					Undoer.FreezeUndo ();
					int offset = iter.Offset;
					Insert (ref iter, "\n");

					iter = GetIterAtMark (insert_mark);
					Gtk.TextIter start = GetIterAtLine (iter.Line);

					// Set the direction of the bullet to be the same
					// as the first character on the new line
					Pango.Direction direction = prev_depth.Direction;
					if (iter.Char != "\n" && iter.Char.Length > 0)
						direction = Pango.Global.UnicharDirection (iter.Char[0]);

					InsertBullet (ref start, prev_depth.Depth, direction);
					Undoer.ThawUndo ();

					NewBulletInserted (this,
					                   new InsertBulletEventArgs (offset, prev_depth.Depth, direction));
				}

				return true;
			}
			// Replace lines starting with any numbers of leading spaces 
			// followed by '*' or '-' and then by a space with bullets
			else if (LineNeedsBullet(iter)) {
				Gtk.TextIter start = GetIterAtLineOffset (iter.Line, 0);
				Gtk.TextIter end = GetIterAtLineOffset (iter.Line, 0);

				// Remove any leading white space
				while (end.Char == " ")
					end.ForwardChar();
				// Remove the '*' or '-' character and the space after
				end.ForwardChars(2);
				
				// Set the direction of the bullet to be the same as
				// the first character after the '*' or '-'
				Pango.Direction direction = Pango.Direction.Ltr;
				if (end.Char.Length > 0)
					direction = Pango.Global.UnicharDirection (end.Char[0]);

				Delete (ref start, ref end);

				if (end.EndsLine ()) {
					IncreaseDepth (ref start);
				} else {
					IncreaseDepth (ref start);

					iter = GetIterAtMark (insert_mark);
					int offset = iter.Offset;
					Insert (ref iter, "\n");

					iter = GetIterAtMark (insert_mark);
					iter.LineOffset = 0;

					Undoer.FreezeUndo ();
					InsertBullet (ref iter, 0, direction);
					Undoer.ThawUndo ();

					NewBulletInserted (this,
					                   new InsertBulletEventArgs (offset, 0, direction));
				}

				return true;
			}

			return false;
		}

		// Returns true if line starts with any numbers of leading spaces
		// followed by '*' or '-' and then by a space
		private bool LineNeedsBullet(Gtk.TextIter iter)
		{
			while (!iter.EndsLine ()) {
				switch (iter.Char) {
				case " ":
					iter.ForwardChar ();
					break;
				case "*":
				case "-":
					if (GetIterAtLineOffset(iter.Line, iter.LineOffset + 1).Char.Equals(" ")) {
						return true;
					} else {
						return false;
					}
				default:
					return false;
				}
			}
			return false;
		}
		
		// Returns true if the depth of the line was increased
		public bool AddTab ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;

			DepthNoteTag depth = FindDepthTag (iter);

			// If the cursor is at a line with a depth and a tab has been
			// inserted then we increase the indent depth of that line.
			if (depth != null) {
				IncreaseDepth (ref iter);
				return true;
			}

			return false;
		}

		// Returns true if the depth of the line was decreased
		public bool RemoveTab ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;

			DepthNoteTag depth = FindDepthTag (iter);

			// If the cursor is at a line with depth and a tab has been
			// inserted, then we decrease the depth of that line.
			if (depth != null) {
				DecreaseDepth (ref iter);
				return true;
			}

			return false;
		}


		// Returns true if a bullet had to be removed
		// This is for the Delete key not Backspace
		public bool DeleteKeyHandler ()
		{
			// See if there is a selection
			Gtk.TextIter start;
			Gtk.TextIter end;

			bool selection = GetSelectionBounds (out start, out end);

			if (selection) {
				AugmentSelection (ref start, ref end);
				Delete (ref start, ref end);
				return true;
			} else if (start.EndsLine () && start.Line < LineCount) {
				Gtk.TextIter next = GetIterAtLine (start.Line + 1);
				end = start;
				
				if (IsBulletedListActive ())
					end.ForwardChars (3);
				else
					end.ForwardChars (1);

				DepthNoteTag depth = FindDepthTag (next);

				if (depth != null) {
					Delete (ref start, ref end);
					return true;
				}
			} else {
				Gtk.TextIter next = start;

				if (next.LineOffset != 0)
					next.ForwardChar ();

				DepthNoteTag depth = FindDepthTag (start);
				DepthNoteTag nextDepth = FindDepthTag (next);
				if (depth != null || nextDepth != null) {
					DecreaseDepth (ref start);
					return true;
				}
			}

			return false;
		}
		public bool BackspaceKeyHandler ()
		{
			Gtk.TextIter start;
			Gtk.TextIter end;

			bool selection = GetSelectionBounds (out start, out end);

			DepthNoteTag depth = FindDepthTag (start);

			if (selection) {
				AugmentSelection (ref start, ref end);
				Delete (ref start, ref end);
				return true;
			} else {
				// See if the cursor is inside or just after a bullet region
				// ie.
				// |* lorum ipsum
				//  ^^^
				// and decrease the depth if it is.

				Gtk.TextIter prev = start;

				if (prev.LineOffset != 0)
					prev.BackwardChars (1);

				DepthNoteTag prev_depth = FindDepthTag (prev);
				if (depth != null || prev_depth != null) {
					DecreaseDepth (ref start);
					return true;
				} else {
					// See if the cursor is before a soft line break
					// and remove it if it is. Otherwise you have to
					// press backspace twice before  it will delete
					// the previous visible character.
					prev = start;
					prev.BackwardChars (2);
					if (prev.Char == "\u2028") {
						Gtk.TextIter end_break = prev;
						end_break.ForwardChar ();
						Delete (ref prev, ref end_break);
					}
				}
			}

			return false;
		}
		// On an InsertEvent we change the selection (if there is one)
		// so that it doesn't slice through bullets.
		[GLib.ConnectBefore]
		public void CheckSelection ()
		{
			Gtk.TextIter start;
			Gtk.TextIter end;

			bool selection = GetSelectionBounds (out start, out end);

			if (selection) {
				AugmentSelection (ref start, ref end);
			} else {
				// If the cursor is at the start of a bulleted line
				// move it so it is after the bullet.
				if ((start.LineOffset == 0 || start.LineOffset == 1) &&
				                FindDepthTag (start) != null)
				{
					start.LineOffset = 2;
					SelectRange (start, start);
				}
			}
		}

		// Change the selection on the buffer taking into account any
		// bullets that are in or near the seletion
		void AugmentSelection (ref Gtk.TextIter start, ref Gtk.TextIter end)
		{
			DepthNoteTag start_depth = FindDepthTag (start);
			DepthNoteTag end_depth = FindDepthTag (end);

			Gtk.TextIter inside_end = end;
			inside_end.BackwardChar ();

			DepthNoteTag inside_end_depth = FindDepthTag (inside_end);

			// Start inside bullet region
			if (start_depth != null) {
				start.LineOffset = 2;
				SelectRange (start, end);
			}

			// End inside another bullet
			if (inside_end_depth != null) {
				end.LineOffset = 2;
				SelectRange (start, end);
			}

			// Check if the End is right before start of bullet
			if (end_depth != null) {
				end.LineOffset = 2;
				SelectRange (start, end);
			}
		}

		// Clear active tags, and add any tags which should be applied:
		// - Avoid the having tags grow frontwords by not adding tags
		//   which start on the next character.
		// - Add tags ending on the prior character, to avoid needing to
		//   constantly toggle tags.
		void MarkSetEvent (object sender, Gtk.MarkSetArgs args)
		{
			if (args.Mark != InsertMark)
				return;

			active_tags.Clear ();

			Gtk.TextIter iter = GetIterAtMark (args.Mark);

			// Add any growable tags not starting on the next character...
			foreach (Gtk.TextTag tag in iter.Tags) {
				if (!iter.BeginsTag (tag) &&
				                NoteTagTable.TagIsGrowable (tag)) {
					active_tags.Add (tag);
				}
			}

			// Add any growable tags not ending on the prior character...
			foreach (Gtk.TextTag tag in iter.GetToggledTags (false)) {
				if (!iter.EndsTag (tag) &&
				                NoteTagTable.TagIsGrowable (tag)) {
					active_tags.Add (tag);
				}
			}
		}

		void WidgetSwap (NoteTag tag,
		                 Gtk.TextIter start,
		                 Gtk.TextIter end,
		                 bool adding)
		{
			if (tag.Widget == null)
				return;

			Gtk.TextIter prev = start;
			prev.BackwardChar ();

			WidgetInsertData data = new WidgetInsertData ();
			data.buffer = start.Buffer;
			data.tag = tag;
			data.widget = tag.Widget;
			data.adding = adding;

			if (adding) {
				data.position = start.Buffer.CreateMark (null, start, true);
			} else {
				data.position = tag.WidgetLocation;
			}

			widgetQueue.Enqueue (data);

			if (widgetQueueTimeout == 0) {
				widgetQueueTimeout = GLib.Idle.Add(RunWidgetQueue);
			}
		}

		public bool RunWidgetQueue ()
		{
			foreach (WidgetInsertData data in widgetQueue) {
				// HACK: This is a quick fix for bug #486551
				if (data.position == null)
					continue;
				
				NoteBuffer buffer = data.buffer as NoteBuffer;
				Gtk.TextIter iter = buffer.GetIterAtMark (data.position);
				Gtk.TextMark location = data.position;

				// Prevent the widget from being inserted before a bullet
				if (FindDepthTag (iter) != null) {
					iter.LineOffset = 2;
					location = CreateMark(data.position.Name, iter, data.position.LeftGravity);
				}

				buffer.Undoer.FreezeUndo();

				if (data.adding && data.tag.WidgetLocation == null) {
					Gtk.TextChildAnchor childAnchor = buffer.CreateChildAnchor (ref iter);
					data.tag.WidgetLocation = location;
					note.AddChildWidget (childAnchor, data.widget);
				} else if (!data.adding && data.tag.WidgetLocation != null) {
					Gtk.TextIter end = iter;
					end.ForwardChar();
					buffer.Delete (ref iter, ref end);
					buffer.DeleteMark (location);
					data.tag.WidgetLocation = null;
				}

				buffer.Undoer.ThawUndo ();
			}

			widgetQueue.Clear ();

			widgetQueueTimeout = 0;
			return false;
		}

		void OnTagChanged (object sender, Gtk.TagChangedArgs args)
		{
			NoteTag note_tag = args.Tag as NoteTag;
			if (note_tag != null) {
				TextTagEnumerator enumerator =
				        new TextTagEnumerator (this, note_tag);
				foreach (TextRange range in enumerator) {
					WidgetSwap (note_tag, range.Start, range.End, true);
				}
			}
		}

		protected override void OnTagApplied (Gtk.TextTag tag,
		                                      Gtk.TextIter start,
		                                      Gtk.TextIter end)
		{
			base.OnTagApplied (tag, start, end);

			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				WidgetSwap (note_tag, start, end, true);
			}
		}

		protected override void OnTagRemoved (Gtk.TextTag tag,
		                                      Gtk.TextIter start,
		                                      Gtk.TextIter end)
		{
			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				WidgetSwap (note_tag, start, end, false);
			}

			base.OnTagRemoved (tag, start, end);
		}

		public UndoManager Undoer
		{
			get {
				return undo_manager;
			}
		}

		public string Selection
		{
			get {
				Gtk.TextIter select_start, select_end;

				if (GetSelectionBounds (out select_start, out select_end)) {
					string text = GetText (select_start, select_end, false);
					if (text.Length > 0)
						return text;
				}

				return null;
			}
		}

		public static void GetBlockExtents (ref Gtk.TextIter start,
		                                    ref Gtk.TextIter end,
		                                    int threshold,
		                                    Gtk.TextTag avoid_tag)
		{
			// Move start and end to the beginning or end of their
			// respective paragraphs, bounded by some threshold.

			start.LineOffset = Math.Max (0, start.LineOffset - threshold);

			// FIXME: Sometimes I need to access this before it
			// returns real values.
			int bug = end.CharsInLine;

			if (end.CharsInLine - end.LineOffset > threshold + 1 /* newline */)
				end.LineOffset += threshold;
			else
				end.ForwardToLineEnd ();

			if (avoid_tag != null) {
				if (start.HasTag (avoid_tag))
					start.BackwardToTagToggle (avoid_tag);

				if (end.HasTag (avoid_tag))
					end.ForwardToTagToggle (avoid_tag);
			}
		}

		// Toggle the lines in the selection to have bullets or not
		public void ToggleSelectionBullets ()
		{
			Gtk.TextIter start;
			Gtk.TextIter end;

			GetSelectionBounds (out start, out end);

			start = GetIterAtLineOffset (start.Line, 0);

			bool toggle_on = true;
			if (FindDepthTag (start) != null) {
				toggle_on = false;
			}

			int start_line = start.Line;
			int end_line = end.Line;

			for (int i = start_line; i <= end_line; i++) {
				Gtk.TextIter curr_line = GetIterAtLine(i);
				if (toggle_on && FindDepthTag (curr_line) == null) {
					IncreaseDepth (ref curr_line);
				} else if (!toggle_on && FindDepthTag (curr_line) != null) {
					Gtk.TextIter bullet_end = GetIterAtLineOffset (curr_line.Line, 2);
					Delete (ref curr_line, ref bullet_end);
				}
			}
		}

		public void IncreaseCursorDepth ()
		{
			ChangeCursorDepth (true);
		}

		public void DecreaseCursorDepth ()
		{
			ChangeCursorDepth (false);
		}

		// Increase or decrease the depth of the line at the
		// cursor depending on wheather it is RTL or LTR
		public void ChangeCursorDepthDirectional (bool right)
		{
			Gtk.TextIter start;
			Gtk.TextIter end;

			GetSelectionBounds (out start, out end);

			// If we are moving right then:
			//   RTL => decrease depth
			//   LTR => increase depth
			// We choose to increase or decrease the depth
			// based on the fist line in the selection.
			bool increase = right;
			start.LineOffset = 0;
			DepthNoteTag start_depth = FindDepthTag (start);

			bool rtl_depth = start_depth != null && start_depth.Direction == Pango.Direction.Rtl;
			bool first_char_rtl = start.Char.Length > 0 &&
			                      (Pango.Global.UnicharDirection (start.Char[0])
			                       == Pango.Direction.Rtl);
			Gtk.TextIter next = start;

			if (start_depth != null) {
				next.ForwardChars (2);
			} else {
				// Look for the first non-space character on the line
				// and use that to determine what direction we should go
				next.ForwardSentenceEnd ();
				next.BackwardSentenceStart ();
				first_char_rtl =
				        next.Char.Length > 0 &&
				        (Pango.Global.UnicharDirection (next.Char[0]) == Pango.Direction.Rtl);
			}

			if ((rtl_depth || first_char_rtl) &&
			                ((next.Line == start.Line) && !next.EndsLine ())) {
				increase = !right;
			}

			ChangeCursorDepth(increase);
		}

		void ChangeCursorDepth(bool increase)
		{
			Gtk.TextIter start;
			Gtk.TextIter end;

			GetSelectionBounds (out start, out end);

			Gtk.TextIter curr_line;

			int start_line = start.Line;
			int end_line = end.Line;

			for (int i = start_line; i <= end_line; i++) {
				curr_line = GetIterAtLine(i);
				if (increase)
					IncreaseDepth (ref curr_line);
				else
					DecreaseDepth (ref curr_line);
			}
		}

		// Change the writing direction (ie. RTL or LTR) of a bullet.
		// This makes the bulleted line use the correct indent
		public void ChangeBulletDirection (Gtk.TextIter iter, Pango.Direction direction)
		{
			iter.LineOffset = 0;

			DepthNoteTag tag = FindDepthTag (iter);
			if (tag != null) {
				if (tag.Direction != direction &&
				                direction != Pango.Direction.Neutral) {
					NoteTagTable note_table = TagTable as NoteTagTable;

					// Get the depth tag for the given direction
					Gtk.TextTag new_tag = note_table.GetDepthTag (tag.Depth, direction);

					Gtk.TextIter next = iter;
					next.ForwardChar ();

					// Replace the old depth tag with the new one
					RemoveAllTags (iter, next);
					ApplyTag (new_tag, iter, next);
				}
			}
		}

		public void InsertBullet (ref Gtk.TextIter iter, int depth, Pango.Direction direction)
		{
			NoteTagTable note_table = TagTable as NoteTagTable;

			DepthNoteTag tag = note_table.GetDepthTag (depth, direction);

			string bullet =
			        indent_bullets [depth % indent_bullets.Length] + " ";

			InsertWithTags (ref iter, bullet, tag);
		}

		public void RemoveBullet (ref Gtk.TextIter iter)
		{
			Gtk.TextIter end;
			Gtk.TextIter line_end = iter;

			line_end.ForwardToLineEnd ();

			if (line_end.LineOffset < 2) {
				end = GetIterAtLineOffset (iter.Line, 1);
			} else {
				end = GetIterAtLineOffset (iter.Line, 2);
			}

			// Go back one more character to delete the \n as well
			iter = GetIterAtLine (iter.Line - 1);
			iter.ForwardToLineEnd ();

			Delete(ref iter, ref end);
		}

		public void IncreaseDepth (ref Gtk.TextIter start)
		{
			if (!CanMakeBulletedList())
				return;

			Gtk.TextIter end;

			start = GetIterAtLineOffset (start.Line, 0);

			Gtk.TextIter line_end = GetIterAtLine (start.Line);
			line_end.ForwardToLineEnd ();

			end = start;
			end.ForwardChars (2);

			DepthNoteTag curr_depth = FindDepthTag (start);

			Undoer.FreezeUndo ();
			if (curr_depth == null) {
				// Insert a brand new bullet
				Gtk.TextIter next = start;
				next.ForwardSentenceEnd ();
				next.BackwardSentenceStart ();

				// Insert the bullet using the same direction
				// as the text on the line
				Pango.Direction direction = Pango.Direction.Ltr;
				if (next.Char.Length > 0 && next.Line == start.Line)
					direction = Pango.Global.UnicharDirection (next.Char[0]);

				InsertBullet (ref start, 0, direction);
			} else {
				// Remove the previous indent
				Delete (ref start, ref end);

				// Insert the indent at the new depth
				int nextDepth = curr_depth.Depth + 1;
				InsertBullet (ref start, nextDepth, curr_depth.Direction);
			}
			Undoer.ThawUndo ();

			ChangeTextDepth (this, new ChangeDepthEventArgs (start.Line, true));
		}

		public void DecreaseDepth (ref Gtk.TextIter start)
		{
			if (!CanMakeBulletedList())
				return;

			Gtk.TextIter end;

			start = GetIterAtLineOffset (start.Line, 0);

			Gtk.TextIter line_end = start;
			line_end.ForwardToLineEnd ();

			if (line_end.LineOffset < 2 || start.EndsLine()) {
				end = start;
			} else {
				end = GetIterAtLineOffset (start.Line, 2);
			}

			DepthNoteTag curr_depth = FindDepthTag (start);

			Undoer.FreezeUndo ();
			if (curr_depth != null) {
				// Remove the previous indent
				Delete (ref start, ref end);

				// Insert the indent at the new depth
				int nextDepth = curr_depth.Depth - 1;

				if (nextDepth != -1) {
					InsertBullet (ref start, nextDepth, curr_depth.Direction);
				}
			}
			Undoer.ThawUndo ();

			ChangeTextDepth (this, new ChangeDepthEventArgs (start.Line, false));
		}

		public DepthNoteTag FindDepthTag (Gtk.TextIter iter)
		{
			DepthNoteTag depth_tag = null;

			foreach (Gtk.TextTag tag in iter.Tags) {
				if (NoteTagTable.TagHasDepth (tag)) {
					depth_tag = (DepthNoteTag) tag;
					break;
				}
			}

			return depth_tag;
		}
	}

	public class ChangeDepthEventArgs : EventArgs
	{
		int line;
		bool direction;

		public int Line { get {
			return line;
		}
		                }

		public bool Direction { get {
			return direction;
		}
		                      }

		public ChangeDepthEventArgs (int line, bool direction)
		{
			this.line = line;
			this.direction = direction;
		}
	}

	public delegate void ChangeDepthHandler (object o, ChangeDepthEventArgs args);

	public class InsertBulletEventArgs : EventArgs
	{
		int offset;
		int line;
		int depth;
		Pango.Direction direction;

		public int Offset { get {
			return offset;
		}
		                  }

		public int Line { get {
			return line;
		}
		                }

		public int Depth { get {
			return depth;
		}
		                 }

		public Pango.Direction Direction { get {
			return direction;
		}
		                                 }

		public InsertBulletEventArgs (int offset, int depth, Pango.Direction direction)
		{
			this.offset = offset;
			this.depth = depth;
			this.direction = direction;
		}
	}

	public delegate void NewBulletHandler (object o, InsertBulletEventArgs args);

	public class NoteBufferArchiver
	{
		public static string Serialize (Gtk.TextBuffer buffer)
		{
			return Serialize (buffer, buffer.StartIter, buffer.EndIter);
		}

		public static string Serialize (Gtk.TextBuffer buffer,
		                                Gtk.TextIter   start,
		                                Gtk.TextIter   end)
		{
			StringWriter stream = new StringWriter ();
			XmlTextWriter xml = new XmlTextWriter (stream);

			Serialize (buffer, start, end, xml);

			xml.Close ();
			string serializedBuffer = stream.ToString ();
			
			// We cannot use newer XmlWriter with XmlWriterSettings
			// to control the newline character, because XmlWriter
			// doesn't like elements with ":" in the name.  The
			// point here is to write these files identically on
			// all platforms, to make synchronization work better.
			if (Environment.NewLine != "\n")
				serializedBuffer = serializedBuffer.Replace (Environment.NewLine, "\n");
			return serializedBuffer;
		}

		static void WriteTag (Gtk.TextTag tag, XmlTextWriter xml, bool start)
		{
			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				note_tag.Write (xml, start);
			} else if (NoteTagTable.TagIsSerializable (tag)) {
				if (start)
					xml.WriteStartElement (null, tag.Name, null);
				else
					xml.WriteEndElement ();
			}
		}

		static bool TagEndsHere (Gtk.TextTag tag,
		                         Gtk.TextIter iter,
		                         Gtk.TextIter next_iter)
		{
			return (iter.HasTag (tag) && !next_iter.HasTag (tag)) || next_iter.IsEnd;
		}

		// This is taken almost directly from GAIM.  There must be a
		// better way to do this...
		public static void Serialize (Gtk.TextBuffer buffer,
		                              Gtk.TextIter   start,
		                              Gtk.TextIter   end,
		                              XmlTextWriter  xml)
		{
			Stack<Gtk.TextTag> tag_stack = new Stack<Gtk.TextTag> ();
			Stack<Gtk.TextTag> replay_stack = new Stack<Gtk.TextTag> ();
			Stack<Gtk.TextTag> continue_stack = new Stack<Gtk.TextTag> ();

			Gtk.TextIter iter = start;
			Gtk.TextIter next_iter = start;
			next_iter.ForwardChar ();

			bool line_has_depth = false;
			int prev_depth_line = -1;
			int prev_depth = -1;

			xml.WriteStartElement (null, "note-content", null);
			xml.WriteAttributeString ("version", "0.1");

			// Insert any active tags at start into tag_stack...
			foreach (Gtk.TextTag start_tag in start.Tags) {
				if (!start.TogglesTag (start_tag)) {
					tag_stack.Push (start_tag);
					WriteTag (start_tag, xml, true);
				}
			}

			while (!iter.Equals (end) && iter.Char != null) {
				DepthNoteTag depth_tag = ((NoteBuffer)buffer).FindDepthTag (iter);

				// If we are at a character with a depth tag we are at the
				// start of a bulleted line
				if (depth_tag != null && iter.StartsLine()) {
					line_has_depth = true;

					if (iter.Line == prev_depth_line + 1) {
						// Line part of existing list

						if (depth_tag.Depth == prev_depth) {
							// Line same depth as previous
							// Close previous <list-item>
							xml.WriteEndElement ();

						} else if (depth_tag.Depth > prev_depth) {
							// Line of greater depth
							xml.WriteStartElement (null, "list", null);

							for (int i = prev_depth + 2; i <= depth_tag.Depth; i++) {
								// Start a new nested list
								xml.WriteStartElement (null, "list-item", null);
								xml.WriteStartElement (null, "list", null);
							}
						} else {
							// Line of lesser depth
							// Close previous <list-item>
							// and nested <list>s
							xml.WriteEndElement ();

							for (int i = prev_depth; i > depth_tag.Depth; i--) {
								// Close nested <list>
								xml.WriteEndElement ();
								// Close <list-item>
								xml.WriteEndElement ();
							}
						}
					} else {
						// Start of new list
						xml.WriteStartElement (null, "list", null);
						for (int i = 1; i <= depth_tag.Depth; i++) {
							xml.WriteStartElement (null, "list-item", null);
							xml.WriteStartElement (null, "list", null);
						}
					}

					prev_depth = depth_tag.Depth;

					// Start a new <list-item>
					WriteTag (depth_tag, xml, true);
				}

				// Output any tags that begin at the current position
				foreach (Gtk.TextTag tag in iter.Tags) {
					if (iter.BeginsTag (tag)) {

						if (!(tag is DepthNoteTag) && NoteTagTable.TagIsSerializable(tag)) {
							WriteTag (tag, xml, true);
							tag_stack.Push (tag);
						}
					}
				}

				// Reopen tags that continued across indented lines
				// or into or out of lines with a depth
				while (continue_stack.Count > 0 &&
				                ((depth_tag == null && iter.StartsLine ()) || iter.LineOffset == 1))
				{
					Gtk.TextTag continue_tag = continue_stack.Pop();

					if (!TagEndsHere (continue_tag, iter, next_iter)
					                && iter.HasTag (continue_tag))
					{
						WriteTag (continue_tag, xml, true);
						tag_stack.Push (continue_tag);
					}
				}

				// Hidden character representing an anchor
				if (iter.Char[0] == (char) 0xFFFC) {
					Logger.Info ("Got child anchor!");
					if (iter.ChildAnchor != null) {
						string serialize =
						        (string) iter.ChildAnchor.Data ["serialize"];
						if (serialize != null)
							xml.WriteRaw (serialize);
					}
				// Line Separator character
				} else if (iter.Char == "\u2028") {
					xml.WriteCharEntity ('\u2028');
				} else if (depth_tag == null) {
					xml.WriteString (iter.Char);
				}

				bool end_of_depth_line = line_has_depth && next_iter.EndsLine ();

				bool next_line_has_depth = false;
				if (iter.Line < buffer.LineCount - 1) {
					Gtk.TextIter next_line = buffer.GetIterAtLine(iter.Line+1);
					next_line_has_depth =
					        ((NoteBuffer)buffer).FindDepthTag (next_line) != null;
				}

				bool at_empty_line = iter.EndsLine () && iter.StartsLine ();

				if (end_of_depth_line ||
				                (next_line_has_depth && (next_iter.EndsLine () || at_empty_line)))
				{
					// Close all tags in the tag_stack
					while (tag_stack.Count > 0) {
						Gtk.TextTag existing_tag = tag_stack.Pop ();

						// Any tags which continue across the indented
						// line are added to the continue_stack to be
						// reopened at the start of the next <list-item>
						if (!TagEndsHere (existing_tag, iter, next_iter)) {
							continue_stack.Push (existing_tag);
						}

						WriteTag (existing_tag, xml, false);
					}
				} else {
					foreach (Gtk.TextTag tag in iter.Tags) {
						if (TagEndsHere (tag, iter, next_iter) &&
						                NoteTagTable.TagIsSerializable(tag) && !(tag is DepthNoteTag))
						{
							while (tag_stack.Count > 0) {
								Gtk.TextTag existing_tag = tag_stack.Pop ();

								if (!TagEndsHere (existing_tag, iter, next_iter)) {
									replay_stack.Push (existing_tag);
								}

								WriteTag (existing_tag, xml, false);
							}

							// Replay the replay queue.
							// Restart any tags that
							// overlapped with the ended
							// tag...
							while (replay_stack.Count > 0) {
								Gtk.TextTag replay_tag = replay_stack.Pop ();
								tag_stack.Push (replay_tag);

								WriteTag (replay_tag, xml, true);
							}
						}
					}
				}

				// At the end of the line record that it
				// was the last line encountered with a depth
				if (end_of_depth_line) {
					line_has_depth = false;
					prev_depth_line = iter.Line;
				}

				// If we are at the end of a line with a depth and the
				// next line does not have a depth line close all <list>
				// and <list-item> tags that remain open
				if (end_of_depth_line && !next_line_has_depth) {
					for (int i = prev_depth; i > -1; i--) {
						// Close <list>
						xml.WriteFullEndElement ();
						// Close <list-item>
						xml.WriteFullEndElement ();
					}

					prev_depth = -1;
				}

				iter.ForwardChar ();
				next_iter.ForwardChar ();
			}

			// Empty any trailing tags left in tag_stack..
			while (tag_stack.Count > 0) {
				Gtk.TextTag tail_tag = tag_stack.Pop ();
				WriteTag (tail_tag, xml, false);
			}

			xml.WriteEndElement (); // </note-content>
		}

		class TagStart
		{
			public int         Start;
			public Gtk.TextTag Tag;
		}

		public static void Deserialize (Gtk.TextBuffer buffer, string content)
		{
			Deserialize (buffer, buffer.StartIter, content);
		}

		public static void Deserialize (Gtk.TextBuffer buffer,
		                                Gtk.TextIter   start,
		                                string         content)
		{
			StringReader reader = new StringReader (content);
			XmlTextReader xml = new XmlTextReader (reader);
			xml.Namespaces = false;

			Deserialize (buffer, buffer.StartIter, xml);
		}

		public static void Deserialize (Gtk.TextBuffer buffer,
		                                Gtk.TextIter   start,
		                                XmlTextReader  xml)
		{
			int offset = start.Offset;
			Stack<TagStart> stack = new Stack<TagStart> ();
			TagStart tag_start;

			NoteTagTable note_table = buffer.TagTable as NoteTagTable;

			int curr_depth = -1;

			// A stack of boolean values which mark if a
			// list-item contains content other than another list
			Stack<bool> list_stack = new Stack<bool> ();

			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					if (xml.Name == "note-content")
						break;

					tag_start = new TagStart ();
					tag_start.Start = offset;

					if (note_table != null &&
					                note_table.IsDynamicTagRegistered (xml.Name)) {
						tag_start.Tag =
						        note_table.CreateDynamicTag (xml.Name);
					} else if (xml.Name == "list") {
						curr_depth++;
						break;
					} else if (xml.Name == "list-item") {
						if (curr_depth >= 0) {
							if (xml.GetAttribute ("dir") == "rtl") {
								tag_start.Tag =
								        note_table.GetDepthTag (curr_depth, Pango.Direction.Rtl);
							} else {
								tag_start.Tag =
								        note_table.GetDepthTag (curr_depth, Pango.Direction.Ltr);
							}
							list_stack.Push (false);
						} else {
							Logger.Error("</list> tag mismatch");
						}
					} else {
						tag_start.Tag = buffer.TagTable.Lookup (xml.Name);
					}

					if (tag_start.Tag is NoteTag) {
						((NoteTag) tag_start.Tag).Read (xml, true);
					}

					stack.Push (tag_start);
					break;
				case XmlNodeType.Text:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					Gtk.TextIter insert_at = buffer.GetIterAtOffset (offset);
					buffer.Insert (ref insert_at, xml.Value);

					offset += xml.Value.Length;

					// If we are inside a <list-item> mark off
					// that we have encountered some content
					if (list_stack.Count > 0) {
						list_stack.Pop ();
						list_stack.Push (true);
					}

					break;
				case XmlNodeType.EndElement:
					if (xml.Name == "note-content")
						break;

					if (xml.Name == "list") {
						curr_depth--;
						break;
					}

					tag_start = stack.Pop ();
					if (tag_start.Tag == null)
						break;

					Gtk.TextIter apply_start, apply_end;
					apply_start = buffer.GetIterAtOffset (tag_start.Start);
					apply_end = buffer.GetIterAtOffset (offset);

					if (tag_start.Tag is NoteTag) {
						((NoteTag) tag_start.Tag).Read (xml, false);
					}

					// Insert a bullet if we have reached a closing
					// <list-item> tag, but only if the <list-item>
					// had content.
					DepthNoteTag depth_tag = tag_start.Tag as DepthNoteTag;

					if (depth_tag != null && list_stack.Pop ()) {
						((NoteBuffer) buffer).InsertBullet (ref apply_start,
						                                    depth_tag.Depth,
						                                    depth_tag.Direction);
						buffer.RemoveAllTags (apply_start, apply_start);
						offset += 2;
					} else if (depth_tag == null) {
						buffer.ApplyTag (tag_start.Tag, apply_start, apply_end);
					}

					break;
				default:
					Logger.Warn ("Unhandled element {0}. Value: '{1}'",
					            xml.NodeType,
					            xml.Value);
					break;
				}
			}
		}
	}
}
