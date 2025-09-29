using System;
using Structures;

namespace MeshGeneration
{
    /// <summary>
    /// Represents a canonical, direction-agnostic identity for an undirected edge in mesh generation.
    /// The key is normalized such that A ≤ B to ensure stable value semantics and consistent edge identification
    /// regardless of vertex order. This struct is immutable and implements value equality semantics.
    /// </summary>
    /// <remarks>
    /// EdgeKey is designed to provide a consistent way to identify edges in mesh structures where
    /// the direction of the edge (from vertex A to B vs B to A) should not matter. The normalization
    /// ensures that EdgeKey(a, b) and EdgeKey(b, a) always produce the same key representation.
    /// This is particularly useful in mesh algorithms where edges need to be uniquely identified
    /// for operations like edge collapse, subdivision, or adjacency queries.
    /// </remarks>
    public readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        /// <summary>
        /// Gets the first vertex index. Always the smaller value due to normalization.
        /// </summary>
        /// <value>
        /// The smaller of the two vertex indices that define this edge.
        /// </value>
        public int A { get; }

        /// <summary>
        /// Gets the second vertex index. Always the larger value due to normalization.
        /// </summary>
        /// <value>
        /// The larger of the two vertex indices that define this edge.
        /// </value>
        public int B { get; }

        /// <summary>
        /// Initializes a new EdgeKey with automatic normalization to ensure A ≤ B.
        /// </summary>
        /// <param name="a">First vertex index.</param>
        /// <param name="b">Second vertex index.</param>
        /// <remarks>
        /// The constructor automatically normalizes the vertex indices so that A is always
        /// the smaller value and B is always the larger value. This ensures that edges
        /// created with the same vertices in different orders will have identical keys.
        /// </remarks>
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

        /// <summary>
        /// Creates an EdgeKey from two Point objects using their indices.
        /// </summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point.</param>
        /// <returns>A normalized EdgeKey representing the edge between the points.</returns>
        /// <remarks>
        /// This factory method provides a convenient way to create EdgeKeys directly from
        /// Point objects, extracting their indices automatically. The resulting EdgeKey
        /// will be normalized according to the same rules as the constructor.
        /// </remarks>
        public static EdgeKey From(Point a, Point b) => new EdgeKey(a.Index, b.Index);

        /// <summary>
        /// Creates an EdgeKey from two integer indices.
        /// </summary>
        /// <param name="a">First vertex index.</param>
        /// <param name="b">Second vertex index.</param>
        /// <returns>A normalized EdgeKey representing the edge between the indices.</returns>
        /// <remarks>
        /// This factory method provides an alternative way to create EdgeKeys that is
        /// semantically equivalent to calling the constructor directly. It can be useful
        /// in scenarios where a more explicit factory method pattern is preferred.
        /// </remarks>
        public static EdgeKey From(int a, int b) => new EdgeKey(a, b);

        /// <summary>
        /// Determines whether this EdgeKey is equal to another EdgeKey.
        /// </summary>
        /// <param name="other">The EdgeKey to compare with this EdgeKey.</param>
        /// <returns>true if the EdgeKeys are equal; otherwise, false.</returns>
        /// <remarks>
        /// Two EdgeKeys are considered equal if their A and B properties are identical.
        /// Due to normalization, this means they represent the same undirected edge
        /// regardless of the original vertex order.
        /// </remarks>
        public bool Equals(EdgeKey other) => A == other.A && B == other.B;

        /// <summary>
        /// Determines whether this EdgeKey is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare with this EdgeKey.</param>
        /// <returns>true if the object is an EdgeKey and is equal to this EdgeKey; otherwise, false.</returns>
        /// <remarks>
        /// This method provides value equality comparison with any object. If the object
        /// is not an EdgeKey, the method returns false. Otherwise, it delegates to the
        /// strongly-typed Equals method for the actual comparison.
        /// </remarks>
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);

        /// <summary>
        /// Returns the hash code for this EdgeKey.
        /// </summary>
        /// <returns>A hash code for the current EdgeKey.</returns>
        /// <remarks>
        /// The hash code is computed using a combination of the A and B property values.
        /// This ensures that equal EdgeKeys always produce the same hash code, which is
        /// essential for proper operation in hash-based collections like HashSet and Dictionary.
        /// </remarks>
        public override int GetHashCode() => HashCode.Combine(A, B);

        /// <summary>
        /// Determines whether two specified EdgeKeys are equal.
        /// </summary>
        /// <param name="left">The first EdgeKey to compare.</param>
        /// <param name="right">The second EdgeKey to compare.</param>
        /// <returns>true if the EdgeKeys are equal; otherwise, false.</returns>
        /// <remarks>
        /// This operator provides a convenient syntax for comparing EdgeKeys for equality.
        /// It delegates to the Equals method to ensure consistent comparison behavior.
        /// </remarks>
        public static bool operator ==(EdgeKey left, EdgeKey right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified EdgeKeys are not equal.
        /// </summary>
        /// <param name="left">The first EdgeKey to compare.</param>
        /// <param name="right">The second EdgeKey to compare.</param>
        /// <returns>true if the EdgeKeys are not equal; otherwise, false.</returns>
        /// <remarks>
        /// This operator provides a convenient syntax for comparing EdgeKeys for inequality.
        /// It returns the negation of the equality comparison result.
        /// </remarks>
        public static bool operator !=(EdgeKey left, EdgeKey right) => !left.Equals(right);

        /// <summary>
        /// Returns the string representation of this EdgeKey.
        /// </summary>
        /// <returns>A string in the format "EdgeKey(A,B)".</returns>
        /// <remarks>
        /// The string representation follows the format "EdgeKey(A,B)" where A and B are
        /// the normalized vertex indices. This format is useful for debugging, logging,
        /// and display purposes. The normalized format ensures consistent string representation
        /// for edges that are logically equivalent.
        /// </remarks>
        public override string ToString() => $"EdgeKey({A},{B})";
    }
}
