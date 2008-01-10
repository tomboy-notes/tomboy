using System;
using System.Xml;
using Cairo;
using Gtk;
using Gdk;

namespace VirtualPaper
{
    public class Handwriting : DrawingArea
    {
        public event EventHandler Changed;

        private Paper paper;

        public Paper Paper {
            get {
                return paper;
            }
            protected set {
                paper = value;
            }
        }
                
        public Handwriting(Paper p) : base()
        {
            AppPaintable = true;
            Events = EventMask.ExposureMask | EventMask.ButtonPressMask |
                     EventMask.ButtonReleaseMask | EventMask.PointerMotionMask;
            ExtensionEvents = ExtensionMode.Cursor;

            Paper = p;

            // TODO: How do you shut the mouse cursor up?  If they're using a
            // tablet, there should be no cursor at all.  At worst, a little
            // black dot representing the pen size and color, but I'd prefer
            // no cursor at all.  If they're using a mouse, then the little
            // dot can stay.
//            GdkWindow.Cursor = Cursor.NewFromName(Display.Default, "Clock");
        }

        public void DrawToSurface(Cairo.Surface surface, Gdk.Rectangle size)
        {
            Cairo.Context cr = new Cairo.Context(surface);

            Paper.Draw(cr, size);

            ((IDisposable)cr).Dispose();
        }

        public void Serialize(XmlTextWriter xml)
        {
/*            xml.WriteStartDocument();
            xml.WriteStartElement(null, "handwriting-data", null);
            xml.WriteAttributeString("version", "0.1");

            // Paper Configuration
            xml.WriteStartElement(null, "paper", null);

            paper.Serialize(xml);

            xml.WriteEndElement();

            // Strokes
            xml.WriteStartElement(null, "strokes", null);

            foreach(Stroke s in strokes) {
                s.WriteXml(xml);
            }

            xml.WriteEndElement();*/
        }

        public void Deserialize(XmlTextReader xml)
        {
            Clear();

/*            while(xml.Read()) {
                switch(xml.NodeType) {
                case XmlNodeType.Element:
                    if(xml.Name == "handwriting-data") {
                        break;
                    } else if(xml.Name == "paper") {
                        paper = Paper.Deserialize(xml);
                    } else if(xml.Name == "strokes") {
                        while(xml.Read()) {
                            if(xml.NodeType == XmlNodeType.Element &&
                                xml.Name == "stroke") {
                                CairoStroke stroke = new CairoStroke(xml);
                                countPoints += stroke.Count;
                                strokes.Add(stroke);
                            } else if(xml.NodeType == XmlNodeType.EndElement) {
                                break;
                            }
                        }
                    } else {
                        Console.WriteLine("Ignoring Unknown XML Element: {0}",
                            xml.Name);
                    }
                    break;
                }
            }*/
        }

        public void Undo()
        {
            Stroke undoneStroke = Paper.Undo();
            if(undoneStroke != null) {
                QueueDrawArea(undoneStroke);
            }
        }

        public void Redo()
        {
            Stroke redoneStroke = Paper.Redo();
            if(redoneStroke != null) {
                QueueDrawArea(redoneStroke);
            }
        }

        public void Clear()
        {
            Paper.Clear();
            QueueDraw();
        }

        protected override bool OnConfigureEvent(EventConfigure ev)
        {
            foreach(Device d in GdkWindow.Display.ListDevices()) {
                if(d.Source == InputSource.Pen ||
                   d.Source == InputSource.Eraser ||
                   d.Source == InputSource.Mouse) {
                    d.SetMode(InputMode.Screen);
                }
            }

            return true;
        }

        protected override bool OnExposeEvent(EventExpose ev)
        {
            if(!IsRealized) {
                return false;
            }

            Cairo.Context cr = CairoHelper.Create(GdkWindow);

            Gdk.Rectangle rect = ev.Area;
            cr.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            cr.Clip();

            Paper.Draw(cr, rect);

            // Uncomment this to see the clipping region
/*            cr.Color = new Cairo.Color(0.0,1.0,0.0,0.5);
            cr.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            cr.Stroke();*/

            ((IDisposable)cr).Dispose();

            return true;
        }

        protected override bool OnButtonPressEvent(EventButton ev)
        {
            if(ev.Device.Source == InputSource.Pen ||
               ev.Device.Source == InputSource.Mouse) {
                if(ev.Device.NumAxes > 2 || ev.Button == 1) {
                    Paper.BeginStroke(Paper.Pen);
                } else if(ev.Button == 3) {
                        Paper.BeginStroke(Paper.Eraser);
                }
                // We don't care about any other buttons
            } else if(ev.Device.Source == InputSource.Eraser ||
                ev.Button == 3) {
                Paper.BeginStroke(Paper.Eraser);
            }
            // Any other combinations?  They go here.

            return true;
        }

        protected override bool OnButtonReleaseEvent(EventButton ev)
        {
            Paper.EndStroke();

            return true;
        }

        protected override bool OnMotionNotifyEvent(EventMotion ev)
        {
            if(!Paper.AwaitingStrokeData) {
                return true;
            }
            double[] axes;
            ModifierType mask;

            if(ev.IsHint) {
                ev.Device.GetState(GdkWindow, out axes, out mask);
            } else {
                axes = ev.Axes;
                mask = ev.State;
            }

            Gdk.Rectangle changed = Gdk.Rectangle.Zero;

            if(ev.Device.Source == InputSource.Pen) {
                if(ev.Device.NumAxes > 2) {
                    Console.WriteLine("X: {0}, Y: {1}", axes[0], axes[1]);
                    changed = Paper.ContinueStroke(axes[0], axes[1], axes[2],
                        0d, 0d);
                } else {
                    changed = Paper.ContinueStroke(axes[0], axes[1], 1d,
                        0d, 0d);
                }
            } else if(ev.Device.Source == InputSource.Eraser) {
                changed = Paper.ContinueStroke(axes[0], axes[1], 1d, 0d, 0d);
            } else if(ev.Device.Source == InputSource.Mouse) {
                changed = Paper.ContinueStroke(ev.X, ev.Y, 1d, 0d, 0d);
            } else {
                // Unknown Button
            }

            if(changed != Gdk.Rectangle.Zero) {
                QueueDrawArea(changed);
            }

            return true;
        }

        private void QueueDrawArea(Stroke s)
        {
            QueueDrawArea((int)s.X, (int)s.Y, (int)s.Width, (int)s.Height);
        }

        private void QueueDrawArea(Gdk.Rectangle r)
        {
            QueueDrawArea(r.X, r.Y, r.Width, r.Height);
        }

/*        private void QueuePaddedDrawArea(Stroke s)
        {
            QueuePaddedDrawArea((int)s.X,(int)s.Y,(int)s.Width,(int)s.Height);
        }

        private void QueuePaddedDrawArea(Gdk.Rectangle r)
        {
            QueuePaddedDrawArea(r.X, r.Y, r.Width, r.Height);
        }

        private void QueuePaddedDrawArea(int X, int Y, int Width, int Height) {
            int lineWidth = Convert.ToInt32(penStyle.Size);
            QueueDrawArea(X     -   lineWidth, Y      -   lineWidth,
                          Width + 2*lineWidth, Height + 2*lineWidth);
        }*/

    }
}
