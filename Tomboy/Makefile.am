

CSC = mcs -g

TARGET = Tomboy.exe
WRAPPER = tomboy

CSFLAGS = -target:exe -define:DEBUG

CSFILES = 				\
	$(srcdir)/Tomboy.cs 		\
	$(srcdir)/Note.cs 		\
	$(srcdir)/NoteManager.cs 	\
	$(srcdir)/NoteWindow.cs 	\
	$(srcdir)/NoteBuffer.cs 	\
	$(srcdir)/RecentChanges.cs	\
	$(srcdir)/Search.cs 		\
	$(srcdir)/Tray.cs 		\
	$(srcdir)/Undo.cs 		\
	$(srcdir)/Utils.cs		\
	$(srcdir)/Watchers.cs		\
	$(srcdir)/XKeybinder.cs

#	$(srcdir)/ActiveVerb.cs 
#	$(srcdir)/GoogleSearchService.cs 

ASSEMBLIES = 					\
	$(TOMBOY_LIBS)				\
	-r:Mono.Posix				\
	-r:System.Web				\
	-r:System.Web.Services			\
	-r:$(top_builddir)/TrayIcon/TrayIcon.dll

RESOURCES = 							\
	-resource:$(srcdir)/Start_Here.note,Start_Here.note	\
	-resource:$(srcdir)/tintin.png,tintin.png		\
	-resource:$(srcdir)/stock_notes.png,stock_notes.png	\
	-resource:$(srcdir)/gnome-stock-searchtool.png,gnome-stock-searchtool.png

if OLD_GTKSPELL
CSFLAGS += -define:OLD_GTKSPELL
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

EXTRA_DIST =            			\
	$(CSFILES)      			\
	$(WRAPPER).in				\
	$(man_MANS)				\
	$(srcdir)/Defines.cs.in			\
	$(srcdir)/Start_Here.note		\
	$(srcdir)/tintin.png			\
	$(srcdir)/stock_notes.png		\
	$(srcdir)/gnome-stock-searchtool.png	\
	$(srcdir)/com.beatniksoftware.Tomboy.service.in

CLEANFILES =            			\
	$(TARGET)       			\
	$(WRAPPER)				\
	Defines.cs				\
	com.beatniksoftware.Tomboy.service
