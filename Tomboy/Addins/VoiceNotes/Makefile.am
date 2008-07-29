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

if ENABLE_GST
ADDIN_NAME = VoiceNote
TARGET = $(ADDIN_NAME).Addin.dll
CSFILES = \
	$(srcdir)/VoiceNote.cs
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml

$(TARGET): $(CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = \
	$(TARGET)

EXTRA_DIST = \
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml

CLEANFILES = \
	$(TARGET).mdb \
	$(TARGET)

##
## Build libvoicenote
##

INCLUDES =					\
	-I$(top_srcdir)				\
	-DG_LOG_DOMAIN=\"libvoicenote\" 	\
	-DGTK_DISABLE_DEPRECATED		\
	-DGDK_DISABLE_DEPRECATED		\
	-DG_DISABLE_DEPRECATED			\
	$(LIBTOMBOY_CFLAGS)                 	\
	$(GST_CFLAGS)				\
	$(GTK_CFLAGS)						


voicenotelibdir = $(pkglibdir)
voicenotelib_LTLIBRARIES = libvoicenote.la

libvoicenote_la_SOURCES =	\
	voicenotemedia.c

libvoicenote_la_LDFLAGS = \
	-export-dynamic -module -avoid-version \
	$(LIBTOMBOY_LIBS) \
	$(GST_LIBS)

maintainer-clean-local:
	rm -f Makefile.in

endif
