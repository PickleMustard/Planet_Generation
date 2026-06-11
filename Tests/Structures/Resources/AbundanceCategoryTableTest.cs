using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Enums;
using Structures.Resources;

namespace Tests.Structures.Resources;

/// <summary>
/// Pure unit tests for <see cref="AbundanceCategoryTable"/> — the float-% → richness-tier
/// mapping and the per-tier fraction-of-a-stack ladder. Uses the built-in defaults (which mirror
/// <c>abundance_categories.yaml</c>); the Load test restores defaults afterwards so it cannot
/// leak custom bands into sibling suites.
/// </summary>
[TestSuite]
public class AbundanceCategoryTableTest
{
    /// <summary>Rebuilds the default config object (matches abundance_categories.yaml).</summary>
    private static AbundanceCategoryConfig DefaultConfig() => new()
    {
        Categories = new List<AbundanceCategoryEntry>
        {
            new() { Name = "rare", Min = 0.00f, Max = 0.10f, Numerator = 1, Denominator = 4 },
            new() { Name = "scarce", Min = 0.10f, Max = 0.22f, Numerator = 1, Denominator = 3 },
            new() { Name = "uncommon", Min = 0.22f, Max = 0.40f, Numerator = 1, Denominator = 2 },
            new() { Name = "common", Min = 0.40f, Max = 0.68f, Numerator = 1, Denominator = 1 },
            new() { Name = "frequent", Min = 0.68f, Max = 0.84f, Numerator = 2, Denominator = 1 },
            new() { Name = "abundant", Min = 0.84f, Max = 0.94f, Numerator = 3, Denominator = 1 },
            new() { Name = "plentiful", Min = 0.94f, Max = 1.00f, Numerator = 4, Denominator = 1 },
        },
    };

    [TestCase]
    public void Categorize_MapsEachBand()
    {
        AssertThat(AbundanceCategoryTable.Categorize(0.05f)).IsEqual(AbundanceCategory.Rare);
        AssertThat(AbundanceCategoryTable.Categorize(0.15f)).IsEqual(AbundanceCategory.Scarce);
        AssertThat(AbundanceCategoryTable.Categorize(0.30f)).IsEqual(AbundanceCategory.Uncommon);
        AssertThat(AbundanceCategoryTable.Categorize(0.50f)).IsEqual(AbundanceCategory.Common);
        AssertThat(AbundanceCategoryTable.Categorize(0.70f)).IsEqual(AbundanceCategory.Frequent);
        AssertThat(AbundanceCategoryTable.Categorize(0.90f)).IsEqual(AbundanceCategory.Abundant);
        AssertThat(AbundanceCategoryTable.Categorize(0.97f)).IsEqual(AbundanceCategory.Plentiful);
    }

    [TestCase]
    public void Categorize_BandEdges_BelongToUpperBand()
    {
        // A value exactly on a band's upper bound falls into the next band up.
        AssertThat(AbundanceCategoryTable.Categorize(0.10f)).IsEqual(AbundanceCategory.Scarce);
        AssertThat(AbundanceCategoryTable.Categorize(0.40f)).IsEqual(AbundanceCategory.Common);
        AssertThat(AbundanceCategoryTable.Categorize(0.94f)).IsEqual(AbundanceCategory.Plentiful);
    }

    [TestCase]
    public void Categorize_ClampsOutOfRange()
    {
        AssertThat(AbundanceCategoryTable.Categorize(-0.5f)).IsEqual(AbundanceCategory.Rare);
        AssertThat(AbundanceCategoryTable.Categorize(0f)).IsEqual(AbundanceCategory.Rare);
        AssertThat(AbundanceCategoryTable.Categorize(2.0f)).IsEqual(AbundanceCategory.Plentiful);
    }

    [TestCase]
    public void Fraction_FollowsLadder()
    {
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Rare)).IsEqual(1f / 4f);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Scarce)).IsEqual(1f / 3f);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Uncommon)).IsEqual(1f / 2f);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Common)).IsEqual(1f);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Frequent)).IsEqual(2f);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Abundant)).IsEqual(3f);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Plentiful)).IsEqual(4f);
    }

    [TestCase]
    public void FractionFor_ComposesCategorizeAndFraction()
    {
        AssertThat(AbundanceCategoryTable.FractionFor(0.5f)).IsEqual(1f);   // common
        AssertThat(AbundanceCategoryTable.FractionFor(0.8f)).IsEqual(2f);   // frequent
        AssertThat(AbundanceCategoryTable.FractionFor(0.05f)).IsEqual(1f / 4f); // rare
    }

    [TestCase]
    public void DisplayName_IsTitleCased()
    {
        AssertThat(AbundanceCategoryTable.DisplayName(AbundanceCategory.Common)).IsEqual("Common");
        AssertThat(AbundanceCategoryTable.DisplayName(AbundanceCategory.Plentiful)).IsEqual("Plentiful");
    }

    [TestCase]
    [RequireGodotRuntime] // Load logs via GameLogger
    public void Load_CustomConfig_OverridesBandsThenRestores()
    {
        // Widen "rare" to swallow everything below 0.5 and make it produce a full stack.
        var custom = new AbundanceCategoryConfig
        {
            Categories = new List<AbundanceCategoryEntry>
            {
                new() { Name = "rare", Min = 0.00f, Max = 0.50f, Numerator = 1, Denominator = 1 },
            },
        };
        AbundanceCategoryTable.Load(custom);

        AssertThat(AbundanceCategoryTable.Categorize(0.30f)).IsEqual(AbundanceCategory.Rare);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Rare)).IsEqual(1f);

        // Restore defaults so other suites see the standard ladder.
        AbundanceCategoryTable.Load(DefaultConfig());
        AssertThat(AbundanceCategoryTable.Categorize(0.30f)).IsEqual(AbundanceCategory.Uncommon);
        AssertThat(AbundanceCategoryTable.Fraction(AbundanceCategory.Rare)).IsEqual(1f / 4f);
    }
}
