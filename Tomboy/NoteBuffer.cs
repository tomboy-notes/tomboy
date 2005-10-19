
using System;
using System.Collections;
using System.IO;
using System.Xml;

namespace Tomboy
{
	// Provides the concept of active tags, which are applied on text
	// insert.  Exposes the UndoManager for this buffer.  And adds a
	// InsertTextWithTags event which is fired after inserted text has all
	// the active tags applied.
	public class NoteBuffer : Gtk.TextBuffer 
	{
		UndoManager undo_manager;
		Gtk.TextTagTable tag_table;

		// list of Gtk.TextTags to apply on insert
		ArrayList active_tags;

		public NoteBuffer (Gtk.TextTagTable tags) 
			: base (tags)
		{
			tag_table = tags;
			active_tags = new ArrayList ();
			undo_manager = new UndoManager (this);

			InsertText += TextInsertedEvent;
			MarkSet += MarkSetEvent;

			tags.TagChanged += OnTagChanged;
		}

		// Signal that text has been inserted, and any active tags have
		// been applied to the text.  This allows undo to pull any
		// active tags from the inserted text.
		public event Gtk.InsertTextHandler InsertTextWithTags;

		public void ToggleActiveTag (string tag_name)
		{
			Console.WriteLine ("ToggleTag called for '{0}'", tag_name);

			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter select_start, select_end;

			if (GetSelectionBounds (out select_start, out select_end)) {
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
			Console.WriteLine ("SetTag called for '{0}'", tag_name);

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
			Console.WriteLine ("RemoveTag called for '{0}'", tag_name);

			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter select_start, select_end;

			if (GetSelectionBounds (out select_start, out select_end)) {
				RemoveTag (tag, select_start, select_end);
			} else {
				active_tags.Remove (tag);
			}
		}

		public bool IsActiveTag (string tag_name)
		{
			Gtk.TextTag tag = TagTable.Lookup (tag_name);
			Gtk.TextIter iter, select_end;

			if (GetSelectionBounds (out iter, out select_end))
				return iter.BeginsTag (tag) || iter.HasTag (tag);
			else
				return active_tags.Contains (tag);
		}

		// Apply active_tags to inserted text
		void TextInsertedEvent (object sender, Gtk.InsertTextArgs args)
		{
			// Only apply active tags when typing, not on paste.
			if (args.Text.Length == 1) {
				Gtk.TextIter insert_start = args.Pos;
				insert_start.BackwardChars (args.Text.Length);

				Undoer.FreezeUndo ();
				foreach (Gtk.TextTag tag in insert_start.Tags) {
					RemoveTag (tag, insert_start, args.Pos);
				}

				foreach (Gtk.TextTag tag in active_tags) {
					ApplyTag (tag, insert_start, args.Pos);
				}
				Undoer.ThawUndo ();
			}

			if (InsertTextWithTags != null)
				InsertTextWithTags (sender, args);
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

		void ImageSwap (NoteTag tag, 
				Gtk.TextIter start, 
				Gtk.TextIter end) 
		{
			if (tag.Image != null &&
			    tag.Image != start.Pixbuf) {
				Console.WriteLine ("ImageSwap: tag='{0}' {1}:'{3}'-{2}:'{4}'", 
						   tag.ElementName,
						   start.Offset,
						   end.Offset,
						   start.Char,
						   end.Char);

				start.Buffer.InsertPixbuf (start, tag.Image);
			}
		}

		void OnTagChanged (object sender, Gtk.TagChangedArgs args)
		{
			NoteTag note_tag = args.Tag as NoteTag;
			if (note_tag != null) {
				TextTagEnumerator enumerator = 
					new TextTagEnumerator (this, note_tag);
				foreach (TextRange range in enumerator) {
					ImageSwap (note_tag, range.Start, range.End);
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
				ImageSwap (note_tag, start, end);
			}
		}

		public UndoManager Undoer
		{
			get { return undo_manager; }
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
	}

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
			return stream.ToString ();
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

		static bool TagEndsHere (Gtk.TextTag tag, Gtk.TextIter iter, Gtk.TextIter next_iter)
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
			Stack tag_stack = new Stack ();
			Stack replay_stack = new Stack ();

			Gtk.TextIter iter = start;
			Gtk.TextIter next_iter = start;
			next_iter.ForwardChar ();

			xml.WriteStartElement (null, "note-content", null);
			xml.WriteAttributeString ("version", "0.1");

			// Insert any active tags at start into tag_stack...
			foreach (Gtk.TextTag start_tag in start.Tags) {
				if (!start.TogglesTag (start_tag)) {
					tag_stack.Push (start_tag);
					WriteTag (start_tag, xml, true);
				}
			}

			while (!iter.Equal (end) && iter.Char != null) {
				foreach (Gtk.TextTag tag in iter.Tags) {
					if (iter.BeginsTag (tag)) {
						tag_stack.Push (tag);
						WriteTag (tag, xml, true);
					}
				}

				// Hidden character representing an anchor
				if (iter.Char[0] == (char) 0xFFFC) {
					Console.WriteLine ("Got child anchor!!!");
					if (iter.ChildAnchor != null) {
						string serialize = 
						    (string) iter.ChildAnchor.Data ["serialize"];
						if (serialize != null)
							xml.WriteRaw (serialize);
					}
				} else 
					xml.WriteString (iter.Char);

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (TagEndsHere (tag, iter, next_iter)) {
						// Unwind until the ended tag.
						// Put any intermediate tags
						// into a replay queue...
						while (tag_stack.Count > 0) {
							Gtk.TextTag existing_tag = 
								(Gtk.TextTag) tag_stack.Pop ();
							//if (existing_tag == tag)
							//	break;

							if (!TagEndsHere (existing_tag, 
									  iter, 
									  next_iter))
								replay_stack.Push (existing_tag);

							WriteTag (existing_tag, xml, false);
						}

						//xml.WriteEndElement ();

						// Replay the replay queue.
						// Restart any tags that
						// overlapped with the ended
						// tag...
						while (replay_stack.Count > 0) {
							Gtk.TextTag replay_tag = 
								(Gtk.TextTag) replay_stack.Pop ();
							tag_stack.Push (replay_tag);

							WriteTag (replay_tag, xml, true);
						}
					}
				}

				iter.ForwardChar ();
				next_iter.ForwardChar ();
			}

			// Empty any trailing tags left in tag_stack..
			while (tag_stack.Count > 0) {
				Gtk.TextTag tail_tag = (Gtk.TextTag) tag_stack.Pop ();
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
			Stack stack = new Stack ();
			TagStart tag_start;

			NoteTagTable note_table = buffer.TagTable as NoteTagTable;

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
					} else {
						tag_start.Tag = buffer.TagTable.Lookup (xml.Name);
					}

					if (tag_start.Tag is NoteTag) {
						((NoteTag) tag_start.Tag).Read (xml, true);
						if (((NoteTag) tag_start.Tag).Image != null) {
							offset++;
						}
					}

					stack.Push (tag_start);
					break;
				case XmlNodeType.Text:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					Gtk.TextIter insert_at = buffer.GetIterAtOffset (offset);
					buffer.Insert (insert_at, xml.Value);

					offset += xml.Value.Length;
					break;
				case XmlNodeType.EndElement:
					if (xml.Name == "note-content")
						break;

					tag_start = (TagStart) stack.Pop ();

					Gtk.TextIter apply_start, apply_end;
					apply_start = buffer.GetIterAtOffset (tag_start.Start);
					apply_end = buffer.GetIterAtOffset (offset);

					if (tag_start.Tag is NoteTag) {
						((NoteTag) tag_start.Tag).Read (xml, false);
					}

					buffer.ApplyTag (tag_start.Tag, apply_start, apply_end);
					break;
				default:
					Console.WriteLine ("Unhandled element {0}. Value: '{1}'",
							   xml.NodeType,
							   xml.Value);
					break;
				}
			}
		}
	}
}
