CSC = $(GMCS)

TARGET = TomboyTest.dll

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
	-r:$(top_builddir)/Tomboy/Tomboy.exe			\
	-r:$(top_builddir)/Tomboy/Plugins/ExportToHTML.dll

MONO_PATH = $(top_builddir)/Tomboy:$(top_builddir)/Tomboy/Plugins

RESSOURCES =

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
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
