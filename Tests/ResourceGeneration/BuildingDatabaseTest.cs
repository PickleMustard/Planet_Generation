using System;
using GdUnit4;
using Godot;
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
        AssertThat(definitions.Count).IsGreaterEqual(2); // Should have example_building and minimal_building

        var exampleBuilding = definitions.Find(b => b.IdName == "example_building");
        AssertThat(exampleBuilding).IsNotNull();

        // Test placement requirements
        AssertThat(exampleBuilding!.Placement.MinElevation).IsEqual(0.2f);
        AssertThat(exampleBuilding.Placement.MaxElevation).IsEqual(0.8f);
        AssertThat(exampleBuilding.Placement.MaxSlope).IsEqual(20.0f);
        AssertThat(exampleBuilding.Placement.CellCount).IsEqual(1);
        AssertThat(exampleBuilding.Placement.RequiresAdjacent).IsFalse();
        AssertThat(exampleBuilding.Placement.Biomes.Count).IsEqual(4); // Grassland, Forest, Mountain, Coastal

        // Test required resources
        AssertThat(exampleBuilding.RequiredResources.Count).IsEqual(5);
        AssertThat(exampleBuilding.RequiredResources["iron"]).IsEqual(100);
        AssertThat(exampleBuilding.RequiredResources["copper"]).IsEqual(50);
        AssertThat(exampleBuilding.RequiredResources["concrete"]).IsEqual(200);
        AssertThat(exampleBuilding.RequiredResources["electronics"]).IsEqual(25);
        AssertThat(exampleBuilding.RequiredResources["water"]).IsEqual(100);

        // Test production
        AssertThat(exampleBuilding.Production.ExtractionRate).IsEqual(1.5f);
        AssertThat(exampleBuilding.Production.Resources.Count).IsEqual(3); // electricity, water, oxygen
        AssertThat(exampleBuilding.Production.Recipes.Count).IsEqual(2); // basic_manufacturing, advanced_processing

        // Test visual
        AssertThat(exampleBuilding.Visual.ModelPath).IsEqual("res://Models/Buildings/example.glb");
        AssertThat(exampleBuilding.Visual.Scale).IsEqual(1.0f);
        AssertThat(exampleBuilding.Visual.RotationOffset).IsEqual(new Vector3(0, 90, 0));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MinimalBuildingParsing()
    {
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );
        var minimalBuilding = definitions.Find(b => b.IdName == "minimal_building");
        AssertThat(minimalBuilding).IsNotNull();

        AssertThat(minimalBuilding!.DisplayName).IsEqual("Minimal Building Example");
        AssertThat(minimalBuilding.Category).IsEqual("minimal");

        // Default values should be set
        AssertThat(minimalBuilding.BuildingTime).IsEqual(60.0f); // Default
        AssertThat(minimalBuilding.WorkRequired).IsEqual(100.0f); // Default

        // Placement defaults
        AssertThat(minimalBuilding.Placement.MinElevation).IsEqual(0.0f);
        AssertThat(minimalBuilding.Placement.MaxElevation).IsEqual(1.0f);
        AssertThat(minimalBuilding.Placement.MaxSlope).IsEqual(45.0f);
        AssertThat(minimalBuilding.Placement.CellCount).IsEqual(1);
        AssertThat(minimalBuilding.Placement.RequiresAdjacent).IsFalse();
        AssertThat(minimalBuilding.Placement.Biomes.Count).IsEqual(0); // Empty list

        // Required resources
        AssertThat(minimalBuilding.RequiredResources.Count).IsEqual(1);
        AssertThat(minimalBuilding.RequiredResources["iron"]).IsEqual(10);

        // Production defaults
        AssertThat(minimalBuilding.Production.ExtractionRate).IsEqual(0.0f);
        AssertThat(minimalBuilding.Production.Resources.Count).IsEqual(0);
        AssertThat(minimalBuilding.Production.Recipes.Count).IsEqual(0);

        // Visual defaults
        AssertThat(minimalBuilding.Visual.ModelPath).IsEqual("");
        AssertThat(minimalBuilding.Visual.Scale).IsEqual(1.0f);
        AssertThat(minimalBuilding.Visual.RotationOffset).IsEqual(Vector3.Zero);
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
}

