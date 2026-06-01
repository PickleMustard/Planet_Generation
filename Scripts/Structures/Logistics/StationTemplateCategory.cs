namespace Structures.Logistics;

/// <summary>
/// Represents a station template category from the configuration.
/// Categories are used to organize and validate station types.
/// </summary>
public class StationTemplateCategory
{
    /// <summary>
    /// The name of the category (e.g., "Shipyard", "Industrial", "Research").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what stations in this category do.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
