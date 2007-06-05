include $(top_srcdir)/Makefile.include

ASSEMBLY_NAME = Mono.Addins.Gui

ASSEMBLY_SOURCES =							\
	$(srcdir)/AssemblyInfo.cs 					\
	$(srcdir)/gtk-gui/generated.cs 					\
	$(srcdir)/gtk-gui/Mono.Addins.Gui.AddinInfoDialog.cs 		\
	$(srcdir)/gtk-gui/Mono.Addins.Gui.AddinInstallDialog.cs 	\
	$(srcdir)/gtk-gui/Mono.Addins.Gui.AddinManagerDialog.cs 	\
	$(srcdir)/gtk-gui/Mono.Addins.Gui.ErrorDialog.cs 		\
	$(srcdir)/gtk-gui/Mono.Addins.Gui.ManageSitesDialog.cs 		\
	$(srcdir)/gtk-gui/Mono.Addins.Gui.NewSiteDialog.cs 		\
	$(srcdir)/Mono.Addins.Gui/AddinInfoDialog.cs 			\
	$(srcdir)/Mono.Addins.Gui/AddinInstallDialog.cs 		\
	$(srcdir)/Mono.Addins.Gui/AddinManagerDialog.cs 		\
	$(srcdir)/Mono.Addins.Gui/AddinManagerWindow.cs 		\
	$(srcdir)/Mono.Addins.Gui/AddinTreeWidget.cs 			\
	$(srcdir)/Mono.Addins.Gui/ErrorDialog.cs 			\
	$(srcdir)/Mono.Addins.Gui/ManageSitesDialog.cs 			\
	$(srcdir)/Mono.Addins.Gui/NewSiteDialog.cs 			\
	$(srcdir)/Mono.Addins.Gui/Services.cs 

REFS =							\
	-r:Mono.Posix					\
	-r:System					\
	-r:$(DIR_ADDINS_ADDINS)/Mono.Addins.dll		\
	-r:$(DIR_ADDINS_SETUP)/Mono.Addins.Setup.dll

PKGS =				\
	-pkg:gnome-sharp-2.0 	\
	-pkg:gtk-sharp-2.0

RESOURCES =							\
	-resource:$(srcdir)/gtk-gui/gui.stetic 			\
	-resource:$(srcdir)/icons/package-x-generic.png 	\
	-resource:$(srcdir)/icons/package-x-generic_16.png 	\
	-resource:$(srcdir)/icons/package-x-generic_22.png 	\
	-resource:$(srcdir)/icons/user-package.png 

CSC_EXTRA_FLAGS =  -noconfig -codepage:utf8 -warn:4 -debug -d:DEBUG

ASSEMBLY = $(ASSEMBLY_NAME).dll

all: $(ASSEMBLY)

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(CSC_LIB) $(CSC_EXTRA_FLAGS) -out:$@ $(PKGS) $(REFS) $(ASSEMBLY_SOURCES) $(RESOURCES)

assemblydir = $(pkglibdir)
assembly_DATA =			\
	$(ASSEMBLY)

EXTRA_DIST = 						\
	$(ASSEMBLY_SOURCES)				\
	$(srcdir)/gtk-gui/gui.stetic 			\
	$(srcdir)/icons/package-x-generic.png 		\
	$(srcdir)/icons/package-x-generic_16.png 	\
	$(srcdir)/icons/package-x-generic_22.png 	\
	$(srcdir)/icons/user-package.png 

CLEANFILES =			\
	$(ASSEMBLY)		\
	$(ASSEMBLY).mdb
