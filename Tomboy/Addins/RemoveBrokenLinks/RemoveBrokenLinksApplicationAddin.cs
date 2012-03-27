// Plugin for removing broken links (ApplicationAddin).
// (c) 2010 Alex Tereschenko <frozenblue@zoho.com>
// LGPL 2.1 or later.

using System;
using Gtk;
using Mono.Unix;
using Tomboy;

namespace Tomboy.RemoveBrokenLinks
{
	/// <summary>
	/// Class for operating on all notes/search results for RemoveBrokenLinks addin
	/// </summary>
	public class RemoveBrokenLinksApplicationAddin : ApplicationAddin
	{
		bool initialized = false;
		static Gtk.ActionGroup action_group;
		static uint rblUi = 0;
				
		public static void OnRemoveBrokenLinksActivated ()
		{
			NoteManager def_note_manager;
			NoteRecentChanges search;
			RemoveBrokenLinksUtils utils = new RemoveBrokenLinksUtils ();
			def_note_manager = Tomboy.DefaultNoteManager;
			search = NoteRecentChanges.GetInstance (def_note_manager);

			foreach (Note note in search.GetFilteredNotes ()) {
				utils.RemoveBrokenLinkTag (note);
				if ((bool) Preferences.Get (Preferences.ENABLE_WIKIWORDS))
					utils.HighlightWikiWords (note);
			}
		}

		public override void Initialize ()
		{
			
			action_group = new Gtk.ActionGroup ("RemoveBrokenLinks");
			action_group.Add (new Gtk.ActionEntry [] {
				new Gtk.ActionEntry ("ToolsMenuAction", null,
				Catalog.GetString ("_Tools"), null, null, null),
				new Gtk.ActionEntry ("RemoveBrokenLinksAction", null,
				Catalog.GetString ("_Remove broken links"), null, null,
				delegate {
					OnRemoveBrokenLinksActivated ();
				})
			});
					
			rblUi = Tomboy.ActionManager.UI.AddUiFromString (@"
			                <ui>
			                <menubar name='MainWindowMenubar'>
			                <placeholder name='MainWindowMenuPlaceholder'>
			                <menu name='ToolsMenu' action='ToolsMenuAction'>
			                <menuitem name='RemoveBrokenLinks' action='RemoveBrokenLinksAction' />
			                </menu>
			                </placeholder>
			                </menubar>
			                </ui>
			                ");
			
			Tomboy.ActionManager.UI.InsertActionGroup (action_group, 0);			
			
			initialized = true;
		}

		public override void Shutdown ()
		{
			Tomboy.ActionManager.UI.RemoveActionGroup (action_group);
			Tomboy.ActionManager.UI.RemoveUi (rblUi);
			initialized = false;
		}

		public override bool Initialized
		{
			get {
				return initialized;
			}
		}		
		
	}
}
