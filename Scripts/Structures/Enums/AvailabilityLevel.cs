namespace Structures.Enums;

/// <summary>
/// Designer-facing availability of a resource group within a biome. Replaces the old
/// per-resource decimal weight modifiers. A group omitted by a biome is treated as
/// "None" (not generated) — there is intentionally no None member here.
/// Threshold/abundance mapping lives in
/// <c>CellResourceGenerator</c> (see its level table).
/// </summary>
public enum AvailabilityLevel
{
    /// <summary>Large, rich patches — lowest field threshold.</summary>
    Abundant,
    Frequent,
    Normal,
    Scarce,
    /// <summary>Small, lean patches — highest field threshold.</summary>
    Rare,
}
