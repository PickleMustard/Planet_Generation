using GdUnit4;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.ResourceGeneration;

/// <summary>
/// Parity + load tests for the YAML-driven biome assigner system.
///
/// Each parity case captures the input → biome mapping that the previously-hardcoded
/// *PlanetBiomeAssigner classes produced. ConfigurableBiomeAssigner must match.
/// </summary>
[TestSuite]
public class BiomeAssignerConfigTest
{
    private BiomeAssignerConfig? _config;

    [BeforeTest]
    public void Setup()
    {
        _config = BiomeAssignerConfigLoader.Load();
    }

    [AfterTest]
    public void Teardown()
    {
        _config = null;
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ConfigLoadsAllNineRockyPlanetSubtypes()
    {
        AssertThat(_config).IsNotNull();
        AssertThat(_config!.Assigners.Count).IsEqual(9);

        foreach (RockyPlanetSubtype subtype in System.Enum.GetValues<RockyPlanetSubtype>())
        {
            string subtypeId = BiomeIdMapper.RockyPlanetSubtypeToId(subtype);
            AssertThat(_config.Assigners.ContainsKey(subtypeId))
                .OverrideFailureMessage($"Missing assigner config for {subtype}")
                .IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EveryAssignerHasAtLeastOneRule()
    {
        AssertThat(_config).IsNotNull();
        foreach (var kvp in _config!.Assigners)
        {
            AssertThat(kvp.Value.Rules.Count)
                .OverrideFailureMessage($"{kvp.Key} has no rules")
                .IsGreater(0);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EveryAssignerLastRuleIsDefaultFallthrough()
    {
        AssertThat(_config).IsNotNull();
        foreach (var kvp in _config!.Assigners)
        {
            var lastRule = kvp.Value.Rules[^1];
            AssertThat(lastRule.When.IsEmpty)
                .OverrideFailureMessage(
                    $"{kvp.Key} last rule must have empty 'when' (default fallthrough), got conditions")
                .IsTrue();
        }
    }

    // ── Parity cases per subtype ─────────────────────────────────────────

    [TestCase(RockyPlanetSubtype.Temperate, 0.95f, 0.5f, 0.0f, Biome.BiomeType.Icecap)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.7f, 0.5f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.5f, 0.5f, 0.85f, Biome.BiomeType.Tundra)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.5f, 0.5f, 0.75f, Biome.BiomeType.Taiga)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.02f, 0.5f, 0.0f, Biome.BiomeType.Ocean)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.06f, 0.5f, 0.0f, Biome.BiomeType.Coastal)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.2f, 0.1f, 0.0f, Biome.BiomeType.Desert)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.2f, 0.4f, 0.0f, Biome.BiomeType.Grassland)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.5f, 0.6f, 0.0f, Biome.BiomeType.Forest)]
    [TestCase(RockyPlanetSubtype.Temperate, 0.5f, 0.9f, 0.0f, Biome.BiomeType.Rainforest)]
    [TestCase(RockyPlanetSubtype.Scoured, 0.9f, 0.0f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Scoured, 0.5f, 0.0f, 0.0f, Biome.BiomeType.StoneDesert)]
    [TestCase(RockyPlanetSubtype.Scoured, 0.2f, 0.0f, 0.0f, Biome.BiomeType.SandDesert)]
    [TestCase(RockyPlanetSubtype.Scoured, 0.05f, 0.0f, 0.0f, Biome.BiomeType.ScouredPlain)]
    [TestCase(RockyPlanetSubtype.Desert, 0.9f, 0.0f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Desert, 0.6f, 0.0f, 0.0f, Biome.BiomeType.StoneDesert)]
    [TestCase(RockyPlanetSubtype.Desert, 0.3f, 0.0f, 0.0f, Biome.BiomeType.SandDesert)]
    [TestCase(RockyPlanetSubtype.Desert, 0.1f, 0.0f, 0.0f, Biome.BiomeType.Desert)]
    [TestCase(RockyPlanetSubtype.Desert, 0.02f, 0.0f, 0.0f, Biome.BiomeType.StoneDesert)]
    [TestCase(RockyPlanetSubtype.Ice, 0.9f, 0.5f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Ice, 0.7f, 0.5f, 0.0f, Biome.BiomeType.Icecap)]
    [TestCase(RockyPlanetSubtype.Ice, 0.5f, 0.5f, 0.0f, Biome.BiomeType.Glacier)]
    [TestCase(RockyPlanetSubtype.Ice, 0.3f, 0.2f, 0.0f, Biome.BiomeType.Desert)]
    [TestCase(RockyPlanetSubtype.Ice, 0.3f, 0.5f, 0.0f, Biome.BiomeType.Tundra)]
    [TestCase(RockyPlanetSubtype.Ice, 0.1f, 0.5f, 0.0f, Biome.BiomeType.FrozenPlain)]
    [TestCase(RockyPlanetSubtype.Cool, 0.9f, 0.5f, 0.0f, Biome.BiomeType.Icecap)]
    [TestCase(RockyPlanetSubtype.Cool, 0.7f, 0.5f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Cool, 0.5f, 0.5f, 0.65f, Biome.BiomeType.Glacier)]
    [TestCase(RockyPlanetSubtype.Cool, 0.5f, 0.5f, 0.45f, Biome.BiomeType.Tundra)]
    [TestCase(RockyPlanetSubtype.Cool, 0.02f, 0.5f, 0.0f, Biome.BiomeType.Ocean)]
    [TestCase(RockyPlanetSubtype.Cool, 0.06f, 0.5f, 0.0f, Biome.BiomeType.Coastal)]
    [TestCase(RockyPlanetSubtype.Cool, 0.3f, 0.2f, 0.0f, Biome.BiomeType.FrozenPlain)]
    [TestCase(RockyPlanetSubtype.Cool, 0.3f, 0.5f, 0.0f, Biome.BiomeType.Tundra)]
    [TestCase(RockyPlanetSubtype.Cool, 0.3f, 0.8f, 0.0f, Biome.BiomeType.Taiga)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.8f, 0.5f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.02f, 0.5f, 0.0f, Biome.BiomeType.Ocean)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.07f, 0.5f, 0.0f, Biome.BiomeType.Coastal)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.3f, 0.1f, 0.0f, Biome.BiomeType.Desert)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.3f, 0.7f, 0.0f, Biome.BiomeType.Rainforest)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.3f, 0.4f, 0.0f, Biome.BiomeType.Forest)]
    [TestCase(RockyPlanetSubtype.Tropical, 0.3f, 0.2f, 0.0f, Biome.BiomeType.Swamp)]
    [TestCase(RockyPlanetSubtype.Ocean, 0.9f, 0.5f, 0.0f, Biome.BiomeType.Mountain)]
    [TestCase(RockyPlanetSubtype.Ocean, 0.8f, 0.5f, 0.0f, Biome.BiomeType.Grassland)]
    [TestCase(RockyPlanetSubtype.Ocean, 0.7f, 0.5f, 0.0f, Biome.BiomeType.Coastal)]
    [TestCase(RockyPlanetSubtype.Ocean, 0.5f, 0.5f, 0.0f, Biome.BiomeType.ShallowOcean)]
    [TestCase(RockyPlanetSubtype.Ocean, 0.2f, 0.5f, 0.0f, Biome.BiomeType.DeepOcean)]
    [TestCase(RockyPlanetSubtype.Rusted, 0.9f, 0.0f, 0.0f, Biome.BiomeType.RustedMountain)]
    [TestCase(RockyPlanetSubtype.Rusted, 0.6f, 0.0f, 0.0f, Biome.BiomeType.RustedDesert)]
    [TestCase(RockyPlanetSubtype.Rusted, 0.4f, 0.0f, 0.0f, Biome.BiomeType.RustedPlain)]
    [TestCase(RockyPlanetSubtype.Rusted, 0.2f, 0.0f, 0.0f, Biome.BiomeType.SandDesert)]
    [TestCase(RockyPlanetSubtype.Rusted, 0.05f, 0.0f, 0.0f, Biome.BiomeType.RustedPlain)]
    [TestCase(RockyPlanetSubtype.Volcanic, 0.9f, 0.0f, 0.0f, Biome.BiomeType.VolcanicPeak)]
    [TestCase(RockyPlanetSubtype.Volcanic, 0.6f, 0.0f, 0.0f, Biome.BiomeType.ObsidianField)]
    [TestCase(RockyPlanetSubtype.Volcanic, 0.4f, 0.0f, 0.0f, Biome.BiomeType.AshPlain)]
    [TestCase(RockyPlanetSubtype.Volcanic, 0.2f, 0.0f, 0.0f, Biome.BiomeType.VolcanicPlain)]
    [TestCase(RockyPlanetSubtype.Volcanic, 0.05f, 0.0f, 0.0f, Biome.BiomeType.LavaOcean)]
    [RequireGodotRuntime]
    public void AssignBiomeMatchesPreRefactorBehavior(
        RockyPlanetSubtype subtype, float height, float moisture, float latitude, Biome.BiomeType expected)
    {
        AssertThat(_config).IsNotNull();
        var entry = _config!.Assigners[BiomeIdMapper.RockyPlanetSubtypeToId(subtype)];
        var assigner = new ConfigurableBiomeAssigner(entry);

        var mesh = new UnifiedCelestialMesh { maxHeight = 1f };
        var actual = assigner.AssignBiome(mesh, height, moisture, latitude);
        mesh.QueueFree();

        string expectedId = BiomeIdMapper.BiomeTypeToId(expected);
        AssertThat(actual)
            .OverrideFailureMessage($"{subtype}(h={height}, m={moisture}, lat={latitude}) expected {expectedId}, got {actual}")
            .IsEqual(expectedId);
    }
}
