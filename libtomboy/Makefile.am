
INCLUDES = 				\
	-I$(top_srcdir)			\
	-DG_LOG_DOMAIN=\"libtomboy\"	\
	-DEGG_COMPILATION 		\
	-DGTK_DISABLE_DEPRECATED 	\
	-DGDK_DISABLE_DEPRECATED 	\
	-DG_DISABLE_DEPRECATED		\
	$(LIBTOMBOY_CFLAGS)

tomboylibdir = $(pkglibdir)
tomboylib_LTLIBRARIES = libtomboy.la

libtomboy_la_SOURCES =		\
	gedit-print.h		\
	gedit-print.c		\
	gtksourceprintjob.h	\
	gtksourceprintjob.c	\
	tomboykeybinder.h	\
	tomboykeybinder.c	\
	tomboyutil.h		\
	tomboyutil.c		\
	eggaccelerators.h	\
	eggaccelerators.c	\
	eggtrayicon.h		\
	eggtrayicon.c

libtomboy_la_LDFLAGS = -export-dynamic -module -avoid-version
libtomboy_la_LIBADD = $(LIBTOMBOY_LIBS)

maintainer-clean-local:
	rm -f Makefile.in
