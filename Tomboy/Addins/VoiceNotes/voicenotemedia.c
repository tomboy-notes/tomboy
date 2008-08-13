#include <gst/gst.h>
#include <string.h>

#define PLAY_CMD_START "filesrc location="
#define PLAY_CMD_END " !oggdemux!vorbisdec!audioconvert!audioresample!gconfaudiosink "
#define RECORD_CMD "gconfaudiosrc !audioconvert !vorbisenc !oggmux !filesink location="

static GstElement *pipeline = NULL;
static GstBus *bus;

void
initialize ()
{
  gst_init (NULL, NULL);
}

static gboolean
bus_callback (GstBus *bus, GstMessage *message, gpointer data)
{
  switch (GST_MESSAGE_TYPE (message)) {
    case GST_MESSAGE_EOS: {
      gst_element_set_state (GST_ELEMENT (pipeline), GST_STATE_READY);
      break;
    }
    default:
      break;
  }

  return TRUE;
}

void
set_bus ()
{
  bus = gst_pipeline_get_bus (GST_PIPELINE (pipeline));
  gst_bus_add_watch (bus, bus_callback, NULL);
  gst_object_unref (bus);
}

void
start_record (gchar *uri)
{
  gchar *gst_command = RECORD_CMD;
  gst_command = g_strconcat(gst_command, uri, NULL);
  pipeline = gst_parse_launch (gst_command, NULL);
  g_free (gst_command);
  set_bus ();
  gst_element_set_state (GST_ELEMENT (pipeline), GST_STATE_PLAYING);
}

void
start_play (char *uri)
{
  gchar *gst_command = PLAY_CMD_START;
  gst_command = g_strconcat (gst_command, uri, NULL);
  gst_command = g_strconcat (gst_command, PLAY_CMD_END);
  pipeline = gst_parse_launch (gst_command, NULL);
  g_free (gst_command);
  set_bus ();
  gst_element_set_state (GST_ELEMENT (pipeline), GST_STATE_PLAYING);
}

void
stop_stream ()
{
  if (pipeline != NULL)
    gst_element_set_state (GST_ELEMENT (pipeline), GST_STATE_NULL);
}

gint
get_state ()
{
  gint status = GST_STATE (pipeline);
  switch (status) {
    case GST_STATE_PLAYING:
      status = 1;
      break;
    default:
      status = 0;
      break;
  }
  return status;
}
