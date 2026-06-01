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
    /// The radius of this body in world units.
    /// Used for building scaling and other size-dependent calculations.
    /// </summary>
    float Radius { get; }

    /// <summary>
    /// Finds the nearest Voronoi cell to the given world-space position.
    /// </summary>
    /// <param name="position">World-space position (typically from a raycast hit).</param>
    /// <returns>A <see cref="CellSelectionResult"/> if a cell was found, or null otherwise.</returns>
    CellSelectionResult? FindNearestCell(Vector3 position);

    /// <summary>
    /// Returns the Voronoi cell from a cached face index
    /// </summary>
    /// <param name="faceIndex">The index of the face in the mesh</param>
    /// <returns>A <see cref="CellSelectionResult"/> if a cell was found, or null otherwise.</returns>
    CellSelectionResult? GetFaceFromIndex(int faceIndex);

    /// <summary>
    /// Gets neighboring Voronoi cells for the given origin cell using the runtime PlanetMap.
    /// Unlike <see cref="UnifiedCelestialMesh.GetCellNeighbors"/>, this uses PlanetMap which
    /// survives mesh finalization and is available during gameplay.
    /// </summary>
    /// <param name="origin">The cell to find neighbors for.</param>
    /// <param name="includeSameContinent">If true, includes neighbors from the same continent.</param>
    /// <returns>Array of neighboring Voronoi cells.</returns>
    VoronoiCell[] GetRuntimeCellNeighbors(VoronoiCell origin, bool includeSameContinent = true);

    /// <summary>
    /// The camera anchor (Node3D) attached to this body for positioning the camera.
    /// Created on-demand via GetOrCreateCameraAnchor().
    /// </summary>
    Node3D? CameraAnchor { get; }

    /// <summary>
    /// Gets or creates the camera anchor for this body.
    /// Anchor is created as a child Node3D named "CameraAnchor".
    /// </summary>
    /// <returns>The CameraAnchor node.</returns>
    Node3D GetOrCreateCameraAnchor();

    /// <summary>
    /// Positions the camera anchor at a world-space position looking at an optional target.
    /// </summary>
    /// <param name="worldPosition">The world-space position for the anchor.</param>
    /// <param name="lookAtTarget">Optional world-space target to look at. If null, looks at body center.</param>
    void PositionCameraAnchor(Vector3 worldPosition, Vector3? lookAtTarget = null);

    /// <summary>
    /// Rotates the camera anchor around its current position using spherical coordinates.
    /// </summary>
    /// <param name="yaw">Yaw angle in radians (horizontal rotation around Y axis).</param>
    /// <param name="pitch">Pitch angle in radians (vertical rotation around X axis).</param>
    void UpdateCameraAnchorRotation(float yaw, float pitch);

    /// <summary>
    /// Positions the camera to focus on a specific cell.
    /// Camera is placed along the cell normal at 1.5x body radius distance.
    /// </summary>
    /// <param name="cell">The VoronoiCell to focus on</param>
    void FocusInspectionCameraOnCell(VoronoiCell cell);

    /// <summary>
    /// Positions the camera to focus on a specific continent.
    /// Camera distance is calculated based on the continent's angular size to ensure
    /// the entire continent is visible within the viewport.
    /// </summary>
    /// <param name="continent">The Continent to focus on</param>
    void FocusInspectionCameraOnContinent(Continent continent);
}
