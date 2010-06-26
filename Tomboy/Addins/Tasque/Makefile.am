include $(top_srcdir)/Makefile.include

CSFLAGS = 		\
	-debug 		\
	-define:DEBUG 	\
	-target:library

ASSEMBLIES = 					\
	$(LINK_TOMBOY_EXE)			\
	$(GTKSHARP_LIBS) 			\
	$(LINK_MONO_ADDINS)			\
	$(DBUS_LIBS)				\
	-r:Mono.Posix

#	-r:/home/boyd/stage/tasque/lib/tasque/Tasque.exe

ADDIN_NAME = Tasque

TARGET = $(top_builddir)/bin/addins/$(ADDIN_NAME).Addin.dll
CSFILES = \
	$(srcdir)/TasqueNoteAddin.cs \
	$(srcdir)/RemoteControl.cs
RESOURCES = \
	-resource:$(srcdir)/$(ADDIN_NAME).addin.xml \
	-resource:$(srcdir)/tasque-22.png,tasque.png

$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) $(TOMBOY_EXE_PATH)
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(CSFILES) $(RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TARGET)		\
	$(TARGET).mdb

EXTRA_DIST =            	\
	$(CSFILES) \
	$(srcdir)/$(ADDIN_NAME).addin.xml \
	$(srcdir)/tasque-22.png

CLEANFILES =				\
	$(TARGET).mdb \
	$(TARGET)

