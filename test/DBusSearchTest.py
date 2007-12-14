#!/usr/bin/python
"""Usage : ./tomboy.py <True|False> <search-term> <search-term> ...  

   For Example:
	./tomboy.py True Hello Res
	./tomboy.py True Hello
	./tomboy.py True hlo


"""

import sys
import dbus
import gobject
import dbus.glib

# Get the D-Bus session bus
bus = dbus.SessionBus()
# Access the Tomboy D-Bus object
obj = bus.get_object("org.gnome.Tomboy", "/org/gnome/Tomboy/RemoteControl")
# Access the Tomboy remote control interface
tomboy = dbus.Interface(obj, "org.gnome.Tomboy.RemoteControl")

def func(a):
	if (len(a)<2):
		print __doc__
	elif (a[1][0].lower() =="t" or a[1][0].lower() == "f"):
		#TODO: Fix passing of case sensitive flag (this will always look True)
		for i in tomboy.SearchNotes(' '.join(a[2:]),a[1]):
			print tomboy.GetNoteTitle(i)


def f(a):
	print ' '.join(a[1:])

if __name__=='__main__':
	sys.exit(func(sys.argv))
