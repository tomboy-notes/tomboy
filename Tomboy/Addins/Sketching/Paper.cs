using System;
using System.Xml;
using System.Collections.Generic;
using Cairo;

namespace VirtualPaper
{
    public class Paper
    {
        private Color        backgroundColor;
        private Stroke       activeStroke;
        private List<Stroke> strokes;
        private Pen          pen;
        private Pen          eraser;

        private int undo;

        public Color BackgroundColor {
            get {
                return backgroundColor;
            }
            protected set {
                backgroundColor = value;
            }
        }

        public List<Stroke> Strokes {
            get {
                return strokes;
            }
            protected set {
                strokes = value;
            }
        }

        public Pen Pen {
            get {
                return pen;
            }
            protected set {
                pen = value;
            }
        }

        public Pen Eraser {
            get {
                return eraser;
            }
            protected set {
                eraser = value;
            }
        }

        public virtual bool CanUndo {
            get {
                return undo > Strokes.Count;
            }
        }

        public virtual bool CanRedo {
            get {
                return undo > 0;
            }
        }

        public virtual Gdk.Rectangle Bounds {
            get {
                Gdk.Rectangle bounds = new Gdk.Rectangle();
                foreach(Stroke stroke in Strokes) {
                    if(stroke.Bounds.X + stroke.Bounds.Width > bounds.Width)
                        bounds.Width = stroke.Bounds.X + stroke.Bounds.Width;
                    if(stroke.Bounds.Y + stroke.Bounds.Height > bounds.Height)
                        bounds.Height = stroke.Bounds.Y + stroke.Bounds.Height;
                }
                return bounds;
            }
        }

        public bool AwaitingStrokeData {
            get {
                return activeStroke != null;
            }
        }

        public Paper(Color background)
        {
            BackgroundColor = background;

            Strokes = new List<Stroke>();

            Pen = new Pen();
            Pen.Color = new Cairo.Color(0.0,0.0,0.0,1.0);
            Pen.Size  = 3.0;

            Eraser = new Pen();
            Eraser.Color = background;
            Eraser.Size  = 10.0;
        }

        public Paper(XmlTextReader xml)
        {
/*            while(xml.Read()) {
                if(xml.NodeType == XmlNodeType.Element) {
                    if(xml.Name == "background-color") {
                        BackgroundColor.R = Convert.ToDouble(xml.GetAttribute("r"));
                        BackgroundColor.G = Convert.ToDouble(xml.GetAttribute("g"));
                        BackgroundColor.B = Convert.ToDouble(xml.GetAttribute("b"));
                        BackgroundColor.A = Convert.ToDouble(xml.GetAttribute("a"));
                    } else {
                        Console.WriteLine("Ignoring Unknown XML Element: {0}",
                            xml.Name);
                    }
                }
            }*/
        }

        public virtual void Draw(Context cr, Gdk.Rectangle clip)
        {
            cr.Rectangle(clip.X, clip.Y, clip.Width, clip.Height);
            cr.Color = BackgroundColor;
            cr.Fill();
            cr.Stroke();

            DrawStrokes(cr, clip);
        }

        protected virtual void DrawStrokes(Context cr, Gdk.Rectangle clip)
        {
            int strokeCount = Strokes.Count;

            foreach(Stroke stroke in Strokes) {
                strokeCount--;
                if(strokeCount < undo) break;

                if(stroke.Bounds.IntersectsWith(clip)) {
                    stroke.Draw(cr, clip);
                }
            }
        }

        public virtual Stroke Undo()
        {
            if(CanUndo) {
                undo++;
                return Strokes[Strokes.Count - undo];
            }

            return null;
        }

        public virtual Stroke Redo()
        {
            if(CanRedo) {
                Stroke redoneStroke = Strokes[Strokes.Count - undo];
                undo--;
                return redoneStroke;
            }

            return null;
        }

        public virtual void Clear()
        {
            Strokes.Clear();
            undo = 0;
        }

        public virtual void BeginStroke(Pen style)
        {
            activeStroke = new Stroke(style);

            Strokes.RemoveRange(Strokes.Count - undo, undo);
            undo = 0;

            Strokes.Add(activeStroke);
        }

        public virtual Gdk.Rectangle ContinueStroke(double x, double y,
            double p, double tx, double ty)
        {
            if(activeStroke == null)
                return new Gdk.Rectangle(0,0,0,0);

            Gdk.Rectangle changed =
                activeStroke.AddPoint(x, y, p);
            if(changed.Width  < 1) changed.Width  = 1;
            if(changed.Height < 1) changed.Height = 1;

            return changed;
        }

        public virtual void EndStroke()
        {
            activeStroke = null;
        }

        public virtual void Serialize(XmlTextWriter xml)
        {
            xml.WriteStartElement(null, "background-color", null);
            xml.WriteAttributeString("r", BackgroundColor.R.ToString());
            xml.WriteAttributeString("g", BackgroundColor.G.ToString());
            xml.WriteAttributeString("b", BackgroundColor.B.ToString());
            xml.WriteAttributeString("a", BackgroundColor.A.ToString());
            xml.WriteEndElement();
        }

        public static Paper Deserialize(XmlTextReader xml)
        {
            // TODO: The following is a poor example of how we do object-
            // oriented programming.  I should be shot for this.  However,
            // much like the Undo code, I just want to see it work and clean
            // it up later.  Enjoy and laugh :-D
/*            string type = xml.GetAttribute("type");
            if(type == "NotebookPaper") {
                return new NotebookPaper(xml);
            } else if(type == "Paper") {
                return new Paper(xml);
            } else {
                Console.WriteLine("Unknown Paper Type: {0}", type);
                Console.WriteLine("Defaulting to Plain Paper");
                return new Paper(xml);
            }*/
            return null;
        }
    }
}
