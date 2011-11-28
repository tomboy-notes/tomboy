NULL =

public_icons_themes = \
	hicolor	\
	$(NULL)

public_icons = \
	hicolor_apps_16x16_tomboy.png \
	hicolor_apps_22x22_tomboy.png \
	hicolor_apps_24x24_tomboy.png \
	hicolor_apps_32x32_tomboy.png \
	hicolor_apps_48x48_tomboy.png \
	hicolor_apps_256x256_tomboy.png \
	hicolor_apps_scalable_tomboy.svg \
	hicolor_mimetypes_16x16_application-x-note.png \
	hicolor_mimetypes_22x22_application-x-note.png \
	hicolor_mimetypes_48x48_application-x-note.png \
	$(NULL)

private_icons = \
	hicolor_actions_16x16_notebook-new.png \
	hicolor_actions_16x16_note-new.png \
	hicolor_actions_22x22_filter-note-all.png \
	hicolor_actions_22x22_filter-note-unfiled.png \
	hicolor_actions_22x22_notebook-new.png \
	hicolor_actions_22x22_note-new.png \
	hicolor_actions_24x24_filter-note-all.png \
	hicolor_actions_24x24_filter-note-unfiled.png \
	hicolor_actions_24x24_notebook-new.png \
	hicolor_actions_24x24_note-new.png \
	hicolor_actions_48x48_notebook-new.png \
	hicolor_actions_scalable_notebook-new.svg \
	hicolor_places_16x16_notebook.png \
	hicolor_places_16x16_note.png \
	hicolor_places_22x22_notebook.png \
	hicolor_places_22x22_note.png \
	hicolor_places_24x24_notebook.png \
	hicolor_places_24x24_note.png \
	hicolor_status_16x16_pin-active.png \
	hicolor_status_16x16_pin-down.png \
	hicolor_status_16x16_pin-up.png \
	$(NULL)

noinst_DATA = \
	hicolor_apps_16x16_tomboy.xcf.bz2 \
	hicolor_apps_22x22_tomboy.xcf.bz2 \
	hicolor_actions_16x16_notebook-new.svg \
	hicolor_actions_16x16_note-new.svg \
	hicolor_actions_22x22_filter-note-all.xcf.bz2 \
	hicolor_actions_22x22_filter-note-unfiled.svg \
	hicolor_actions_22x22_notebook-new.svg \
	hicolor_actions_22x22_note-new.svg \
	hicolor_places_16x16_notebook.svg \
	hicolor_places_16x16_note.xcf.bz2 \
	hicolor_places_22x22_notebook.svg \
	hicolor_places_22x22_note.svg \
	$(NULL)

EXTRA_DIST = \
	$(public_icons)		\
	$(private_icons)	\
	$(noinst_DATA)		\
	$(NULL)

###############################################################################

gtk_update_icon_cache = gtk-update-icon-cache -f -t

update-icon-cache:
	@-if test -z "$(DESTDIR)"; then \
		echo "Updating Gtk icon cache."; \
		for theme in $(public_icons_themes); do \
			$(gtk_update_icon_cache) $(datadir)/icons/$$theme; \
		done; \
	else \
		echo "*** Icon cache not updated.  After (un)install, run this:"; \
		for theme in $(public_icons_themes); do \
			echo "***   $(gtk_update_icon_cache) $(datadir)/icons/$$theme"; \
		done; \
	fi

install-icons:
	for icon in $(public_icons); do \
		THEME=`echo $$icon | cut -d_ -f1`; \
		CONTEXT=`echo $$icon | cut -d_ -f2`; \
		SIZE=`echo $$icon | cut -d_ -f3`; \
		ICONFILE=`echo $$icon | cut -d_ -f4`; \
		mkdir -p $(DESTDIR)$(datadir)/icons/$$THEME/$$SIZE/$$CONTEXT; \
		$(INSTALL_DATA) $(srcdir)/$$icon $(DESTDIR)$(datadir)/icons/$$THEME/$$SIZE/$$CONTEXT/$$ICONFILE; \
	done; \
	for icon in $(private_icons); do \
		THEME=`echo $$icon | cut -d_ -f1`; \
		CONTEXT=`echo $$icon | cut -d_ -f2`; \
		SIZE=`echo $$icon | cut -d_ -f3`; \
		ICONFILE=`echo $$icon | cut -d_ -f4`; \
		mkdir -p $(DESTDIR)$(pkgdatadir)/icons/$$THEME/$$SIZE/$$CONTEXT; \
		$(INSTALL_DATA) $(srcdir)/$$icon $(DESTDIR)$(pkgdatadir)/icons/$$THEME/$$SIZE/$$CONTEXT/$$ICONFILE; \
	done

uninstall-icons:
	-for icon in $(public_icons); do \
		THEME=`echo $$icon | cut -d_ -f1`; \
		CONTEXT=`echo $$icon | cut -d_ -f2`; \
		SIZE=`echo $$icon | cut -d_ -f3`; \
		ICONFILE=`echo $$icon | cut -d_ -f4`; \
		rm -f $(DESTDIR)$(datadir)/icons/$$THEME/$$SIZE/$$CONTEXT/$$ICONFILE; \
	done; \
	for icon in $(private_icons); do \
		THEME=`echo $$icon | cut -d_ -f1`; \
		CONTEXT=`echo $$icon | cut -d_ -f2`; \
		SIZE=`echo $$icon | cut -d_ -f3`; \
		ICONFILE=`echo $$icon | cut -d_ -f4`; \
		rm -f $(DESTDIR)$(pkgdatadir)/icons/$$THEME/$$SIZE/$$CONTEXT/$$ICONFILE; \
	done

install-data-local: install-icons update-icon-cache

uninstall-local: uninstall-icons update-icon-cache

