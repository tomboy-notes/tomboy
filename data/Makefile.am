
SUBDIRS = icons

@INTLTOOL_DESKTOP_RULE@

desktop_in_files = tomboy.desktop.in
desktop_files    = $(desktop_in_files:.desktop.in=.desktop)

desktopdir   = $(datadir)/applications
desktop_DATA = $(desktop_files)


pkgconfigdir = $(libdir)/pkgconfig
pkgconfig_in_files = tomboy-addins.pc.in
pkgconfig_DATA = $(pkgconfig_in_files:.pc.in=.pc)


schemasdir	 = $(GCONF_SCHEMA_FILE_DIR)
schemas_in_files = tomboy.schemas.in
schemas_DATA     = $(schemas_in_files:.schemas.in=.schemas)

@INTLTOOL_SCHEMAS_RULE@

if GCONF_SCHEMAS_INSTALL
install-data-local:
	GCONF_CONFIG_SOURCE=$(GCONF_SCHEMA_CONFIG_SOURCE) \
	$(GCONFTOOL) --makefile-install-rule $(schemas_DATA)
endif


dbusservicedir       = $(DBUS_SERVICE_DIR)
dbusservice_in_files = org.gnome.Tomboy.service.in
dbusservice_DATA     = $(dbusservice_in_files:.service.in=.service)

$(dbusservice_DATA): $(dbusservice_in_files) Makefile
	sed -e "s|\@bindir\@|$(bindir)|g"	\
	    -e "s|\@wrapper\@|tomboy|g"		\
	    < $< > $@


if ENABLE_PANEL_APPLET
serverdir       = $(PANELAPPLET_SERVER_DIR)
server_in_files = GNOME_TomboyApplet.server.in
server_DATA     = $(server_in_files:.server.in=.server)

$(server_in_files): $(server_in_files:.server.in=.server.in.in) Makefile
	sed -e "s|\@bindir\@|$(bindir)|g"	\
	    -e "s|\@wrapper\@|tomboy-panel|g"		\
	    < $< > $@
endif

@INTLTOOL_SERVER_RULE@


man_MANS = tomboy.1

mimedir = $(datadir)/mime/packages
mime_DATA = tomboy.xml

noinst_DATA = 			\
	GNOME_TomboyApplet.xml


EXTRA_DIST = 					\
	$(desktop_in_files)     		\
	$(pkgconfig_in_files)			\
	$(schemas_in_files)     		\
	$(man_MANS)				\
	$(noinst_DATA)				\
	GNOME_TomboyApplet.server.in.in		\
	$(dbusservice_in_files)				\
	tomboy-note-clipboard-html.xsl		\
	UIManagerLayout.xml		\
	tomboy.xml


DISTCLEANFILES = 				\
	$(desktop_files)       			\
	$(pkgconfig_DATA)			\
	$(schemas_DATA)				\
	$(server_in_files) 			\
	$(server_DATA)				\
	$(dbusservice_DATA)

install-data-hook:
if ENABLE_UPDATE_MIMEDB
	$(UPDATE_MIME_DATABASE) "$(DESTDIR)$(datadir)/mime"
endif

uninstall-hook:
if ENABLE_UPDATE_MIMEDB
	$(UPDATE_MIME_DATABASE) "$(DESTDIR)$(datadir)/mime"
endif

