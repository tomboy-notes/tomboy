using Tomboy;
using Mono.Unix;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Gtk;

namespace Tomboy.VoiceNote
{
	public class VoiceNote : NoteAddin
	{
		Gtk.ToolButton recordButton;
		Gtk.ToolButton playButton;
		Gtk.ToolButton stopButton;
		Gtk.SeparatorToolItem separator;
		String voice_note_path;
		FileStream voice_note_file;
		
		static VoiceNote ()
		{
		}		

		[DllImport("libvoicenote")]
		static extern void initialize ();

		[DllImport("libvoicenote")]
		static extern void start_play (string uri);

		[DllImport("libvoicenote")]
		static extern void start_record (string uri);
		
		[DllImport("libvoicenote")]
		static extern void stop_stream ();


		public override void Initialize ()
		{
			separator = new Gtk.SeparatorToolItem ();
			recordButton = new Gtk.ToolButton (Gtk.Stock.MediaRecord);
			recordButton.Clicked += OnRecordButtonClicked;
			playButton = new Gtk.ToolButton (Gtk.Stock.MediaPlay);
			playButton.Clicked += OnPlayButtonClicked;
			stopButton = new Gtk.ToolButton (Gtk.Stock.MediaStop);
			stopButton.Clicked += OnStopButtonClicked;
			initialize ();
		}

		
		public override void Shutdown ()
		{
			recordButton.Clicked -= OnRecordButtonClicked;
			playButton.Clicked -= OnPlayButtonClicked;
			stopButton.Clicked -= OnStopButtonClicked;
		}


		public override void OnNoteOpened ()
		{
			voice_note_path = Note.FilePath + ".ogg";			
			separator.Show ();
			recordButton.Show ();
			playButton.Show ();
			stopButton.Show ();
			AddToolItem (separator, -1);
			AddToolItem (recordButton, -1);
			AddToolItem (playButton, -1);
			AddToolItem (stopButton, -1);
		}
		

		void OnRecordButtonClicked (object sender, EventArgs args)
		{
			start_record (voice_note_path);
		}


		void OnPlayButtonClicked (object sender, EventArgs args)
		{
			start_play (voice_note_path);
		}
		
		void OnStopButtonClicked (object sender, EventArgs args)
		{
			stop_stream ();
		}


		bool voice_note_exists ()
		{
			try{
				/* Not guilty, I presume */
				voice_note_file = File.Open (voice_note_path, FileMode.Open);
			}
			catch (Exception except) {
				/* Oh, you've disappointed me 
				 * I'll do nothing for you */
				return false;
			}
			return true;
		}
	}
}
