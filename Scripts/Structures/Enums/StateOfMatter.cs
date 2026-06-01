namespace Structures.Enums;

/// <summary>
/// Physical state of a resource. Storage slots filter by matter compatibility,
/// so fluids cannot be placed in slots designated for solids and vice versa.
/// </summary>
public enum StateOfMatter
{
    Solid,
    Fluid
}

public static class StateOfMatterExtensions
{
    public static StateOfMatter Parse(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "solid" => StateOfMatter.Solid,
            "fluid" => StateOfMatter.Fluid,
            "liquid" => StateOfMatter.Fluid,
            "gas" => StateOfMatter.Fluid,
            _ => throw new System.ArgumentException(
                $"Unknown state_of_matter value: '{value}'. Expected 'solid' or 'fluid'.")
        };
    }
}
