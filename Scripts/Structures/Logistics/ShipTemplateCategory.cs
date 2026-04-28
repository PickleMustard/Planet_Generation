namespace Structures.Logistics;

/// <summary>
/// Represents a ship template category from the configuration.
/// Categories are used to organize and validate ship types.
/// </summary>
public class ShipTemplateCategory
{
    /// <summary>
    /// The name of the category (e.g., "Cargo_Freighter", "Fast_Courier", "Heavy_Transport").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what ships in this category are used for.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
