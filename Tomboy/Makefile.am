

CSC = mcs -g

TARGET = Tomboy.exe
WRAPPER = tomboy

CSFLAGS = -target:exe

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
	$(srcdir)/Watchers.cs

#	$(srcdir)/ActiveVerb.cs 		
#	$(srcdir)/GoogleSearchService.cs 

ASSEMBLIES = 					\
	$(TOMBOY_LIBS)				\
	-r:System.Web				\
	-r:System.Web.Services			\
	-r:$(top_builddir)/TrayIcon/TrayIcon.dll

RESOURCES = 							\
	-resource:$(srcdir)/Start_Here.note,Start_Here.note	\
	-resource:$(srcdir)/tintin.png,tintin.png		\
	-resource:$(srcdir)/stock_notes.png,stock_notes.png	\
	-resource:$(srcdir)/gnome-stock-searchtool.png,gnome-stock-searchtool.png

$(TARGET): $(CSFILES) $(LOCAL_ASSEMBLIES) 
	$(CSC) -o $@ $(CSFLAGS) $(CSFILES) $(ASSEMBLIES) $(RESOURCES)

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_PROGRAM) $(TARGET) $(DESTDIR)$(pkglibdir)/$(TARGET)

uninstall-local:
	rm -f $(DESTDIR)$(pkglibdir)/$(TARGET)

bin_SCRIPTS = $(WRAPPER)

$(WRAPPER): $(srcdir)/$(WRAPPER).in
	sed -e "s|\@prefix\@|$(prefix)|g" -e "s|\@pkglibdir\@|$(pkglibdir)|g" -e "s|\@target\@|$(TARGET)|g" < $^ > $@
	chmod +x $(WRAPPER)

EXTRA_DIST =            			\
	$(CSFILES)      			\
	$(WRAPPER).in				\
	$(srcdir)/Start_Here.note		\
	$(srcdir)/tintin.png			\
	$(srcdir)/stock_notes.png		\
	$(srcdir)/gnome-stock-searchtool.png

CLEANFILES =            \
	$(TARGET)       \
	$(WRAPPER)
