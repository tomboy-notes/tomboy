
using System;
using System.Collections;

namespace Tomboy 
{
	public class UndoManager 
	{
		NoteBuffer buffer;

		Stack undo_stack;
		Stack redo_stack;

		int frozen_count;

		abstract class Action 
		{
			public bool CanMerge;
			public UndoManager Manager;
			public NoteBuffer Buffer;

			public Action (UndoManager manager, NoteBuffer buffer)
			{
				this.Manager = manager;
				this.Buffer = buffer;
			}

			public abstract void Undo ();
			public abstract void Redo ();
			public abstract bool Merge (Action action);
		}

		class InsertAction : Action
		{
			public int    Position;
			public string Text;
			public string MarkupText;

			public InsertAction (UndoManager manager, NoteBuffer buffer)
				: base (manager, buffer)
			{
			}

			public override void Undo ()
			{
				Manager.DeleteText (Position, Position + Text.Length);
				Manager.PlaceCursor (Position);
			}

			public override void Redo ()
			{
				Manager.PlaceCursor (Position);
				Manager.InsertText (Position, Text);
				//Gtk.TextIter pos = Buffer.GetIterAtOffset (Position);
				//NoteBufferArchiver.Deserialize (Buffer, pos, MarkupText);
			}

			public override bool Merge (Action action)
			{
				InsertAction next = (InsertAction) action;

				if (next.Position != Position + Text.Length ||
				    (!next.Text.StartsWith (" ") && 
				     !next.Text.StartsWith ("\t") &&
				     (Text.EndsWith (" ") || Text.EndsWith ("\t")))) {
					CanMerge = false;
					return false;
				}

				Text += next.Text;
				return true;
			}
		}

		class DeleteAction : Action
		{
			public int    Start;
			public int    End;
			public bool   IsForward;
			public string Text;
			public string MarkupText;

			public DeleteAction (UndoManager manager, NoteBuffer buffer)
				: base (manager, buffer)
			{
			}

			public override void Undo ()
			{
				Manager.InsertText (Start, Text);
				//Gtk.TextIter pos = Buffer.GetIterAtOffset (Start);
				//NoteBufferArchiver.Deserialize (Buffer, pos, MarkupText);

				if (IsForward) 
					Manager.PlaceCursor (Start);
				else
					Manager.PlaceCursor (End);
			}

			public override void Redo ()
			{
				Manager.DeleteText (Start, End);
				Manager.PlaceCursor (Start);
			}

			public override bool Merge (Action action)
			{
				DeleteAction next = (DeleteAction) action;

				if (IsForward != next.IsForward ||
				    (Start != next.Start && Start != next.End)) {
					CanMerge = false;
					return false;
				}

				if (Start == next.Start) {
					if (!next.Text.StartsWith (" ") && 
					    !next.Text.StartsWith ("\t") &&
					    (Text.EndsWith (" ") || Text.EndsWith ("\t"))) {
						CanMerge = false;
						return false;
					}

					Text += next.Text;
					End += next.End - next.Start;
				} else {
					if (!next.Text.StartsWith (" ") &&
					    !next.Text.StartsWith ("\t") &&
					    (Text.StartsWith (" ") || Text.StartsWith ("\t"))) {
						CanMerge = false;
						return false;
					}

					Text = next.Text + Text;
					Start = next.Start;
				}

				return true;
			}
		}

		class ApplyTag : Action
		{
			public Gtk.TextTag Tag;
			public int         Start;
			public int         End;

			public ApplyTag (UndoManager manager, NoteBuffer buffer)
				: base (manager, buffer)
			{
			}

			public override void Undo ()
			{
				Gtk.TextIter start, end;
				start = Buffer.GetIterAtOffset (Start);
				end = Buffer.GetIterAtOffset (End);
				
				Buffer.MoveMark (Buffer.SelectionBound, start);
				Buffer.RemoveTag (Tag, start, end);
				Manager.PlaceCursor (End);
			}

			public override void Redo ()
			{
				Gtk.TextIter start, end;
				start = Buffer.GetIterAtOffset (Start);
				end = Buffer.GetIterAtOffset (End);
				
				Buffer.MoveMark (Buffer.SelectionBound, start);
				Buffer.ApplyTag (Tag, start, end);
				Manager.PlaceCursor (End);
			}

			public override bool Merge (Action action)
			{
				return false;
			}
		}

		class RemoveTag : Action
		{
			public Gtk.TextTag Tag;
			public int         Start;
			public int         End;

			public RemoveTag (UndoManager manager, NoteBuffer buffer)
				: base (manager, buffer)
			{
			}

			public override void Undo ()
			{
				Gtk.TextIter start, end;
				start = Buffer.GetIterAtOffset (Start);
				end = Buffer.GetIterAtOffset (End);
				
				Buffer.MoveMark (Buffer.SelectionBound, start);
				Buffer.ApplyTag (Tag, start, end);
				Manager.PlaceCursor (End);
			}

			public override void Redo ()
			{
				Gtk.TextIter start, end;
				start = Buffer.GetIterAtOffset (Start);
				end = Buffer.GetIterAtOffset (End);
				
				Buffer.MoveMark (Buffer.SelectionBound, start);
				Buffer.RemoveTag (Tag, start, end);
				Manager.PlaceCursor (End);
			}

			public override bool Merge (Action action)
			{
				return false;
			}
		}

		public UndoManager (NoteBuffer buffer)
		{
			this.buffer = buffer;

			undo_stack = new Stack ();
			redo_stack = new Stack ();
			
			buffer.InsertTextWithTags += TextInserted;
			buffer.DeleteRange += RangeDeleted;
			buffer.TagApplied += TagApplied;
			buffer.TagRemoved += TagRemoved;
		}

		void TextInserted (object sender, Gtk.InsertTextArgs args)
		{
			if (frozen_count > 0)
				return;

			InsertAction action = new InsertAction (this, buffer);

			action.Position = args.Pos.Offset - args.Text.Length;
			action.Text = args.Text;

			//Gtk.TextIter start = buffer.GetIterAtOffset (action.Position);
			//action.MarkupText = NoteBufferArchiver.Serialize (buffer, start, args.Pos);

			//Console.WriteLine ("TextInserted '{0}' at {1}", action.MarkupText, args.Pos.Offset);

			// If a paste or a newline, don't merge
			action.CanMerge = !(args.Text.Length > 1 || args.Text == "\n");
			
			AddAction (action);
		}

		[GLib.ConnectBefore]
		void RangeDeleted (object sender, Gtk.DeleteRangeArgs args)
		{
			if (frozen_count > 0)
				return;

			DeleteAction action = new DeleteAction (this, buffer);

			action.Start = args.Start.Offset;
			action.End = args.End.Offset;
			action.Text = buffer.GetSlice (args.Start, args.End, true);

			//action.MarkupText = NoteBufferArchiver.Serialize (buffer, args.Start, args.End);

			Console.WriteLine ("RangeDeleted '{0}' at start:{1}, end:{2}", 
					   action.Text, action.Start, action.End);

			Gtk.TextIter cursor = buffer.GetIterAtMark (buffer.InsertMark);

			// Figure out if the user used the Delete or the Backspace key
			action.IsForward = (cursor.Offset < action.Start);

			Console.WriteLine ("RangeDeleted cursor-at:{0}, is-forward:{1}", 
					   cursor.Offset, action.IsForward);
			
			// If a selection delete or a newline, don't merge
			action.CanMerge = !((action.End - action.Start) > 1 || action.Text == "\n");

			AddAction (action);
		}

		void TagApplied (object sender, Gtk.TagAppliedArgs args)
		{
			if (frozen_count > 0)
				return;

			if (!NoteTagTable.TagIsUndoable (args.Tag))
				return;

			// Only care about explicit tag applies, not those
			// inserted along with text.
			if (args.EndChar.Offset - args.StartChar.Offset == 1)
				return;

			ApplyTag apply = new ApplyTag (this, buffer);
			apply.Tag = args.Tag;
			apply.Start = args.StartChar.Offset;
			apply.End = args.EndChar.Offset;
			apply.CanMerge = false;

			AddAction (apply);
		}

		void TagRemoved (object sender, Gtk.TagRemovedArgs args)
		{
			if (frozen_count > 0)
				return;

			if (!NoteTagTable.TagIsUndoable (args.Tag))
				return;

			// FIXME: Gtk# bug. StartChar and EndChar are note
			//        mapped, so grab them from the Args iter.
			Gtk.TextIter start, end;
			start = (Gtk.TextIter) args.Args[1];
			end = (Gtk.TextIter) args.Args[2];

			RemoveTag remove = new RemoveTag (this, buffer);
			remove.Tag = args.Tag;
			remove.Start = start.Offset;
			remove.End = end.Offset;
			remove.CanMerge = false;

			AddAction (remove);
		}

		void AddAction (Action action)
		{
			if (redo_stack.Count > 0)
				redo_stack.Clear ();

			if (!MergeAction (action)) {
				undo_stack.Push (action);
				if (undo_stack.Count == 1 && UndoChanged != null)
					UndoChanged (this, null);
			}
		}

		bool MergeAction (Action action)
		{
			if (undo_stack.Count == 0)
				return false;

			Action last_action = (Action) undo_stack.Peek ();

			if (!last_action.CanMerge)
				return false;

			if (!action.CanMerge || action.GetType() != last_action.GetType()) {
				last_action.CanMerge = false;
				return false;
			}

			return last_action.Merge (action);
		}

		public bool CanUndo 
		{
			get { return undo_stack.Count != 0; }
		}

		public bool CanRedo 
		{
			get { return redo_stack.Count != 0; }
		}

		public event EventHandler UndoChanged;

		public void Undo ()
		{
			Action action = (Action) undo_stack.Pop ();
			if (action == null) {
				Console.WriteLine ("Nothing to Undo!");
				return;
			}

			FreezeUndo ();

			// Perform the undo
			action.Undo ();

			ThawUndoInternal ();

			redo_stack.Push (action);

			if (redo_stack.Count == 1 || undo_stack.Count == 0) {
				if (UndoChanged != null)
					UndoChanged (this, null);
			}
		}

		public void Redo ()
		{
			Action action = (Action) redo_stack.Pop ();
			if (action == null) {
				Console.WriteLine ("Nothing to Redo!");
				return;
			}

			FreezeUndo ();

			// Perform the undo
			action.Redo ();

			ThawUndoInternal ();

			undo_stack.Push (action);

			if (redo_stack.Count == 0 || undo_stack.Count == 1) {
				if (UndoChanged != null)
					UndoChanged (this, null);
			}
		}

		public void FreezeUndo ()
		{
			++frozen_count;
		}

		void ThawUndoInternal ()
		{
			--frozen_count;
		}

		public void ThawUndo ()
		{
			ThawUndoInternal ();

			if (frozen_count == 0) {
				bool fire_changed = false;

				if (undo_stack.Count != 0 || redo_stack.Count != 0)
					fire_changed = true;

				redo_stack.Clear ();
				undo_stack.Clear ();

				if (fire_changed && UndoChanged != null)
					UndoChanged (this, null);
			}
		}

		//
		// Some utility functions...
		//

		void PlaceCursor (int position) 
		{
			Console.WriteLine ("PlaceCursor position:{0}", position);

			Gtk.TextIter iter = buffer.GetIterAtOffset (position);
			buffer.MoveMark (buffer.InsertMark, iter);
		}

		void InsertText (int position, string text)
		{
			Gtk.TextIter iter;

			Console.WriteLine ("InsertText position:{0}, text:'{1}'", position, text);

			iter = buffer.GetIterAtOffset (position);
			buffer.MoveMark (buffer.SelectionBound, iter);

			iter = buffer.GetIterAtOffset (position);
			buffer.Insert (iter, text);
		}

		void DeleteText (int start, int end)
		{
			Console.WriteLine ("DeleteText start:{0}, end:{1}", start, end);

			Gtk.TextIter start_iter = buffer.GetIterAtOffset (start);
			Gtk.TextIter end_iter;

			if (end < 0)
				end_iter = buffer.EndIter;
			else
				end_iter = buffer.GetIterAtOffset (end);
			
			buffer.Delete (start_iter, end_iter);
		}
	}
}
