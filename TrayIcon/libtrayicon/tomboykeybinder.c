

#include <gtk/gtkmain.h>
#include <gdk/gdk.h>
#include <gdk/gdkwindow.h>
#include <gdk/gdkx.h>
#include <X11/Xlib.h>

#include "eggaccelerators.h"
#include "tomboykeybinder.h"

GSList *bindings = NULL;

typedef struct _Binding {
	TomboyBindkeyHandler  handler;
	gpointer              user_data;
	char                 *keystring;
	uint                  keycode;
	uint                  modifiers;
} Binding;

static GdkFilterReturn
filter_func (GdkXEvent *gdk_xevent, GdkEvent *event, gpointer data)
{
	GdkFilterReturn return_val;
	GSList *iter;
	XEvent *xevent;

	xevent = (XEvent *) gdk_xevent;
	return_val = GDK_FILTER_CONTINUE;

	g_print ("Got Event! %d, %d\n", xevent->type, event->type);

	switch (xevent->type) {
	case KeyPress:
		g_print ("Got KeyPress! keycode: %d, modifiers: %d\n", 
			 xevent->xkey.keycode, 
			 xevent->xkey.state);

		for (iter = bindings; iter != NULL; iter = iter->next) {
			Binding *binding = (Binding *) iter->data;

			if (binding->keycode == xevent->xkey.keycode &&
			    binding->modifiers == xevent->xkey.state) {
				g_print ("Calling handler...\n");
				(binding->handler) (binding->keystring, 
						    binding->user_data);
			}
		}
		break;
	case KeyRelease:
		g_print ("Got KeyRelease! \n");
		break;
	}

	return return_val;
}

void
devirtualize_modifiers (uint                    meta_mask,
			uint                    hyper_mask,
			uint                    super_mask,
			EggVirtualModifierType  modifiers,
			unsigned int           *mask)
{
	*mask = 0;
  
	if (modifiers & EGG_VIRTUAL_SHIFT_MASK)
		*mask |= ShiftMask;
	if (modifiers & EGG_VIRTUAL_CONTROL_MASK)
		*mask |= ControlMask;
	if (modifiers & EGG_VIRTUAL_ALT_MASK)
		*mask |= Mod1Mask;
	if (modifiers & EGG_VIRTUAL_META_MASK)
		*mask |= meta_mask;
	if (modifiers & EGG_VIRTUAL_HYPER_MASK)
		*mask |= hyper_mask;
	if (modifiers & EGG_VIRTUAL_SUPER_MASK)
		*mask |= super_mask;
	if (modifiers & EGG_VIRTUAL_MOD2_MASK)
		*mask |= Mod2Mask;
	if (modifiers & EGG_VIRTUAL_MOD3_MASK)
		*mask |= Mod3Mask;
	if (modifiers & EGG_VIRTUAL_MOD4_MASK)
		*mask |= Mod4Mask;
	if (modifiers & EGG_VIRTUAL_MOD5_MASK)
		*mask |= Mod5Mask;  
}

void tomboy_keybinder_init (void)
{
	GdkWindow *rootwin = gdk_get_default_root_window ();

	gdk_window_add_filter (rootwin, 
			       filter_func, 
			       NULL);
}

void tomboy_keybinder_bind (const char           *keystring,
			    TomboyBindkeyHandler  handler,
			    gpointer              user_data)
{
	Binding *binding;
	GdkKeymap *keymap;
	GdkWindow *rootwin;

	EggVirtualModifierType virtual_mods = 0;
	GdkModifierType real_mods = 0;
	guint keysym = 0;
	guint keycode = 0;

	if (!egg_accelerator_parse_virtual (keystring, 
					    &keysym, 
					    &virtual_mods))
		return;

	g_print ("Got accel %d, %d\n", keysym, virtual_mods);

	rootwin = gdk_get_default_root_window ();

	keycode = XKeysymToKeycode (GDK_WINDOW_XDISPLAY (rootwin), keysym);

	g_print ("Got keycode %d\n", keycode);

	keymap = gdk_keymap_get_default ();
	if (!keymap)
		return;

	egg_keymap_resolve_virtual_modifiers (keymap,
					      virtual_mods,
					      &real_mods);

	g_print ("Got modmask %d\n", real_mods);

	XGrabKey (GDK_WINDOW_XDISPLAY (rootwin),
		  keycode,
                  real_mods,
                  GDK_WINDOW_XWINDOW (rootwin),
                  True,
                  GrabModeAsync, 
		  GrabModeAsync);

	binding = g_new0 (Binding, 1);
	binding->keystring = g_strdup (keystring);
	binding->handler = handler;
	binding->user_data = user_data;
	binding->keycode = keycode;
	binding->modifiers = real_mods;

	bindings = g_slist_prepend (bindings, binding);
}

void tomboy_keybinder_unbind (const char           *keystring, 
			      TomboyBindkeyHandler  handler)
{
	GSList *iter;

	for (iter = bindings; iter != NULL; iter = iter->next) {
		Binding *binding = (Binding *) iter->data;
		GdkWindow *rootwin;
		
		if (strcmp (keystring, binding->keystring) != 0 ||
		    handler != binding->handler) 
			continue;

		g_print ("Removing grab for '%s'\n", binding->keystring);

		rootwin = gdk_get_default_root_window ();

		XUngrabKey (GDK_WINDOW_XDISPLAY (rootwin),
			    binding->keycode,
			    binding->modifiers,
			    GDK_WINDOW_XWINDOW (rootwin));

		bindings = g_slist_remove (bindings, binding);

		g_free (binding->keystring);
		g_free (binding);
		break;
	}
}
