include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(GTKSHARP_LIBS) 				\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix               \
	-r:System.Web.Extensions

#
# Web Sync Service Addin
#

TARGET = WebSyncServiceAddin.dll
CSFILES = \
	$(srcdir)/WebSyncServer.cs		\
	$(srcdir)/WebSyncServiceAddin.cs	\
	$(srcdir)/Api/NoteInfo.cs	\
	$(srcdir)/Api/UserInfo.cs
RESOURCES = \
	-resource:$(srcdir)/WebSyncService.addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/WebSyncService.addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

