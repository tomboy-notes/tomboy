
SUBDIRS = . Plugins

CSC = mcs -g

TARGET = Tomboy.exe
WRAPPER = tomboy

if ENABLE_DBUS
DBUS_CSFLAGS = -define:ENABLE_DBUS
DBUS_CSFILES = 				\
	$(srcdir)/RemoteControl.cs	\
	$(srcdir)/RemoteControlProxy.cs
endif

if OLD_GTKSPELL
OLD_GTKSPELL_CSFLAGS = -define:OLD_GTKSPELL
endif

if FIXED_GTKSPELL
FIXED_GTKSPELL_CSFLAGS = -define:FIXED_GTKSPELL
endif

CSFLAGS = 				\
	-target:exe 			\
	-define:DEBUG 			\
	-unsafe 			\
	$(DBUS_CSFLAGS) 		\
	$(FIXED_GTKSPELL_CSFLAGS) 	\
	$(OLD_GTKSPELL_CSFLAGS)

CSFILES = 					\
	$(srcdir)/Tomboy.cs 			\
	$(srcdir)/Applet.cs 			\
	$(srcdir)/Note.cs 			\
	$(srcdir)/NoteManager.cs 		\
	$(srcdir)/NoteWindow.cs 		\
	$(srcdir)/NoteBuffer.cs 		\
	$(srcdir)/PluginManager.cs		\
	$(srcdir)/Preferences.cs		\
	$(srcdir)/RecentChanges.cs		\
	$(srcdir)/Search.cs 			\
	$(srcdir)/Tray.cs 			\
	$(srcdir)/Trie.cs			\
	$(srcdir)/Undo.cs 			\
	$(srcdir)/Utils.cs			\
	$(srcdir)/Watchers.cs			\
	$(srcdir)/XKeybinder.cs			\
	\
	$(srcdir)/panelapplet/*.cs		\
	$(srcdir)/panelapplet/generated/*.cs	\
	\
	$(DBUS_CSFILES)	

ASSEMBLIES = 		\
	$(TOMBOY_LIBS) 	\
	-r:Mono.Posix	\
	$(DBUS_LIBS)

RESOURCES = 										\
	-resource:$(top_srcdir)/data/GNOME_TomboyApplet.xml,GNOME_TomboyApplet.xml	\
	-resource:$(top_srcdir)/data/Start_Here.note,Start_Here.note			\
	-resource:$(top_srcdir)/data/images/tintin.png,tintin.png			\
	-resource:$(top_srcdir)/data/images/stock_notes.png,stock_notes.png		\
	-resource:$(top_srcdir)/data/images/gnome-stock-searchtool.png,gnome-stock-searchtool.png

$(TARGET): $(CSFILES) $(srcdir)/Defines.cs
	$(CSC) -o $@ $(CSFLAGS) $^ $(ASSEMBLIES) $(RESOURCES)

tomboylibdir = $(pkglibdir)
tomboylib_DATA = $(TARGET) $(TARGET).config


bin_SCRIPTS = $(WRAPPER)

$(WRAPPER): $(srcdir)/$(WRAPPER).in
	sed -e "s|\@prefix\@|$(prefix)|g" 		\
	    -e "s|\@pkglibdir\@|$(pkglibdir)|g" 	\
	    -e "s|\@bindir\@|$(bindir)|g"		\
	    -e "s|\@target\@|$(TARGET)|g" 		\
	    -e "s|\@wrapper\@|$(WRAPPER)|g"		\
	    -e "s|\@extragac\@|$(EXTRA_GAC_PREFIX)|g"	\
	    < $^ > $@
	chmod +x $(WRAPPER)

$(TARGET).config: $(srcdir)/$(TARGET).config.in
	sed -e "s|\@pkglibdir\@|$(pkglibdir)|" \
	    < $^ > $@

$(srcdir)/Defines.cs: $(srcdir)/Defines.cs.in
	sed -e "s|\@version\@|$(VERSION)|"     \
	    -e "s|\@datadir\@|$(datadir)|"     \
	    -e "s|\@pkglibdir\@|$(pkglibdir)|" \
	    < $^ > $@


EXTRA_DIST =            		\
	$(CSFILES)      		\
	$(WRAPPER).in			\
	$(TARGET).config.in		\
	$(srcdir)/Defines.cs.in	

CLEANFILES =			\
	$(TARGET) 		\
	$(TARGET).config	\
	$(WRAPPER)		\
	Defines.cs
