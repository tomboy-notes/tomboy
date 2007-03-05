
CSC = gmcs

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(TOMBOY_LIBS) 				\
	-r:Mono.Posix				\
	-r:$(top_builddir)/Tomboy/Tomboy.exe


#
# Printing plugin
#

PRINT_TARGET = PrintNotes.dll
PRINT_CSFILES = $(srcdir)/PrintNotes.cs
PRINT_RESOURCES =

$(PRINT_TARGET): $(PRINT_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(PRINT_CSFILES) $(PRINT_RESOURCES)


#
# Evolution plugin
#

EVO_CSFILES = $(srcdir)/Evolution.cs

if ENABLE_EVOLUTION
EVO_TARGET = Evolution.dll
EVO_RESOURCES = 		\
	$(EVOLUTION_LIBS)	\
	-resource:$(top_srcdir)/data/images/stock_mail.png,stock_mail.png

$(EVO_TARGET): $(EVO_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe 
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(EVO_CSFILES) $(EVO_RESOURCES)
endif


#
# Export to HTML plugin
#

EXPORT_TARGET = ExportToHTML.dll
EXPORT_CSFILES = $(srcdir)/ExportToHTML.cs
EXPORT_RESOURCES = -resource:$(srcdir)/ExportToHTML.xsl

$(EXPORT_TARGET): $(EXPORT_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe $(srcdir)/ExportToHTML.xsl
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(EXPORT_CSFILES) $(EXPORT_RESOURCES)


#
# Note of the Day plugin
#

NOTD_TARGET = NoteOfTheDay.dll
NOTD_CSFILES = $(srcdir)/NoteOfTheDay.cs
NOTD_RESOURCES =

$(NOTD_TARGET): $(NOTD_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(NOTD_CSFILES) $(NOTD_RESOURCES)


#
# Galago plugin
#

GALAGO_CSFILES = $(srcdir)/GalagoPresence.cs

if ENABLE_GALAGO
GALAGO_TARGET = GalagoPresence.dll
GALAGO_RESOURCES = $(GALAGO_LIBS)

$(GALAGO_TARGET): $(GALAGO_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(GALAGO_CSFILES) $(GALAGO_RESOURCES)
endif


#
# Fixed width font plugin
# 

FIXEDWIDTH_TARGET = FixedWidth.dll
FIXEDWIDTH_CSFILES = $(srcdir)/FixedWidth.cs
FIXEDWIDTH_RESOURCES = 

$(FIXEDWIDTH_TARGET): $(FIXEDWIDTH_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(FIXEDWIDTH_CSFILES) $(FIXEDWIDTH_RESOURCES)


#
# Sticky note import plugin
#

STICKYIMPORT_TARGET = StickyNoteImport.dll
STICKYIMPORT_CSFILES = $(srcdir)/StickyNoteImport.cs
STICKYIMPORT_RESOURCES =

$(STICKYIMPORT_TARGET): $(STICKYIMPORT_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(STICKYIMPORT_CSFILES) $(STICKYIMPORT_RESOURCES)


#
# Bugzilla plugin
#

BUGZILLA_TARGET = Bugzilla.dll
BUGZILLA_CSFILES = $(srcdir)/Bugzilla.cs
BUGZILLA_RESOURCES = -resource:$(top_srcdir)/data/images/stock_bug.png,stock_bug.png

$(BUGZILLA_TARGET): $(BUGZILLA_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(BUGZILLA_CSFILES) $(BUGZILLA_RESOURCES)


#
# Backlinks plugin
#

BACKLINKS_TARGET = Backlinks.dll
BACKLINKS_CSFILES = $(srcdir)/Backlinks.cs
BACKLINKS_RESOURCES = -resource:$(top_srcdir)/data/images/stock_bug.png,stock_bug.png

$(BACKLINKS_TARGET): $(BACKLINKS_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(BACKLINKS_CSFILES) $(BACKLINKS_RESOURCES)


pluginsdir = $(pkglibdir)/Plugins
plugins_DATA = 			\
	$(BACKLINKS_TARGET)	\
	$(BUGZILLA_TARGET)	\
	$(EVO_TARGET)		\
	$(EXPORT_TARGET)	\
	$(FIXEDWIDTH_TARGET)	\
	$(GALAGO_TARGET)	\
	$(NOTD_TARGET)		\
	$(PRINT_TARGET)		\
	$(STICKYIMPORT_TARGET)


EXTRA_DIST =            	\
	$(BACKLINKS_CSFILES)	\
	$(BUGZILLA_CSFILES)	\
	$(EVO_CSFILES)		\
	ExportToHTML.xsl	\
	$(EXPORT_CSFILES)	\
	$(GALAGO_CSFILES)	\
	$(PRINT_CSFILES)	\
	$(NOTD_CSFILES)		\
	$(FIXEDWIDTH_CSFILES)	\
	$(STICKYIMPORT_CSFILES)

CLEANFILES =				\
	$(BACKLINKS_TARGET)		\
	$(BACKLINKS_TARGET).mdb	\
	$(BUGZILLA_TARGET)		\
	$(BUGZILLA_TARGET).mdb		\
	$(EVO_TARGET)			\
	$(EVO_TARGET).mdb		\
	$(EXPORT_TARGET) 		\
	$(EXPORT_TARGET).mdb		\
	$(GALAGO_TARGET)		\
	$(GALAGO_TARGET).mdb		\
	$(PRINT_TARGET)			\
	$(PRINT_TARGET).mdb		\
	$(NOTD_TARGET)			\
	$(NOTD_TARGET).mdb		\
	$(FIXEDWIDTH_TARGET)		\
	$(FIXEDWIDTH_TARGET).mdb	\
	$(STICKYIMPORT_TARGET)		\
	$(STICKYIMPORT_TARGET).mdb
