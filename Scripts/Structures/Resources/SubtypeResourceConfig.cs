using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Configuration for resources associated with a specific celestial body subtype.
/// Defines generatable resources via named resource groups + explicit add/remove lists.
/// </summary>
public class SubtypeResourceConfig
{
    /// <summary>
    /// The string representation of the enum subtype (e.g., "Temperate", "Desert", etc.)
    /// </summary>
    public string? Subtype { get; set; }

    /// <summary>
    /// Named group references to include in resource resolution.
    /// Each group name maps to a list of resource IDs in ResourceGroups dictionary.
    /// </summary>
    public List<string> ResourceGroups { get; set; } = new();

    /// <summary>
    /// Explicit resource ID additions after group resolution.
    /// These resources are added after expanding all groups.
    /// </summary>
    public List<string> AddResources { get; set; } = new();

    /// <summary>
    /// Explicit resource ID removals after group resolution.
    /// These resources are removed from the resolved set.
    /// </summary>
    public List<string> RemoveResources { get; set; } = new();

    /// <summary>
    /// Base weight multiplier for resource generation on this subtype.
    /// Values between 0.0 and 2.0 are recommended.
    /// Default: 1.0 (normal generation rate)
    /// </summary>
    public float BaseResourceWeight { get; set; } = 1.0f;

    /// <summary>
    /// The resolved set of resource IDs after expanding groups and applying add/remove.
    /// Populated by ResolveResources(). Not serialized to YAML.
    /// </summary>
    public HashSet<string> ResolvedResources { get; private set; } = new();

    /// <summary>
    /// Resolves all named groups into resource IDs, applies AddResources,
    /// then removes RemoveResources. Stores result in ResolvedResources.
    /// </summary>
    /// <param name="groupDefinitions">Dictionary mapping group names to their resource ID lists</param>
    public void ResolveResources(Dictionary<string, List<string>> groupDefinitions)
    {
        ResolvedResources.Clear();

        // Expand each named group
        foreach (var groupName in ResourceGroups)
        {
            if (groupDefinitions != null && groupDefinitions.TryGetValue(groupName, out var resources))
            {
                foreach (var resourceId in resources)
                {
                    ResolvedResources.Add(resourceId);
                }
            }
            else
            {
                GameLogger.Warning($"SubtypeResourceConfig '{Subtype}': Unknown group name '{groupName}'");
            }
        }

        // Apply explicit adds
        foreach (var resourceId in AddResources)
        {
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                ResolvedResources.Add(resourceId);
            }
        }

        // Apply explicit removes
        foreach (var resourceId in RemoveResources)
        {
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                ResolvedResources.Remove(resourceId);
            }
        }
    }

    /// <summary>
    /// Validates the configuration for consistency and reasonable values.
    /// Note: Does NOT validate resource IDs exist - that happens at PlanetaryResourceConfig level.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Subtype))
        {
            GameLogger.Error("SubtypeResourceConfig: Subtype is null or empty");
            return false;
        }

        if (BaseResourceWeight < 0.0f || BaseResourceWeight > 2.0f)
        {
            GameLogger.Error($"SubtypeResourceConfig '{Subtype}': BaseResourceWeight {BaseResourceWeight} is outside valid range [0.0, 2.0]");
            return false;
        }

        if (ResourceGroups == null)
        {
            GameLogger.Error($"SubtypeResourceConfig '{Subtype}': ResourceGroups collection is null");
            return false;
        }

        if (AddResources == null)
        {
            GameLogger.Error($"SubtypeResourceConfig '{Subtype}': AddResources collection is null");
            return false;
        }

        if (RemoveResources == null)
        {
            GameLogger.Error($"SubtypeResourceConfig '{Subtype}': RemoveResources collection is null");
            return false;
        }

        // Validate no empty group names
        foreach (var group in ResourceGroups)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                GameLogger.Error($"SubtypeResourceConfig '{Subtype}': Contains empty or whitespace group name");
                return false;
            }
        }

        // Validate no empty resource IDs in add/remove lists
        foreach (var resourceId in AddResources)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                GameLogger.Error($"SubtypeResourceConfig '{Subtype}': AddResources contains empty or whitespace resource ID");
                return false;
            }
        }

        foreach (var resourceId in RemoveResources)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                GameLogger.Error($"SubtypeResourceConfig '{Subtype}': RemoveResources contains empty or whitespace resource ID");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the resource weight multiplier for this subtype.
    /// </summary>
    /// <returns>The base resource weight, clamped to valid range [0.0, 2.0]</returns>
    public float GetResourceWeight()
    {
        return Mathf.Clamp(BaseResourceWeight, 0.0f, 2.0f);
    }
}