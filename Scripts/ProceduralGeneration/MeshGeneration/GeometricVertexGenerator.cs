
using System;
using Godot;
using Structures.MeshGeneration;
using UtilityLibrary;

namespace ProceduralGeneration.MeshGeneration
{
    /// <summary>
    /// Generates vertices along a line segment using geometric parameterization.
    /// This generator creates vertices with non-linear spacing based on an exponential function,
    /// allowing for controlled distribution of vertices between two endpoints.
    /// </summary>
    public class GeometricVertexGenerator : IVertexGenerator
    {
        /// <summary>
        /// Gets or sets the exponent used for geometric parameterization.
        /// Controls the distribution pattern of vertices along the line segment.
        /// Higher values create more clustered vertices near the start point,
        /// while lower values create more uniform distribution.
        /// </summary>
        /// <value>The exponent value (default: 2.0f)</value>
        public float Exponent { get; set; } = 2.0f;

        /// <summary>
        /// Generates an array of vertices along a line segment between two points.
        /// The vertices are distributed using geometric parameterization based on the Exponent property.
        /// </summary>
        /// <param name="count">The number of vertices to generate</param>
        /// <param name="start">The starting point of the line segment</param>
        /// <param name="end">The ending point of the line segment</param>
        /// <param name="db">The structure database (unused in this implementation)</param>
        /// <returns>An array of Point objects representing the generated vertices</returns>
        /// <remarks>
        /// If count is 0 or negative, returns an empty array.
        /// The first vertex is positioned at the start point, and the last vertex is positioned at the end point.
        /// Intermediate vertices are distributed according to the geometric parameterization function.
        /// </remarks>
        public Point[] GenerateVertices(int count, Point start, Point end, StructureDatabase db)
        {
            Logger.EnterFunction("GeometricVertexGenerator.GenerateVertices", $"count={count}, start={start.Index}, end={end.Index}, exp={Exponent}");
            if (count <= 0) { Logger.ExitFunction("GeometricVertexGenerator.GenerateVertices", "returned 0 points"); return new Point[0]; }
            Point[] vertices = new Point[count];
            Vector3 startVector = start.Position;
            Vector3 endVector = end.Position;
            float step = 1.0f / (count - 1);

            for (int i = 0; i < count; ++i)
            {
                float t = GetParameterization(i, count);
                Vector3 pos = startVector.Lerp(endVector, t);
                vertices[i] = db.GetOrCreatePoint(pos);
            }

            Logger.ExitFunction("GeometricVertexGenerator.GenerateVertices", $"returned {vertices.Length} points");
            return vertices;
        }

        /// <summary>
        /// Calculates the parameterization value for a given vertex index using geometric progression.
        /// This method determines the relative position of a vertex along the line segment.
        /// </summary>
        /// <param name="index">The index of the vertex (0-based)</param>
        /// <param name="total">The total number of vertices to generate</param>
        /// <returns>A parameterization value between 0.0 and 1.0 for linear interpolation</returns>
        /// <remarks>
        /// The parameterization uses the formula: pow(index / (total + 1.0), 1.0 / Exponent)
        /// This creates non-linear spacing that can be controlled by the Exponent property.
        /// </remarks>
        public float GetParameterization(float index, float total)
        {
            float normalized = (index) / (total + 1.0f);
            return (float)Math.Pow(normalized, 1.0f / Exponent);
        }

        /// <summary>
        /// Validates the configuration parameters for vertex generation.
        /// Ensures that the vertex count is valid for the generation process.
        /// </summary>
        /// <param name="vertexCount">The number of vertices to validate</param>
        /// <returns>True if the configuration is valid, false otherwise</returns>
        /// <remarks>
        /// Currently only validates that vertexCount is non-negative.
        /// Additional validation logic can be added as needed.
        /// </remarks>
        public bool ValidateConfiguration(int vertexCount)
        {
            return vertexCount >= 0;
        }
    }
}
