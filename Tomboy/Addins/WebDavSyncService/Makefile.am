include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(GTKSHARP_LIBS) 				\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix

#
# WebDav Sync Service Addin
#

TARGET = $(top_builddir)/bin/addins/WebDavSyncService.dll
CSFILES = \
	$(srcdir)/WebDavSyncServiceAddin.cs
RESOURCES = \
	-resource:$(srcdir)/WebDavSyncService.addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/WebDavSyncService.addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

