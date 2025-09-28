using System;
using Structures;

namespace MeshGeneration
{
    /// <summary>
    /// Canonical, direction-agnostic identity for an undirected edge.
    /// The key is normalized such that A < B to ensure stable value semantics.
    /// </summary>
    public readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public int A { get; }
        public int B { get; }

        public EdgeKey(int a, int b)
        {
            if (a <= b)
            {
                A = a; B = b;
            }
            else
            {
                A = b; B = a;
            }
        }

        public static EdgeKey From(Point a, Point b) => new EdgeKey(a.Index, b.Index);
        public static EdgeKey From(int a, int b) => new EdgeKey(a, b);

        public bool Equals(EdgeKey other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B);
        public static bool operator ==(EdgeKey left, EdgeKey right) => left.Equals(right);
        public static bool operator !=(EdgeKey left, EdgeKey right) => !left.Equals(right);
        public override string ToString() => $"EdgeKey({A},{B})";
    }
}
