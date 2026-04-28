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
}
