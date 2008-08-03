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
		Gtk.ToolButton record_button;
		Gtk.ToolButton play_button;
		Gtk.ToolButton stop_button;
		Gtk.SeparatorToolItem separator;
		InterruptableTimeout button_manager;
		String voice_note_path;
		bool has_voice_note;

		
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
		
		[DllImport("libvoicenote")]
		static extern int get_state ();


		public override void Initialize ()
		{
			separator = new Gtk.SeparatorToolItem ();
			record_button = new Gtk.ToolButton (Gtk.Stock.MediaRecord);
			record_button.Clicked += OnRecordButtonClicked;
			play_button = new Gtk.ToolButton (Gtk.Stock.MediaPlay);
			play_button.Clicked += OnPlayButtonClicked;
			stop_button = new Gtk.ToolButton (Gtk.Stock.MediaStop);
			stop_button.Clicked += OnStopButtonClicked;
			initialize ();
		}

		
		public override void Shutdown ()
		{
			record_button.Clicked -= OnRecordButtonClicked;
			play_button.Clicked -= OnPlayButtonClicked;
			stop_button.Clicked -= OnStopButtonClicked;
		}


		public override void OnNoteOpened ()
		{
			voice_note_path = Note.FilePath + ".ogg";
			has_voice_note = voice_note_exists ();
			separator.Show ();
			record_button.Show ();
			play_button.Sensitive = has_voice_note; 
			play_button.Show ();
			stop_button.Sensitive = false;
			stop_button.Show ();
			AddToolItem (separator, -1);
			AddToolItem (record_button, -1);
			AddToolItem (play_button, -1);
			AddToolItem (stop_button, -1);
			
			button_manager = new InterruptableTimeout ();
			button_manager.Timeout += UpdateButtons;
		}
		

		void OnRecordButtonClicked (object sender, EventArgs args)
		{
			start_record (voice_note_path);
			button_manager.Reset (100);
		}


		void OnPlayButtonClicked (object sender, EventArgs args)
		{
			start_play (voice_note_path);
			button_manager.Reset (100);
		}
		
		void OnStopButtonClicked (object sender, EventArgs args)
		{
			stop_stream ();
		}
		
		void UpdateButtons (object sender, EventArgs args)
		{
			int media_state = get_state ();
			switch (media_state) {
			case 0: //Stopped
				record_button.Sensitive = true;
				play_button.Sensitive = true;
				stop_button.Sensitive = false;
				break;
			case 1: //Streaming
				record_button.Sensitive = false;
				play_button.Sensitive = false;
				stop_button.Sensitive = true;
				button_manager.Reset (100);
				break;
			default: //should not happen!!!
				break;				
			}
		}

		bool voice_note_exists ()
		{
			FileStream voice_note_file;
			try{
				voice_note_file = File.Open (voice_note_path, FileMode.Open);
			}
			catch (Exception except) {
				return false;
			}
			return true;
		}
	}
}
