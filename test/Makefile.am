CSC = $(GMCS)

TARGET = $(top_builddir)/bin/TomboyTest.dll

CSFLAGS =				\
	-debug				\
	-define:DEBUG			\
	-target:library

CSFILES =					\
	$(srcdir)/DummyNote.cs			\
	$(srcdir)/LoggerTest.cs			\
	$(srcdir)/NoteTest.cs			\
	$(srcdir)/NoteManagerTest.cs		\
	$(srcdir)/Plugins/ExportToHTMLTest.cs

ASSEMBLIES =							\
	$(NUNIT_LIBS)						\
	$(TOMBOY_LIBS)						\
	-r:$(LINK_TOMBOY_EXE)			\
	-r:$(top_builddir)/bin/ExportToHTML.dll

MONO_PATH = $(top_builddir)/Tomboy:$(top_builddir)/Tomboy/Plugins

RESSOURCES =

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	$(CSC) -out:$@ $(CSFLAGS) $(CSFILES) $(ASSEMBLIES) $(RESSOURCES)

test: $(TARGET)
	MONO_PATH=$(MONO_PATH) $(NUNIT) $(TARGET) /nologo

EXTRA_DIST = 				\
	$(CSFILES)

CLEANFILES = 				\
	$(TARGET)			\
	$(TARGET).mdb			\
	TestResult.xml

.PHONY: test
