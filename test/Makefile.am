CSC = mcs

TARGET = TomboyTest.dll

CSFLAGS =				\
	-debug				\
	-define:DEBUG			\
	-target:library

CSFILES =				\
	$(srcdir)/NoteTest.cs

ASSEMBLIES =				\
	$(NUNIT_LIBS)			\
	-r:$(top_builddir)/Tomboy/Tomboy.exe

RESSOURCES =

$(TARGET): $(CSFILES)
	$(CSC) -out:$@ $(CSFLAGS) $^ $(ASSEMBLIES) $(RESSOURCES)

test: $(TARGET)
	MONO_PATH=$(top_builddir)/Tomboy $(NUNIT) $(TARGET) /nologo

EXTRA_DIST = 				\
	$(CSFILES)

CLEANFILES = 				\
	$(TARGET)			\
	$(TARGET).mdb			\
	TestResult.xml
