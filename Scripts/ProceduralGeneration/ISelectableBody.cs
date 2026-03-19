using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.GameState;
using Structures.MeshGeneration;

namespace ProceduralGeneration.PlanetGeneration;

/// <summary>
/// Result of a cell selection query against a celestial or satellite body.
/// Contains the nearest point, the Voronoi cell it belongs to, and optionally the continent.
/// </summary>
public class CellSelectionResult
{
    public required Point Point { get; set; }
    public required VoronoiCell Cell { get; set; }

    /// <summary>
    /// The continent containing the selected cell. May be null for satellite bodies
    /// that do not use tectonic/continent generation.
    /// </summary>
    public Continent? CellContinent { get; set; }
}

/// <summary>
/// Interface for celestial bodies that support Voronoi cell selection via raycast.
/// Implemented by both <see cref="CelestialBody"/> and <see cref="SatelliteBody"/>
/// to provide a uniform selection API for the shader-based highlight system.
/// </summary>
public interface ISelectableBody
{
    /// <summary>
    /// The generated mesh for this body, which contains the cell highlight shader materials.
    /// </summary>
    UnifiedCelestialMesh? Mesh { get; }

    /// <summary>
    /// The global position of this body in world space.
    /// Used to convert raycast hit positions from world space to local space.
    /// </summary>
    Vector3 GlobalPosition { get; set; }

    /// <summary>
    /// Finds the nearest Voronoi cell to the given world-space position.
    /// </summary>
    /// <param name="position">World-space position (typically from a raycast hit).</param>
    /// <returns>A <see cref="CellSelectionResult"/> if a cell was found, or null otherwise.</returns>
    CellSelectionResult? FindNearestCell(Vector3 position);
}
