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

    // Removed ConfigurationValidation, RockyPlanetSubtypeCoverage, GasGiantSubtypeCoverage,
    // SpecificSubtypeValidation, HelperMethods: planetary_type_tags.yaml is no longer the
    // source-of-truth for subtype metadata (replaced by SubtypeDatabase loaded from
    // Configuration/Subtypes/*.yaml). The yaml only retains a Scoured entry as a stub;
    // legacy expectations across the full subtype enum no longer apply.
}