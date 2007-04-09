
SUBDIRS = . Plugins

CSC = gmcs

TARGET = Tomboy.exe
WRAPPER = tomboy

DBUS_SHARP_CSFILES = \
	$(srcdir)/dbus-sharp/dbus-sharp/src/*.cs	\
	$(srcdir)/dbus-sharp/dbus-sharp-glib/glib/*.cs

if ENABLE_DBUS
DBUS_CSFLAGS = -define:ENABLE_DBUS
if EXTERNAL_DBUS
DBUS_CSFILES = 				\
	$(srcdir)/RemoteControl.cs	\
	$(srcdir)/RemoteControlProxy.cs
else
DBUS_CSFILES = 				\
	$(DBUS_SHARP_CSFILES)		\
	$(srcdir)/RemoteControl.cs	\
	$(srcdir)/RemoteControlProxy.cs
endif
endif

if FIXED_GTKSPELL
FIXED_GTKSPELL_CSFLAGS = -define:FIXED_GTKSPELL
endif

CSFLAGS = 				\
	-debug				\
	-define:DEBUG 			\
	-unsafe 			\
	-target:exe 			\
	$(DBUS_CSFLAGS) 		\
	$(FIXED_GTKSPELL_CSFLAGS)

CSFILES = 					\
	$(srcdir)/Tomboy.cs 			\
	$(srcdir)/ActionManager.cs		\
	$(srcdir)/Applet.cs 			\
	$(srcdir)/Logger.cs			\
	$(srcdir)/Note.cs 			\
	$(srcdir)/NoteManager.cs 		\
	$(srcdir)/NoteWindow.cs 		\
	$(srcdir)/NoteBuffer.cs 		\
	$(srcdir)/NoteTag.cs 			\
	$(srcdir)/PluginManager.cs		\
	$(srcdir)/Preferences.cs		\
	$(srcdir)/RecentChanges.cs		\
	$(srcdir)/Tray.cs 			\
	$(srcdir)/Trie.cs			\
	$(srcdir)/Undo.cs 			\
	$(srcdir)/Utils.cs			\
	$(srcdir)/Watchers.cs			\
	$(srcdir)/XKeybinder.cs			\
	\
	$(srcdir)/panelapplet/*.cs		\
	\
	$(DBUS_CSFILES)	


ASSEMBLIES = 		\
	$(TOMBOY_LIBS) 	\
	-r:Mono.Posix	\
	$(DBUS_LIBS)

RESOURCES = 										\
	-resource:$(top_srcdir)/data/GNOME_TomboyApplet.xml,GNOME_TomboyApplet.xml	\
	-resource:$(top_srcdir)/data/UIManagerLayout.xml	\
	-resource:$(top_srcdir)/data/images/tomboy.png,tomboy.png			\
	-resource:$(top_srcdir)/data/images/tomboy-note-16.png,tomboy-note.png		\
	-resource:$(top_srcdir)/data/images/gnome-searchtool.png,gnome-searchtool.png	\
	-resource:$(top_srcdir)/data/images/pinup.png,pinup.png				\
	-resource:$(top_srcdir)/data/images/pinup-active.png,pinup-active.png		\
	-resource:$(top_srcdir)/data/images/pindown.png,pindown.png


$(TARGET): $(CSFILES) Defines.cs
	$(CSC) -out:$@ $(CSFLAGS) $^ $(ASSEMBLIES) $(RESOURCES)

tomboylibdir = $(pkglibdir)
tomboylib_DATA = $(TARGET) $(TARGET).config


bin_SCRIPTS = $(WRAPPER)

$(WRAPPER): $(srcdir)/$(WRAPPER).in Makefile
	sed -e "s|\@prefix\@|$(prefix)|g" 		\
	    -e "s|\@pkglibdir\@|$(pkglibdir)|g" 	\
	    -e "s|\@bindir\@|$(bindir)|g"		\
	    -e "s|\@target\@|$(TARGET)|g" 		\
	    -e "s|\@wrapper\@|$(WRAPPER)|g"		\
	    -e "s|\@extragac\@|$(EXTRA_GAC_PREFIX)|g"	\
	    -e "s|\@srcdir\@|$(PWD)|g"	\
	    < $< > $@
	chmod +x $(WRAPPER)

$(TARGET).config: $(srcdir)/$(TARGET).config.in Makefile
	sed -e "s|\@pkglibdir\@|$(pkglibdir)|" \
	    < $< > $@

Defines.cs: $(srcdir)/Defines.cs.in Makefile
	sed -e "s|\@version\@|$(VERSION)|"     \
	    -e "s|\@datadir\@|$(datadir)|"     \
	    -e "s|\@pkglibdir\@|$(pkglibdir)|" \
	    < $< > $@


EXTRA_DIST =            		\
	$(CSFILES)      		\
	$(DBUS_SHARP_CSFILES)		\
	$(WRAPPER).in			\
	$(TARGET).config.in		\
	$(srcdir)/Defines.cs.in	

CLEANFILES =			\
	$(TARGET) 		\
	$(TARGET).config	\
	$(TARGET).mdb 		\
	$(WRAPPER)		\
	Defines.cs
