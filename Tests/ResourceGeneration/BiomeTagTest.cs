using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;
using Structures.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using System.Collections.Generic;
using System;
using UtilityLibrary.DataLoading;

namespace Tests.ResourceGeneration;

[TestSuite]
public class BiomeTagTest
{
    private BiomeTagConfig? _config;

    [TestCase]
    [RequireGodotRuntime]
    public void YamlParsing()
    {
        var yamlPath = "res://Configuration/ResourceDefinition/biome_tag_probabilities.yaml";
        var globalPath = ProjectSettings.GlobalizePath(yamlPath);
        
        // Check file exists
        AssertThat(File.Exists(globalPath)).IsTrue();

        // Try to parse the YAML
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlContent = File.ReadAllText(globalPath);
        _config = deserializer.Deserialize<BiomeTagConfig>(yamlContent);
        
        AssertThat(_config).IsNotNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ConfigurationValidation()
    {
        // Load configuration first
        YamlParsing();
        
        AssertThat(_config).IsNotNull();
        
        // Validate the configuration
        bool isValid = _config!.Validate();
        
        AssertThat(isValid).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BiomeTypeCoverage()
    {
        // Load configuration first
        YamlParsing();
        
        AssertThat(_config).IsNotNull();
        AssertThat(_config!.Biomes).IsNotNull();
        
        // Check all Biome.BiomeType enum values are covered
        var allEnumValues = Enum.GetValues<Biome.BiomeType>();
        foreach (var enumValue in allEnumValues)
        {
            AssertThat(_config.Biomes.ContainsKey(enumValue))
                .OverrideFailureMessage($"Missing configuration for biome type {enumValue}")
                .IsTrue();
            
            var config = _config.Biomes[enumValue];
            AssertThat(config)
                .OverrideFailureMessage($"Null configuration for biome type {enumValue}")
                .IsNotNull();
            AssertThat(config!.Biome).IsEqual(enumValue);
            AssertThat(config.Tags)
                .OverrideFailureMessage($"Tags collection is null for biome type {enumValue}")
                .IsNotNull();
            AssertThat(config.Tags.Count)
                .OverrideFailureMessage($"No tags defined for biome type {enumValue}")
                .IsGreater(0);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SpecificBiomeValidation()
    {
        // Load configuration first
        YamlParsing();
        
        AssertThat(_config).IsNotNull();
        
        // Test specific biome configurations
        var mountainConfig = _config!.GetBiomeConfig(Biome.BiomeType.Mountain);
        AssertThat(mountainConfig).IsNotNull();
        AssertThat(mountainConfig!.HasTag("mountain")).IsTrue();
        AssertThat(mountainConfig.HasTag("rocky")).IsTrue();
        AssertThat(mountainConfig.GetProbabilityModifier("temperate")).IsEqual(1.2f);
        AssertThat(mountainConfig.GetProbabilityModifier("arid")).IsEqual(1.3f);
        AssertThat(mountainConfig.GetProbabilityModifier("habitable")).IsEqual(0.8f);
        
        var desertConfig = _config.GetBiomeConfig(Biome.BiomeType.Desert);
        AssertThat(desertConfig).IsNotNull();
        AssertThat(desertConfig!.HasTag("arid")).IsTrue();
        AssertThat(desertConfig.HasTag("hot")).IsTrue();
        AssertThat(desertConfig.GetProbabilityModifier("arid")).IsEqual(1.5f);
        AssertThat(desertConfig.GetProbabilityModifier("habitable")).IsEqual(0.3f);
        
        var forestConfig = _config.GetBiomeConfig(Biome.BiomeType.Forest);
        AssertThat(forestConfig).IsNotNull();
        AssertThat(forestConfig!.HasTag("trees")).IsTrue();
        AssertThat(forestConfig.HasTag("temperate")).IsTrue();
        AssertThat(forestConfig.GetProbabilityModifier("temperate")).IsEqual(1.4f);
        AssertThat(forestConfig.GetProbabilityModifier("arid")).IsEqual(0.2f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HelperMethods()
    {
        // Load configuration first
        YamlParsing();
        
        AssertThat(_config).IsNotNull();
        
        // Test helper methods
        var mountainTags = _config!.GetBiomeTags(Biome.BiomeType.Mountain);
        AssertThat(mountainTags).IsNotNull();
        AssertThat(mountainTags!.Count).IsGreater(0);
        AssertThat(mountainTags.Contains("mountain")).IsTrue();
        
        var probabilityModifier = _config.GetProbabilityModifier(Biome.BiomeType.Mountain, "temperate");
        AssertThat(probabilityModifier).IsEqual(1.2f);
        
        var hasTag = _config.BiomeHasTag(Biome.BiomeType.Mountain, "rocky");
        AssertThat(hasTag).IsTrue();
        
        var invalidTag = _config.BiomeHasTag(Biome.BiomeType.Mountain, "nonexistent_tag");
        AssertThat(invalidTag).IsFalse();
        
        // Test GetBiomesWithTag
        var biomesWithMountainTag = _config.GetBiomesWithTag("mountain");
        AssertThat(biomesWithMountainTag).IsNotNull();
        AssertThat(biomesWithMountainTag!.Count).IsGreater(0);
        AssertThat(biomesWithMountainTag.Contains(Biome.BiomeType.Mountain)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProbabilityModifierValidation()
    {
        // Load configuration first
        YamlParsing();
        
        AssertThat(_config).IsNotNull();
        
        // Test that probability modifiers are within valid range
        foreach (var kvp in _config!.Biomes)
        {
            var biomeType = kvp.Key;
            var entry = kvp.Value;
            
            AssertThat(entry).IsNotNull();
            
            foreach (var modifier in entry!.ProbabilityModifiers)
            {
                AssertThat(modifier.Value)
                    .OverrideFailureMessage($"Probability modifier for tag '{modifier.Key}' in biome '{biomeType}' is outside valid range")
                    .IsBetween(0.0f, 3.0f);
            }
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResourceConfigLoaderIntegration()
    {
        // Test the ResourceConfigLoader integration
        var config = ResourceConfigLoader.LoadBiomeTagConfig();
        
        AssertThat(config).IsNotNull();
        AssertThat(config!.Validate()).IsTrue();
        
        // Test a few specific values to ensure proper loading
        var mountainConfig = config.GetBiomeConfig(Biome.BiomeType.Mountain);
        AssertThat(mountainConfig).IsNotNull();
        AssertThat(mountainConfig!.HasTag("mountain")).IsTrue();
        
        var desertConfig = config.GetBiomeConfig(Biome.BiomeType.Desert);
        AssertThat(desertConfig).IsNotNull();
        AssertThat(desertConfig!.HasTag("arid")).IsTrue();
    }
}