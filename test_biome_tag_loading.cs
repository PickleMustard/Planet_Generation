using Godot;
using Structures.Resources;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using System;

public partial class TestBiomeTagLoading : Node
{
    public override void _Ready()
    {
        GD.Print("Testing Biome Tag Loading...");

        // Test 1: Direct YAML parsing
        TestDirectYamlParsing();

        // Test 2: ResourceConfigLoader integration
        TestResourceConfigLoader();

        GD.Print("Biome Tag Loading Test Complete!");
    }

    private void TestDirectYamlParsing()
    {
        GD.Print("Test 1: Direct YAML Parsing");

        try
        {
            var yamlPath = "res://Configuration/ResourceDefinition/biome_tag_probabilities.yaml";
            var globalPath = ProjectSettings.GlobalizePath(yamlPath);

            if (!File.Exists(globalPath))
            {
                GD.PrintErr($"File not found: {globalPath}");
                return;
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlContent = File.ReadAllText(globalPath);
            var config = deserializer.Deserialize<BiomeTagConfig>(yamlContent);

            if (config == null)
            {
                GD.PrintErr("Failed to deserialize biome tag configuration");
                return;
            }

            GD.Print($"Successfully loaded biome tag configuration");
            GD.Print($"Number of biomes configured: {config.Biomes.Count}");

            // Test validation
            bool isValid = config.Validate();
            GD.Print($"Configuration validation: {isValid}");

            // Test some specific biomes
            TestBiomeConfig(config, Biome.BiomeType.Mountain, "mountain");
            TestBiomeConfig(config, Biome.BiomeType.Desert, "arid");
            TestBiomeConfig(config, Biome.BiomeType.Forest, "trees");

        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error in direct YAML parsing: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }

    private void TestBiomeConfig(BiomeTagConfig config, Biome.BiomeType biomeType, string expectedTag)
    {
        var entry = config.GetBiomeConfig(biomeType);
        if (entry == null)
        {
            GD.PrintErr($"No configuration found for biome: {biomeType}");
            return;
        }

        GD.Print($"  {biomeType}: Has {entry.Tags.Count} tags, {entry.ProbabilityModifiers.Count} probability modifiers");

        if (entry.HasTag(expectedTag))
        {
            GD.Print($"    ✓ Contains expected tag: '{expectedTag}'");
        }
        else
        {
            GD.PrintErr($"    ✗ Missing expected tag: '{expectedTag}'");
        }
    }

    private void TestResourceConfigLoader()
    {
        GD.Print("\nTest 2: ResourceConfigLoader Integration");

        try
        {
            var config = ResourceConfigLoader.LoadBiomeTagConfig();

            if (config == null)
            {
                GD.PrintErr("ResourceConfigLoader.LoadBiomeTagConfig() returned null");
                return;
            }

            GD.Print($"Successfully loaded via ResourceConfigLoader");
            GD.Print($"Number of biomes: {config.Biomes.Count}");

            // Test helper methods
            var mountainTags = config.GetBiomeTags(Biome.BiomeType.Mountain);
            if (mountainTags != null && mountainTags.Count > 0)
            {
                GD.Print($"  Mountain biome has {mountainTags.Count} tags");
            }

            var temperateModifier = config.GetProbabilityModifier(Biome.BiomeType.Mountain, "temperate");
            GD.Print($"  Mountain biome temperate probability modifier: {temperateModifier}");

            var hasRockyTag = config.BiomeHasTag(Biome.BiomeType.Mountain, "rocky");
            GD.Print($"  Mountain has 'rocky' tag: {hasRockyTag}");

        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error in ResourceConfigLoader test: {ex.Message}");
            GD.PrintErr(ex.StackTrace);
        }
    }
}