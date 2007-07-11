include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(TOMBOY_LIBS) 				\
	$(EVOLUTION_LIBS)			\
	$(LINK_MONO_ADDINS)			\
	-r:Mono.Posix

ADDIN_NAME = Evolution
CSFILES = \
	$(srcdir)/EvolutionNoteAddin.cs


if ENABLE_EVOLUTION
TARGET = $(ADDIN_NAME).dll
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml \
	-resource:$(top_srcdir)/data/images/stock_mail.png,stock_mail.png

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

