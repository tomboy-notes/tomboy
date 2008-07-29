#include <gst/gst.h>
#include <string.h>

#define PLAY_CMD_START "filesrc location="
#define PLAY_CMD_END " !oggdemux!vorbisdec!audioconvert!audioresample!gconfaudiosink "
#define RECORD_CMD "gconfaudiosrc !audioconvert !vorbisenc !oggmux !filesink location="

static GstElement *pipeline;
static GstBus *bus;

void
initialize ()
{
  gst_init (NULL, NULL);
  return 0;
}

void
start_record (gchar *uri)
{
  gchar *gst_command = RECORD_CMD;
  gst_command = g_strconcat(gst_command, uri, NULL);
  pipeline = gst_parse_launch (gst_command, NULL);
  g_free (gst_command);
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
  gst_element_set_state (GST_ELEMENT (pipeline), GST_STATE_PLAYING);
}

void
stop_stream ()
{
  gst_element_set_state (GST_ELEMENT (pipeline), GST_STATE_NULL);
}
