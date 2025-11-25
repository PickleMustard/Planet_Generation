namespace Structures.Enums;
/// <summary>
/// Specifies the distribution strategy for vertex generation.
/// </summary>
/// <remarks>
/// This enumeration defines different approaches to distributing vertices
/// across a surface or path. Each strategy has different characteristics
/// suitable for various mesh generation scenarios.
/// </remarks>
public enum VertexDistribution
{
    /// <summary>
    /// Vertices are distributed evenly along the path or surface.
    /// </summary>
    /// <remarks>
    /// Linear distribution provides uniform spacing between vertices,
    /// making it suitable for regular geometric shapes and simple meshes.
    /// </remarks>
    Linear,

    /// <summary>
    /// Vertices are distributed using a geometric progression.
    /// </summary>
    /// <remarks>
    /// Geometric distribution creates varying spacing between vertices,
    /// which can be useful for creating natural-looking surfaces or
    /// emphasizing certain areas of the mesh.
    /// </remarks>
    Geometric,

    /// <summary>
    /// Vertices are distributed using a custom algorithm.
    /// </summary>
    /// <remarks>
    /// Custom distribution allows for specialized vertex placement
    /// strategies that don't fit into linear or geometric patterns.
    /// This is useful for complex mesh generation requirements.
    /// </remarks>
    Custom
};
