using Godot;
using Structures;
using UtilityLibrary;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration
{
    /// <summary>
    /// Generates vertices linearly distributed between two points in 3D space.
    /// </summary>
    /// <remarks>
    /// This class implements the IVertexGenerator interface to create evenly spaced vertices
    /// along a straight line segment between a start and end point. The vertices are generated
    /// using linear interpolation (lerp) and are stored in a StructureDatabase for reuse.
    /// </remarks>
    public class LinearVertexGenerator : IVertexGenerator
    {
        /// <summary>
        /// Generates a specified number of vertices linearly distributed between two points.
        /// </summary>
        /// <param name="count">The number of vertices to generate between the start and end points.</param>
        /// <param name="start">The starting point of the line segment.</param>
        /// <param name="end">The ending point of the line segment.</param>
        /// <param name="db">The StructureDatabase used to store and retrieve points.</param>
        /// <returns>An array of Point objects representing the linearly distributed vertices.</returns>
        /// <remarks>
        /// The vertices are evenly spaced along the line segment, with the first vertex
        /// appearing at 1/(count+1) of the distance from start to end, and the last vertex
        /// appearing at count/(count+1) of the distance. If count is 0 or negative,
        /// returns an empty array.
        /// </remarks>
        public Point[] GenerateVertices(int count, Point start, Point end, StructureDatabase db)
        {
            Logger.EnterFunction("LinearVertexGenerator.GenerateVertices", $"count={count}, start={start.Index}, end={end.Index}");
            if (count <= 0) { Logger.ExitFunction("LinearVertexGenerator.GenerateVertices", "returned 0 points"); return new Point[0]; }
            Point[] vertices = new Point[count];
            float step = 1.0f / (count + 1);

            for (int i = 0; i < count; ++i)
            {
                float t = (i + 1) * step;
                Vector3 pos = start.ToVector3().Lerp(end.ToVector3(), t);
                vertices[i] = db.GetOrCreatePoint(pos);
            }

            Logger.ExitFunction("LinearVertexGenerator.GenerateVertices", $"returned {vertices.Length} points");
            return vertices;
        }

        /// <summary>
        /// Calculates the parameterization value for a vertex at a given index.
        /// </summary>
        /// <param name="index">The zero-based index of the vertex.</param>
        /// <param name="total">The total number of vertices being generated.</param>
        /// <returns>A float value between 0 and 1 representing the relative position along the line segment.</returns>
        /// <remarks>
        /// This method returns the parameter t used in linear interpolation, where 0 represents
        /// the start point and 1 represents the end point. The formula (index + 1)/(total + 1)
        /// ensures that vertices are evenly distributed without including the endpoints.
        /// </remarks>
        public float GetParameterization(float index, float total)
        {
            return (index + 1.0f) / (total + 1.0f);
        }

        /// <summary>
        /// Validates whether the specified vertex count configuration is valid.
        /// </summary>
        /// <param name="vertexCount">The number of vertices to validate.</param>
        /// <returns>True if the vertex count is valid (non-negative); otherwise, false.</returns>
        /// <remarks>
        /// This method ensures that the vertex count is not negative, which would be
        /// an invalid configuration for vertex generation.
        /// </remarks>
        public bool ValidateConfiguration(int vertexCount)
        {
            return vertexCount >= 0;
        }
    }
}
