//#if WIN32

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Tomboy.Windows.Interop;
using IShellLink = Tomboy.Windows.Interop.IShellLinkW;
using Mono.Unix;

namespace Tomboy
{
	public class JumpListManager
	{
		private static readonly string NoteIcon = "note.ico";
		private static readonly string NewNoteIcon = "new_note.ico";
		private static readonly string SearchIcon = "search.ico";

		private static readonly string tomboy_path = System.Reflection.Assembly.GetExecutingAssembly ().Location;
		private static readonly string icons_path = Defines.DATADIR;

		public static void CreateJumpList ()
		{
			CreateJumpList (null);
		}

		public static void CreateJumpList (NoteManager note_manager)
		{
			ICustomDestinationList custom_destinationd_list = null;
			IObjectArray removed_objects = null;

			try {
				custom_destinationd_list =
				    (ICustomDestinationList) Activator.CreateInstance (Type.GetTypeFromCLSID (CLSID.DestinationList));

				uint slots;
				Guid riid = CLSID.IObjectArray;

				Logger.Debug ("Windows Taskbar: Begin jump list");
				custom_destinationd_list.BeginList (out slots, ref riid, out removed_objects);

				try {
					AddUserTasks (custom_destinationd_list);
				} catch (UnauthorizedAccessException uae) {
					Logger.Warn ("Access denied adding user tasks to jump list: {0}\n{1}",
						uae.Message, uae.StackTrace);
				}
				try {
					AddRecentNotes (custom_destinationd_list, note_manager, slots);
				} catch (UnauthorizedAccessException uae) {
					Logger.Warn ("Access denied adding recent notes to jump list: {0}\n{1}",
						uae.Message, uae.StackTrace);
				}

				Logger.Debug ("Windows Taskbar: Commit jump list");
				custom_destinationd_list.CommitList ();
			} catch (Exception e) {
				Logger.Error ("Error creating jump list: {0}\n{1}", e.Message, e.StackTrace);
				if (custom_destinationd_list != null) {
					custom_destinationd_list.AbortList ();
				}
			} finally {
				if (removed_objects != null) {
					Marshal.FinalReleaseComObject (removed_objects);
					removed_objects = null;
				}

				if (custom_destinationd_list != null) {
					Marshal.FinalReleaseComObject (custom_destinationd_list);
					custom_destinationd_list = null;
				}
			}
		}

		public static void DeleteJumpList ()
		{
			try {
				ICustomDestinationList custom_destinationd_list =
				    (ICustomDestinationList) Activator.CreateInstance (Type.GetTypeFromCLSID (CLSID.DestinationList));

				Logger.Debug ("Windows Taskbar: Remove jump list");
				custom_destinationd_list.DeleteList (null);

				Marshal.FinalReleaseComObject (custom_destinationd_list);
				custom_destinationd_list = null;
			} catch (Exception e) {
				Logger.Error ("Error removing jump list: {0}\n{1}", e.Message, e.StackTrace);
			}
		}

		private static void AddUserTasks (ICustomDestinationList custom_destinationd_list)
		{
			IObjectCollection object_collection =
			    (IObjectCollection) Activator.CreateInstance (Type.GetTypeFromCLSID (CLSID.EnumerableObjectCollection));

			IShellLink search_notes = CreateShellLink (Catalog.GetString ("Search All Notes"), tomboy_path, "--search",
			                                           System.IO.Path.Combine (icons_path, SearchIcon),  -1);
			if (search_notes != null)
				object_collection.AddObject (search_notes);

			//IShellLink new_notebook = CreateShellLink("New Notebook", topmboy_path, "--new-notebook",
			//    icons_path, (int)TomboyIcons.NewNotebook);
			//if (new_notebook != null)
			//    object_collection.AddObject(new_notebook);

			IShellLink new_note = CreateShellLink (Catalog.GetString ("Create New Note"), tomboy_path, "--new-note",
			                                       System.IO.Path.Combine (icons_path, NewNoteIcon), -1);
			if (new_note != null)
				object_collection.AddObject (new_note);

			custom_destinationd_list.AddUserTasks ((IObjectArray) object_collection);

			Marshal.ReleaseComObject (object_collection);
			object_collection = null;
		}

		private static void AddRecentNotes (ICustomDestinationList custom_destinationd_list, NoteManager note_manager, uint slots)
		{
			IObjectCollection object_collection =
			    (IObjectCollection) Activator.CreateInstance (Type.GetTypeFromCLSID (CLSID.EnumerableObjectCollection));

			// Prevent template notes from appearing in the menu
			Tag template_tag = TagManager.GetOrCreateSystemTag (TagManager.TemplateNoteSystemTag);

			uint index = 0;
			foreach (Note note in note_manager.Notes) {
				if (note.IsSpecial)
					continue;

				// Skip template notes
				if (note.ContainsTag (template_tag))
					continue;

				string note_title = note.Title;
				if (note.IsNew) {
					note_title = String.Format (Catalog.GetString ("{0} (new)"), note_title);
				}

				IShellLink note_link = CreateShellLink (note_title, tomboy_path, "--open-note " + note.Uri,
				                                        System.IO.Path.Combine (icons_path, NoteIcon), -1);
				if (note_link != null)
					object_collection.AddObject (note_link);

				if (++index == slots - 1)
					break;
			}

			// Add Start Here note
			Note start_note = note_manager.FindByUri (NoteManager.StartNoteUri);
			if (start_note != null) {
				IShellLink start_note_link = CreateShellLink (start_note.Title, tomboy_path, "--open-note " +
				                                              NoteManager.StartNoteUri,
				                                              System.IO.Path.Combine (icons_path, NoteIcon), -1);
				if (start_note_link != null)
					object_collection.AddObject (start_note_link);
			}

			custom_destinationd_list.AppendCategory (Catalog.GetString ("Recent Notes"), (IObjectArray) object_collection);

			Marshal.ReleaseComObject (object_collection);
			object_collection = null;
		}

		private static IShellLink CreateShellLink (string title, string path)
		{
			return CreateShellLink (title, path, string.Empty, string.Empty, 0);
		}

		private static IShellLink CreateShellLink (string title, string path, string arguments)
		{
			return CreateShellLink (title, path, arguments, string.Empty, 0);
		}

		private static IShellLink CreateShellLink (string title, string path, string arguments, string icon_path, int icon_pos)
		{
			try {
				IShellLink shell_link = (IShellLink) Activator.CreateInstance (Type.GetTypeFromCLSID (CLSID.ShellLink));
				shell_link.SetPath (path);

				if (!string.IsNullOrEmpty (arguments))
					shell_link.SetArguments (arguments);

				if (!string.IsNullOrEmpty (icon_path))
					shell_link.SetIconLocation (icon_path, icon_pos);

				IntPtr pps;
				Guid ipsiid = CLSID.IPropertyStore;

				Marshal.QueryInterface (Marshal.GetIUnknownForObject (shell_link), ref ipsiid, out pps);
				IPropertyStore property_store = (IPropertyStore) Marshal.GetTypedObjectForIUnknown (pps, typeof (IPropertyStore));

				PROPVARIANT propvar = new PROPVARIANT ();
				propvar.SetString (title);

				// PKEY_Title
				PROPERTYKEY PKEY_Title = new PROPERTYKEY ();
				PKEY_Title.fmtid = new Guid ("F29F85E0-4FF9-1068-AB91-08002B27B3D9");
				PKEY_Title.pid = 2;

				property_store.SetValue (ref PKEY_Title, ref propvar);
				property_store.Commit ();

				IntPtr psl;
				Guid psliid = CLSID.IShellLinkW;

				Marshal.QueryInterface (Marshal.GetIUnknownForObject (shell_link), ref psliid, out psl);
				IShellLink link = (IShellLink) Marshal.GetTypedObjectForIUnknown (psl, typeof (IShellLink));

				propvar.Clear ();

				Marshal.ReleaseComObject (property_store);
				property_store = null;

				Marshal.ReleaseComObject (shell_link);
				shell_link = null;

				return link;
			} catch (COMException e) {
				Logger.Error ("Error createing shell link: {0}\n{1}", e.Message, e.StackTrace);
			}

			return null;
		}
	}
}

//#endif // WIN32
