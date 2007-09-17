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

#
# Tasks Addin
#

TASKS_TARGET = Tasks.dll
TASKS_CSFILES = \
	$(srcdir)/CellRendererDate.cs \
	$(srcdir)/ComplexMenuItem.cs \
	$(srcdir)/DateButton.cs \
	$(srcdir)/Task.cs \
	$(srcdir)/TasksApplicationAddin.cs \
	$(srcdir)/TasksNoteAddin.cs \
	$(srcdir)/TaskArchiver.cs \
	$(srcdir)/TaskData.cs \
	$(srcdir)/TaskListWindow.cs \
	$(srcdir)/TaskManager.cs \
	$(srcdir)/TaskOptionsDialog.cs \
	$(srcdir)/TaskTag.cs
TASKS_RESOURCES = \
	-resource:$(srcdir)/checkbox-done.png,checkbox-done.png \
	-resource:$(srcdir)/checkbox-todo.png,checkbox-todo.png \
	-resource:$(srcdir)/TasksUIManagerLayout.xml \
	-resource:$(srcdir)/Tasks.addin.xml
TASKS_EXTRA_DIST = \
	$(srcdir)/checkbox-done.png \
	$(srcdir)/checkbox-todo.png \
	$(srcdir)/TasksUIManagerLayout.xml \
	$(srcdir)/Tasks.addin.xml

$(TASKS_TARGET): $(TASKS_CSFILES) $(top_builddir)/Tomboy/Tomboy.exe
	$(CSC) -out:$@ $(CSFLAGS) $(ASSEMBLIES) $(TASKS_CSFILES) $(TASKS_RESOURCES)


addinsdir = $(pkglibdir)/addins
addins_DATA = 			\
	$(TASKS_TARGET)

EXTRA_DIST =            	\
	$(TASKS_CSFILES) \
	$(TASKS_EXTRA_DIST)

CLEANFILES =				\
	$(TASKS_TARGET).mdb \
	$(TASKS_TARGET)

