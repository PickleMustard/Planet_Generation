using System.Collections.Generic;
using GdUnit4;
using Godot;
using ProceduralGeneration;
using static GdUnit4.Assertions;

namespace Tests.ProceduralGeneration;

/// <summary>
/// Phase 7 unit coverage for <see cref="AUProbabilityManager.SelectFromWeights"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SubtypeWeightedRollTest
{
    private AUProbabilityManager New(ulong seed = 42UL)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        return new AUProbabilityManager(rng);
    }

    [TestCase]
    public void EmptyMap_ReturnsFallback()
    {
        var sel = New();
        AssertThat(sel.SelectFromWeights(new Dictionary<string, float>(), "fallback"))
            .IsEqual("fallback");
    }

    [TestCase]
    public void AllZeroWeights_ReturnsFallback()
    {
        var sel = New();
        var weights = new Dictionary<string, float> { ["a"] = 0f, ["b"] = 0f };
        AssertThat(sel.SelectFromWeights(weights, "fallback")).IsEqual("fallback");
    }

    [TestCase]
    public void SingleWeight_AlwaysSelected()
    {
        var sel = New();
        var weights = new Dictionary<string, float> { ["only"] = 1f };
        for (int i = 0; i < 50; i++)
            AssertThat(sel.SelectFromWeights(weights, "fb")).IsEqual("only");
    }

    [TestCase]
    public void RollWithinDeclaredKeys()
    {
        var sel = New();
        var weights = new Dictionary<string, float>
        {
            ["subtype_rocky_temperate"] = 0.5f,
            ["subtype_rocky_desert"] = 0.5f,
        };
        var seen = new HashSet<string>();
        for (int i = 0; i < 200; i++)
            seen.Add(sel.SelectFromWeights(weights, "fb"));

        AssertThat(seen.Contains("subtype_rocky_temperate")).IsTrue();
        AssertThat(seen.Contains("subtype_rocky_desert")).IsTrue();
        foreach (var s in seen) AssertThat(weights.ContainsKey(s)).IsTrue();
    }

    [TestCase]
    public void HeavyWeight_SkewsDistribution()
    {
        var sel = New();
        var weights = new Dictionary<string, float>
        {
            ["heavy"] = 9f,
            ["light"] = 1f,
        };

        int heavy = 0;
        const int trials = 1000;
        for (int i = 0; i < trials; i++)
            if (sel.SelectFromWeights(weights, "") == "heavy") heavy++;

        // expected ~900, allow ±100 slack for 1k-trial variance
        AssertThat(heavy > 750)
            .OverrideFailureMessage($"heavy={heavy}/{trials}, expected ~900")
            .IsTrue();
    }

    [TestCase]
    public void NegativeWeights_AreIgnored()
    {
        var sel = New();
        var weights = new Dictionary<string, float>
        {
            ["valid"] = 1f,
            ["broken"] = -1f,
        };
        for (int i = 0; i < 30; i++)
            AssertThat(sel.SelectFromWeights(weights, "fb")).IsEqual("valid");
    }
}
