tomboylibdir = $(pkglibdir)
tomboylib_LTLIBRARIES=libgtkspell.la

AM_CFLAGS=@GTKSPELL_CFLAGS@ -DG_LOG_DOMAIN=\"gtkspell\" -DLOCALEDIR=\""$(datadir)/locale"\"
libgtkspell_la_LDFLAGS=-export-dynamic -module -avoid-version
libgtkspell_la_LIBADD=@GTKSPELL_LIBS@ @SPELLER_LIB@
libgtkspell_la_SOURCES=gtkspell.c deprecated.c gtkspell.h

install-data-hook:
	rm $(DESTDIR)/$(tomboylibdir)/libgtkspell.a
	rm $(DESTDIR)/$(tomboylibdir)/libgtkspell.la

uninstall-info-am:
	rm $(DESTDIR)/$(tomboylibdir)/libgtkspell.so

