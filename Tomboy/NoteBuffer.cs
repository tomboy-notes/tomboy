
using System;
using System.Collections;
using System.IO;
using System.Xml;

namespace Tomboy
{
	public class NoteTagTable : Gtk.TextTagTable
	{
		static NoteTagTable instance;

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
			InitCommonTags ();
		}

		public NoteTagTable (IntPtr ptr)
			: base (ptr)
		{
			Console.WriteLine ("NoteTagTable Native ptr instantiation");
			Console.WriteLine ((new System.Diagnostics.StackTrace ()).ToString ());
		}
		
		void InitCommonTags () 
		{
			Gtk.TextTag tag;

			// Font stylings

			// This is just an empty tag we can use to get default
			// attributes.
			tag = new Gtk.TextTag ("normal");
			Add (tag);

			tag = new Gtk.TextTag ("centered");
			tag.Justification = Gtk.Justification.Center;
			Add (tag);

			tag = new Gtk.TextTag ("bold");
			tag.Weight = Pango.Weight.Bold;
			Add (tag);

			tag = new Gtk.TextTag ("italic");
			tag.Style = Pango.Style.Italic;
			Add (tag);

			tag = new Gtk.TextTag ("strikethrough");
			tag.Strikethrough = true;
			Add (tag);

			tag = new Gtk.TextTag ("highlight");
 			tag.Background = "yellow";
			Add (tag);

			tag = new Gtk.TextTag ("find-match");
			tag.Background = "green";
			tag.Data ["avoid-save"] = true;
			Add (tag);

			tag = new Gtk.TextTag ("note-title");
			tag.Underline = Pango.Underline.Single;
			tag.Foreground = "red";
			tag.Scale = Pango.Scale.XX_Large;
			Add (tag);

			tag = new Gtk.TextTag ("related-to");
			tag.Scale = Pango.Scale.Small;
			tag.LeftMargin = 40;
			tag.Editable = false;
			Add (tag);

			// Font sizes

			tag = new Gtk.TextTag ("size:huge");
			tag.Scale = Pango.Scale.XX_Large;
			Add (tag);

			tag = new Gtk.TextTag ("size:large");
			tag.Scale = Pango.Scale.X_Large;
			Add (tag);

			tag = new Gtk.TextTag ("size:normal");
			tag.Scale = Pango.Scale.Medium;
			Add (tag);

			tag = new Gtk.TextTag ("size:small");
			tag.Scale = Pango.Scale.Small;
			Add (tag);

			// Font coloring

			/*
			tag = new Gtk.TextTag ("color:red");
			tag.Foreground = "red";
			Add (tag);

			tag = new Gtk.TextTag ("color:blue");
			tag.Foreground = "blue";
			Add (tag);

			tag = new Gtk.TextTag ("color:green");
			tag.Foreground = "green";
			Add (tag);
			*/

			// Lists

			tag = new Gtk.TextTag ("list:bullet");
			Add (tag);

			tag = new Gtk.TextTag ("list:numbered");
			Add (tag);

			// Underlining

			/*
			tag = new Gtk.TextTag ("underline:single");
			tag.Underline = Pango.Underline.Single;
			Add (tag);

			tag = new Gtk.TextTag ("underline:double");
			tag.Underline = Pango.Underline.Double;
			Add (tag);
			*/

			// Links

			tag = new Gtk.TextTag ("link:broken");
			tag.Underline = Pango.Underline.Single;
			tag.Foreground = "darkgrey";
			Add (tag);

			tag = new Gtk.TextTag ("link:internal");
			tag.Underline = Pango.Underline.Single;
			tag.Foreground = "red";
			Add (tag);

			tag = new Gtk.TextTag ("link:url");
			tag.Underline = Pango.Underline.Single;
			tag.Foreground = "blue";
			Add (tag);
		}

		public static bool TagIsIgnored (Gtk.TextTag tag)
		{
			// FIXME: tag.Data["avoid-save"] isn't being returned,
			// so we have to match explicitly on name for now.

			return (tag.Name == null ||
				tag.Name == "find-match" || 
				tag.Name == "gtkspell-misspelled" ||
				tag.Name == "note-title" ||
				tag.Data ["avoid-save"] != null);
		}

		public static bool TagIsGrowable (Gtk.TextTag tag)
		{
			if (tag.Name != null &&
			    (tag.Name.StartsWith ("link:") || 
			     tag.Name == "find-match" || 
			     tag.Name == "gtkspell-misspelled" ||
			     tag.Name == "note-title"))
				return false;

			if (tag.Data ["growable"] != null)
				return (bool) tag.Data ["growable"];

			return true;
		}

		public static bool TagIsUndoable (Gtk.TextTag tag)
		{
			if (tag.Name != null &&
			    (tag.Name.StartsWith ("link:") || 
			     tag.Name == "find-match" ||
			     tag.Name == "gtkspell-misspelled" ||
			     tag.Name == "note-title"))
				return false;
			if (tag.Data ["undoable"] != null)
				return (bool) tag.Data ["undoable"];
			return true;
		}
	}

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
			Gtk.TextIter insert_start = args.Pos;
			insert_start.BackwardChars (args.Text.Length);

			foreach (Gtk.TextTag tag in insert_start.Tags) {
				RemoveTag (tag, insert_start, args.Pos);
			}

			foreach (Gtk.TextTag tag in active_tags) {
				ApplyTag (tag, insert_start, args.Pos);
			}

			if (InsertTextWithTags != null)
				InsertTextWithTags (sender, args);
		}

		// Clear active tags, and add any tags present in the cursor
		// position
		void MarkSetEvent (object sender, Gtk.MarkSetArgs args)
		{
			if (args.Mark != InsertMark)
				return;

			active_tags.Clear ();

			Gtk.TextIter iter = GetIterAtMark (args.Mark);
			Gtk.TextTag [] tags;

			if (iter.IsEnd)
				tags = iter.GetToggledTags (false);
			else
				tags = iter.Tags;

			foreach (Gtk.TextTag tag in tags) {
				if (NoteTagTable.TagIsGrowable (tag))
					active_tags.Add (tag);
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
			
				if (GetSelectionBounds (out select_start, out select_end))
					return GetText (select_start, select_end, false);
				else
					return null;	
			}
		}

		// Retrieve the contiguous block of text surrounds cursor.  This
		// only breaks for whitespace.
		public string GetCurrentBlock (Gtk.TextIter     iter, 
					       out Gtk.TextIter start, 
					       out Gtk.TextIter end)
		{
			Gtk.TextIter temp;
			start = end = iter;

			temp = start;
			while (true) {
				if (!temp.BackwardChar() || 
				    Char.IsWhiteSpace (temp.Char [0]))
					break;
				start = temp;
			}

			temp = end;
			while (true) {
				if (temp.IsEnd ||
				    Char.IsWhiteSpace (temp.Char [0]) || 
				    !temp.ForwardChar())
					break;
				end = temp;
			}

			if (start.Equal (end))
				return null;

			string word_block = start.GetText (end);
			//Console.WriteLine ("Got word '{0}' at point", start.GetText (end));

			return word_block;
		}

		public string GetCurrentBlock (out Gtk.TextIter start, 
					       out Gtk.TextIter end)
		{
			Gtk.TextIter cursor = GetIterAtMark (InsertMark);

			return GetCurrentBlock (cursor, out start, out end);
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
				if (NoteTagTable.TagIsIgnored (start_tag))
					continue;

				if (!start.TogglesTag (start_tag)) {
					tag_stack.Push (start_tag);
					xml.WriteStartElement (null, start_tag.Name, null);
				}
			}

			while (!iter.Equal (end) && iter.Char != null) {
				foreach (Gtk.TextTag tag in iter.Tags) {
					if (NoteTagTable.TagIsIgnored (tag))
						continue;

					if (iter.BeginsTag (tag)) {
						tag_stack.Push (tag);
						xml.WriteStartElement (null, tag.Name, null);
					}
				}

				// Hidden character representing an anchor
				if (iter.Char[0] == (char) 0xFFFC) {
					Console.WriteLine ("Got child anchor!!!");
					if (iter.ChildAnchor != null) {
						string serialize = (string) iter.ChildAnchor.Data ["serialize"];
						if (serialize != null)
							xml.WriteRaw (serialize);
					}
				} else 
					xml.WriteString (iter.Char);

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (NoteTagTable.TagIsIgnored (tag))
						continue;

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

							xml.WriteEndElement ();
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

							xml.WriteStartElement (null, 
									       replay_tag.Name, 
									       null);
						}
					}
				}

				iter.ForwardChar ();
				next_iter.ForwardChar ();
			}

			// Empty any trailing tags left in tag_stack..
			while (tag_stack.Count > 0) {
				Gtk.TextTag trailinging_tag = (Gtk.TextTag) tag_stack.Pop ();
				xml.WriteEndElement ();
			}

			xml.WriteEndElement (); // </note-content>
		}

		static bool TagEndsHere (Gtk.TextTag tag, Gtk.TextIter iter, Gtk.TextIter next_iter)
		{
			return (iter.HasTag (tag) && !next_iter.HasTag (tag)) || next_iter.IsEnd;
		}

		class TagStart 
		{
			public int    Start;
			public string TagName;
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
			bool finished = false;
			int offset = start.Offset;
			Stack stack = new Stack ();
			TagStart tag_start;

			while (!finished && xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					if (xml.Name == "note-content")
						break;

					tag_start = new TagStart ();
					tag_start.Start = offset;
					tag_start.TagName = xml.Name;

					stack.Push (tag_start);
					break;
				case XmlNodeType.Text:
				case XmlNodeType.Whitespace:
					Gtk.TextIter insert_at = buffer.GetIterAtOffset (offset);
					buffer.Insert (insert_at, xml.Value);

					offset += xml.Value.Length;
					break;
				case XmlNodeType.EndElement:
					if (xml.Name == "note-content") {
						//finished = true;
						break;
					}

					tag_start = (TagStart) stack.Pop ();

					Gtk.TextIter apply_start, apply_end;
					apply_start = buffer.GetIterAtOffset (tag_start.Start);
					apply_end = buffer.GetIterAtOffset (offset);

					buffer.ApplyTag (tag_start.TagName, apply_start, apply_end);
					break;
				}
			}
		}
	}
}
