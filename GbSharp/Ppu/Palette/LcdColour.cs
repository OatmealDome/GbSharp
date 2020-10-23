namespace GbSharp.Ppu.Palette
{
    class LcdColour
    {
        public byte R
        {
            get;
            private set;
        }

        public byte G
        {
            get;
            private set;
        }

        public byte B
        {
            get;
            private set;
        }

        public LcdColour(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static bool operator ==(LcdColour a, LcdColour b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B;
        }

        public static bool operator !=(LcdColour a, LcdColour b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return obj is LcdColour && (obj as LcdColour) == this;
        }

        public override int GetHashCode()
        {
            return (R * 1000000) + (G * 1000) + B;
        }

        public override string ToString()
        {
            return $"LcdColour(R = {R}, G = {G}, B = {B})";
        }

    }
}
