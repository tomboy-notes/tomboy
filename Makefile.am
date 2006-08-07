
SUBDIRS = data libtomboy Tomboy test po help

EXTRA_DIST = 			\
	gnome-doc-utils.make	\
	\
	intltool-extract.in     \
	intltool-merge.in       \
	intltool-update.in	\
	\
	tomboy.spec.in		\
	tomboy.spec

DISTCLEANFILES = 			\
	gnome-doc-utils.make		\
	\
	intltool-extract        	\
	intltool-merge          	\
	intltool-update         	\
	po/.intltool-merge-cache

DISTCHECK_CONFIGURE_FLAGS = --disable-scrollkeeper

test: all
	$(MAKE) -C test test

.PHONY: test
