include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(TOMBOY_LIBS) 				\
	$(GALAGO_LIBS)				\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix

ADDIN_NAME = GalagoPresence
CSFILES = \
	$(srcdir)/GalagoPresenceNoteAddin.cs


if ENABLE_GALAGO
TARGET = $(ADDIN_NAME).dll
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)
endif


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

