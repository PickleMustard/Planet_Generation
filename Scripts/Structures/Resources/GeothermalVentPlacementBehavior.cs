using Godot;
using Structures.Enums;
using Structures.GameState;

namespace Structures.Resources;

/// <summary>
/// Placement behavior that requires cells to have geothermal vent properties.
/// Example of a specialized placement behavior for the geothermal power plant.
/// </summary>
public partial class GeothermalVentPlacementBehavior : RefCounted, IPlacementBehavior
{
    public bool IsValidPlacement(VoronoiCell cell)
    {
        // Check for volcanic biomes (where vents are likely)
        bool isVolcanicBiome = cell.Biome == Biome.BiomeType.VolcanicPeak ||
                               cell.Biome == Biome.BiomeType.VolcanicPlain ||
                               cell.Biome == Biome.BiomeType.AshPlain ||
                               cell.Biome == Biome.BiomeType.ObsidianField;

        if (!isVolcanicBiome)
            return false;

        // Check for high stress (indicates tectonic activity)
        if (cell.Stress < 0.5f)
            return false;

        // Check for sufficient elevation
        if (cell.Height < 0.4f || cell.Height > 0.95f)
            return false;

        // Check if cell has geothermal resources
        if (cell.Resources.Count > 0)
        {
            foreach (var resource in cell.Resources)
            {
                if (resource.Key.Contains("geothermal", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        // Volcanic + high stress + elevation is sufficient for now
        return true;
    }
}
