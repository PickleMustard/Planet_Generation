using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;

namespace ProceduralGeneration.MeshGeneration.ResourceGeneration;

/// <summary>
/// Provides visualization utilities for resource deposits on celestial bodies.
/// </summary>
/// <remarks>
/// The ResourceVisualizer applies color tinting to terrain based on dominant resources,
/// blending resource colors with existing biome colors to create visual indicators
/// of resource-rich areas.
/// </remarks>
public static class ResourceVisualizer
{
    /// <summary>
    /// Default tint strength for resource visualization (0-1).
    /// </summary>
    public const float DefaultTintStrength = 0.35f;

    /// <summary>
    /// Applies a resource-based color tint to a base color.
    /// </summary>
    /// <param name="baseColor">The original color (typically from biome assignment).</param>
    /// <param name="resources">Dictionary of resource IDs to abundance values.</param>
    /// <param name="tintStrength">How strongly to apply the resource color (0-1).</param>
    /// <returns>The blended color with resource tinting applied.</returns>
    public static Color ApplyResourceTint(Color baseColor, Dictionary<string, float> resources, float tintStrength = DefaultTintStrength)
    {
        if (resources == null || resources.Count == 0)
            return baseColor;

        var dominantResource = resources.OrderByDescending(r => r.Value).First();
        
        if (dominantResource.Value < 0.1f)
            return baseColor;

        var resourceColor = ResourceDatabase.Instance.GetResourceColor(dominantResource.Key);
        float effectiveTint = Mathf.Clamp(dominantResource.Value * tintStrength, 0f, 0.5f);

        return baseColor.Lerp(resourceColor, effectiveTint);
    }

    /// <summary>
    /// Gets the color for a specific resource deposit.
    /// </summary>
    /// <param name="deposit">The resource deposit to visualize.</param>
    /// <returns>The resource color, or white if the resource is not found.</returns>
    public static Color GetDepositColor(ResourceDeposit deposit)
    {
        if (deposit == null || string.IsNullOrEmpty(deposit.ResourceId))
            return Colors.White;

        return ResourceDatabase.Instance.GetResourceColor(deposit.ResourceId);
    }

    /// <summary>
    /// Gets a blended color for multiple resource deposits.
    /// </summary>
    /// <param name="deposits">Dictionary of resource deposits.</param>
    /// <returns>A blended color representing all deposits, weighted by effective yield.</returns>
    public static Color GetBlendedDepositColor(Dictionary<string, ResourceDeposit> deposits)
    {
        if (deposits == null || deposits.Count == 0)
            return Colors.White;

        float totalWeight = 0f;
        Color blendedColor = Colors.Black;

        foreach (var kvp in deposits)
        {
            float weight = kvp.Value.GetEffectiveYield();
            Color resourceColor = ResourceDatabase.Instance.GetResourceColor(kvp.Key);
            blendedColor += resourceColor * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0)
            return blendedColor / totalWeight;

        return Colors.White;
    }

    /// <summary>
    /// Creates a summary dictionary of resource colors and their weights for a cell.
    /// </summary>
    /// <param name="resources">Dictionary of resource IDs to abundance values.</param>
    /// <returns>Dictionary mapping resource IDs to (color, weight) tuples.</returns>
    public static Dictionary<string, (Color color, float weight)> GetResourceColorWeights(Dictionary<string, float> resources)
    {
        var result = new Dictionary<string, (Color color, float weight)>();

        if (resources == null)
            return result;

        foreach (var kvp in resources)
        {
            Color color = ResourceDatabase.Instance.GetResourceColor(kvp.Key);
            result[kvp.Key] = (color, kvp.Value);
        }

        return result;
    }
}
