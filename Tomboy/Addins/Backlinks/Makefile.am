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

#
# Backlinks Addin
#

TARGET = Backlinks.dll
CSFILES = \
	$(srcdir)/BacklinkMenuItem.cs		\
	$(srcdir)/BacklinksNoteAddin.cs
RESOURCES = \
	-resource:$(srcdir)/Backlinks.addin.xml

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/Backlinks.addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

