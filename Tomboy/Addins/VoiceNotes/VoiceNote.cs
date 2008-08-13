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
		Gtk.MenuItem delete_item;
		String voice_note_path;
		bool has_voice_note;
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
			separator.Show ();
			
			record_button = new Gtk.ToolButton (Gtk.Stock.MediaRecord);
			record_button.Clicked += OnRecordButtonClicked;
			record_button.Show ();

			play_button = new Gtk.ToolButton (Gtk.Stock.MediaPlay);
			play_button.Clicked += OnPlayButtonClicked;
			play_button.Show ();
			
			stop_button = new Gtk.ToolButton (Gtk.Stock.MediaStop);
			stop_button.Clicked += OnStopButtonClicked;
			stop_button.Show ();

			delete_item = new Gtk.MenuItem ("Delete Voice Note");
			delete_item.Activated += OnDeleteItemActivated;
			delete_item.Show ();
		
			initialize ();
		}

		
		public override void Shutdown ()
		{
			record_button.Clicked -= OnRecordButtonClicked;
			play_button.Clicked -= OnPlayButtonClicked;
			stop_button.Clicked -= OnStopButtonClicked;
			delete_item.Activated -= OnDeleteItemActivated;
			stop_stream ();
			DeleteVoiceNote ();
		}


		public override void OnNoteOpened ()
		{
			voice_note_path = Note.FilePath + ".ogg";
			has_voice_note = VoiceNoteExists ();
			
			if (has_voice_note)
				Window.Icon = icon;
			
			AddToolItem (separator, -1);
			AddToolItem (record_button, -1);
			AddToolItem (play_button, -1);
			AddToolItem (stop_button, -1);
			AddPluginMenuItem (delete_item);			
			
			play_button.Sensitive = has_voice_note; 
			stop_button.Sensitive = false;
			Window.Hidden += OnStopButtonClicked;
			button_manager = new InterruptableTimeout ();
			button_manager.Timeout += UpdateButtons;
		}
		

		void OnRecordButtonClicked (object sender, EventArgs args)
		{
			start_record (voice_note_path);
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
			record_button.Sensitive = false;
			play_button.Sensitive = false;
			stop_button.Sensitive = true;
			button_manager.Reset (500);
		}

		
		void OnStopButtonClicked (object sender, EventArgs args)
		{
			stop_stream ();
		}
		
		void OnDeleteItemActivated (object sender, EventArgs args)
		{
			stop_stream ();
			DeleteVoiceNote ();
			has_voice_note = VoiceNoteExists ();
			play_button.Sensitive = false;
			stop_button.Sensitive = false;
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
		
		void DeleteVoiceNote ()
		{
			if (has_voice_note)
				File.Delete (voice_note_path);
		}

		bool VoiceNoteExists ()
		{
			try{ 
				File.Open (voice_note_path, FileMode.Open); 
			}
			catch (Exception except) { 
				return false; 
			}
			return true;
		}
	}
}
