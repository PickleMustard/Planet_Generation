using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Deserialization DTO for <c>Configuration/ResourceDefinition/abundance_categories.yaml</c>.
/// </summary>
public class AbundanceCategoryConfig
{
    public List<AbundanceCategoryEntry> Categories { get; set; } = new();
}

/// <summary>One configured richness tier: its abundance-% band and per-cycle fraction.</summary>
public class AbundanceCategoryEntry
{
    public string Name { get; set; } = "";
    public float Min { get; set; }
    public float Max { get; set; }
    public int Numerator { get; set; } = 1;
    public int Denominator { get; set; } = 1;
}

/// <summary>
/// Maps a raw abundance percentage (0–1) to an <see cref="AbundanceCategory"/> and each category
/// to its fraction-of-a-stack per work cycle. Single source of truth shared by extraction and UI.
///
/// Holds hard-coded defaults so it is safe to query before <see cref="Load"/> runs or when the
/// YAML is missing. <see cref="Load"/> overrides those defaults from config; an unspecified or
/// invalid category keeps its default band/fraction.
/// </summary>
public static class AbundanceCategoryTable
{
    private static readonly AbundanceCategory[] Order =
    {
        AbundanceCategory.Rare,
        AbundanceCategory.Scarce,
        AbundanceCategory.Uncommon,
        AbundanceCategory.Common,
        AbundanceCategory.Frequent,
        AbundanceCategory.Abundant,
        AbundanceCategory.Plentiful,
    };

    // Per-category upper bound (on abundance %) and fraction. Kept in ascending-band order so
    // Categorize can scan once. Defaults mirror abundance_categories.yaml.
    private static readonly Dictionary<AbundanceCategory, float> Max = new()
    {
        [AbundanceCategory.Rare] = 0.10f,
        [AbundanceCategory.Scarce] = 0.22f,
        [AbundanceCategory.Uncommon] = 0.40f,
        [AbundanceCategory.Common] = 0.68f,
        [AbundanceCategory.Frequent] = 0.84f,
        [AbundanceCategory.Abundant] = 0.94f,
        [AbundanceCategory.Plentiful] = 1.00f,
    };

    private static readonly Dictionary<AbundanceCategory, float> Fractions = new()
    {
        [AbundanceCategory.Rare] = 1f / 4f,
        [AbundanceCategory.Scarce] = 1f / 3f,
        [AbundanceCategory.Uncommon] = 1f / 2f,
        [AbundanceCategory.Common] = 1f / 1f,
        [AbundanceCategory.Frequent] = 2f / 1f,
        [AbundanceCategory.Abundant] = 3f / 1f,
        [AbundanceCategory.Plentiful] = 4f / 1f,
    };

    /// <summary>
    /// Overrides the default bands/fractions from a parsed config. A null config (or null entry
    /// list) leaves the defaults in place. Entries are matched to categories by name
    /// (case-insensitive); unrecognised names are ignored with a warning.
    /// </summary>
    public static void Load(AbundanceCategoryConfig? config)
    {
        if (config?.Categories == null || config.Categories.Count == 0)
        {
            GameLogger.Warning("AbundanceCategoryTable: no config provided; using built-in defaults");
            return;
        }

        foreach (var entry in config.Categories)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                continue;

            if (!Enum.TryParse<AbundanceCategory>(entry.Name, ignoreCase: true, out var cat))
            {
                GameLogger.Warning($"AbundanceCategoryTable: unknown category '{entry.Name}' — ignored");
                continue;
            }

            Max[cat] = Mathf.Clamp(entry.Max, 0f, 1f);
            int denom = entry.Denominator == 0 ? 1 : entry.Denominator;
            Fractions[cat] = (float)entry.Numerator / denom;
        }

        // The lowest band always starts at 0 and the highest always reaches 1, regardless of
        // configured min values, so the bands cover the whole [0,1] range with no gaps.
        Max[Order[^1]] = 1.0f;

        GameLogger.Info("AbundanceCategoryTable: loaded abundance category configuration");
    }

    /// <summary>
    /// Returns the richness tier whose band contains <paramref name="pct"/>. Values at or below
    /// the lowest band map to <see cref="AbundanceCategory.Rare"/>; values at or above the
    /// highest band map to <see cref="AbundanceCategory.Plentiful"/>.
    /// </summary>
    public static AbundanceCategory Categorize(float pct)
    {
        for (int i = 0; i < Order.Length - 1; i++)
        {
            if (pct < Max[Order[i]])
                return Order[i];
        }
        return Order[^1];
    }

    /// <summary>Fraction-of-a-stack produced per work cycle for <paramref name="category"/>.</summary>
    public static float Fraction(AbundanceCategory category) =>
        Fractions.TryGetValue(category, out var f) ? f : 1f;

    /// <summary>Convenience: maps a raw abundance % straight to its per-cycle fraction.</summary>
    public static float FractionFor(float pct) => Fraction(Categorize(pct));

    /// <summary>Title-cased display name for UI (e.g. "Common").</summary>
    public static string DisplayName(AbundanceCategory category) => category.ToString();
}
