
@INTLTOOL_DESKTOP_RULE@

desktop_in_files = tomboy.desktop.in
desktop_files    = $(desktop_in_files:.desktop.in=.desktop)

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


if ENABLE_DBUS
com.beatniksoftware.Tomboy.service: com.beatniksoftware.Tomboy.service.in
	sed -e "s|\@bindir\@|$(bindir)|g"	\
	    -e "s|\@wrapper\@|tomboy|g"		\
	    < $^ > $@

dbusservicedir = $(libdir)/dbus-1.0/services
dbusservice_DATA = com.beatniksoftware.Tomboy.service
endif


serverdir       = $(libdir)/bonobo/servers
server_in_files = GNOME_TomboyApplet.server.in
server_DATA     = $(server_in_files:.server.in=.server)

$(server_in_files): $(server_in_files:.server.in=.server.in.in)
	sed -e "s|\@bindir\@|$(bindir)|g"	\
	    -e "s|\@wrapper\@|tomboy|g"		\
	    < $^ > $@

@INTLTOOL_SERVER_RULE@


man_MANS = tomboy.1


uidir = $(datadir)/gnome-2.0/ui
ui_DATA = GNOME_TomboyApplet.xml


noinst_DATA = Start_Here.note


EXTRA_DIST = 					\
	$(desktop_in_files)     		\
	$(desktop_files)        		\
	$(schemas_in_files)     		\
	$(schemas_DATA)				\
	$(man_MANS)				\
	$(noinst_DATA)				\
	GNOME_TomboyApplet.server.in.in		\
	GNOME_TomboyApplet.xml

DISTCLEANFILES = 				\
	$(desktop_files)        		\
	$(schemas_DATA)				\
	$(server_in_files) 			\
	$(server_DATA)				\
	com.beatniksoftware.Tomboy.service
