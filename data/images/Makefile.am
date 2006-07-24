
pixmapdir = $(datadir)/pixmaps
pixmap_DATA = tomboy.png

hicolordir = $(datadir)/icons/hicolor

tango_icons = 				\
	tomboy-16.png			\
	tomboy-22.png			\
	tomboy.png			\
	tomboy.svg

install-data-local:
	@-$(mkinstalldirs) $(DESTDIR)$(hicolordir)/scalable/apps
	$(INSTALL_DATA) $(srcdir)/tomboy.svg $(DESTDIR)$(hicolordir)/scalable/apps/tomboy.svg
	@-$(mkinstalldirs) $(DESTDIR)$(hicolordir)/16x16/apps
	$(INSTALL_DATA) $(srcdir)/tomboy-16.png $(DESTDIR)$(hicolordir)/16x16/apps/tomboy.png
	@-$(mkinstalldirs) $(DESTDIR)$(hicolordir)/22x22/apps
	$(INSTALL_DATA) $(srcdir)/tomboy-22.png $(DESTDIR)$(hicolordir)/22x22/apps/tomboy.png
	@-$(mkinstalldirs) $(DESTDIR)$(hicolordir)/48x48/apps
	$(INSTALL_DATA) $(srcdir)/tomboy.png $(DESTDIR)$(hicolordir)/48x48/apps/tomboy.png

gtk_update_icon_cache = gtk-update-icon-cache -f -t $(datadir)/icons/hicolor

install-data-hook:
	@-if test -z "$(DESTDIR)"; then \
		echo "Updating Gtk icon cache."; \
		$(gtk_update_icon_cache); \
	else \
		echo "*** Icon cache not updated.  After install, run this:"; \
		echo "***   $(gtk_update_icon_cache)"; \
	fi

uninstall-hook:
	rm -f $(DESTDIR)$(hicolordir)/scalable/apps/tomboy.svg
	rm -f $(DESTDIR)$(hicolordir)/16x16/apps/tomboy.png
	rm -f $(DESTDIR)$(hicolordir)/22x22/apps/tomboy.png
	rm -f $(DESTDIR)$(hicolordir)/48x48/apps/tomboy.png


noinst_DATA = 			\
	gnome-searchtool.png	\
	stock_mail.png

EXTRA_DIST = $(noinst_DATA) $(pixmap_DATA) $(tango_icons)
