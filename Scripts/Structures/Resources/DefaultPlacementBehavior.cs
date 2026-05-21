using Godot;
using Structures.GameState;

namespace Structures.Resources;

/// <summary>
/// Default placement validation logic using biome, elevation, and slope constraints.
/// This is used when no custom behavior is specified.
/// </summary>
public class DefaultPlacementBehavior : IPlacementBehavior
{
    private readonly BuildingDefinition.PlacementRequirements _requirements;

    public DefaultPlacementBehavior(BuildingDefinition.PlacementRequirements requirements)
    {
        _requirements = requirements;
    }

    public bool IsValidPlacement(VoronoiCell cell)
    {
        if (cell.NormalizedHeight < _requirements.MinElevation ||
            cell.NormalizedHeight > _requirements.MaxElevation)
            return false;

        if (cell.GetSlope() > _requirements.MaxSlope)
            return false;

        if (!_requirements.AllowAnyBiome && _requirements.Biomes.Count > 0)
        {
            if (!_requirements.Biomes.Contains(cell.Biome))
                return false;
        }

        return true;
    }
}
