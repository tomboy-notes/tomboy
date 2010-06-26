include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(GTKSHARP_LIBS) 			\
	$(GNOME_LIBS) 				\
	$(EVOLUTION_LIBS)			\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix

ADDIN_NAME = Evolution
CSFILES = \
	$(srcdir)/EvolutionNoteAddin.cs


if ENABLE_EVOLUTION
TARGET = $(top_builddir)/bin/addins/$(ADDIN_NAME).dll
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml \
	-resource:$(srcdir)/mail.png,mail.png

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)

addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb
endif

noinst_DATA = \
	$(srcdir)/mail.png

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml \
	$(srcdir)/mail.png

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

