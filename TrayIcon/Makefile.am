
SUBDIRS =       libtrayicon

EXTRA_DIST = $(srcdir)/generated/*.cs $(srcdir)/libtrayicon-sources.xml $(srcdir)/Egg.metadata

ASSEMBLY =	TrayIcon
LIBRARY =	$(ASSEMBLY).dll
API =		libtrayicon-api.xml

INCLUDE_API =	$(GTK_SHARP_PREFIX)/share/gapi/art-api.xml	\
		$(GTK_SHARP_PREFIX)/share/gapi/pango-api.xml	\
		$(GTK_SHARP_PREFIX)/share/gapi/atk-api.xml	\
		$(GTK_SHARP_PREFIX)/share/gapi/gnome-api.xml	\
		$(GTK_SHARP_PREFIX)/share/gapi/gtk-api.xml	\
		$(GTK_SHARP_PREFIX)/share/gapi/gdk-api.xml	\
		$(GTK_SHARP_PREFIX)/share/gapi/gdk-symbols.xml

ASSEMBLIES =	-r gtk-sharp.dll	\
		-r glib-sharp.dll	\
		-r gnome-sharp.dll	\
		-r gdk-sharp.dll	\
		-r pango-sharp.dll	\
		-r art-sharp.dll

all: $(LIBRARY)

$(LIBRARY):
	mcs --unsafe --target library --recurse $(srcdir)/generated/*.cs -o $(LIBRARY) $(TOMBOY_LIBS)

generated-stamp: $(API)
	gapi-fixup --api=$(API) --metadata=Egg.metadata && \
	gapi-codegen --include $(INCLUDE_API) --generate $(API) \
	--outdir=generated --customdir=. --assembly-name=$(ASSEMBLY) \
	&& touch generated-stamp

$(API):
	PERLLIB=/usr/share/perl5 gapi-parser libtrayicon-sources.xml

install-exec-local: $(LIBRARY)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_PROGRAM) $(LIBRARY) $(DESTDIR)$(pkglibdir)

uninstall-local:
	rm -f $(DESTDIR)$(pkglibdir)/$(LIBRARY)

clean-local:
	rm -f generated-stamp
	rm -f $(LIBRARY)
	rm -f $(API)

maintainer-clean-local:
	rm -rf $(COMMON_MAINTAINER_CLEAN_FILES)

