
INCLUDES = 				\
	$(LIBTRAYICON_CFLAGS) 		\
	-DEGG_COMPILATION 		\
	-DGTK_DISABLE_DEPRECATED 	\
	-DGDK_DISABLE_DEPRECATED 	\
	-DG_DISABLE_DEPRECATED

tomboylibdir = $(pkglibdir)
tomboylib_LTLIBRARIES = libtrayicon.la

libtrayicon_la_SOURCES =	\
	tomboykeybinder.h	\
	tomboykeybinder.c	\
	eggaccelerators.h	\
	eggaccelerators.c	\
	eggtrayicon.h		\
	eggtrayicon.c

libtrayicon_la_LDFLAGS = -export-dynamic -module -avoid-version
libtrayicon_la_LIBADD = $(LIBTRAYICON_LIBS)

install-data-hook:
	rm $(DESTDIR)/$(tomboylibdir)/libtrayicon.a
	rm $(DESTDIR)/$(tomboylibdir)/libtrayicon.la

uninstall-local:
	rm $(DESTDIR)/$(tomboylibdir)/libtrayicon.so

maintainer-clean-local:
	rm -f Makefile.in
