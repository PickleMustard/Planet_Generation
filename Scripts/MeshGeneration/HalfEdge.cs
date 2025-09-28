using System;
using Structures;

namespace MeshGeneration
{
    /// <summary>
    /// Directed half-edge representation for canonical mesh topology traversal.
    /// Twin points in the opposite direction; Left is the triangle on the left side.
    /// </summary>
    public class HalfEdge
    {
        public int Id { get; internal set; }
        public Point From { get; internal set; }
        public Point To { get; internal set; }
        public HalfEdge Twin { get; internal set; }
        public Triangle Left { get; internal set; }
        public EdgeKey Key { get; internal set; }

        internal HalfEdge(int id, Point from, Point to)
        {
            Id = id;
            From = from;
            To = to;
            Key = EdgeKey.From(from, to);
        }

        public override string ToString() => $"HalfEdge(Id={Id}, From={From.Index}, To={To.Index})";
    }
}
