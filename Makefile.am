
SUBDIRS = data libtomboy Tomboy test po help

EXTRA_DIST = 			\
	gnome-doc-utils.make	\
	\
	intltool-extract.in     \
	intltool-merge.in       \
	intltool-update.in	\
	\
	tomboy.spec.in		\
	tomboy.spec			\
	\
	MAINTAINERS

DISTCLEANFILES = 			\
	gnome-doc-utils.make		\
	\
	intltool-extract        	\
	intltool-merge          	\
	intltool-update         	\
	po/.intltool-merge-cache

DISTCHECK_CONFIGURE_FLAGS = --disable-schemas-install --disable-scrollkeeper

# Ignore scrollkeeper issues for now.  @#*$& scrollkeeper (from Evince)
distuninstallcheck_listfiles = find . -type f -print | grep -v scrollkeeper | grep -v /share/gnome/help/ | grep -v \.omf

test: all
	$(MAKE) -C test test

.PHONY: test
