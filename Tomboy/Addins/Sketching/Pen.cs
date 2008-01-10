using Cairo;

namespace VirtualPaper
{
    public class Pen
    {
        private Color color;
        private double size;

        public Color Color {
            get { return color; }
            set { color = value; }
        }

        public double Size {
            get { return size; }
            set { size = value; }
        }

        public Pen()
        {
            color = new Color(0.0,0.0,0.0,1.0);
            size = 3.0;
        }

        public Pen Clone()
        {
            Pen p = new Pen();

            p.Color = new Color(color.R, Color.G, Color.B, Color.A);
            p.Size  = size;

            return p;
        }
    }
}
