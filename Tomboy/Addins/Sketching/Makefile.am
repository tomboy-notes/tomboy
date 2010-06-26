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
	-r:Mono.Cairo

ADDIN_NAME = Sketching
CSFILES = \
	$(srcdir)/SketchingNoteAddin.cs	\
	$(srcdir)/SketchingTextTag.cs   \
	$(srcdir)/Handwriting.cs        \
	$(srcdir)/Paper.cs              \
	$(srcdir)/Pen.cs                \
	$(srcdir)/Stroke.cs

if ENABLE_SKETCHING
TARGET = $(top_builddir)/bin/addins/$(ADDIN_NAME).dll
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)

addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb
endif

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

