
SUBDIRS = data libtomboy Tomboy test po 

EXTRA_DIST = 			\
        intltool-extract.in     \
        intltool-merge.in       \
        intltool-update.in	\
	tomboy.spec.in		\
	tomboy.spec

DISTCLEANFILES = 			\
	intltool-extract        	\
	intltool-merge          	\
	intltool-update         	\
	po/.intltool-merge-cache

test: all
	$(MAKE) -C test test

.PHONY: test
