
CSC = mcs -g

CSFLAGS = 		\
	-target:library	\
	-define:DEBUG 

ASSEMBLIES = 					\
	$(TOMBOY_LIBS) 				\
	-r:Mono.Posix				\
	$(DBUS_LIBS)				\
	-r:$(top_srcdir)/Tomboy/Tomboy.exe


#
# Printing plugin
#

PRINT_TARGET = PrintNotes.dll
PRINT_CSFILES = $(srcdir)/PrintNotes.cs
PRINT_RESOURCES =

$(PRINT_TARGET): $(PRINT_CSFILES) $(top_srcdir)/Tomboy/Tomboy.exe
	$(CSC) -o $@ $(CSFLAGS) $(ASSEMBLIES) $(PRINT_CSFILES) $(PRINT_RESOURCES)


#
# Export to HTML plugin
#

EXPORT_TARGET = ExportToHTML.dll
EXPORT_CSFILES = $(srcdir)/ExportToHTML.cs
EXPORT_RESOURCES =

$(EXPORT_TARGET): $(EXPORT_CSFILES) $(top_srcdir)/Tomboy/Tomboy.exe
	$(CSC) -o $@ $(CSFLAGS) $(ASSEMBLIES) $(EXPORT_CSFILES) $(EXPORT_RESOURCES)


pluginsdir = $(pkglibdir)/Plugins
plugins_DATA = 			\
	$(EXPORT_TARGET)	\
	$(PRINT_TARGET)

EXTRA_DIST =            	\
	$(EXPORT_CSFILES)	\
	$(PRINT_CSFILES)

CLEANFILES =			\
	$(EXPORT_TARGET) 	\
	$(PRINT_TARGET) 
