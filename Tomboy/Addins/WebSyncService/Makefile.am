include $(top_srcdir)/Makefile.include

if HAVE_NUNIT
    TEST_FLAGS = -define:ENABLE_TESTS
endif

CSFLAGS = \
	-debug			\
	-define:DEBUG	\
	-target:library	\
	$(TEST_FLAGS)

ASSEMBLIES = \
	$(LINK_TOMBOY_EXE)			\
	$(GTKSHARP_LIBS)			\
	$(LINK_MONO_ADDINS)			\
	$(NUNIT_LIBS)				\
	-r:Mono.Posix

#
# Web Sync Service Addin
#

TARGET = $(top_builddir)/bin/addins/WebSyncServiceAddin.dll
CSFILES = \
	$(srcdir)/NoteConvert.cs       	\
	$(srcdir)/WebSyncPreferencesWidget.cs   \
	$(srcdir)/WebSyncServer.cs          \
	$(srcdir)/WebSyncServiceAddin.cs	\
	$(srcdir)/Tests/*.cs            	\
	$(srcdir)/Api/*.cs                  \
	$(srcdir)/Api/Tests/*.cs            \
	$(srcdir)/Hyena.Json/*.cs           \
	$(srcdir)/Hyena.Json/Tests/*.cs     \
	$(srcdir)/LibProxy/*.cs             \
	$(srcdir)/OAuth/*.cs                \
	$(srcdir)/OAuth/Mono.Rocks/*.cs
RESOURCES = \
	-resource:$(srcdir)/WebSyncService.addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


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

