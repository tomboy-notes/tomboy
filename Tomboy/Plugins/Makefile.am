
CSC = mcs

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(TOMBOY_LIBS) 				\
	-r:Mono.Posix				\
	$(DBUS_LIBS)				\
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
# Export to HTML plugin
#

EXPORT_TARGET = ExportToHTML.dll
EXPORT_CSFILES = $(srcdir)/ExportToHTML.cs
EXPORT_RESOURCES = -resource:$(srcdir)/ExportToHTML.xsl

$(EXPORT_TARGET): $(EXPORT_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe $(srcdir)/ExportToHTML.xsl
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(EXPORT_CSFILES) $(EXPORT_RESOURCES)


pluginsdir = $(pkglibdir)/Plugins
plugins_DATA = 			\
	$(EXPORT_TARGET)	\
	$(PRINT_TARGET)


EXTRA_DIST =            	\
	$(EXPORT_CSFILES)	\
	$(PRINT_CSFILES)

CLEANFILES =			\
	$(EXPORT_TARGET) 	\
	$(EXPORT_TARGET).mdb	\
	$(PRINT_TARGET)		\
	$(PRINT_TARGET).mdb
