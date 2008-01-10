using System;
using System.Xml;
using System.Collections.Generic;
using Cairo;

namespace VirtualPaper
{
    public class Stroke
    {
        protected List<double> x, y;
        protected List<Color> color;
        protected Pen style;
        protected int count;
        protected double minX;
        protected double minY;
        protected double maxX;
        protected double maxY;
        protected bool firstStroke;

        public virtual double X {
            get {
                return minX;
            }
        }

        public virtual double Y {
            get {
                return minY;
            }
        }

        public virtual double Height {
            get {
                return maxY - minY;
            }
        }

        public virtual double Width {
            get {
                return maxX - minX;
            }
        }

        public Gdk.Rectangle Bounds {
            get {
                int w = (int)Math.Ceiling(style.Size);
                int w2 = w*2;

                Gdk.Rectangle rect = new Gdk.Rectangle();
                rect.X = (int)minX - w;
                rect.Y = (int)minY - w;
                rect.Width = (int)(maxX - minX) + w2;
                rect.Height = (int)(maxY - minY) + w2;
                return rect;
            }
        }
        
        public virtual int Count {
            get {
                return count;
            }
        }

        public Stroke(Pen penStyle)
        {
            x = new List<double>();
            y = new List<double>();
            color = new List<Cairo.Color>();
            style = penStyle.Clone();
            count = 0;
            minX = 0;
            minY = 0;
            maxX = 0;
            maxY = 0;
            firstStroke = false;
        }

        public virtual void AddPoint(double x, double y)
        {
            AddPoint(x, y, 1.0);
        }

        public virtual Gdk.Rectangle AddPoint(double x, double y, double pressure)
        {
            Cairo.Color c = new Cairo.Color(
                style.Color.R,
                style.Color.G,
                style.Color.B,
                pressure);
            this.x.Add(x);
            this.y.Add(y);
            this.color.Add(c);
            count++;

            if(firstStroke) {
                if(x < minX) minX = x;
                else if(x > maxX) maxX = x;
                if(y < minY) minY = y;
                else if(y > maxY) maxY = y;
            } else {
                minX = x;
                maxX = x;
                minY = y;
                maxY = y;
                firstStroke = true;
            }

            int w = (int)Math.Ceiling(style.Size);
            int w2 = w*2;

            if(this.x.Count > 1 && this.y.Count > 1) {
                double oldX = this.x[this.x.Count - 2];
                double oldY = this.y[this.y.Count - 2];
                if(x < oldX) {
                    double temp = oldX;
                    oldX = x;
                    x = temp;
                }
                if(y < oldY) {
                    double temp = oldY;
                    oldY = y;
                    y = temp;
                }

                return new Gdk.Rectangle((int)oldX - w, (int)oldY - w,
                    (int)(x - oldX) + w2, (int)(y - oldY) + w2);
            }
            return new Gdk.Rectangle((int)x - w, (int)y - w, w2, w2);
        }

        public void Draw(Context cr, Gdk.Rectangle clip)
        {
            cr.LineWidth = style.Size;

            for(int i = 1; i < count; i++) {
                Gdk.Rectangle rect = new Gdk.Rectangle();
                rect.X = (int)((x[i] < x[i-1]) ? x[i] : x[i-1]);
                rect.Y = (int)((y[i] < y[i-1]) ? y[i] : y[i-1]);
                rect.Width  = (int)((x[i] < x[i-1]) ? x[i-1]-x[i] : x[i]-x[i-1]);
                rect.Height = (int)((y[i] < y[i-1]) ? y[i-1]-y[i] : y[i]-y[i-1]);

                if(clip.IntersectsWith(rect)) {
                    cr.MoveTo(x[i-1], y[i-1]);
                    cr.LineTo(x[i], y[i]);

                    LinearGradient g = new LinearGradient(x[i-1], y[i-1], x[i], y[i]);
                    g.AddColorStop(0.0, color[i-1]);
                    g.AddColorStop(1.0, color[i]);

                    cr.Pattern = g;
                    cr.Stroke();
                }
            }
        }

        public virtual void WriteXml(XmlTextWriter xml)
        {
            xml.WriteStartElement(null, "stroke", null);

            xml.WriteAttributeString("left", minX.ToString());
            xml.WriteAttributeString("top", minY.ToString());
            xml.WriteAttributeString("right", maxX.ToString());
            xml.WriteAttributeString("bottom", maxY.ToString());

            WriteXmlPoints(xml);

            xml.WriteEndElement();
        }

        protected virtual void WriteXmlPoints(XmlTextWriter xml)
        {
            for(int i = 0; i < count; i++) {
                xml.WriteStartElement(null, "point", null);
                xml.WriteAttributeString("x", x[i].ToString());
                xml.WriteAttributeString("y", y[i].ToString());
                xml.WriteEndElement();
            }
        }
    }
}
