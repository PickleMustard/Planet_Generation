using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.ResourceGeneration;

[TestSuite]
public class BiomeCategoryTest
{
    private BiomeCategoryConfig? _config;

    [BeforeTest]
    public void Setup()
    {
        _config = ResourceConfigLoader.LoadBiomeCategories();
    }

    [AfterTest]
    public void Teardown()
    {
        _config = null;
    }

    private static string Id(Biome.BiomeType b) => BiomeIdMapper.BiomeTypeToId(b);

    [TestCase]
    [RequireGodotRuntime]
    public void ConfigLoadsSuccessfully()
    {
        AssertThat(_config).IsNotNull();
        AssertThat(_config!.Categories).IsNotNull();
        AssertThat(_config.Categories.Count).IsGreater(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ConfigContainsExpectedCategories()
    {
        AssertThat(_config).IsNotNull();

        var expectedCategories = new[]
        {
            "flat", "mountain", "rocky", "arable", "temperate",
            "tropical", "desert", "ocean", "deep_ocean", "frozen",
            "terrestrial", "volcanic", "rusted", "scoured", "coastal",
            "forested"
        };

        foreach (var categoryId in expectedCategories)
        {
            AssertThat(_config!.HasCategory(categoryId))
                .OverrideFailureMessage($"Expected category '{categoryId}' not found")
                .IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MountainCategoryContainsMountainBiomes()
    {
        AssertThat(_config).IsNotNull();

        var mountainBiomes = _config!.GetBiomesForCategory("mountain");
        AssertThat(mountainBiomes).IsNotNull();
        AssertThat(mountainBiomes!.Contains(Id(Biome.BiomeType.Mountain))).IsTrue();
        AssertThat(mountainBiomes.Contains(Id(Biome.BiomeType.VolcanicPeak))).IsTrue();
        AssertThat(mountainBiomes.Contains(Id(Biome.BiomeType.RustedMountain))).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ArableCategoryContainsFarmableBiomes()
    {
        AssertThat(_config).IsNotNull();

        var arableBiomes = _config!.GetBiomesForCategory("arable");
        AssertThat(arableBiomes).IsNotNull();
        AssertThat(arableBiomes!.Contains(Id(Biome.BiomeType.Grassland))).IsTrue();
        AssertThat(arableBiomes.Contains(Id(Biome.BiomeType.Forest))).IsTrue();
        AssertThat(arableBiomes.Contains(Id(Biome.BiomeType.Rainforest))).IsTrue();
        AssertThat(arableBiomes.Contains(Id(Biome.BiomeType.Coastal))).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CategoryLookupIsCaseInsensitive()
    {
        AssertThat(_config).IsNotNull();

        AssertThat(_config!.HasCategory("FLAT")).IsTrue();
        AssertThat(_config.HasCategory("Flat")).IsTrue();
        AssertThat(_config.HasCategory("fLaT")).IsTrue();
        AssertThat(_config.HasCategory("mountain")).IsTrue();
        AssertThat(_config.HasCategory("MOUNTAIN")).IsTrue();
        AssertThat(_config.HasCategory("Mountain")).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_WithWildcard()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "*" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsTrue();
        AssertThat(result.Count).IsEqual(0);
    }

    [TestCase]
    public void ResolveBiomeEntries_WithIndividualBiomes()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "Grassland", "Mountain", "Desert" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Count).IsEqual(3);
        AssertThat(result.Contains(Id(Biome.BiomeType.Grassland))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.Mountain))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.Desert))).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_WithSingleCategory()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:flat" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Count).IsGreater(0);
        AssertThat(result.Contains(Id(Biome.BiomeType.Grassland))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.Coastal))).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_WithMultipleCategories()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:mountain", "category:desert" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Contains(Id(Biome.BiomeType.Mountain))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.Desert))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.SandDesert))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.StoneDesert))).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_MixedBiomesAndCategories()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:flat", "Ocean", "Mountain" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Contains(Id(Biome.BiomeType.Grassland))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.Ocean))).IsTrue();
        AssertThat(result.Contains(Id(Biome.BiomeType.Mountain))).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_DuplicateBiomesFromMultipleCategories()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:mountain", "category:rocky" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Contains(Id(Biome.BiomeType.Mountain))).IsTrue();

        var mountainCount = result.Count(b => b == Id(Biome.BiomeType.Mountain));
        AssertThat(mountainCount).IsEqual(1);
    }

    [TestCase]
    public void ResolveBiomeEntries_InvalidCategory()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:nonexistent" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Count).IsEqual(0);
    }

    [TestCase]
    public void ResolveBiomeEntries_InvalidBiome()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "InvalidBiome" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Count).IsEqual(0);
    }

    [TestCase]
    public void ResolveBiomeEntries_CategoryPrefixIsCaseInsensitive()
    {
        var config = CreateTestConfig();

        var entriesLower = new List<string> { "category:flat" };
        var entriesUpper = new List<string> { "CATEGORY:flat" };
        var entriesMixed = new List<string> { "Category:flat" };

        var resultLower = config.ResolveBiomeEntries(entriesLower, out _);
        var resultUpper = config.ResolveBiomeEntries(entriesUpper, out _);
        var resultMixed = config.ResolveBiomeEntries(entriesMixed, out _);

        AssertThat(resultLower.Count).IsGreater(0);
        AssertThat(resultUpper.Count).IsGreater(0);
        AssertThat(resultMixed.Count).IsGreater(0);
        AssertThat(resultLower.Count).IsEqual(resultUpper.Count);
        AssertThat(resultUpper.Count).IsEqual(resultMixed.Count);
    }

    [TestCase]
    public void TryNormalizeBiomeId_ValidBiomes()
    {
        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("Grassland", out var biome1)).IsTrue();
        AssertThat(biome1).IsEqual("biome_grassland");

        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("grassland", out var biome2)).IsTrue();
        AssertThat(biome2).IsEqual("biome_grassland");

        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("Mountain", out var biome3)).IsTrue();
        AssertThat(biome3).IsEqual("biome_mountain");

        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("Stone_Desert", out var biome4)).IsTrue();
        AssertThat(biome4).IsEqual("biome_stone_desert");

        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("biome_mountain", out var biome5)).IsTrue();
        AssertThat(biome5).IsEqual("biome_mountain");
    }

    [TestCase]
    public void TryNormalizeBiomeId_InvalidBiomes()
    {
        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("", out _)).IsFalse();
        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("Invalid", out _)).IsFalse();
        AssertThat(BiomeCategoryConfig.TryNormalizeBiomeId("category:flat", out _)).IsFalse();
    }

    [TestCase]
    public void GetAllBiomes_ReturnsAllBiomeTypes()
    {
        var allBiomes = BiomeCategoryConfig.GetAllBiomes();
        var enumValues = Enum.GetValues<Biome.BiomeType>();

        AssertThat(allBiomes.Count).IsGreaterEqual(enumValues.Length);

        foreach (var biomeType in enumValues)
        {
            AssertThat(allBiomes.Contains(Id(biomeType))).IsTrue();
        }
    }

    [TestCase]
    public void GetCategoriesForBiome_ReturnsCorrectCategories()
    {
        var config = CreateTestConfig();

        var mountainCategories = config.GetCategoriesForBiome(Id(Biome.BiomeType.Mountain));
        AssertThat(mountainCategories.Contains("mountain")).IsTrue();
        AssertThat(mountainCategories.Contains("rocky")).IsTrue();

        var grasslandCategories = config.GetCategoriesForBiome(Id(Biome.BiomeType.Grassland));
        AssertThat(grasslandCategories.Contains("flat")).IsTrue();
        AssertThat(grasslandCategories.Contains("arable")).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingWithCategoryBiomes_LoadsCorrectly()
    {
        BuildingConfigLoader.ClearBiomeCategoryCache();

        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/agriculture/new_building.yaml"
        );

        AssertThat(definitions).IsNotNull();
        AssertThat(definitions.Count).IsGreater(0);

        var farm = definitions.Find(b => b.IdName == "farm");
        AssertThat(farm).IsNotNull();
        AssertThat(farm!.Placement.AllowAnyBiome).IsFalse();

        AssertThat(farm.Placement.Biomes.Count).IsGreater(0);
        AssertThat(farm.Placement.Biomes.Contains(Id(Biome.BiomeType.Grassland))).IsTrue();
    }

    // Removed BuildingWithMultipleCategories_LoadsCorrectly + BuildingWithMixedBiomesAndCategories_LoadsCorrectly:
    // the Wind.yaml + Mine.yaml fixtures these tests depended on were deleted in a prior refactor.

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingWithWildcard_StillWorks()
    {
        BuildingConfigLoader.ClearBiomeCategoryCache();

        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Administration/BusinessAdmin.yaml"
        );

        var businessAdmin = definitions.Find(b => b.IdName == "labor_resources");
        AssertThat(businessAdmin).IsNotNull();
        AssertThat(businessAdmin!.Placement.AllowAnyBiome).IsTrue();
        AssertThat(businessAdmin.Placement.Biomes.Count).IsEqual(0);
    }

    private BiomeCategoryConfig CreateTestConfig()
    {
        var config = new BiomeCategoryConfig();

        config.Categories["flat"] = new BiomeCategoryEntry
        {
            CategoryId = "flat",
            Biomes = new HashSet<string> { Id(Biome.BiomeType.Grassland), Id(Biome.BiomeType.Coastal), Id(Biome.BiomeType.Desert) }
        };

        config.Categories["mountain"] = new BiomeCategoryEntry
        {
            CategoryId = "mountain",
            Biomes = new HashSet<string> { Id(Biome.BiomeType.Mountain), Id(Biome.BiomeType.VolcanicPeak) }
        };

        config.Categories["rocky"] = new BiomeCategoryEntry
        {
            CategoryId = "rocky",
            Biomes = new HashSet<string> { Id(Biome.BiomeType.Mountain), Id(Biome.BiomeType.StoneDesert) }
        };

        config.Categories["desert"] = new BiomeCategoryEntry
        {
            CategoryId = "desert",
            Biomes = new HashSet<string> { Id(Biome.BiomeType.Desert), Id(Biome.BiomeType.SandDesert), Id(Biome.BiomeType.StoneDesert) }
        };

        config.Categories["ocean"] = new BiomeCategoryEntry
        {
            CategoryId = "ocean",
            Biomes = new HashSet<string> { Id(Biome.BiomeType.Ocean), Id(Biome.BiomeType.Coastal) }
        };

        config.Categories["arable"] = new BiomeCategoryEntry
        {
            CategoryId = "arable",
            Biomes = new HashSet<string> { Id(Biome.BiomeType.Grassland), Id(Biome.BiomeType.Forest), Id(Biome.BiomeType.Rainforest), Id(Biome.BiomeType.Coastal) }
        };

        return config;
    }
}
