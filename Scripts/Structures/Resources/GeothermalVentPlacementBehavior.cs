using Godot;
using Structures.GameState;

namespace Structures.Resources;

/// <summary>
/// Placement behavior for geothermal plants. Requires the cell to have a
/// geothermal vent assigned during procedural generation
/// (<see cref="VoronoiCell.HasGeothermalVent"/>). Vents are placed per-cell by
/// the GeothermalVentGenerator pipeline stage, weighted by biome and tectonic
/// boundary type.
/// </summary>
public partial class GeothermalVentPlacementBehavior : RefCounted, IPlacementBehavior
{
    public bool IsValidPlacement(VoronoiCell cell)
    {
        return cell.HasGeothermalVent;
    }
}
