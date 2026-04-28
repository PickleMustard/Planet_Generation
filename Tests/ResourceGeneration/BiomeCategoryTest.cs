using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
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
        AssertThat(mountainBiomes!.Contains(Biome.BiomeType.Mountain)).IsTrue();
        AssertThat(mountainBiomes.Contains(Biome.BiomeType.VolcanicPeak)).IsTrue();
        AssertThat(mountainBiomes.Contains(Biome.BiomeType.RustedMountain)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ArableCategoryContainsFarmableBiomes()
    {
        AssertThat(_config).IsNotNull();

        var arableBiomes = _config!.GetBiomesForCategory("arable");
        AssertThat(arableBiomes).IsNotNull();
        AssertThat(arableBiomes!.Contains(Biome.BiomeType.Grassland)).IsTrue();
        AssertThat(arableBiomes.Contains(Biome.BiomeType.Forest)).IsTrue();
        AssertThat(arableBiomes.Contains(Biome.BiomeType.Rainforest)).IsTrue();
        AssertThat(arableBiomes.Contains(Biome.BiomeType.Coastal)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CategoryLookupIsCaseInsensitive()
    {
        AssertThat(_config).IsNotNull();

        // Test various case combinations
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
        AssertThat(result.Contains(Biome.BiomeType.Grassland)).IsTrue();
        AssertThat(result.Contains(Biome.BiomeType.Mountain)).IsTrue();
        AssertThat(result.Contains(Biome.BiomeType.Desert)).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_WithSingleCategory()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:flat" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Count).IsGreater(0);
        AssertThat(result.Contains(Biome.BiomeType.Grassland)).IsTrue();
        AssertThat(result.Contains(Biome.BiomeType.Coastal)).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_WithMultipleCategories()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:mountain", "category:desert" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Contains(Biome.BiomeType.Mountain)).IsTrue();
        AssertThat(result.Contains(Biome.BiomeType.Desert)).IsTrue();
        AssertThat(result.Contains(Biome.BiomeType.SandDesert)).IsTrue();
        AssertThat(result.Contains(Biome.BiomeType.StoneDesert)).IsTrue();
    }

    [TestCase]
    public void ResolveBiomeEntries_MixedBiomesAndCategories()
    {
        var config = CreateTestConfig();
        var entries = new List<string> { "category:flat", "Ocean", "Mountain" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Contains(Biome.BiomeType.Grassland)).IsTrue(); // From category:flat
        AssertThat(result.Contains(Biome.BiomeType.Ocean)).IsTrue();     // Individual
        AssertThat(result.Contains(Biome.BiomeType.Mountain)).IsTrue();  // Individual
    }

    [TestCase]
    public void ResolveBiomeEntries_DuplicateBiomesFromMultipleCategories()
    {
        // Mountain appears in both "mountain" and "rocky" categories
        var config = CreateTestConfig();
        var entries = new List<string> { "category:mountain", "category:rocky" };

        var result = config.ResolveBiomeEntries(entries, out bool wildcardPresent);

        AssertThat(wildcardPresent).IsFalse();
        AssertThat(result.Contains(Biome.BiomeType.Mountain)).IsTrue();

        // Should only be added once - HashSet deduplicates
        var mountainCount = result.Count(b => b == Biome.BiomeType.Mountain);
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

        // Test various case combinations of the "category:" prefix
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
    public void TryParseBiomeType_ValidBiomes()
    {
        AssertThat(BiomeCategoryConfig.TryParseBiomeType("Grassland", out var biome1)).IsTrue();
        AssertThat(biome1).IsEqual(Biome.BiomeType.Grassland);

        AssertThat(BiomeCategoryConfig.TryParseBiomeType("grassland", out var biome2)).IsTrue();
        AssertThat(biome2).IsEqual(Biome.BiomeType.Grassland);

        AssertThat(BiomeCategoryConfig.TryParseBiomeType("Mountain", out var biome3)).IsTrue();
        AssertThat(biome3).IsEqual(Biome.BiomeType.Mountain);

        AssertThat(BiomeCategoryConfig.TryParseBiomeType("Stone_Desert", out var biome4)).IsTrue();
        AssertThat(biome4).IsEqual(Biome.BiomeType.StoneDesert);
    }

    [TestCase]
    public void TryParseBiomeType_InvalidBiomes()
    {
        AssertThat(BiomeCategoryConfig.TryParseBiomeType("", out _)).IsFalse();
        AssertThat(BiomeCategoryConfig.TryParseBiomeType("Invalid", out _)).IsFalse();
        AssertThat(BiomeCategoryConfig.TryParseBiomeType("category:flat", out _)).IsFalse();
    }

    [TestCase]
    public void GetAllBiomes_ReturnsAllBiomeTypes()
    {
        var allBiomes = BiomeCategoryConfig.GetAllBiomes();
        var enumValues = Enum.GetValues<Biome.BiomeType>();

        AssertThat(allBiomes.Count).IsEqual(enumValues.Length);

        foreach (var biomeType in enumValues)
        {
            AssertThat(allBiomes.Contains(biomeType)).IsTrue();
        }
    }

    [TestCase]
    public void GetCategoriesForBiome_ReturnsCorrectCategories()
    {
        var config = CreateTestConfig();

        // Mountain should be in both "mountain" and "rocky" categories
        var mountainCategories = config.GetCategoriesForBiome(Biome.BiomeType.Mountain);
        AssertThat(mountainCategories.Contains("mountain")).IsTrue();
        AssertThat(mountainCategories.Contains("rocky")).IsTrue();

        // Grassland should be in "flat" and "arable"
        var grasslandCategories = config.GetCategoriesForBiome(Biome.BiomeType.Grassland);
        AssertThat(grasslandCategories.Contains("flat")).IsTrue();
        AssertThat(grasslandCategories.Contains("arable")).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingWithCategoryBiomes_LoadsCorrectly()
    {
        // Clear the cache to ensure fresh load
        BuildingConfigLoader.ClearBiomeCategoryCache();

        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Agriculture/Farm.yaml"
        );

        AssertThat(definitions).IsNotNull();
        AssertThat(definitions.Count).IsGreater(0);

        var farm = definitions.Find(b => b.IdName == "farm");
        AssertThat(farm).IsNotNull();
        AssertThat(farm!.Placement.AllowAnyBiome).IsFalse();

        // Farm uses "category:arable" which should resolve to multiple biomes
        AssertThat(farm.Placement.Biomes.Count).IsGreater(0);
        AssertThat(farm.Placement.Biomes.Contains(Biome.BiomeType.Grassland)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingWithMultipleCategories_LoadsCorrectly()
    {
        BuildingConfigLoader.ClearBiomeCategoryCache();

        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Power/Wind.yaml"
        );

        var windTurbine = definitions.Find(b => b.IdName == "wind_turbine");
        AssertThat(windTurbine).IsNotNull();

        // Wind turbine uses: [category:flat, category:ocean, category:mountain]
        var biomes = windTurbine!.Placement.Biomes;
        AssertThat(biomes.Count).IsGreater(0);

        // Should have biomes from all three categories
        AssertThat(biomes.Contains(Biome.BiomeType.Grassland) || biomes.Contains(Biome.BiomeType.Coastal))
            .IsTrue(); // From flat
        AssertThat(biomes.Contains(Biome.BiomeType.Ocean) || biomes.Contains(Biome.BiomeType.Coastal))
            .IsTrue(); // From ocean
        AssertThat(biomes.Contains(Biome.BiomeType.Mountain))
            .IsTrue(); // From mountain
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingWithMixedBiomesAndCategories_LoadsCorrectly()
    {
        BuildingConfigLoader.ClearBiomeCategoryCache();

        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Extraction/Mine.yaml"
        );

        var mountainExtractor = definitions.Find(b => b.IdName == "mountain_extractor");
        AssertThat(mountainExtractor).IsNotNull();

        // Uses: [category:mountain, category:rocky, volcanic]
        var biomes = mountainExtractor!.Placement.Biomes;
        AssertThat(biomes.Count).IsGreater(0);

        // Should have Mountain from categories AND VolcanicPeak from individual entry
        AssertThat(biomes.Contains(Biome.BiomeType.Mountain)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.VolcanicPeak)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingWithWildcard_StillWorks()
    {
        BuildingConfigLoader.ClearBiomeCategoryCache();

        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/Administration/BusinessAdmin.yaml"
        );

        var businessAdmin = definitions.Find(b => b.IdName == "business_admin");
        AssertThat(businessAdmin).IsNotNull();
        AssertThat(businessAdmin!.Placement.AllowAnyBiome).IsTrue();
        AssertThat(businessAdmin.Placement.Biomes.Count).IsEqual(0);
    }

    // Helper method to create a test config with sample categories
    private BiomeCategoryConfig CreateTestConfig()
    {
        var config = new BiomeCategoryConfig();

        config.Categories["flat"] = new BiomeCategoryEntry
        {
            CategoryId = "flat",
            Biomes = new HashSet<Biome.BiomeType> { Biome.BiomeType.Grassland, Biome.BiomeType.Coastal, Biome.BiomeType.Desert }
        };

        config.Categories["mountain"] = new BiomeCategoryEntry
        {
            CategoryId = "mountain",
            Biomes = new HashSet<Biome.BiomeType> { Biome.BiomeType.Mountain, Biome.BiomeType.VolcanicPeak }
        };

        config.Categories["rocky"] = new BiomeCategoryEntry
        {
            CategoryId = "rocky",
            Biomes = new HashSet<Biome.BiomeType> { Biome.BiomeType.Mountain, Biome.BiomeType.StoneDesert }
        };

        config.Categories["desert"] = new BiomeCategoryEntry
        {
            CategoryId = "desert",
            Biomes = new HashSet<Biome.BiomeType> { Biome.BiomeType.Desert, Biome.BiomeType.SandDesert, Biome.BiomeType.StoneDesert }
        };

        config.Categories["ocean"] = new BiomeCategoryEntry
        {
            CategoryId = "ocean",
            Biomes = new HashSet<Biome.BiomeType> { Biome.BiomeType.Ocean, Biome.BiomeType.Coastal }
        };

        return config;
    }
}
