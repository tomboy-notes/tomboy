
SUBDIRS = TrayIcon Tomboy po 

desktop_in_files = tomboy.desktop.in
desktop_files    = $(desktop_in_files:.desktop.in=.desktop)
@INTLTOOL_DESKTOP_RULE@

applicationsdir   = $(datadir)/applications
applications_DATA = $(desktop_files)

schemasdir	 = $(GCONF_SCHEMA_FILE_DIR)
schemas_in_files = tomboy.schemas.in
schemas_DATA     = $(schemas_in_files:.schemas.in=.schemas)
@INTLTOOL_SCHEMAS_RULE@

if GCONF_SCHEMAS_INSTALL
install-data-local:
	GCONF_CONFIG_SOURCE=$(GCONF_SCHEMA_CONFIG_SOURCE) \
	$(GCONFTOOL) --makefile-install-rule $(schemas_DATA)
	$(GCONFTOOL) --shutdown	
endif

INTLTOOL =                      \
        intltool-extract.in     \
        intltool-merge.in       \
        intltool-update.in

EXTRA_DIST = 			\
	$(desktop_in_files)     \
	$(desktop_files)        \
	$(schemas_in_files)     \
	$(schemas_DATA)         \
	$(INTLTOOL)		\
	tomboy.spec

DISTCLEANFILES = 			\
	intltool-extract        	\
	intltool-merge          	\
	intltool-update         	\
	po/.intltool-merge-cache	\
	$(desktop_files)        	\
	$(schemas_DATA)
