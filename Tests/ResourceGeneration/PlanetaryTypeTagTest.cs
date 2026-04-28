using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Resources;
using Structures.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using System.Collections.Generic;

namespace Tests.ResourceGeneration;

[TestSuite]
public class PlanetaryTypeTagTest
{
    private PlanetaryTypeTagConfig? _config;

    [TestCase]
    [RequireGodotRuntime]
    public void YamlParsing()
    {
        var yamlPath = "res://Configuration/ResourceDefinition/planetary_type_tags.yaml";
        var globalPath = ProjectSettings.GlobalizePath(yamlPath);

        // Check file exists
        AssertThat(File.Exists(globalPath)).IsTrue();

        // Try to parse the YAML
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlContent = File.ReadAllText(globalPath);
        _config = deserializer.Deserialize<PlanetaryTypeTagConfig>(yamlContent);

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
    public void RockyPlanetSubtypeCoverage()
    {
        // Load configuration first
        YamlParsing();

        AssertThat(_config).IsNotNull();
        AssertThat(_config!.RockyPlanetSubtypes).IsNotNull();

        // Check all RockyPlanetSubtype enum values are covered
        RockyPlanetSubtype[] allEnumValues = [RockyPlanetSubtype.Scoured, RockyPlanetSubtype.Desert, RockyPlanetSubtype.Temperate, RockyPlanetSubtype.Tropical, RockyPlanetSubtype.Ocean, RockyPlanetSubtype.Cool, RockyPlanetSubtype.Ice, RockyPlanetSubtype.Rusted, RockyPlanetSubtype.Volcanic];
        foreach (var enumValue in allEnumValues)
        {
            AssertThat(_config.RockyPlanetSubtypes.ContainsKey(enumValue)).IsTrue();

            var config = _config.RockyPlanetSubtypes[enumValue];
            AssertThat(config).IsNotNull();
            AssertThat(config!.Subtype).IsEqual(enumValue.ToString());
            AssertThat(config.Tags).IsNotNull();
            AssertThat(config.Tags.Count).IsGreater(0);
            AssertThat(config.BaseResourceWeight).IsBetween(0.0f, 2.0f);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GasGiantSubtypeCoverage()
    {
        // Load configuration first
        YamlParsing();

        AssertThat(_config).IsNotNull();
        AssertThat(_config!.GasGiantSubtypes).IsNotNull();

        // Check all GasGiantSubtype enum values are covered
        GasGiantSubtype[] allEnumValues = [GasGiantSubtype.HotJupiter, GasGiantSubtype.StandardJupiter, GasGiantSubtype.ColdJupiter, GasGiantSubtype.FailedStar, GasGiantSubtype.RingedGiant, GasGiantSubtype.StormyGiant, GasGiantSubtype.PuffyGiant];
        foreach (var enumValue in allEnumValues)
        {
            AssertThat(_config.GasGiantSubtypes.ContainsKey(enumValue)).IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SpecificSubtypeValidation()
    {
        // Load configuration first
        YamlParsing();

        AssertThat(_config).IsNotNull();

        // Test specific subtype configurations
        var temperateConfig = _config!.GetRockyPlanetConfig(RockyPlanetSubtype.Temperate);
        AssertThat(temperateConfig).IsNotNull();
        AssertThat(temperateConfig!.HasTag("temperate")).IsTrue();
        AssertThat(temperateConfig.HasTag("habitable")).IsTrue();
        AssertThat(temperateConfig.BaseResourceWeight).IsEqual(1.0f);

        var desertConfig = _config.GetRockyPlanetConfig(RockyPlanetSubtype.Desert);
        AssertThat(desertConfig).IsNotNull();
        AssertThat(desertConfig!.HasTag("arid")).IsTrue();
        AssertThat(desertConfig.HasTag("extreme_heat")).IsTrue();
        AssertThat(desertConfig.BaseResourceWeight).IsEqual(0.8f);

        var standardJupiterConfig = _config.GetGasGiantConfig(GasGiantSubtype.StandardJupiter);
        AssertThat(standardJupiterConfig).IsNotNull();
        AssertThat(standardJupiterConfig!.HasTag("gaseous")).IsTrue();
        AssertThat(standardJupiterConfig.HasTag("high_pressure")).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HelperMethods()
    {
        // Load configuration first
        YamlParsing();

        AssertThat(_config).IsNotNull();

        // Test helper methods
        var temperateTags = _config!.GetRockyPlanetTags(RockyPlanetSubtype.Temperate);
        AssertThat(temperateTags).IsNotNull();
        AssertThat(temperateTags!.Count).IsGreater(0);
        AssertThat(temperateTags.Contains("temperate")).IsTrue();

        var temperateWeight = _config.GetRockyPlanetResourceWeight(RockyPlanetSubtype.Temperate);
        AssertThat(temperateWeight).IsEqual(1.0f);

        var hasTag = _config.RockyPlanetHasTag(RockyPlanetSubtype.Temperate, "habitable");
        AssertThat(hasTag).IsTrue();

        var invalidTag = _config.RockyPlanetHasTag(RockyPlanetSubtype.Temperate, "nonexistent_tag");
        AssertThat(invalidTag).IsFalse();
    }
}