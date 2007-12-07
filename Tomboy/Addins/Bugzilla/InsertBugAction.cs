using System;

namespace Tomboy.Bugzilla
{
	public class InsertBugAction : SplitterAction
	{
		BugzillaLink Tag;
		int Offset;
		string Id;

		public InsertBugAction (Gtk.TextIter start,
		                        string id,
		                        Gtk.TextBuffer buffer,
		                        BugzillaLink tag)
		{
			Tag = tag;
			Id = id;

			Offset = start.Offset;
		}

		public override void Undo (Gtk.TextBuffer buffer)
		{
			// Tag images change the offset by one, but only when deleting.
			Gtk.TextIter start_iter = buffer.GetIterAtOffset (Offset);
			Gtk.TextIter end_iter = buffer.GetIterAtOffset (Offset + chop.Length + 1);
			buffer.Delete (ref start_iter, ref end_iter);
			buffer.MoveMark (buffer.InsertMark, buffer.GetIterAtOffset (Offset));
			buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (Offset));

			Tag.WidgetLocation = null;

			ApplySplitTags (buffer);
		}

		public override void Redo (Gtk.TextBuffer buffer)
		{
			RemoveSplitTags (buffer);

			Gtk.TextIter cursor = buffer.GetIterAtOffset (Offset);

			Gtk.TextTag[] tags = {Tag};
			buffer.InsertWithTags (ref cursor, Id, tags);

			buffer.MoveMark (buffer.SelectionBound, buffer.GetIterAtOffset (Offset));
			buffer.MoveMark (buffer.InsertMark,
			                 buffer.GetIterAtOffset (Offset + chop.Length));

		}

		public override void Merge (EditAction action)
		{
			SplitterAction splitter = action as SplitterAction;
			this.splitTags = splitter.SplitTags;
			this.chop = splitter.Chop;
		}

		/*
		 * The internal listeners will create an InsertAction when the text
		 * is inserted.  Since it's ugly to have the bug insertion appear
		 * to the user as two items in the undo stack, have this item eat
		 * the other one.
		 */
		public override bool CanMerge (EditAction action)
		{
			InsertAction insert = action as InsertAction;
			if (insert == null) {
				return false;
			}

			if (String.Compare(Id, insert.Chop.Text) == 0) {
				return true;
			}

			return false;
		}

		public override void Destroy ()
		{
		}
	}
}
