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
# SSH Sync Service Addin
#

TARGET = SshSyncServiceAddin.dll
CSFILES = \
	$(srcdir)/SshSyncServiceAddin.cs
RESOURCES = \
	-resource:$(srcdir)/SshSyncService.addin.xml

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/SshSyncService.addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

