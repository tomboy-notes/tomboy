
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
		char[] indent_bullets = {'\u2022', '\u2218', '\u2023'};

		// GODDAMN Gtk.TextBuffer. I hate you. Hate Hate Hate.
		struct ImageInsertData
		{
			public bool adding;
			public Gtk.TextBuffer buffer;
			public Gtk.TextMark position;
			public Gdk.Pixbuf image;
			public NoteTag tag;
		};
		ArrayList imageQueue;
		uint imageQueueTimeout;
		// HATE.

		// list of Gtk.TextTags to apply on insert
		ArrayList active_tags;

		public NoteBuffer (Gtk.TextTagTable tags) 
			: base (tags)
		{
			active_tags = new ArrayList ();
			undo_manager = new UndoManager (this);

			InsertText += TextInsertedEvent;
			MarkSet += MarkSetEvent;

			tags.TagChanged += OnTagChanged;

			imageQueue = new ArrayList();
			imageQueueTimeout = 0;
		}

		// Signal that text has been inserted, and any active tags have
		// been applied to the text.  This allows undo to pull any
		// active tags from the inserted text.
		public event Gtk.InsertTextHandler InsertTextWithTags;

		public event ChangeDepthHandler ChangeTextDepth;
		
		public event NewBulletHandler NewBulletInserted;

		public void ToggleActiveTag (string tag_name)
		{
			Logger.Log ("ToggleTag called for '{0}'", tag_name);

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
			Logger.Log ("SetTag called for '{0}'", tag_name);

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
			Logger.Log ("RemoveTag called for '{0}'", tag_name);

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
		
		// Returns true if the cursor is inside of a bulleted list
		public bool IsBulletedListActive ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;
			
			DepthNoteTag depth = FindDepthTag (ref iter);
			
			if (depth == null)
				return false;
			
			return true;
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
		
		public bool AddNewline()
		{			
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;
			
			DepthNoteTag prev_depth = FindDepthTag (ref iter);

			// If the previous line has a bullet point on it we add a bullet
			// to the new line, unless the previous line was blank (apart from
			// the bullet), in which case we clear the bullet/indent from the
			// previous line.
			if (prev_depth != null) {
				iter.ForwardChar ();

				Gtk.TextIter insert = GetIterAtMark (insert_mark);
				
				// See if the line was left contentless and remove the bullet
				// if so.
				if (iter.EndsLine () || insert.LineOffset < 3 ) {
					Gtk.TextIter start = GetIterAtLine (iter.Line);
					Gtk.TextIter end = start;
					end.ForwardToLineEnd ();

					if (end.LineOffset < 1) {
						end = start;
					} else {
						end = GetIterAtLineOffset (iter.Line, 1);
					}
					
					Delete (ref start, ref end);
					
					iter = GetIterAtMark (insert_mark);
					Insert (ref iter, "\n");					
				} else {
					Undoer.FreezeUndo ();
					iter = GetIterAtMark (insert_mark);
					int offset = iter.Offset;
					Insert (ref iter, "\n");
				
					iter = GetIterAtMark (insert_mark);
					Gtk.TextIter start = GetIterAtLine (iter.Line);

					InsertBullet (ref start, prev_depth.Depth);
					Undoer.ThawUndo ();
					
					NewBulletInserted (this,
						new InsertBulletEventArgs (offset, prev_depth.Depth));

					Insert (ref start, " ");
				}
				
				return true;
			}			
			// Replace lines starting with '*' or '-' with bullets
			else if (iter.Char.Equals ("*") || iter.Char.Equals ("-")) {		
				Gtk.TextIter start = GetIterAtLineOffset (iter.Line, 0);
				Gtk.TextIter end = GetIterAtLineOffset (iter.Line, 1);
				
				// Remove the '*' character and any leading white space
				Delete (ref start, ref end);

				if (end.EndsLine ()) {
					IncreaseDepth (ref start);
				} else {
					IncreaseDepth (ref start);

					if (start.Char != " ")
						Insert (ref start, " ");
					
					iter = GetIterAtMark (insert_mark);
					int offset = iter.Offset;
					Insert (ref iter, "\n");
					
					iter = GetIterAtMark (insert_mark);
					iter.LineOffset = 0;

					Undoer.FreezeUndo ();
					InsertBullet (ref iter, 0);
					Undoer.ThawUndo ();

					NewBulletInserted (this,
						new InsertBulletEventArgs (offset, 0));

					Insert (ref iter, " ");
				}			
				
				return true;
			}
			
			return false;
		}
		
		// Returns true if the depth of the line was increased
		public bool AddTab ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter iter = GetIterAtMark (insert_mark);
			iter.LineOffset = 0;		
			
			DepthNoteTag depth = FindDepthTag (ref iter);
			
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
			
			DepthNoteTag depth = FindDepthTag (ref iter);
			
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
				end.ForwardChars (2);
				
				DepthNoteTag depth = FindDepthTag (ref next);
				
				if (depth != null) {
					Delete (ref start, ref end);
					return true;
				}
			} else {
				Gtk.TextIter next = start;
				
				if (next.LineOffset != 0)
					next.ForwardChar ();
				
				DepthNoteTag depth = FindDepthTag (ref start);
				DepthNoteTag nextDepth = FindDepthTag (ref next);
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
			
			DepthNoteTag depth = FindDepthTag (ref start);
			
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
					prev.BackwardChars (2);
				
				DepthNoteTag prev_depth = FindDepthTag (ref prev);
				if (depth != null || prev_depth != null) {
					DecreaseDepth (ref start);
					return true;
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
			
			bool selection = GetSelectionBounds (out start, out end);;			

			if (selection) {
				AugmentSelection (ref start, ref end);
			} else {
				// If the cursor is at the start of a bulleted line
				// move it so it is after the bullet.
				if (start.LineOffset == 0 && FindDepthTag (ref start) != null) {
					start.ForwardChar ();
					if (start.Char == " ")
						start.ForwardChar ();
					SelectRange (start, start);
				}
			}
		}

		// Change the selection on the buffer taking into account any
		// bullets that are in or near the seletion
		void AugmentSelection (ref Gtk.TextIter start, ref Gtk.TextIter end)
		{
			DepthNoteTag end_depth = FindDepthTag (ref end);

			// Check if the End is right before start of bullet
			if (end_depth != null) {
				
				end.LineOffset = 1;
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

		void ImageSwap (NoteTag tag, 
				Gtk.TextIter start, 
				Gtk.TextIter end,
				bool adding) 
		{
			if (tag.Image == null)
				return;

			Gtk.TextIter prev = start;
			prev.BackwardChar ();

			if ((adding == true  && tag.Image != prev.Pixbuf) ||
			    (adding == false && tag.Image == prev.Pixbuf)) {
				ImageInsertData data = new ImageInsertData ();
				data.buffer = start.Buffer;
				data.tag = tag;
				data.image = tag.Image;
				data.adding = adding;

				if (adding) {
					data.position = start.Buffer.CreateMark (null, start, true);
				} else {
					data.position = tag.ImageLocation;
				}

				imageQueue.Add(data);

				if (imageQueueTimeout == 0) {
					imageQueueTimeout = GLib.Idle.Add(RunImageQueue);
				}
			}
		}

		public bool RunImageQueue ()
		{
			foreach (ImageInsertData data in imageQueue) {
				NoteBuffer buffer = data.buffer as NoteBuffer;
				Gtk.TextIter iter = buffer.GetIterAtMark (data.position);

				buffer.Undoer.FreezeUndo();

				if (data.adding && data.tag.ImageLocation == null) {
					buffer.InsertPixbuf (ref iter, data.image);
					data.tag.ImageLocation = data.position;
				} else if (!data.adding && data.tag.ImageLocation != null) {
					Gtk.TextIter end = iter;
					end.ForwardChar();
					buffer.Delete (ref iter, ref end);
					buffer.DeleteMark (data.position);
					data.tag.ImageLocation = null;
				}

				buffer.Undoer.ThawUndo ();
			}

			imageQueue.Clear ();

			imageQueueTimeout = 0;
			return false;
		}

		void OnTagChanged (object sender, Gtk.TagChangedArgs args)
		{
			NoteTag note_tag = args.Tag as NoteTag;
			if (note_tag != null) {
				TextTagEnumerator enumerator = 
					new TextTagEnumerator (this, note_tag);
				foreach (TextRange range in enumerator) {
					ImageSwap (note_tag, range.Start, range.End, true);
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
				ImageSwap (note_tag, start, end, true);
			}
		}

		protected override void OnTagRemoved (Gtk.TextTag tag,
		                                      Gtk.TextIter start,
		                                      Gtk.TextIter end)
		{
			NoteTag note_tag = tag as NoteTag;
			if (note_tag != null) {
				ImageSwap (note_tag, start, end, false);
			}

			base.OnTagRemoved (tag, start, end);
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

		public void IncreaseCursorDepth ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter insert_iter = GetIterAtMark (insert_mark);

			insert_iter.LineOffset = 0;

			if (insert_iter.Char != " " && FindDepthTag (ref insert_iter) == null)
				Insert (ref insert_iter, " ");			

			IncreaseDepth (ref insert_iter);
		}
		
		public void DecreaseCursorDepth ()
		{
			Gtk.TextMark insert_mark = InsertMark;
			Gtk.TextIter insert_iter = GetIterAtMark (insert_mark);
			
			DecreaseDepth (ref insert_iter);
		}
		
		public void InsertBullet (ref Gtk.TextIter iter, int depth)
		{
			NoteTagTable note_table = TagTable as NoteTagTable;

			DepthNoteTag tag = note_table.GetDepthTag (depth);

			string bullet =
				indent_bullets [depth % indent_bullets.Length] + String.Empty;
			
			InsertWithTags (ref iter, bullet, tag);
		}
		
		public void RemoveBullet (ref Gtk.TextIter iter)
		{
			Gtk.TextIter end;
			Gtk.TextIter line_end = iter;

			line_end.ForwardToLineEnd ();

			if (line_end.LineOffset < 1) {
				end = GetIterAtLineOffset (iter.Line, 0);
			} else {
				end = GetIterAtLineOffset (iter.Line, 1);
			}

			// Go back one more character to delete the \n as well
			iter = GetIterAtLine (iter.Line - 1);
			iter.ForwardToLineEnd ();
			
			Delete(ref iter, ref end);
		}	
		
		public void IncreaseDepth (ref Gtk.TextIter start)
		{
			Gtk.TextIter end;

			start = GetIterAtLineOffset (start.Line, 0);
			
			Gtk.TextIter line_end = GetIterAtLine (start.Line);
			line_end.ForwardToLineEnd ();

			end = start;
			end.ForwardChars (1);

			DepthNoteTag curr_depth = FindDepthTag (ref start);

			Undoer.FreezeUndo ();
			if (curr_depth == null) {
				// Insert a brand new bullet
				InsertBullet (ref start, 0);
			} else {
				// Remove the previous indent
				Delete (ref start, ref end);
				
				// Insert the indent at the new depth
				int nextDepth = curr_depth.Depth + 1;
				InsertBullet (ref start, nextDepth);
			}
			Undoer.ThawUndo ();			

			ChangeTextDepth (this, new ChangeDepthEventArgs (start.Line, true));
		}
				
		public void DecreaseDepth (ref Gtk.TextIter start)
		{
			Gtk.TextIter end;

			start = GetIterAtLineOffset (start.Line, 0);

			Gtk.TextIter line_end = start;
			line_end.ForwardToLineEnd ();

			if (line_end.LineOffset < 1) {
				end = start;
			} else {
				end = GetIterAtLineOffset (start.Line, 1);
			}

			DepthNoteTag curr_depth = FindDepthTag (ref start);

			Undoer.FreezeUndo ();
			if (curr_depth != null) {
				// Remove the previous indent
				Delete (ref start, ref end);
				
				// Insert the indent at the new depth
				int nextDepth = curr_depth.Depth - 1;

				if (nextDepth != -1) {
					InsertBullet (ref start, nextDepth);
				}
			}
			Undoer.ThawUndo ();

			ChangeTextDepth (this, new ChangeDepthEventArgs (start.Line, false));
		}
				
		public DepthNoteTag FindDepthTag (ref Gtk.TextIter iter)
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

		public int Line	{ get {return line; } }

		public bool Direction { get {return direction; } }
		
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

		public int Offset { get {return offset; } }

		public int Line	{ get {return line; } }

		public int Depth { get {return depth; } }
		
		public InsertBulletEventArgs (int offset, int depth)
		{
			this.offset = offset;
			this.depth = depth;
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
			Stack tag_stack = new Stack ();
			Stack replay_stack = new Stack ();
			Stack continue_stack = new Stack ();

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

			while (!iter.Equal (end) && iter.Char != null) {
				bool iter_has_depth_tag = false;
				bool new_list = false;

				Queue tag_queue = new Queue();
				DepthNoteTag depth_tag = null;

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (iter.BeginsTag (tag)) {

						depth_tag = tag as DepthNoteTag;

						if (depth_tag != null) {
							iter_has_depth_tag = true;
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
								for (int i = 0; i <= depth_tag.Depth; i++) {
									xml.WriteStartElement (null, "list", null);
								}
								new_list = true;
							}
							
							prev_depth = depth_tag.Depth;
						} else {
							tag_stack.Push (tag);
						}
						
						// Put the tags into a queue so they will be
						// written after the start of a <list-item> tag
						if (!(tag is DepthNoteTag))
							tag_queue.Enqueue(tag);
					}
				}

				// Start a new <list-item> if we are on a line with a depth
				if (depth_tag != null) {
					WriteTag (depth_tag, xml, true);
				}

				// Write all the queued tags after the 
				// <list-item> (if there was one)
				while (tag_queue.Count > 0) {
					Gtk.TextTag tag = (Gtk.TextTag) tag_queue.Dequeue();
					WriteTag (tag, xml, true);
				}	

				// Close all <list> and <list-item> tags that remain open
				// upon reaching the start of a depth-less line.
				if (prev_depth != -1 && !line_has_depth && 
					iter.StartsLine() && prev_depth_line == iter.Line - 1) 
				{
					for (int i = prev_depth; i > -1; i--) {
						// Close <list>
						xml.WriteEndElement ();
						// Close <list-item>
						xml.WriteEndElement ();
					}
							
					prev_depth = -1;			
				}

				// Reopen tags that continue across indented lines 
				// or continue into or out of lines with a depth
				while (continue_stack.Count > 0 && iter.StartsLine() &&
						(prev_depth_line == iter.Line - 1 || new_list))
				{
					Gtk.TextTag continue_tag = (Gtk.TextTag) continue_stack.Pop();
					WriteTag (continue_tag, xml, true);
				}				

				// Hidden character representing an anchor
				if (iter.Char[0] == (char) 0xFFFC) {
					Logger.Log ("Got child anchor!!!");
					if (iter.ChildAnchor != null) {
						string serialize = 
						    (string) iter.ChildAnchor.Data ["serialize"];
						if (serialize != null)
							xml.WriteRaw (serialize);
					}
				} else if (!iter_has_depth_tag) {
					xml.WriteString (iter.Char);
				}

				bool end_of_depth_line = line_has_depth && next_iter.EndsLine ();

				// See if the next line has a depth
				bool next_line_has_depth = false;
				if (iter.Line < buffer.LineCount) {
					Gtk.TextIter next_line = buffer.GetIterAtLine(iter.Line+1);
					next_line_has_depth =
						((NoteBuffer)buffer).FindDepthTag(ref next_line) != null;
				}
				
				// Find all the tags that continue across, 
				// into or outof indented lines
				if (end_of_depth_line || (next_line_has_depth && next_iter.EndsLine())) {
					foreach (Gtk.TextTag tag in iter.Tags) {
						if (!TagEndsHere (tag, 
									  iter, 
									  next_iter))
						{
							WriteTag (tag, xml, false);
							continue_stack.Push (tag);
						}
					}
				}

				foreach (Gtk.TextTag tag in iter.Tags) {
					if (TagEndsHere (tag, iter, next_iter)) {
						// Unwind until the ended tag.
						// Put any intermediate tags
						// into a replay queue...
							
						if (!(tag is DepthNoteTag)) {
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
				}

				// At the end of the line record that it
				// was the last line encountered with a depth
				if (end_of_depth_line) {
					line_has_depth = false;
					prev_depth_line = iter.Line;
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
			
			int curr_depth = -1;
			
			// A stack of boolean values which mark if a
			// list-item contains content other than another list
			Stack list_stack = new Stack();

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
							tag_start.Tag = note_table.GetDepthTag (curr_depth);
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

					tag_start = (TagStart) stack.Pop ();
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
					
					if (depth_tag != null && (bool) list_stack.Pop ()) {
						((NoteBuffer) buffer).InsertBullet (ref apply_start, depth_tag.Depth);
						offset += 1;
					} else if(depth_tag == null) {
						buffer.ApplyTag (tag_start.Tag, apply_start, apply_end);
					}

					break;
				default:
					Logger.Log ("Unhandled element {0}. Value: '{1}'",
						    xml.NodeType,
						    xml.Value);
					break;
				}
			}
		}
	}
}
