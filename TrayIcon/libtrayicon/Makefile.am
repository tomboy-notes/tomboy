
TRAY_ICON_CFLAGS = $(GDK20_CFLAGS) $(ATK_CFLAGS)
TRAY_ICON_LIBS = $(GDK20_LIBS) $(ATK_LIBS)

EXTRA_DIST = $(srcdir)/eggtrayicon.h

INCLUDES = \
  $(TRAY_ICON_CFLAGS) \
  -DEGG_COMPILATION \
  -DGTK_DISABLE_DEPRECATED \
  -DGDK_DISABLE_DEPRECATED \
  -DG_DISABLE_DEPRECATED

tomboylibdir = $(DESTDIR)$(pkglibdir)
tomboylib_LTLIBRARIES = libtrayicon.la

#libtrayicon_la_LIBADD = \
#	$(TRAY_ICON_LIBS) \
#	$(top_builddir)/libegg/util/libeggutil.la

libtrayicon_la_SOURCES = \
  eggtrayicon.c		
#  eggstatusicon.c	\
#  eggtraymanager.c

libtrayicon_la_LDFLAGS = -export-dynamic -module -avoid-version

#noinst_HEADERS = \
#  eggtrayicon.h	 
#  eggstatusicon.h\
#  eggtraymanager.h

### FIXME: This breaks distcheck.
install-data-hook:
	rm $(tomboylibdir)/libtrayicon.a
	rm $(tomboylibdir)/libtrayicon.la

maintainer-clean-local:
	rm -f Makefile.in
