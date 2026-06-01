using System.Collections.Generic;
using GdUnit4;
using Godot;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using static GdUnit4.Assertions;

namespace Tests.ResourceGeneration;

/*
[TestSuite]
public class SatelliteGeneratorTest
{
    private Godot.Collections.Dictionary CreateMockSatelliteConfig()
    {
        return new Godot.Collections.Dictionary
        {
            ["main"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "iron_ore",
                    ["base_weight"] = 1.0f,
                    ["distance_weight"] = 1.0f
                },
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "gold_ore",
                    ["base_weight"] = 0.5f,
                    ["distance_weight"] = 1.0f
                }
            },
            ["secondary"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "copper_ore",
                    ["base_weight"] = 0.7f,
                    ["distance_weight"] = 1.0f
                },
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "cobalt_ore",
                    ["base_weight"] = 0.4f,
                    ["distance_weight"] = 1.0f
                }
            }
        };
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BasicGeneration()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        var config = CreateMockSatelliteConfig();
        var deposits = SatelliteResourceGenerator.GenerateResources(config, rng);

        AssertThat(deposits).IsNotNull();
        AssertThat(deposits.Count).IsGreater(0);

        foreach (var kvp in deposits)
        {
            AssertThat(kvp.Key).IsNotEmpty();
            AssertThat(kvp.Value).IsNotNull();
            AssertThat(kvp.Value.Abundance).IsGreater(0);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EmptyConfig()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        var deposits1 = SatelliteResourceGenerator.GenerateResources(null!, rng);
        AssertThat(deposits1).IsNotNull();
        AssertThat(deposits1.Count).IsEqual(0);

        var deposits2 = SatelliteResourceGenerator.GenerateResources(new Godot.Collections.Dictionary(), rng);
        AssertThat(deposits2.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WeightedSelection()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        var config = CreateMockSatelliteConfig();
        var selectionCounts = new Dictionary<string, int>();

        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            rng.Seed = (ulong)(12345 + i);
            var deposits = SatelliteResourceGenerator.GenerateResources(config, rng);
            foreach (var kvp in deposits)
            {
                if (!selectionCounts.ContainsKey(kvp.Key))
                    selectionCounts[kvp.Key] = 0;
                selectionCounts[kvp.Key]++;
            }
        }

        AssertThat(selectionCounts.ContainsKey("iron_ore")).IsTrue();
        AssertThat(selectionCounts["iron_ore"]).IsGreater((int)(iterations * 0.3));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResourceValidation()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 99999;

        var invalidConfig = new Godot.Collections.Dictionary
        {
            ["main"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "nonexistent_resource",
                    ["base_weight"] = 1.0f,
                    ["distance_weight"] = 1.0f
                }
            }
        };

        var deposits = SatelliteResourceGenerator.GenerateResources(invalidConfig, rng);
        AssertThat(deposits.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DeterministicGeneration()
    {
        var config = CreateMockSatelliteConfig();

        var rng1 = new RandomNumberGenerator();
        rng1.Seed = 42;
        var deposits1 = SatelliteResourceGenerator.GenerateResources(config, rng1);

        var rng2 = new RandomNumberGenerator();
        rng2.Seed = 42;
        var deposits2 = SatelliteResourceGenerator.GenerateResources(config, rng2);

        AssertThat(deposits1.Count).IsEqual(deposits2.Count);

        foreach (var kvp in deposits1)
        {
            AssertThat(deposits2.ContainsKey(kvp.Key)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(kvp.Value.Abundance, deposits2[kvp.Key].Abundance, 0.001f)).IsTrue();
        }
    }
}*/
