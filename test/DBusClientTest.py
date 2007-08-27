import dbus
import dbus.glib
import gtk
import gobject
import os

TOMBOY_DBUS_PATH = "/org/gnome/Tomboy/RemoteControl"
TOMBOY_DBUS_IFACE = "org.gnome.Tomboy"

def freeze():
    # Burn some CPU..
    while True:
        pass

def crash():
   # Dump some core
   os.abort()

class TestApp(object):
    def __init__(self):
        self.bus = dbus.SessionBus()

        tomboy_obj = self.bus.get_object(TOMBOY_DBUS_IFACE, TOMBOY_DBUS_PATH)
        self.tomboy = dbus.Interface(tomboy_obj, "org.gnome.Tomboy.RemoteControl")
        self.tomboy.connect_to_signal("NoteAdded", self.note_added_cb)
        self.tomboy.connect_to_signal("NoteSaved", self.note_saved_cb)
        self.tomboy.connect_to_signal("NoteDeleted", self.note_deleted_cb)

        notif_obj = self.bus.get_object("org.freedesktop.Notifications", "/org/freedesktop/Notifications")
        self.notify = dbus.Interface(notif_obj, "org.freedesktop.Notifications")

        # Uncomment one of these to test crash behaviour when app is connected to tomboy, but not mid signal
        #gobject.idle_add(crash)
        #gobject.idle_add(freeze)

    def note_added_cb(self, uid):
        self.doMessage("Note added", uid)

    def note_saved_cb(self, uid):
        self.doMessage("Note saved", uid)

    def note_deleted_cb(self, uid, title):
        self.doMessage("Note deleted", uid + "\n" + title) 

    def doMessage(self, msg, uid):
        self.notify.Notify("Conduit Devs Rock", 0, "", msg, uid, [], {}, 3000)

        # Uncomment one of these to test crash behaviour when app is connected to tomboy and mid signal
        #crash()
        #freeze()

TestApp()
gtk.main()
