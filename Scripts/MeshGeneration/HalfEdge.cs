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
        /// <summary>
        /// Gets the unique identifier for this half-edge.
        /// </summary>
        public int Id { get; internal set; }

        /// <summary>
        /// Gets the starting point (vertex) of this half-edge.
        /// </summary>
        public Point From { get; internal set; }

        /// <summary>
        /// Gets the ending point (vertex) of this half-edge.
        /// </summary>
        public Point To { get; internal set; }

        /// <summary>
        /// Gets the twin half-edge that points in the opposite direction.
        /// The twin edge connects the same vertices but in reverse order (To -> From).
        /// </summary>
        public HalfEdge Twin { get; internal set; }

        /// <summary>
        /// Gets the triangle that lies on the left side of this directed edge.
        /// When traversing the edge from From to To, this triangle is to the left.
        /// </summary>
        public Triangle Left { get; internal set; }

        /// <summary>
        /// Gets the edge key that uniquely identifies the undirected edge between two points.
        /// This key is used for edge lookup and comparison operations.
        /// </summary>
        public EdgeKey Key { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the HalfEdge class.
        /// </summary>
        /// <param name="id">The unique identifier for this half-edge.</param>
        /// <param name="from">The starting point (vertex) of the edge.</param>
        /// <param name="to">The ending point (vertex) of the edge.</param>
        /// <remarks>
        /// This constructor is internal and should only be called by mesh generation systems.
        /// The edge key is automatically generated from the from and to points.
        /// </remarks>
        internal HalfEdge(int id, Point from, Point to)
        {
            Id = id;
            From = from;
            To = to;
            Key = EdgeKey.From(from, to);
        }

        /// <summary>
        /// Returns a string representation of this half-edge.
        /// </summary>
        /// <returns>A string containing the edge ID and the indices of the from and to points.</returns>
        public override string ToString() => $"HalfEdge(Id={Id}, From={From.Index}, To={To.Index})";
    }
}
