// This file has been modified from its original project.  The following is a
// copy of the original copyright information:

/***************************************************************************
 *  ActionManager.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections;
using Mono.Unix;

namespace Tomboy
{
	public class ActionManager : IEnumerable
	{
		private Gtk.UIManager ui = new Gtk.UIManager ();
		
		private Gtk.ActionGroup main_window_actions =
			new Gtk.ActionGroup ("MainWindow");
	
		public ActionManager ()
		{
			PopulateActionGroups ();
		}
		
		public void LoadInterface ()
		{
			ui.AddUiFromResource ("UIManagerLayout.xml");
			Gtk.Window.DefaultIconName = "tomboy";
		}
		
		private void PopulateActionGroups ()
		{
			///
			/// Global Actions
			///
			main_window_actions.Add (new Gtk.ActionEntry [] {
				new Gtk.ActionEntry ("FileMenuAction", null,
					Catalog.GetString ("_File"), null, null, null),

				new Gtk.ActionEntry ("NewNoteAction", Gtk.Stock.New,
					Catalog.GetString ("_New"), "<Control>N",
					Catalog.GetString ("Create a new note"), null),
				
				new Gtk.ActionEntry ("OpenNoteAction", String.Empty,
					Catalog.GetString ("_Open..."), "<Control>O",
					Catalog.GetString ("Open the selected note"), null),
				
				new Gtk.ActionEntry ("DeleteNoteAction", Gtk.Stock.Delete,
					Catalog.GetString ("_Delete"), "Delete",
					Catalog.GetString ("Delete the selected note"), null),
				
				new Gtk.ActionEntry ("CloseWindowAction", Gtk.Stock.Close,
					Catalog.GetString ("_Close"), "<Control>W",
					Catalog.GetString ("Close this window"), null),
				
				new Gtk.ActionEntry ("QuitTomboyAction", Gtk.Stock.Quit,
					Catalog.GetString ("_Quit"), "<Control>Q",
					Catalog.GetString ("Quit Tomboy"), null),
				
				new Gtk.ActionEntry ("EditMenuAction", null,
					Catalog.GetString ("_Edit"), null, null, null),
					
				new Gtk.ActionEntry ("ShowPreferencesAction", Gtk.Stock.Preferences,
					Catalog.GetString ("_Preferences"), null,
					Catalog.GetString ("Tomboy Preferences"), null),
				
				new Gtk.ActionEntry ("HelpMenuAction", null,
					Catalog.GetString ("_Help"), null, null, null),
					
				new Gtk.ActionEntry ("ShowHelpAction", Gtk.Stock.Help,
					Catalog.GetString ("_Contents"), "F1",
					Catalog.GetString ("Tomboy Help"), null),
					
				new Gtk.ActionEntry ("ShowAboutAction", Gtk.Stock.About,
					Catalog.GetString ("_About"), null,
					Catalog.GetString ("About Tomboy"), null),
				
				new Gtk.ActionEntry ("TrayIconMenuAction", null,
					Catalog.GetString ("TrayIcon"), null, null, null),
				
				new Gtk.ActionEntry ("TrayNewNoteAction", Gtk.Stock.New,
					Catalog.GetString ("Create _New Note"), null,
					Catalog.GetString ("Create a new note"), null),
					
				new Gtk.ActionEntry ("ShowSearchAllNotesAction", Gtk.Stock.Find,
					Catalog.GetString ("_Search All Notes"), null,
					Catalog.GetString ("Open the Search All Notes window"), null),
				
				new Gtk.ActionEntry ("NoteSynchronizationAction", null,
					Catalog.GetString ("S_ynchronize Notes"), null,
					Catalog.GetString ("Start synchronizing notes"), null)
			});

			main_window_actions.GetAction ("OpenNoteAction").Sensitive = false;
			main_window_actions.GetAction ("DeleteNoteAction").Sensitive = false;
			
			ui.InsertActionGroup (main_window_actions, 0);
		}
		
		public Gtk.Action FindActionByName (string action_name)
		{
			foreach (Gtk.ActionGroup group in ui.ActionGroups) {
				foreach (Gtk.Action action in group.ListActions ()) {
					if (action.Name == action_name)
						return action;
				}
			}
			
			return null;
		}
		
		public Gtk.Action this [string widget_path_or_action_name]
		{
			get {
				Gtk.Action action = FindActionByName (widget_path_or_action_name);
				if (action == null)
					return ui.GetAction (widget_path_or_action_name);
				
				return action;
			}
		}
		
		public Gtk.Widget GetWidget (string widget_path)
		{
			return ui.GetWidget (widget_path);
		}
		
		public void SetActionLabel (string action_name, string label)
		{
			this [action_name].Label = label;
			// FIXME: SyncButtons () ?
		}
		
		public void SetActionIcon (string action_name, string icon)
		{
			this [action_name].StockId = icon;
			// FIXME: SyncButtons () ?
		}
		
		public void UpdateAction (string action_name, string label, string icon)
		{
			Gtk.Action action = this [action_name];
			action.Label = label;
			action.StockId = icon;
			// FIXME: SyncButtons () ?
		}
		
		public IEnumerator GetEnumerator ()
		{
			foreach (Gtk.ActionGroup group in ui.ActionGroups) {
				foreach (Gtk.Action action in group.ListActions ()) {
					yield return action;
				}
			}
		}
		
		public Gtk.UIManager UI
		{
			get { return ui; }
		}
		
		public Gtk.ActionGroup MainWindowActions
		{
			get { return main_window_actions; }
		}
	}
}
