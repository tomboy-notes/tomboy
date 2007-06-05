include $(top_srcdir)/Makefile.include

ASSEMBLY_NAME = Mono.Addins.Setup

ASSEMBLY_SOURCES =									\
	$(srcdir)/Mono.Addins.Setup.ProgressMonitoring/ConsoleProgressMonitor.cs 	\
	$(srcdir)/Mono.Addins.Setup.ProgressMonitoring/LogTextWriter.cs 		\
	$(srcdir)/Mono.Addins.Setup.ProgressMonitoring/NullProgressMonitor.cs 		\
	$(srcdir)/Mono.Addins.Setup.ProgressMonitoring/ProgressStatusMonitor.cs 	\
	$(srcdir)/Mono.Addins.Setup.ProgressMonitoring/ProgressTracker.cs 		\
	$(srcdir)/Mono.Addins.Setup/AddinInfo.cs 					\
	$(srcdir)/Mono.Addins.Setup/AddinInfoCollection.cs 				\
	$(srcdir)/Mono.Addins.Setup/AddinPackage.cs 					\
	$(srcdir)/Mono.Addins.Setup/AddinRepositoryEntry.cs 				\
	$(srcdir)/Mono.Addins.Setup/AddinStore.cs 					\
	$(srcdir)/Mono.Addins.Setup/AddinSystemConfiguration.cs 			\
	$(srcdir)/Mono.Addins.Setup/AddinSystemConfigurationReaderWriter.cs 		\
	$(srcdir)/Mono.Addins.Setup/AddinSystemConfigurationSerializer.cs 		\
	$(srcdir)/Mono.Addins.Setup/InstallException.cs 				\
	$(srcdir)/Mono.Addins.Setup/IProgressMonitor.cs 				\
	$(srcdir)/Mono.Addins.Setup/Package.cs 						\
	$(srcdir)/Mono.Addins.Setup/PackageCollection.cs 				\
	$(srcdir)/Mono.Addins.Setup/ReferenceRepositoryEntry.cs 			\
	$(srcdir)/Mono.Addins.Setup/Repository.cs 					\
	$(srcdir)/Mono.Addins.Setup/RepositoryEntry.cs 					\
	$(srcdir)/Mono.Addins.Setup/RepositoryEntryCollection.cs 			\
	$(srcdir)/Mono.Addins.Setup/RepositoryReaderWriter.cs 				\
	$(srcdir)/Mono.Addins.Setup/RepositoryRecord.cs 				\
	$(srcdir)/Mono.Addins.Setup/RepositoryRegistry.cs 				\
	$(srcdir)/Mono.Addins.Setup/RepositorySerializer.cs 				\
	$(srcdir)/Mono.Addins.Setup/SetupService.cs 					\
	$(srcdir)/Mono.Addins.Setup/SetupTool.cs

REFS =						\
	-r:ICSharpCode.SharpZipLib 		\
	-r:Mono.Posix 				\
	-r:System 				\
	-r:System.Xml				\
	-r:$(DIR_ADDINS_ADDINS)/Mono.Addins.dll

PKGS =

CSC_EXTRA_FLAGS =  -langversion:ISO-1 -noconfig -codepage:utf8 -warn:4 -debug -d:DEBUG

ASSEMBLY = $(ASSEMBLY_NAME).dll

all: $(ASSEMBLY)

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(CSC_LIB) $(CSC_EXTRA_FLAGS) -out:$@ $(PKGS) $(REFS) $(ASSEMBLY_SOURCES)

assemblydir = $(pkglibdir)
assembly_DATA =			\
	$(ASSEMBLY)		

EXTRA_DIST = 			\
	$(ASSEMBLY_SOURCES)

CLEANFILES =			\
	$(ASSEMBLY)		\
	$(ASSEMBLY).mdb
