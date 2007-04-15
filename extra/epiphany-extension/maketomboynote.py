#!/usr/bin/env python
#
#   Make Tomboy Note - Epiphany Extension
#   Put in public domain by Sandy Armstrong

import dbus
import gtk
import epiphany

ui_str = """
<ui>
  <menubar name="menubar">
    <menu name="ToolsMenu" action="Tools">
      <separator/>
      <menuitem name="MakeTomboyNote" action="MakeTomboyNote"/>
      <separator/>
    </menu>
  </menubar>
</ui>
"""

# TODO: There is basically no error checking here
def _menu_entry_cb(action, window):
	session_bus = dbus.SessionBus()
	# NOTE: This automatically starts Tomboy, but it makes Epiphany hang while Tomboy starts
	tomboy = session_bus.get_object("org.gnome.Tomboy", "/org/gnome/Tomboy/RemoteControl")

	embed = window.get_active_embed()
	active_url = embed.get_location(True)
	active_title = embed.get_title()
	note_title = active_title[0:50] # Trim after 50 characters

	uri = tomboy.CreateNamedNote(note_title)
	if uri is not  "":
		tomboy.DisplayNote(uri)
		# NOTE: Tomboy bug makes it so you can't set links properly,
		#	but if note is open it will automatically linkify
		tomboy.SetNoteContents(uri, note_title + "\n\n" + active_url)
	else:
		pass #error dialog?

_actions = [('MakeTomboyNote', None, 'Note This in Tomboy!',
	     None, None, _menu_entry_cb)]

def attach_window(window):
	ui_manager = window.get_ui_manager()
	group = gtk.ActionGroup('MakeTomboyNote')
	group.add_actions(_actions, window)
	ui_manager.insert_action_group(group, 0)
	ui_id = ui_manager.add_ui_from_string(ui_str)

	window._make_tomboy_note_data = (group, ui_id)

def detach_window(window):
	group, ui_id = window._make_tomboy_note_data
	del window._make_tomboy_note_data

	ui_manager = window.get_ui_manager()
	ui_manager.remove_ui(ui_id)
	ui_manager.remove_action_group(group)
	ui_manager.ensure_update()
