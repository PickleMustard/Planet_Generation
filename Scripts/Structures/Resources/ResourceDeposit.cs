using Godot;

namespace Structures.Resources;

/// <summary>
/// Represents a deposit of a specific resource on a celestial body or terrain cell.
/// </summary>
/// <remarks>
/// Resource deposits store information about the type of resource present,
/// its abundance (how much is available), and accessibility (how easy it is to extract).
/// This class is used by both satellite bodies and continental terrain cells.
/// </remarks>
public class ResourceDeposit
{
    /// <summary>
    /// The unique identifier of the resource type.
    /// Corresponds to a ResourceDefinition's IdName.
    /// </summary>
    public string ResourceId { get; set; }

    /// <summary>
    /// Normalized abundance value from 0 to 1.
    /// Higher values indicate larger deposits of the resource.
    /// </summary>
    public float Abundance { get; set; }

    /// <summary>
    /// How easy the resource is to extract, from 0 to 1.
    /// Higher values indicate easier extraction (surface deposits, softer material).
    /// Lower values indicate difficult extraction (deep deposits, hard material).
    /// </summary>
    public float Accessibility { get; set; }

    public ResourceDeposit()
    {
        ResourceId = string.Empty;
        Abundance = 0.0f;
        Accessibility = 1.0f;
    }

    public ResourceDeposit(string resourceId, float abundance, float accessibility = 1.0f)
    {
        ResourceId = resourceId;
        Abundance = Mathf.Clamp(abundance, 0.0f, 1.0f);
        Accessibility = Mathf.Clamp(accessibility, 0.0f, 1.0f);
    }

    /// <summary>
    /// Calculates the effective yield of this deposit.
    /// Takes into account both abundance and accessibility.
    /// </summary>
    /// <returns>A value representing the effective extractable amount.</returns>
    public float GetEffectiveYield()
    {
        return Abundance * Accessibility;
    }

    public override string ToString()
    {
        return $"ResourceDeposit({ResourceId}, Abundance: {Abundance:F2}, Accessibility: {Accessibility:F2})";
    }
}
