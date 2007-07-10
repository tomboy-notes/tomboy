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

ADDIN_NAME = Bugzilla
TARGET = $(ADDIN_NAME).dll
CSFILES = \
	$(srcdir)/BugzillaLink.cs		\
	$(srcdir)/BugzillaNoteAddin.cs		\
	$(srcdir)/BugzillaPreferences.cs		\
	$(srcdir)/BugzillaPreferencesFactory.cs \
	$(srcdir)/InsertBugAction.cs
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml \
	-resource:$(top_srcdir)/data/images/stock_bug.png,stock_bug.png


$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

