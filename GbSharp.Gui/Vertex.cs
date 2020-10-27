using System.Numerics;
using Veldrid;

namespace GbSharp.Gui
{
    struct Vertex
    {
        public Vector2 Position
        {
            get;
            set;
        }

        public Vector2 Uv
        {
            get;
            set;
        }

        public Vertex(Vector2 position, Vector2 uv)
        {
            Position = position;
            Uv = uv;
        }
    }
}
