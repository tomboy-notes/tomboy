
using System;
using Mono.Posix;

using Gnome;
using Gtk;

using Tomboy;

public class PrintPlugin : NotePlugin
{
	Gtk.Widget toolbar_item;

	protected override void Initialize ()
	{
		// Do nothing.
	}

	protected override void Shutdown ()
	{
		if (toolbar_item != null) {
			Window.Toolbar.Remove (toolbar_item);
			toolbar_item = null;
		}
	}

	protected override void OnNoteOpened () {
		toolbar_item = 
			Window.Toolbar.AppendItem (Catalog.GetString ("Print"), 
						   Catalog.GetString ("Print this note"), 
						   null, 
						   new Gtk.Image (Gtk.Stock.Print, 
								  Gtk.IconSize.LargeToolbar),
						   new Gtk.SignalFunc (PrintButtonClicked));
	}

	//
	// Print the Note
	//

	void PrintNote (PrintContext context, PrintJob job)
	{
		FontWeight weight;
		bool italic;
		Font font;
		String base_font = "Sans";
		double small_size = 10;
		double normal_size = 12;
		double large_size = 14;
		double huge_size = 16;
		double size = normal_size;

		double width, height, line_space;
		job.GetPageSize (out width, out height);

		double cur_x = 50;
		double cur_y = height - 100;

		Print.Beginpage (context, Note.Title);
		Print.Moveto (context, cur_x, cur_y);

		for (int char_offset = 0; char_offset < Buffer.CharCount; char_offset++) {
			weight = FontWeight.Regular;
			italic = false;
			size = normal_size;
			font = Font.FindClosestFromWeightSlant (base_font, weight, italic, size);
			line_space = (font.Descender + font.Ascender) * 1.2;
			Print.Setfont (context, font);
			TextIter iter = Buffer.GetIterAtOffset (char_offset);
			TextTag[] tags = iter.Tags;

			for (int i = 0; i < tags.Length; i++) {
				if (tags[i].Weight == Pango.Weight.Bold)
					weight = FontWeight.Bold;
				else if (tags[i].Style == Pango.Style.Italic)
					italic = true;
				else if (tags[i].Scale == Pango.Scale.Small)
					size = small_size;
				else if (tags[i].Scale == Pango.Scale.Medium)
					size = normal_size;
				else if (tags[i].Scale == Pango.Scale.X_Large)
					size = large_size;
				else if (tags[i].Scale == Pango.Scale.XX_Large)
					size = huge_size;

				font = Font.FindClosestFromWeightSlant (base_font, weight, italic, size);
				line_space = (font.Descender + font.Ascender) * 1.2;
				Print.Setfont (context, font);
			}

			if (iter.Char.Equals("\n")) {
				cur_y -= line_space;
				cur_x = 50;
			} else {
				Print.Show (context, iter.Char);
				cur_x += font.GetWidthUtf8 (iter.Char);
			}

			if (cur_x >= width - 50) {
				cur_x = 50;
				cur_y -= line_space;
			}

			Print.Moveto (context, cur_x, cur_y);
		}

		Print.Showpage (context);
		job.Close ();
	}

	//
	// Handle Print Button Click
	//

	void PrintButtonClicked () 
	{
		PrintJob job = new PrintJob (PrintConfig.Default ());
		PrintDialog dialog = new PrintDialog (job, Note.Title, 0);
		int response = dialog.Run ();

		if (response == (int) PrintButtons.Cancel) {
			dialog.Hide ();
			dialog.Dispose ();
			return;
		}

		PrintContext context = job.Context;
		PrintNote (context, job);

		switch (response) {
		case (int) PrintButtons.Print:
			job.Print ();
			break;
		case (int) PrintButtons.Preview:
			new PrintJobPreview (job, Note.Title).Show ();
			break;
		}

		dialog.Hide ();
		dialog.Dispose ();
	}
}
