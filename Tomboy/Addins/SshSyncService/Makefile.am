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
# SSH Sync Service Addin
#

TARGET = $(top_builddir)/bin/addins/SshSyncServiceAddin.dll
CSFILES = \
	$(srcdir)/SshSyncServiceAddin.cs
RESOURCES = \
	-resource:$(srcdir)/SshSyncService.addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/SshSyncService.addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

