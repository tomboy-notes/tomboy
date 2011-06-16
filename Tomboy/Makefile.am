include $(top_srcdir)/Makefile.include

SUBDIRS = . Addins

TARGET_NAME = Tomboy.exe
TARGET = $(top_builddir)/bin/$(TARGET_NAME)
WRAPPER = tomboy
PANEL_WRAPPER = tomboy-panel

DBUS_CSFILES = 				\
	$(srcdir)/IRemoteControl.cs	\
	$(srcdir)/RemoteControl.cs	\
	$(srcdir)/RemoteControlProxy.cs

OSX_CSFILES =					\
	$(srcdir)/MacApplication.cs		\
	$(srcdir)/MacFactory.cs			\
	$(srcdir)/WindowsApplication.cs		\
	$(srcdir)/XmlPreferencesClient.cs

if ENABLE_OSX
OSX_CSFLAGS = -define:MAC
OSX_USED_CSFILES = $(OSX_CSFILES)
OSX_LIBS =					\
	-r:$(top_srcdir)/macbin/ige-mac-integration-sharp.dll
endif

WIN_CSFILES =					\
	$(srcdir)/JumpListManager.cs		\
	$(srcdir)/ManagedWinapi.EventDispatchingNativeWindow.cs	\
	$(srcdir)/ManagedWinapi.Hotkey.cs	\
	$(srcdir)/WindowsApplication.cs		\
	$(srcdir)/WindowsFactory.cs		\
	$(srcdir)/WindowsKeybinder.cs		\
	$(srcdir)/XmlPreferencesClient.cs

if ENABLE_WIN
WIN_CSFLAGS = -define:WIN32
WIN_USED_CSFILES = $(WIN_CSFILES)
WIN_LIBS =					\
	-r:System.Windows.Forms.dll
endif

PANELAPPLET_CSFILES =					\
	$(srcdir)/Applet.cs

GNOME_CSFILES =								\
	$(srcdir)/GConf.PropertyEditors/PropertyEditor.cs		\
	$(srcdir)/GConf.PropertyEditors/PropertyEditorBool.cs		\
	$(srcdir)/GConf.PropertyEditors/PropertyEditorEntry.cs		\
	$(srcdir)/GConf.PropertyEditors/PropertyEditorToggleButton.cs	\
	$(srcdir)/GConfPreferencesClient.cs				\
	$(srcdir)/GnomeApplication.cs					\
	$(srcdir)/GnomeFactory.cs					\
	$(srcdir)/GnomeSession.cs					\
	$(srcdir)/XKeybinder.cs						\
	$(srcdir)/gtk-sharp-beans/*.cs

if ENABLE_PANEL_APPLET
PANEL_APPLET_CSFLAGS = -define:PANEL_APPLET
PANELAPPLET_USED_CSFILES = $(PANELAPPLET_CSFILES)
endif

if ENABLE_GNOME
DBUS_CSFLAGS = -define:ENABLE_DBUS
DBUS_USED_CSFILES = $(DBUS_CSFILES)
GNOME_USED_CSFILES = $(GNOME_CSFILES)
endif

if FIXED_GTKSPELL
FIXED_GTKSPELL_CSFLAGS = -define:FIXED_GTKSPELL
endif

if HAS_GTK_2_16
HAS_GTK_2_16_FLAGS = -define:HAS_GTK_2_16
endif

CSFLAGS = 				\
	-debug				\
	-define:DEBUG 			\
	-define:NET_2_0			\
	-unsafe 			\
	-target:exe 			\
	$(DBUS_CSFLAGS) 		\
	$(OSX_CSFLAGS) 		\
	$(WIN_CSFLAGS) 		\
	$(PANEL_APPLET_CSFLAGS)		\
	$(FIXED_GTKSPELL_CSFLAGS) \
	$(HAS_GTK_2_16_FLAGS) \
	$(CSC_DEFINES)

CSFILES = 					\
	$(srcdir)/Tomboy.cs 			\
	$(srcdir)/Search.cs 			\
	$(srcdir)/AbstractAddin.cs		\
	$(srcdir)/ActionManager.cs		\
	$(srcdir)/AddinManager.cs		\
	$(srcdir)/AddinPreferenceFactory.cs	\
	$(srcdir)/ApplicationAddin.cs		\
	$(srcdir)/Contrast.cs		\
	$(srcdir)/ExportAllApplicationAddin.cs		\
	$(srcdir)/Keybinder.cs			\
	$(srcdir)/Logger.cs			\
	$(srcdir)/NativeApplication.cs		\
	$(srcdir)/Note.cs 			\
	$(srcdir)/NoteAddin.cs			\
	$(srcdir)/NoteEditor.cs			\
	$(srcdir)/NoteManager.cs 		\
	$(srcdir)/NoteWindow.cs 		\
	$(srcdir)/NoteBuffer.cs 		\
	$(srcdir)/NoteRenameDialog.cs 		\
	$(srcdir)/NoteTag.cs 			\
	$(srcdir)/PlatformFactory.cs		\
	$(srcdir)/Preferences.cs		\
	$(srcdir)/PreferencesClient.cs		\
	$(srcdir)/PreferencesDialog.cs		\
	$(srcdir)/PreferenceTabAddin.cs		\
	$(srcdir)/PrefsKeybinder.cs		\
	$(srcdir)/RecentChanges.cs		\
	$(srcdir)/RecentTreeView.cs		\
	$(srcdir)/Services.cs			\
	$(srcdir)/Tag.cs			\
	$(srcdir)/TagButton.cs			\
	$(srcdir)/TagManager.cs			\
	$(srcdir)/Tray.cs 			\
	$(srcdir)/Trie.cs			\
	$(srcdir)/Undo.cs 			\
	$(srcdir)/Utils.cs			\
	$(srcdir)/Watchers.cs			\
	$(srcdir)/Notebooks/*.cs		\
	$(srcdir)/Synchronization/*.cs		\
	\
	$(srcdir)/Hyena/*.cs			\
	\
	$(srcdir)/System.Web/*.cs			\
	\
	$(GNOME_USED_CSFILES)			\
	$(WIN_USED_CSFILES)			\
	$(OSX_USED_CSFILES)			\
	\
	$(PANELAPPLET_USED_CSFILES)			\
	\
	$(srcdir)/Gnome.Keyring/*.cs		\
	\
	$(DBUS_USED_CSFILES)	


ASSEMBLIES = 		\
	$(GTKSHARP_LIBS) 	\
	$(GNOME_LIBS) 	\
	-r:Mono.Posix	\
	$(DBUS_LIBS)	\
	$(LINK_MONO_ADDINS)	\
	$(WIN_LIBS)	\
	$(OSX_LIBS)

RESOURCES = 										\
	-resource:$(top_srcdir)/data/GNOME_TomboyApplet.xml,GNOME_TomboyApplet.xml	\
	-resource:$(top_srcdir)/data/UIManagerLayout.xml	\
	-resource:$(top_srcdir)/data/tomboy-note-clipboard-html.xsl,tomboy-note-clipboard-html.xsl	\
	-resource:$(top_srcdir)/data/icons/hicolor_apps_48x48_tomboy.png,tomboy.png			\
	-resource:$(top_srcdir)/data/icons/hicolor_places_22x22_note.png,note.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_actions_16x16_note-new.png,note-new.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_actions_22x22_filter-note-all.png,filter-note-all.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_actions_22x22_filter-note-unfiled.png,filter-note-unfiled.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_places_22x22_notebook.png,notebook.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_actions_48x48_notebook-new.png,notebook-new.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_status_16x16_pin-down.png,pin-down.png				\
	-resource:$(top_srcdir)/data/icons/hicolor_status_16x16_pin-up.png,pin-up.png		\
	-resource:$(top_srcdir)/data/icons/hicolor_status_16x16_pin-active.png,pin-active.png			\
	-resource:$(srcdir)/Tomboy.addin.xml


$(TARGET).mdb: $(TARGET)

$(TARGET): $(CSFILES) Defines.cs
	mkdir -p `dirname $(TARGET)` && $(CSC) -out:$@ $(CSFLAGS) $^ $(ASSEMBLIES) $(RESOURCES)

tomboylibdir = $(pkglibdir)
tomboylib_DATA = $(TARGET) $(TARGET).config $(TARGET).mdb


bin_SCRIPTS = $(WRAPPER)
if ENABLE_PANEL_APPLET
bin_SCRIPTS += $(PANEL_WRAPPER)
endif

$(WRAPPER): $(srcdir)/$(WRAPPER).in Makefile
	mkdir -p `dirname $(WRAPPER)` && 		\
	sed -e "s|\@prefix\@|$(prefix)|g" 		\
	    -e "s|\@pkglibdir\@|$(pkglibdir)|g" 	\
	    -e "s|\@bindir\@|$(bindir)|g"		\
	    -e "s|\@target\@|$(TARGET_NAME)|g" 		\
	    -e "s|\@wrapper\@|$(WRAPPER)|g"		\
	    -e "s|\@extragac\@|$(EXTRA_GAC_PREFIX)|g"	\
	    -e "s|\@srcdir\@|$(PWD)|g"	\
	    < $< > $@
	chmod +x $(WRAPPER)

$(PANEL_WRAPPER): $(srcdir)/$(PANEL_WRAPPER).in Makefile
	sed -e "s|\@prefix\@|$(prefix)|g" 		\
	    -e "s|\@pkglibdir\@|$(pkglibdir)|g" 	\
	    -e "s|\@bindir\@|$(bindir)|g"		\
	    -e "s|\@target\@|$(TARGET_NAME)|g" 		\
	    -e "s|\@wrapper\@|$(WRAPPER)|g"		\
	    -e "s|\@extragac\@|$(EXTRA_GAC_PREFIX)|g"	\
	    -e "s|\@srcdir\@|$(PWD)|g"	\
	    < $< > $@
	chmod +x $(PANEL_WRAPPER)

$(TARGET).config: $(srcdir)/$(TARGET_NAME$).config.in Makefile
	sed -e "s|\@pkglibdir\@|$(pkglibdir)|" \
	    < $< > $@

Defines.cs: $(srcdir)/Defines.cs.in Makefile
	sed -e "s|\@version\@|$(VERSION)|"     \
	    -e "s|\@datadir\@|$(datadir)|"     \
	    -e "s|\@pkglibdir\@|$(pkglibdir)|" \
	    < $< > $@

EXTRA_DIST =            		\
	$(CSFILES)      		\
	$(GNOME_CSFILES)		\
	$(OSX_CSFILES)		\
	$(WIN_CSFILES)		\
	$(DBUS_CSFILES)		\
	$(PANELAPPLET_CSFILES)		\
	$(WRAPPER).in			\
	$(PANEL_WRAPPER).in		\
	$(TARGET_NAME).config.in		\
	$(srcdir)/Defines.cs.in		\
	$(srcdir)/Tomboy.addin.xml

CLEANFILES =			\
	$(TARGET) 		\
	$(TARGET).config	\
	$(TARGET).mdb 		\
	$(WRAPPER)		\
	$(PANEL_WRAPPER)	\
	Defines.cs

