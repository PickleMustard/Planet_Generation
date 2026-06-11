namespace Structures.Enums;

/// <summary>
/// Player-facing richness tier of a resource deposit within a cell. Buckets the raw abundance
/// percentage (0–1, stored on <c>VoronoiCell.Resources</c>) into seven legible categories so the
/// percentage→extraction relationship is obvious. Distinct from <see cref="AvailabilityLevel"/>,
/// which is the per-biome group <em>patch-coverage input</em> to generation — a different axis.
///
/// Each category maps to a fraction-of-a-stack per work cycle: <see cref="Common"/> = 1/1; tiers
/// below it grow the denominator, tiers above it grow the numerator. The % ranges and fractions
/// are configurable; see <c>AbundanceCategoryTable</c> and
/// <c>Configuration/ResourceDefinition/abundance_categories.yaml</c>.
/// </summary>
public enum AbundanceCategory
{
    /// <summary>Poorest deposits — smallest fraction per cycle.</summary>
    Rare,
    Scarce,
    Uncommon,
    /// <summary>Baseline — produces 1/1 stack per work cycle.</summary>
    Common,
    Frequent,
    Abundant,
    /// <summary>Richest deposits — largest fraction per cycle.</summary>
    Plentiful,
}
