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
        // Check elevation using cell height (normalized to 0-1 range)
        if (cell.Height < _requirements.MinElevation || cell.Height > _requirements.MaxElevation)
            return false;

        // Check slope (placeholder - need actual slope calculation)
        if (_requirements.MaxSlope < 0.0f || _requirements.MaxSlope > 90.0f)
            return false;

        // Check biomes - skip if AllowAnyBiome is true
        if (!_requirements.AllowAnyBiome && _requirements.Biomes.Count > 0)
        {
            // Future: Use cell.Biome property when comparing
            // if (!_requirements.Biomes.Contains(cell.Biome))
            //     return false;
        }

        return true;
    }
}
