using System.Collections.Generic;
using GdUnit4;
using Godot;
using ProceduralGeneration;
using static GdUnit4.Assertions;

namespace Tests.ProceduralGeneration;

/// <summary>
/// Unit coverage for <see cref="SubtypeResolver.SelectFromWeights"/> — the per-body weighted
/// subtype roll that replaced the global AU-band probability system.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SubtypeWeightedRollTest
{
    private RandomNumberGenerator New(ulong seed = 42UL) =>
        new RandomNumberGenerator { Seed = seed };

    [TestCase]
    public void EmptyMap_ReturnsFallback()
    {
        var rng = New();
        AssertThat(SubtypeResolver.SelectFromWeights(new Dictionary<string, float>(), rng, "fallback"))
            .IsEqual("fallback");
    }

    [TestCase]
    public void AllZeroWeights_ReturnsFallback()
    {
        var rng = New();
        var weights = new Dictionary<string, float> { ["a"] = 0f, ["b"] = 0f };
        AssertThat(SubtypeResolver.SelectFromWeights(weights, rng, "fallback")).IsEqual("fallback");
    }

    [TestCase]
    public void SingleWeight_AlwaysSelected()
    {
        var rng = New();
        var weights = new Dictionary<string, float> { ["only"] = 1f };
        for (int i = 0; i < 50; i++)
            AssertThat(SubtypeResolver.SelectFromWeights(weights, rng, "fb")).IsEqual("only");
    }

    [TestCase]
    public void RollWithinDeclaredKeys()
    {
        var rng = New();
        var weights = new Dictionary<string, float>
        {
            ["subtype_rocky_temperate"] = 0.5f,
            ["subtype_rocky_desert"] = 0.5f,
        };
        var seen = new HashSet<string>();
        for (int i = 0; i < 200; i++)
            seen.Add(SubtypeResolver.SelectFromWeights(weights, rng, "fb"));

        AssertThat(seen.Contains("subtype_rocky_temperate")).IsTrue();
        AssertThat(seen.Contains("subtype_rocky_desert")).IsTrue();
        foreach (var s in seen) AssertThat(weights.ContainsKey(s)).IsTrue();
    }

    [TestCase]
    public void HeavyWeight_SkewsDistribution()
    {
        var rng = New();
        var weights = new Dictionary<string, float>
        {
            ["heavy"] = 9f,
            ["light"] = 1f,
        };

        int heavy = 0;
        const int trials = 1000;
        for (int i = 0; i < trials; i++)
            if (SubtypeResolver.SelectFromWeights(weights, rng, "") == "heavy") heavy++;

        // expected ~900, allow ±100 slack for 1k-trial variance
        AssertThat(heavy > 750)
            .OverrideFailureMessage($"heavy={heavy}/{trials}, expected ~900")
            .IsTrue();
    }

    [TestCase]
    public void NegativeWeights_AreIgnored()
    {
        var rng = New();
        var weights = new Dictionary<string, float>
        {
            ["valid"] = 1f,
            ["broken"] = -1f,
        };
        for (int i = 0; i < 30; i++)
            AssertThat(SubtypeResolver.SelectFromWeights(weights, rng, "fb")).IsEqual("valid");
    }
}
