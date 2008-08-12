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
		bool pipeline_set = false;
		static Gdk.Pixbuf icon = null;

		
		static VoiceNote ()
		{
			icon = GuiUtils.GetIcon (System.Reflection.Assembly.GetExecutingAssembly (),
				"voicenote", 22);
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
			
			// Stop if streaming, and delete the voice note
			if (pipeline_set)
				stop_stream ();
			if (has_voice_note)				
				File.Delete (voice_note_path);
		}


		public override void OnNoteOpened ()
		{
			voice_note_path = Note.FilePath + ".ogg";
			has_voice_note = voice_note_exists ();
			//set the icon if there is a voice note
			if (has_voice_note) {
				Window.Icon = icon;
			}
			
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
			
			// This has to be done here and not in initialize,
			//otherwise the button's layout becomes disformed
			Window.Hidden += OnNoteClosed;
			
			button_manager = new InterruptableTimeout ();
			button_manager.Timeout += UpdateButtons;
		}
		

		void OnRecordButtonClicked (object sender, EventArgs args)
		{
			start_record (voice_note_path);
			pipeline_set = true;
			record_button.Sensitive = false;
			play_button.Sensitive = false;
			stop_button.Sensitive = true;

			// If there was not, now there is...
			if (!has_voice_note) {
				has_voice_note = true;
				Window.Icon = icon;
			}
			
			button_manager.Reset (500);
		}


		void OnPlayButtonClicked (object sender, EventArgs args)
		{
			start_play (voice_note_path);	
			pipeline_set = true;
			record_button.Sensitive = false;
			play_button.Sensitive = false;
			stop_button.Sensitive = true;
			button_manager.Reset (500);
		}
		
		void OnStopButtonClicked (object sender, EventArgs args)
		{
			stop_stream ();
		}
		
		void OnNoteClosed (object sender, EventArgs args)
		{
			// Stop streaming before hide the window
			if (pipeline_set)
				stop_stream ();
		}
		
		void UpdateButtons (object sender, EventArgs args)
		{
			int media_state = get_state ();
			switch (media_state) {
			case 0: //Stopped, back to initial state
				record_button.Sensitive = true;
				play_button.Sensitive = true;
				stop_button.Sensitive = false;
				break;
			case 1: //Still streaming, don't change
				button_manager.Reset (500);
				break;
			default: //should not happen!!!
				break;				
			}
		}

		bool voice_note_exists ()
		{
			try{ File.Open (voice_note_path, FileMode.Open); }
			catch (Exception except) { return false; }
			return true;
		}
	}
}
