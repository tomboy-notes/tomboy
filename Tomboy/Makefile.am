

CSC = mcs -g

TARGET = Tomboy.exe
WRAPPER = tomboy

CSFLAGS = -target:exe -define:DEBUG -unsafe

CSFILES = 				\
	$(srcdir)/Tomboy.cs 		\
	$(srcdir)/Applet.cs 		\
	$(srcdir)/Note.cs 		\
	$(srcdir)/NoteManager.cs 	\
	$(srcdir)/NoteWindow.cs 	\
	$(srcdir)/NoteBuffer.cs 	\
	$(srcdir)/Preferences.cs	\
	$(srcdir)/RecentChanges.cs	\
	$(srcdir)/Search.cs 		\
	$(srcdir)/Tray.cs 		\
	$(srcdir)/Undo.cs 		\
	$(srcdir)/Utils.cs		\
	$(srcdir)/Watchers.cs		\
	$(srcdir)/XKeybinder.cs		\
	\
	$(srcdir)/panelapplet/*.cs	\
	$(srcdir)/panelapplet/generated/*.cs

#	$(srcdir)/ActiveVerb.cs 
#	$(srcdir)/GoogleSearchService.cs 

ASSEMBLIES = 					\
	$(TOMBOY_LIBS)				\
	-r:Mono.Posix				\
	-r:$(top_builddir)/TrayIcon/TrayIcon.dll

RESOURCES = 									\
	-resource:$(srcdir)/Start_Here.note,Start_Here.note			\
	-resource:$(srcdir)/tintin.png,tintin.png				\
	-resource:$(srcdir)/stock_notes.png,stock_notes.png			\
	-resource:$(srcdir)/GNOME_TomboyApplet.xml,GNOME_TomboyApplet.xml	\
	-resource:$(srcdir)/gnome-stock-searchtool.png,gnome-stock-searchtool.png

if OLD_GTKSPELL
CSFLAGS += -define:OLD_GTKSPELL
endif

if FIXED_GTKSPELL
CSFLAGS += -define:FIXED_GTKSPELL
endif

if ENABLE_DBUS
CSFLAGS += -define:ENABLE_DBUS
CSFILES += 				\
	$(srcdir)/RemoteControl.cs	\
	$(srcdir)/RemoteControlProxy.cs
ASSEMBLIES += $(DBUS_LIBS)

com.beatniksoftware.Tomboy.service: $(srcdir)/com.beatniksoftware.Tomboy.service.in
	sed -e "s|\@bindir\@|$(bindir)|g"	\
	    -e "s|\@wrapper\@|$(WRAPPER)|g"	\
	    < $^ > $@

dbusservicedir = $(libdir)/dbus-1.0/services
dbusservice_DATA = com.beatniksoftware.Tomboy.service
endif

$(TARGET): $(CSFILES) $(srcdir)/Defines.cs
	$(CSC) -o $@ $(CSFLAGS) $^ $(ASSEMBLIES) $(RESOURCES)

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_PROGRAM) $(TARGET) $(DESTDIR)$(pkglibdir)/$(TARGET)

uninstall-local:
	rm -f $(DESTDIR)$(pkglibdir)/$(TARGET)

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

Defines.cs: $(srcdir)/Defines.cs.in
	sed -e "s|\@version\@|$(VERSION)|" \
	    -e "s|\@datadir\@|$(datadir)|" \
	    < $^ > $@

pixmapdir = $(datadir)/pixmaps
pixmap_DATA = tintin.png

man_MANS = tomboy.1

serverdir       = $(libdir)/bonobo/servers
server_in_files = GNOME_TomboyApplet.server.in
server_DATA     = $(server_in_files:.server.in=.server)

$(server_in_files): $(server_in_files:.server.in=.server.in.in)
	sed -e "s|\@pkglibdir\@|$(pkglibdir)|g" 	\
	    -e "s|\@bindir\@|$(bindir)|g"		\
	    -e "s|\@wrapper\@|$(WRAPPER)|g"		\
	    < $^ > $@

@INTLTOOL_SERVER_RULE@

EXTRA_DIST =            				\
	$(CSFILES)      				\
	$(WRAPPER).in					\
	$(man_MANS)					\
	$(srcdir)/Defines.cs.in				\
	$(srcdir)/Start_Here.note			\
	$(srcdir)/tintin.png				\
	$(srcdir)/stock_notes.png			\
	$(srcdir)/gnome-stock-searchtool.png		\
	$(srcdir)/com.beatniksoftware.Tomboy.service.in \
	$(srcdir)/GNOME_TomboyApplet.server.in.in	\
	$(srcdir)/GNOME_TomboyApplet.xml

CLEANFILES =            			\
	$(TARGET)       			\
	$(WRAPPER)				\
	Defines.cs				\
	com.beatniksoftware.Tomboy.service	\
	$(server_in_files) 			\
	$(server_DATA)
