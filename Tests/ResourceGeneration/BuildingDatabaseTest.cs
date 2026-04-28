using System;
using GdUnit4;
using Godot;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.ResourceGeneration;

[TestSuite]
public class BuildingDatabaseTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoading()
    {
        // Note: BuildingDatabase needs to be registered as an autoload singleton
        // For now, we'll test the BuildingConfigLoader directly
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );

        AssertThat(definitions).IsNotNull();
        AssertThat(definitions.Count).IsGreater(0);

        // Check that we loaded at least the example building
        bool foundExample = false;
        foreach (var building in definitions)
        {
            if (building.IdName == "example_building")
            {
                foundExample = true;
                AssertThat(building.DisplayName).IsEqual("Example Building");
                AssertThat(building.Category).IsEqual("example");
                AssertThat(building.BuildingTime).IsEqual(300.0f);
                AssertThat(building.WorkRequired).IsEqual(500.0f);
                break;
            }
        }

        AssertThat(foundExample).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingDefinitionParsing()
    {
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );
        AssertThat(definitions.Count).IsGreaterEqual(2); // Should have example_building and universal_building

        var exampleBuilding = definitions.Find(b => b.IdName == "example_building");
        AssertThat(exampleBuilding).IsNotNull();

        // Test placement requirements
        AssertThat(exampleBuilding!.Placement.MinElevation).IsEqual(0.2f);
        AssertThat(exampleBuilding.Placement.MaxElevation).IsEqual(0.8f);
        AssertThat(exampleBuilding.Placement.MaxSlope).IsEqual(20.0f);
        AssertThat(exampleBuilding.Placement.CellCount).IsEqual(1);
        AssertThat(exampleBuilding.Placement.RequiresAdjacent).IsFalse();
        // Biomes are resolved from categories: [category:temperate, category:coastal]
        // temperate = Grassland, Forest, Coastal, Mountain, Swamp, Taiga
        // coastal = Coastal, ShallowOcean
        // Combined (deduplicated) = Grassland, Forest, Coastal, Mountain, Swamp, Taiga, ShallowOcean = 7 biomes
        AssertThat(exampleBuilding.Placement.Biomes.Count).IsEqual(7);
        AssertThat(exampleBuilding.Placement.AllowAnyBiome).IsFalse(); // Specific biomes only

        // Test required resources
        AssertThat(exampleBuilding.RequiredResources.Count).IsEqual(5);
        AssertThat(exampleBuilding.RequiredResources["iron"]).IsEqual(100);
        AssertThat(exampleBuilding.RequiredResources["copper"]).IsEqual(50);
        AssertThat(exampleBuilding.RequiredResources["concrete"]).IsEqual(200);
        AssertThat(exampleBuilding.RequiredResources["electronics"]).IsEqual(25);
        AssertThat(exampleBuilding.RequiredResources["water"]).IsEqual(100);

        // Test production
        AssertThat(exampleBuilding.Production.DefaultRecipe).IsEqual("recipe_id");
        AssertThat(exampleBuilding.Production.AlternativeRecipes.Count).IsEqual(1);
        AssertThat(exampleBuilding.Production.InputStorageAmount).IsEqual(100);
        AssertThat(exampleBuilding.Production.OutputStorageAmount).IsEqual(100);
        AssertThat(exampleBuilding.Production.ProductionSpeed).IsEqual(5.0f);

        // Test visual (model_path may be null if file doesn't exist on disk)
        // AssertThat(exampleBuilding.Visual.ModelPath).IsEqual("res://Models/Buildings/example.glb");
        AssertThat(exampleBuilding.Visual.Scale).IsEqual(1.0f);
        AssertThat(exampleBuilding.Visual.RotationOffset).IsEqual(new Vector3(0, 90, 0));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void UniversalBuildingParsing()
    {
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );
        var universalBuilding = definitions.Find(b => b.IdName == "universal_building");
        AssertThat(universalBuilding).IsNotNull();

        AssertThat(universalBuilding!.DisplayName).IsEqual("Universal Building Example");
        AssertThat(universalBuilding.Category).IsEqual("universal");

        // Default values should be set
        AssertThat(universalBuilding.BuildingTime).IsEqual(60.0f); // Default
        AssertThat(universalBuilding.WorkRequired).IsEqual(100.0f); // Default

        // Placement - should allow any biome via wildcard
        AssertThat(universalBuilding.Placement.AllowAnyBiome).IsTrue();
        AssertThat(universalBuilding.Placement.Biomes.Count).IsEqual(0); // No specific biomes stored when using wildcard
        AssertThat(universalBuilding.Placement.MinElevation).IsEqual(0.0f);
        AssertThat(universalBuilding.Placement.MaxElevation).IsEqual(1.0f);
        AssertThat(universalBuilding.Placement.MaxSlope).IsEqual(45.0f);
        AssertThat(universalBuilding.Placement.CellCount).IsEqual(1);
        AssertThat(universalBuilding.Placement.RequiresAdjacent).IsFalse();

        // Required resources
        AssertThat(universalBuilding.RequiredResources.Count).IsEqual(1);
        AssertThat(universalBuilding.RequiredResources["iron"]).IsEqual(10);

        // Production defaults
        AssertThat(universalBuilding.Production.DefaultRecipe ?? "").IsEqual("");
        AssertThat(universalBuilding.Production.AlternativeRecipes.Count).IsEqual(0);
        AssertThat(universalBuilding.Production.InputStorageAmount).IsEqual(0);
        AssertThat(universalBuilding.Production.OutputStorageAmount).IsEqual(0);
        AssertThat(universalBuilding.Production.ProductionSpeed).IsEqual(1.0f);

        // Visual defaults
        AssertThat(universalBuilding.Visual.ModelPath).IsNull();
        AssertThat(universalBuilding.Visual.Scale).IsEqual(1.0f);
        AssertThat(universalBuilding.Visual.RotationOffset).IsEqual(Vector3.Zero);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AllBuildingFilesLoad()
    {
        // Test that all building YAML files can be loaded without errors
        var buildingFiles = new[]
        {
            "res://Configuration/Buildings/Agriculture/Farm.yaml",
            "res://Configuration/Buildings/Extraction/DeepSeaMine.yaml",
            "res://Configuration/Buildings/Power/PowerPlant.yaml",
            "res://Configuration/Buildings/Administration/BusinessAdmin.yaml",
            "res://Configuration/Buildings/example_building.yaml",
        };

        foreach (var filePath in buildingFiles)
        {
            try
            {
                var definitions = BuildingConfigLoader.LoadBuildingDefinitions(filePath);
                AssertThat(definitions).IsNotNull();
                AssertThat(definitions.Count).IsGreater(0);

                GD.Print($"Successfully loaded {definitions.Count} buildings from {filePath}");

                // Verify each building has required fields
                foreach (var building in definitions)
                {
                    AssertThat(building.IdName).IsNotNull().IsNotEmpty();
                    AssertThat(building.DisplayName).IsNotNull().IsNotEmpty();
                    AssertThat(building.Category).IsNotNull().IsNotEmpty();

                    // Verify placement requirements are within valid ranges
                    AssertThat(building.Placement.MinElevation).IsBetween(0.0f, 1.0f);
                    AssertThat(building.Placement.MaxElevation).IsBetween(0.0f, 1.0f);
                    AssertThat(building.Placement.MinElevation)
                        .IsLessEqual(building.Placement.MaxElevation);
                    AssertThat(building.Placement.MaxSlope).IsBetween(0.0f, 90.0f);
                    AssertThat(building.Placement.CellCount).IsGreaterEqual(1);
                }
            }
            catch (Exception e)
            {
                AssertThat($"Failed to load {filePath}: {e.Message}")
                    .OverrideFailureMessage("File loading failed")
                    .IsEmpty();
            }
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingValidation()
    {
        // Test YAML validation
        var validation = YamlValidator.ValidateBuildingDefinition(
            "res://Configuration/Buildings/example_building.yaml"
        );
        AssertThat(validation.IsValid).IsTrue();

        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                GD.PrintErr($"Validation error: {error}");
            }
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WildcardBiomeValidation()
    {
        // Test that buildings with wildcard biomes validate correctly
        var validation = YamlValidator.ValidateBuildingDefinition(
            "res://Configuration/Buildings/Administration/BusinessAdmin.yaml"
        );
        AssertThat(validation.IsValid).IsTrue();

        // The wildcard should not produce errors
        foreach (var error in validation.Errors)
        {
            AssertThat(error.Contains('*')).IsFalse();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CategoryBiomes_ResolveCorrectly()
    {
        // Test that category:X syntax resolves to the correct biomes
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );

        var exampleBuilding = definitions.Find(b => b.IdName == "example_building");
        AssertThat(exampleBuilding).IsNotNull();

        // Should have biomes from [category:temperate, category:coastal]
        var biomes = exampleBuilding!.Placement.Biomes;

        // From temperate: Grassland, Forest, Coastal, Mountain, Swamp, Taiga
        AssertThat(biomes.Contains(Biome.BiomeType.Grassland)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Forest)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Coastal)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Mountain)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Swamp)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Taiga)).IsTrue();

        // From coastal: ShallowOcean (plus Coastal which is already counted)
        AssertThat(biomes.Contains(Biome.BiomeType.ShallowOcean)).IsTrue();

        // Should not have biomes not in these categories
        AssertThat(biomes.Contains(Biome.BiomeType.Desert)).IsFalse();
        AssertThat(biomes.Contains(Biome.BiomeType.Ocean)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MixedBiomesAndCategories_ResolveCorrectly()
    {
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );

        var mixedBuilding = definitions.Find(b => b.IdName == "example_mixed_biomes");
        AssertThat(mixedBuilding).IsNotNull();

        // Should have biomes from [category:arable, category:mountain, Ocean]
        var biomes = mixedBuilding!.Placement.Biomes;

        // From arable: Grassland, Forest, Rainforest, Coastal, Swamp
        AssertThat(biomes.Contains(Biome.BiomeType.Grassland)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Forest)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.Rainforest)).IsTrue();

        // From mountain: Mountain, VolcanicPeak, RustedMountain
        AssertThat(biomes.Contains(Biome.BiomeType.Mountain)).IsTrue();
        AssertThat(biomes.Contains(Biome.BiomeType.VolcanicPeak)).IsTrue();

        // Individual Ocean
        AssertThat(biomes.Contains(Biome.BiomeType.Ocean)).IsTrue();
    }
}

