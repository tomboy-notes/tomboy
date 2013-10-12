
using System;
using System.Collections.Generic;

namespace Tomboy
{
	public interface EditAction
	{
		void Undo (Gtk.TextBuffer buffer);
		void Redo (Gtk.TextBuffer buffer);
		void Merge (EditAction action);
		bool CanMerge (EditAction action);
		void Destroy ();
	}

	public class ChopBuffer : Gtk.TextBuffer
	{
		public ChopBuffer (Gtk.TextTagTable table)
: base (table)
		{
		}

		public TextRange AddChop (Gtk.TextIter start_iter, Gtk.TextIter end_iter)
		{
			int chop_start, chop_end;
			Gtk.TextIter current_end = EndIter;

			chop_start = EndIter.Offset;
			InsertRange (ref current_end, start_iter, end_iter);
			chop_end = EndIter.Offset;

			return new TextRange (GetIterAtOffset (chop_start),
			                      GetIterAtOffset (chop_end));
		}
	}

	public abstract class SplitterAction : EditAction
	{
		public struct TagData {
			public int start;
			public int end;
			public Gtk.TextTag tag;
		};
		protected List<TagData> splitTags;
		protected TextRange chop;

		protected SplitterAction ()
		{
			this.splitTags = new List<TagData> ();
		}

		public TextRange Chop
		{
			get {
				return chop;
			}
		}

		public List<TagData> SplitTags
		{
			get {
				return splitTags;
			}
		}

		public void Split (Gtk.TextIter iter,
		                   Gtk.TextBuffer buffer)
		{
			foreach (Gtk.TextTag tag in iter.Tags) {
				NoteTag noteTag = tag as NoteTag;
				if (noteTag != null && !noteTag.CanSplit) {
					Gtk.TextIter start = iter;
					Gtk.TextIter end = iter;

					// We only care about enclosing tags
					if (start.TogglesTag (tag) || end.TogglesTag (tag))
						continue;

					start.BackwardToTagToggle (tag);
					end.ForwardToTagToggle (tag);
					AddSplitTag (start, end, tag);
					buffer.RemoveTag(tag, start, end);
				}
			}
		}

		public void AddSplitTag (Gtk.TextIter start,
		                         Gtk.TextIter end,
		                         Gtk.TextTag tag)
		{
			TagData data = new TagData();
			data.start = start.Offset;
			data.end = end.Offset;
			data.tag = tag;
			splitTags.Add(data);

			/*
			 * The text chop will contain these tags, which means that when
			 * the text is inserted again during redo, it will have the tag.
			 */
			chop.RemoveTag(tag);
		}

		protected int GetSplitOffset ()
		{
			int offset = 0;
			foreach (TagData tag in splitTags) {
				NoteTag noteTag = tag.tag as NoteTag;
				if (noteTag.Image != null) {
					offset++;
				}
			}
			return offset;
		}

		protected void ApplySplitTags (Gtk.TextBuffer buffer)
		{
			foreach (TagData tag in splitTags) {
				int offset = GetSplitOffset ();

				Gtk.TextIter start = buffer.GetIterAtOffset (tag.start - offset);
				Gtk.TextIter end = buffer.GetIterAtOffset (tag.end - offset);
				buffer.ApplyTag(tag.tag, start, end);
			}
		}

		protected void RemoveSplitTags (Gtk.TextBuffer buffer)
		{
			foreach (TagData tag in splitTags) {
				Gtk.TextIter start = buffer.GetIterAtOffset (tag.start);
				Gtk.TextIter end = buffer.GetIterAtOffset (tag.end);
				buffer.RemoveTag(tag.tag, start, end);
			}
		}

		public abstract void Undo (Gtk.TextBuffer buffer);
		public abstract void Redo (Gtk.TextBuffer buffer);
		public abstract void Merge (EditAction action);
		public abstract bool CanMerge (EditAction action);
		public abstract void Destroy ();
	}

	public class InsertAction : SplitterAction
	{
		int index;
		bool is_paste;

		public InsertAction (Gtk.TextIter start,
		                     string text,
		                     int length,
		                     ChopBuffer chop_buf)
		{
			this.index = start.Offset - length;
			// GTKBUG: No way to tell a 1-char paste.
			this.is_paste = length > 1;

			Gtk.TextIter index_iter = start.Buffer.GetIterAtOffset (index);
			this.chop = chop_buf.AddChop (index_iter, start);
		}

		public override void Undo (Gtk.TextBuffer buffer)
		{
			int tag_images = GetSplitOffset ();

			Gtk.TextIter start_iter = buffer.GetIterAtOffset (index - tag_images);
			Gtk.TextIter end_iter = buffer.GetIterAtOffset (index - tag_images + chop.Length);
			buffer.Delete (ref start_iter, ref end_iter);
			buffer.MoveMark (buffer.InsertMark, buffer.GetIterAtOffset (index - tag_images));
			buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (index - tag_images));

			ApplySplitTags (buffer);
		}

		public override void Redo (Gtk.TextBuffer buffer)
		{
			RemoveSplitTags (buffer);

			Gtk.TextIter idx_iter = buffer.GetIterAtOffset (index);
			buffer.InsertRange (ref idx_iter, chop.Start, chop.End);

			buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (index));
			buffer.MoveMark (buffer.InsertMark,
			                 buffer.GetIterAtOffset (index + chop.Length));
		}

		public override void Merge (EditAction action)
		{
			InsertAction insert = (InsertAction) action;

			chop.End = insert.chop.End;

			insert.chop.Destroy ();
		}

		public override bool CanMerge (EditAction action)
		{
			InsertAction insert = action as InsertAction;
			if (insert == null)
				return false;

			// Don't group text pastes
			if (is_paste || insert.is_paste)
				return false;

			// Must meet eachother
			if (insert.index != index + chop.Length)
				return false;

			// Don't group more than one line (inclusive)
			if (chop.Text[0] == '\n')
				return false;

			// Don't group more than one word (exclusive)
			if (insert.chop.Text[0] == ' ' || insert.chop.Text[0] == '\t')
				return false;

			return true;
		}

		public override void Destroy ()
		{
			chop.Erase ();
			chop.Destroy ();
		}
	}

	public class EraseAction : SplitterAction
	{
		int start;
		int end;
		bool is_forward;
		bool is_cut;

		public EraseAction (Gtk.TextIter start_iter,
		                    Gtk.TextIter end_iter,
		                    ChopBuffer chop_buf)
		{
			this.start = start_iter.Offset;
			this.end = end_iter.Offset;
			this.is_cut = end - start > 1;

			Gtk.TextIter insert =
			        start_iter.Buffer.GetIterAtMark (start_iter.Buffer.InsertMark);
			this.is_forward = insert.Offset <= start;

			this.chop = chop_buf.AddChop (start_iter, end_iter);
		}

		public override void Undo (Gtk.TextBuffer buffer)
		{
			int tag_images = GetSplitOffset ();

			Gtk.TextIter start_iter = buffer.GetIterAtOffset (start - tag_images);
			buffer.InsertRange (ref start_iter, chop.Start, chop.End);

			buffer.MoveMark (buffer.InsertMark,
			                 buffer.GetIterAtOffset (is_forward ? start - tag_images
			                                         : end - tag_images));
			buffer.MoveMark (buffer.SelectionBound,
			                 buffer.GetIterAtOffset (is_forward ? end - tag_images
			                                         : start - tag_images));

			ApplySplitTags (buffer);
		}

		public override void Redo (Gtk.TextBuffer buffer)
		{
			RemoveSplitTags (buffer);

			Gtk.TextIter start_iter = buffer.GetIterAtOffset (start);
			Gtk.TextIter end_iter = buffer.GetIterAtOffset (end);
			buffer.Delete (ref start_iter, ref end_iter);
			buffer.MoveMark (buffer.InsertMark, buffer.GetIterAtOffset (start));
			buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (start));
		}

		public override void Merge (EditAction action)
		{
			EraseAction erase = (EraseAction) action;
			if (start == erase.start) {
				end += erase.end - erase.start;
				chop.End = erase.chop.End;

				// Delete the marks, leave the text
				erase.chop.Destroy ();
			} else {
				start = erase.start;

				Gtk.TextIter chop_start = chop.Start;
				chop.Buffer.InsertRange (ref chop_start,
				                         erase.chop.Start,
				                         erase.chop.End);

				// Delete the marks and text
				erase.Destroy ();
			}
		}

		public override bool CanMerge (EditAction action)
		{
			EraseAction erase = action as EraseAction;
			if (erase == null)
				return false;

			// Don't group separate text cuts
			if (is_cut || erase.is_cut)
				return false;

			// Must meet eachother
			if (start != (is_forward ? erase.start : erase.end))
				return false;

			// Don't group deletes with backspaces
			if (is_forward != erase.is_forward)
				return false;

			// Group if something other than text was deleted
			// (e.g. an email image)
			if (chop.Text.Length == 0 || erase.chop.Text.Length == 0)
				return true;

			// Don't group more than one line (inclusive)
			if (chop.Text[0] == '\n')
				return false;

			// Don't group more than one word (exclusive)
			if (erase.chop.Text[0] == ' ' || erase.chop.Text[0] == '\t')
				return false;

			return true;
		}

		public override void Destroy ()
		{
			chop.Erase ();
			chop.Destroy ();
		}
	}

	class TagApplyAction : EditAction
	{
		Gtk.TextTag tag;
		int         start;
		int         end;

		public TagApplyAction (Gtk.TextTag tag, Gtk.TextIter start, Gtk.TextIter end)
		{
			this.tag = tag;
			this.start = start.Offset;
			this.end = end.Offset;
		}

		public void Undo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter start_iter, end_iter;
			start_iter = buffer.GetIterAtOffset (start);
			end_iter = buffer.GetIterAtOffset (end);

			buffer.MoveMark (buffer.SelectionBound, start_iter);
			buffer.RemoveTag (tag, start_iter, end_iter);
			buffer.MoveMark (buffer.InsertMark, end_iter);
		}

		public void Redo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter start_iter, end_iter;
			start_iter = buffer.GetIterAtOffset (start);
			end_iter = buffer.GetIterAtOffset (end);

			buffer.MoveMark (buffer.SelectionBound, start_iter);
			buffer.ApplyTag (tag, start_iter, end_iter);
			buffer.MoveMark (buffer.InsertMark, end_iter);
		}

		public void Merge (EditAction action)
		{
			throw new Exception ("TagApplyActions cannot be merged");
		}

		public bool CanMerge (EditAction action)
		{
			return false;
		}

		public void Destroy ()
		{
		}
	}

	class TagRemoveAction : EditAction
	{
		Gtk.TextTag tag;
		int         start;
		int         end;

		public TagRemoveAction (Gtk.TextTag tag, Gtk.TextIter start, Gtk.TextIter end)
		{
			this.tag = tag;
			this.start = start.Offset;
			this.end = end.Offset;
		}

		public void Undo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter start_iter, end_iter;
			start_iter = buffer.GetIterAtOffset (start);
			end_iter = buffer.GetIterAtOffset (end);

			buffer.MoveMark (buffer.SelectionBound, start_iter);
			buffer.ApplyTag (tag, start_iter, end_iter);
			buffer.MoveMark (buffer.InsertMark, end_iter);
		}

		public void Redo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter start_iter, end_iter;
			start_iter = buffer.GetIterAtOffset (start);
			end_iter = buffer.GetIterAtOffset (end);

			buffer.MoveMark (buffer.SelectionBound, start_iter);
			buffer.RemoveTag (tag, start_iter, end_iter);
			buffer.MoveMark (buffer.InsertMark, end_iter);
		}

		public void Merge (EditAction action)
		{
			throw new Exception ("TagRemoveActions cannot be merged");
		}

		public bool CanMerge (EditAction action)
		{
			return false;
		}

		public void Destroy ()
		{
		}
	}

	class ChangeDepthAction : EditAction
	{
		int line;
		bool direction;

		public ChangeDepthAction (int line, bool direction)
		{
			this.line = line;
			this.direction = direction;
		}

		public void Undo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter iter = buffer.GetIterAtLine (line);

			if (direction) {
				((NoteBuffer) buffer).DecreaseDepth (ref iter);
			} else {
				((NoteBuffer) buffer).IncreaseDepth (ref iter);
			}

			buffer.MoveMark (buffer.InsertMark, iter);
			buffer.MoveMark (buffer.SelectionBound, iter);
		}

		public void Redo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter iter = buffer.GetIterAtLine (line);

			if (direction) {
				((NoteBuffer) buffer).IncreaseDepth (ref iter);
			} else {
				((NoteBuffer) buffer).DecreaseDepth (ref iter);
			}

			buffer.MoveMark (buffer.InsertMark, iter);
			buffer.MoveMark (buffer.SelectionBound, iter);
		}

		public void Merge (EditAction action)
		{
			throw new Exception ("ChangeDepthActions cannot be merged");
		}

		public bool CanMerge (EditAction action)
		{
			return false;
		}

		public void Destroy ()
		{
		}
	}

	class InsertBulletAction : EditAction
	{
		int offset;
		int depth;
		Pango.Direction direction;

		public InsertBulletAction (int offset, int depth, Pango.Direction direction)
		{
			this.offset = offset;
			this.depth = depth;
			this.direction = direction;
		}

		public void Undo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter iter = buffer.GetIterAtOffset (offset);
			iter.ForwardLine ();
			iter = buffer.GetIterAtLine (iter.Line);

			((NoteBuffer) buffer).RemoveBullet (ref iter);

			iter.ForwardToLineEnd ();

			buffer.MoveMark (buffer.InsertMark, iter);
			buffer.MoveMark (buffer.SelectionBound, iter);
		}

		public void Redo (Gtk.TextBuffer buffer)
		{
			Gtk.TextIter iter = buffer.GetIterAtOffset (offset);

			buffer.Insert (ref iter, "\n");

			((NoteBuffer) buffer).InsertBullet (ref iter, depth, direction);

			buffer.MoveMark (buffer.InsertMark, iter);
			buffer.MoveMark (buffer.SelectionBound, iter);
		}

		public void Merge (EditAction action)
		{
			throw new Exception ("InsertBulletActions cannot be merged");
		}

		public bool CanMerge (EditAction action)
		{
			return false;
		}

		public void Destroy ()
		{
		}
	}

	public class UndoManager
	{
		uint frozen_cnt;
		bool try_merge;
		NoteBuffer buffer;
		ChopBuffer chop_buffer;

		Stack<EditAction> undo_stack;
		Stack<EditAction> redo_stack;

		public UndoManager (NoteBuffer buffer)
		{
			frozen_cnt = 0;
			try_merge = false;
			undo_stack = new Stack<EditAction> ();
			redo_stack = new Stack<EditAction> ();

			this.buffer = buffer;
			chop_buffer = new ChopBuffer (buffer.TagTable);

			buffer.InsertTextWithTags += OnInsertText;
			buffer.NewBulletInserted += OnBulletInserted;
			buffer.ChangeTextDepth += OnChangeDepth;
			buffer.DeleteRange += OnDeleteRange; // Before handler
			buffer.TagApplied += OnTagApplied;
			buffer.TagRemoved += OnTagRemoved;
		}

		public bool CanUndo
		{
			get {
				return undo_stack.Count > 0;
			}
		}

		public bool CanRedo
		{
			get {
				return redo_stack.Count > 0;
			}
		}

		public event EventHandler UndoChanged;

		public void Undo ()
		{
			UndoRedo (undo_stack, redo_stack, true /*undo*/);
		}

		public void Redo ()
		{
			UndoRedo (redo_stack, undo_stack, false /*redo*/);
		}

		public void FreezeUndo ()
		{
			++frozen_cnt;
		}

		public void ThawUndo ()
		{
			--frozen_cnt;
		}

		void UndoRedo (Stack<EditAction> pop_from, Stack<EditAction> push_to, bool is_undo)
		{
			if (pop_from.Count > 0) {
				EditAction action = (EditAction) pop_from.Pop ();

				FreezeUndo ();
				if (is_undo)
					action.Undo (buffer);
				else
					action.Redo (buffer);
				ThawUndo ();

				push_to.Push (action);

				// Lock merges until a new undoable event comes in...
				try_merge = false;

				if (pop_from.Count == 0 || push_to.Count == 1)
					if (UndoChanged != null)
						UndoChanged (this, new EventArgs ());
			}
		}

		void ClearActionStack (Stack<EditAction> stack)
		{
			foreach (EditAction action in stack) {
				action.Destroy ();
			}
			stack.Clear ();
		}

		public void ClearUndoHistory ()
		{
			ClearActionStack (undo_stack);
			ClearActionStack (redo_stack);

			if (UndoChanged != null)
				UndoChanged (this, new EventArgs ());
		}

		public void AddUndoAction (EditAction action)
		{
			if (try_merge && undo_stack.Count > 0) {
				EditAction top = undo_stack.Peek ();

				if (top.CanMerge (action)) {
					// Merging object should handle freeing
					// action's resources, if needed.
					top.Merge (action);
					return;
				}
			}

			undo_stack.Push (action);

			// Clear the redo stack
			ClearActionStack (redo_stack);

			// Try to merge new incoming actions...
			try_merge = true;

			// Have undoable actions now
			if (undo_stack.Count == 1) {
				if (UndoChanged != null)
					UndoChanged (this, new EventArgs ());
			}
		}

		// Action-creating event handlers...

		[GLib.ConnectBefore]
		void OnInsertText (object sender, Gtk.InsertTextArgs args)
		{
			if (frozen_cnt == 0) {
				InsertAction action = new InsertAction (args.Pos,
				                                        args.NewText,
				                                        args.NewTextLength,
				                                        chop_buffer);

				/*
				 * If this insert occurs in the middle of any
				 * non-splittable tags, remove them first and
				 * add them to the InsertAction.
				 */
				frozen_cnt++;
				action.Split(args.Pos, buffer);
				frozen_cnt--;

				AddUndoAction (action);
			}
		}

		[GLib.ConnectBefore]
		void OnDeleteRange (object sender, Gtk.DeleteRangeArgs args)
		{
			if (frozen_cnt == 0) {
				EraseAction action = new EraseAction (args.Start,
				                                      args.End,
				                                      chop_buffer);
				/*
				 * Delete works a lot like insert here, except
				 * there are two positions in the buffer that
				 * may need to have their tags removed.
				 */
				frozen_cnt++;
				action.Split (args.Start, buffer);
				action.Split (args.End, buffer);
				frozen_cnt--;

				AddUndoAction (action);
			}
		}

		void OnTagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (frozen_cnt == 0) {
				if (NoteTagTable.TagIsUndoable (args.Tag)) {
					AddUndoAction (new TagApplyAction (args.Tag,
					                                   args.StartChar,
					                                   args.EndChar));
				}
			}
		}

		void OnTagRemoved (object sender, Gtk.TagRemovedArgs args)
		{
			if (frozen_cnt == 0) {
				if (NoteTagTable.TagIsUndoable (args.Tag)) {
					// FIXME: Gtk# bug. StartChar and EndChar are not
					//        mapped, so grab them from the Args iter.
					Gtk.TextIter start, end;
					start = (Gtk.TextIter) args.Args[1];
					end = (Gtk.TextIter) args.Args[2];

					AddUndoAction (new TagRemoveAction (args.Tag, start, end));
				}
			}
		}

		void OnChangeDepth(object sender, ChangeDepthEventArgs args)
		{
			if (frozen_cnt == 0) {
				AddUndoAction (new ChangeDepthAction (args.Line, args.Direction));
			}
		}

		void OnBulletInserted(object sender, InsertBulletEventArgs args)
		{
			if (frozen_cnt == 0) {
				AddUndoAction (new InsertBulletAction (args.Offset, args.Depth, args.Direction));
			}
		}
	}
}
