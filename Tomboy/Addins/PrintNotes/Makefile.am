include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(TOMBOY_LIBS) 				\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix

ADDIN_NAME = PrintNotes
TARGET = $(ADDIN_NAME).dll
CSFILES = \
	$(srcdir)/PrintNotesNoteAddin.cs
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml \
	COPYING

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

##
## Build libprintnotes for gedit print capability
##

INCLUDES =								\
	-I$(top_srcdir)						\
	-DG_LOG_DOMAIN=\"libprintnotes\"	\
	-DGTK_DISABLE_DEPRECATED			\
	-DGDK_DISABLE_DEPRECATED			\
	-DG_DISABLE_DEPRECATED				\
	$(LIBTOMBOY_CFLAGS)

printnoteslibdir = $(pkglibdir)
printnoteslib_LTLIBRARIES = libprintnotes.la

libprintnotes_la_SOURCES =				\
	gedit-print.h						\
	gedit-print.c						\
	gtksourceprintjob.h					\
	gtksourceprintjob.c

libprintnotes_la_LDFLAGS = -export-dynamic -module -avoid-version
libprintnotes_la_LIBADD = $(LIBTOMBOY_LIBS)

maintainer-clean-local:
	rm -f Makefile.in
