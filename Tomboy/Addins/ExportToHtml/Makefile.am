include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(GTKSHARP_LIBS) 				\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix

ADDIN_NAME = ExportToHtml
TARGET = $(ADDIN_NAME).dll
CSFILES = \
	$(srcdir)/ExportToHtmlDialog.cs		\
	$(srcdir)/ExportToHtmlNoteAddin.cs		\
	$(srcdir)/NoteNameResolver.cs
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml \
	-resource:$(srcdir)/ExportToHtml.xsl


$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml \
	$(srcdir)/ExportToHtml.xsl

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

