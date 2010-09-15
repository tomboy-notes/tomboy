
if HAS_GTK_2_20
EXTRA_FLAGS = 				\
	-DHAS_GTK_2_20			\
	-DGSEAL_ENABLE
endif

INCLUDES = 				\
	-I$(top_srcdir)			\
	-DG_LOG_DOMAIN=\"libtomboy\"	\
	-DEGG_COMPILATION 		\
	$(LIBTOMBOY_CFLAGS)		\
	$(EXTRA_FLAGS)

tomboylibdir = $(pkglibdir)
tomboylib_LTLIBRARIES = libtomboy.la

libtomboy_la_SOURCES =		\
	tomboykeybinder.h	\
	tomboykeybinder.c	\
	tomboyutil.h		\
	tomboyutil.c		\
	eggaccelerators.h	\
	eggaccelerators.c

libtomboy_la_CFLAGS = $(X_CFLAGS)
libtomboy_la_LDFLAGS = -export-dynamic -module -avoid-version
libtomboy_la_LIBADD = $(LIBTOMBOY_LIBS) $(X_LIBS)

maintainer-clean-local:
	rm -f Makefile.in
