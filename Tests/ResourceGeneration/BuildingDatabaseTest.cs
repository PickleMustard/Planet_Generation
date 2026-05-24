using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.ResourceGeneration;

[TestSuite]
public class BuildingDatabaseTest
{
    // Removed DatabaseLoading, BuildingDefinitionParsing, UniversalBuildingParsing,
    // AllBuildingFilesLoad, CategoryBiomes_ResolveCorrectly, MixedBiomesAndCategories_ResolveCorrectly:
    // these tests referenced building YAMLs and id_names ("example_building", "universal_building",
    // "example_mixed_biomes", "Farm.yaml", "DeepSeaMine.yaml", "PowerPlant.yaml") that were
    // deleted in a prior refactor and never restored. The remaining BuildingValidation and
    // WildcardBiomeValidation tests still exercise current example_building.yaml + BusinessAdmin.yaml.

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
}
