include $(top_srcdir)/Makefile.include

ASSEMBLY_NAME = Mono.Addins

ASSEMBLY_SOURCES =								\
	$(srcdir)/AssemblyInfo.cs 						\
	$(srcdir)/Mono.Addins.Database/AddinDatabase.cs 			\
	$(srcdir)/Mono.Addins.Database/AddinHostIndex.cs 			\
	$(srcdir)/Mono.Addins.Database/AddinScanFolderInfo.cs 			\
	$(srcdir)/Mono.Addins.Database/AddinScanner.cs 				\
	$(srcdir)/Mono.Addins.Database/AddinScanResult.cs 			\
	$(srcdir)/Mono.Addins.Database/AddinUpdateData.cs 			\
	$(srcdir)/Mono.Addins.Database/DatabaseConfiguration.cs 		\
	$(srcdir)/Mono.Addins.Database/FileDatabase.cs 				\
	$(srcdir)/Mono.Addins.Database/ProcessProgressStatus.cs 		\
	$(srcdir)/Mono.Addins.Database/SetupProcess.cs 				\
	$(srcdir)/Mono.Addins.Database/Util.cs 					\
	$(srcdir)/Mono.Addins.Description/AddinDependency.cs 			\
	$(srcdir)/Mono.Addins.Description/AddinDescription.cs 			\
	$(srcdir)/Mono.Addins.Description/AssemblyDependency.cs 		\
	$(srcdir)/Mono.Addins.Description/ConditionTypeDescription.cs 		\
	$(srcdir)/Mono.Addins.Description/ConditionTypeDescriptionCollection.cs \
	$(srcdir)/Mono.Addins.Description/Dependency.cs 			\
	$(srcdir)/Mono.Addins.Description/DependencyCollection.cs 		\
	$(srcdir)/Mono.Addins.Description/Extension.cs 				\
	$(srcdir)/Mono.Addins.Description/ExtensionCollection.cs 		\
	$(srcdir)/Mono.Addins.Description/ExtensionNodeDescription.cs 		\
	$(srcdir)/Mono.Addins.Description/ExtensionNodeDescriptionCollection.cs \
	$(srcdir)/Mono.Addins.Description/ExtensionNodeSet.cs 			\
	$(srcdir)/Mono.Addins.Description/ExtensionNodeSetCollection.cs 	\
	$(srcdir)/Mono.Addins.Description/ExtensionNodeType.cs 			\
	$(srcdir)/Mono.Addins.Description/ExtensionNodeTypeCollection.cs 	\
	$(srcdir)/Mono.Addins.Description/ExtensionPoint.cs 			\
	$(srcdir)/Mono.Addins.Description/ExtensionPointCollection.cs 		\
	$(srcdir)/Mono.Addins.Description/ModuleDescription.cs 			\
	$(srcdir)/Mono.Addins.Description/ModuleDescriptionCollection.cs 	\
	$(srcdir)/Mono.Addins.Description/NativeDependency.cs 			\
	$(srcdir)/Mono.Addins.Description/NodeTypeAttribute.cs 			\
	$(srcdir)/Mono.Addins.Description/NodeTypeAttributeCollection.cs 	\
	$(srcdir)/Mono.Addins.Description/ObjectDescription.cs 			\
	$(srcdir)/Mono.Addins.Description/ObjectDescriptionCollection.cs 	\
	$(srcdir)/Mono.Addins.Serialization/BinaryXmlReader.cs 			\
	$(srcdir)/Mono.Addins.Serialization/BinaryXmlTypeMap.cs 		\
	$(srcdir)/Mono.Addins.Serialization/BinaryXmlWriter.cs 			\
	$(srcdir)/Mono.Addins.Serialization/IBinaryXmlElement.cs 		\
	$(srcdir)/Mono.Addins/Addin.cs 						\
	$(srcdir)/Mono.Addins/AddinAttribute.cs 				\
	$(srcdir)/Mono.Addins/AddinDependencyAttribute.cs 			\
	$(srcdir)/Mono.Addins/AddinErrorEventArgs.cs 				\
	$(srcdir)/Mono.Addins/AddinEventArgs.cs 				\
	$(srcdir)/Mono.Addins/AddinInfo.cs 					\
	$(srcdir)/Mono.Addins/AddinManager.cs 					\
	$(srcdir)/Mono.Addins/AddinRegistry.cs 					\
	$(srcdir)/Mono.Addins/AddinRootAttribute.cs 				\
	$(srcdir)/Mono.Addins/AddinSessionService.cs 				\
	$(srcdir)/Mono.Addins/ConditionType.cs 					\
	$(srcdir)/Mono.Addins/ConsoleProgressStatus.cs 				\
	$(srcdir)/Mono.Addins/ExtensionAttribute.cs 				\
	$(srcdir)/Mono.Addins/ExtensionContext.cs 				\
	$(srcdir)/Mono.Addins/ExtensionNode.cs 					\
	$(srcdir)/Mono.Addins/ExtensionNodeAttribute.cs 			\
	$(srcdir)/Mono.Addins/ExtensionNodeChildAttribute.cs 			\
	$(srcdir)/Mono.Addins/ExtensionNodeList.cs 				\
	$(srcdir)/Mono.Addins/ExtensionPointAttribute.cs 			\
	$(srcdir)/Mono.Addins/ExtensionTree.cs 					\
	$(srcdir)/Mono.Addins/GettextCatalog.cs 				\
	$(srcdir)/Mono.Addins/IAddinInstaller.cs 				\
	$(srcdir)/Mono.Addins/InstanceExtensionNode.cs 				\
	$(srcdir)/Mono.Addins/IProgressStatus.cs 				\
	$(srcdir)/Mono.Addins/MissingDependencyException.cs 			\
	$(srcdir)/Mono.Addins/NodeAttributeAttribute.cs 			\
	$(srcdir)/Mono.Addins/NodeElement.cs 					\
	$(srcdir)/Mono.Addins/RuntimeAddin.cs 					\
	$(srcdir)/Mono.Addins/TreeNode.cs 					\
	$(srcdir)/Mono.Addins/TreeNodeCollection.cs 				\
	$(srcdir)/Mono.Addins/TypeExtensionNode.cs 				\
	$(srcdir)/Mono.Addins/TypeExtensionPointAttribute.cs

REFS =			\
	-r:Mono.Posix	\
	-r:System	\
	-r:System.Xml

PKGS =

CSC_EXTRA_FLAGS =  -noconfig -codepage:utf8 -warn:4 -debug -d:DEBUG "-main:Mono.Addins.Database.SetupProcess"

ASSEMBLY = $(ASSEMBLY_NAME).dll

all: $(ASSEMBLY)

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(CSC) $(CSC_EXTRA_FLAGS) -out:$@ $(PKGS) $(REFS) $(ASSEMBLY_SOURCES)

assemblydir = $(pkglibdir)
assembly_DATA =			\
	$(ASSEMBLY)		\
	$(ASSEMBLY).config

EXTRA_DIST = 			\
	$(ASSEMBLY_SOURCES)	\
	$(ASSEMBLY).config

CLEANFILES =			\
	$(ASSEMBLY)		\
	$(ASSEMBLY).mdb
