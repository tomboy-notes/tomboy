#!/usr/bin/env python

#
#  Note Creation Stress testing
#
#  Based on example by Ryan Paul
#

import sys, dbus, gobject, dbus.glib, time, random

try:
	num_notes = int(sys.argv[1])
except:
	num_notes = 10

bus = dbus.SessionBus()
obj = bus.get_object("org.gnome.Tomboy", "/org/gnome/Tomboy/RemoteControl")
tomboy = dbus.Interface(obj, "org.gnome.Tomboy.RemoteControl")

# Word notes
fd = open('/usr/share/dict/words')
words = []

for i in fd.readlines():
	words.append(i.split()[0])
num_words = len(words)

random.seed()
def get_random_word():
	while True:
		# dbus likes utf8... force it
		try:
			word = unicode ( words[random.randrange(0, num_words)], "utf-8")
			return word
		except:
			pass

# Create lots of notes
for i in range(0,num_notes-1):
	title = get_random_word()
	text = get_random_word()

	start = time.time()

	new_note = tomboy.CreateNamedNote(title)
	tomboy.SetNoteContents(new_note, title + "\n\n" + text)

	end = time.time()

	print "%s,%f" % (i, end - start)



"""

# benchmarks on t42p:

note number, total creation time, startup time, last note creation time

100 notes: 3.67
200 notes: 7.867
300 notes: 9.33, ~3 second startup
400 notes: 22.179
800 notes: 1m36s, startup ~6 second startup, last note took 0.23 seconds (ouch)
1600 notes: 10m57s

sqlite backend

wow, once I get to around 200 notes, my disk goes crazy!!! what's going on there? and then this script dies...

100 notes: 3.27, ~3s, 0.015
200 notes: 4.71, ~4s, 0.029
400 notes: died!!!   the disk issue...

once 800 notes were loaded, startup time was about 6 seconds still.


with both @800, deleting notes is REALLY slow...


"""


