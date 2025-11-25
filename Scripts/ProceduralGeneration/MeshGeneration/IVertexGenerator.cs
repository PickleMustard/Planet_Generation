using Structures.MeshGeneration;

namespace ProceduralGeneration.MeshGeneration
{
    /// <summary>
    /// Defines the contract for vertex generation algorithms used in mesh generation.
    /// </summary>
    /// <remarks>
    /// This interface provides a standardized way to generate vertices for various mesh generation
    /// scenarios, including planetary surfaces and other geometric structures. Implementations
    /// can use different distribution strategies such as linear, geometric, or custom patterns.
    /// </remarks>
    public interface IVertexGenerator
    {
        /// <summary>
        /// Generates an array of vertices between two boundary points.
        /// </summary>
        /// <param name="count">The number of vertices to generate.</param>
        /// <param name="start">The starting point for vertex generation.</param>
        /// <param name="end">The ending point for vertex generation.</param>
        /// <param name="db">The structure database containing contextual information.</param>
        /// <returns>An array of generated points representing the vertices.</returns>
        /// <remarks>
        /// The generated vertices should follow a logical distribution pattern based on the
        /// specific implementation. The vertices will be used to construct mesh faces
        /// and other geometric structures.
        /// </remarks>
        public Point[] GenerateVertices(int count, Point start, Point end, StructureDatabase db);

        /// <summary>
        /// Calculates the parameterization value for a given vertex index.
        /// </summary>
        /// <param name="index">The index of the vertex in the sequence.</param>
        /// <param name="total">The total number of vertices in the sequence.</param>
        /// <returns>A float value representing the parameterization of the vertex.</returns>
        /// <remarks>
        /// Parameterization is used to determine the relative position of a vertex
        /// along a path or surface. This is particularly useful for non-linear
        /// distributions where vertices are not evenly spaced.
        /// </remarks>
        public float GetParameterization(float index, float total);

        /// <summary>
        /// Validates whether the given vertex count configuration is valid for this generator.
        /// </summary>
        /// <param name="vertexCount">The number of vertices to validate.</param>
        /// <returns>True if the configuration is valid; otherwise, false.</returns>
        /// <remarks>
        /// Different vertex generation algorithms may have specific requirements
        /// for valid vertex counts. This method allows implementations to enforce
        /// constraints such as minimum/maximum counts or specific mathematical relationships.
        /// </remarks>
        public bool ValidateConfiguration(int vertexCount);
    }

}
