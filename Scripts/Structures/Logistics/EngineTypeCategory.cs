namespace Structures.Logistics;

/// <summary>
/// Represents an engine type category from the configuration.
/// Categories are used to organize and validate engine types (e.g., "Chemical", "Nuclear", "Fusion").
/// </summary>
public class EngineTypeCategory
{
    /// <summary>
    /// The name of the category (e.g., "Chemical", "Electric", "Nuclear", "Fusion").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of engines in this category.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
