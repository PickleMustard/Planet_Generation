using Structures.GameState;

namespace Structures.Resources;

/// <summary>
/// Defines custom placement validation logic for buildings.
/// Implementations can check for specific cell properties beyond the standard biome/elevation/slope checks.
/// </summary>
public interface IPlacementBehavior
{
    /// <summary>
    /// Validates if a Voronoi cell is valid for building placement.
    /// </summary>
    /// <param name="cell">The Voronoi cell to validate.</param>
    /// <returns>True if the cell is valid for placement; otherwise false.</returns>
    bool IsValidPlacement(VoronoiCell cell);

    /// <summary>
    /// Body-aware overload. Default implementation delegates to the cell-only overload.
    /// Behaviors that need to inspect body-level properties (atmosphere, parent star
    /// distance, classification) should override this and ignore the cell-only path.
    /// </summary>
    /// <param name="cell">The Voronoi cell to validate.</param>
    /// <param name="body">The body that owns the cell, or null when the caller can't resolve it.</param>
    bool IsValidPlacement(VoronoiCell cell, IOrbitalBody? body) => IsValidPlacement(cell);
}
