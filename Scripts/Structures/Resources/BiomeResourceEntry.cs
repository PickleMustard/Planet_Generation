using System.Collections.Generic;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Configuration for resource group availability within a specific biome.
/// Biomes assign each eligible resource group one of five <see cref="AvailabilityLevel"/>
/// choices instead of tuning per-resource decimal weights. A group not listed here is
/// treated as "None" (not generated in this biome). Biomes only gate/scale probability —
/// they cannot add resources a body subtype does not already allow.
/// </summary>
public class BiomeResourceEntry
{
    /// <summary>
    /// The biome type this configuration applies to.
    /// </summary>
    public Biome.BiomeType Biome { get; set; }

    /// <summary>
    /// Key: resource group name (e.g. "commonOres"), Value: availability level.
    /// Groups absent from this map are not generated in this biome.
    /// </summary>
    public Dictionary<string, AvailabilityLevel> GroupAvailability { get; set; } = new();

    /// <summary>
    /// Gets the availability level for a resource group, or null if the group is not
    /// listed (meaning the group does not generate in this biome).
    /// </summary>
    public AvailabilityLevel? GetGroupAvailability(string groupName)
    {
        if (GroupAvailability != null && GroupAvailability.TryGetValue(groupName, out var level))
        {
            return level;
        }
        return null;
    }

    /// <summary>
    /// Validates the configuration for consistency.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        if (GroupAvailability == null)
        {
            GameLogger.Error($"BiomeResourceEntry '{Biome}': GroupAvailability collection is null");
            return false;
        }

        foreach (var kvp in GroupAvailability)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                GameLogger.Error($"BiomeResourceEntry '{Biome}': Contains empty or whitespace group name");
                return false;
            }
        }

        return true;
    }
}
