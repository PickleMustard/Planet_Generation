using GdUnit4;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.ColorSystem;
using Structures;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.Structures;

/// <summary>
/// Regression coverage for the "No resource config for satellite classification" warning:
/// satellites must carry a non-null subtype so resource/mesh-param lookups resolve. Also exercises
/// the per-member subtype roll (<see cref="SubtypeResolver.ResolveSatelliteSubtype"/>) that
/// replaced the AU-band selection for belt members and satellites.
/// </summary>
[TestSuite]
public class SatelliteSubtypeResourceConfigTest
{
    private static readonly OrbitalBodyType[] _satelliteTypes =
    {
        OrbitalBodyType.Moon, OrbitalBodyType.Asteroid, OrbitalBodyType.Comet,
    };

    [TestCase]
    public void FromSatelliteType_WithoutSubtype_FallsBackToNonNullDefault()
    {
        foreach (OrbitalBodyType satType in _satelliteTypes)
        {
            var classification = BodyClassification.FromSatelliteType(satType);
            AssertThat(classification).IsInstanceOf<BodyClassification.Satellite>();
            var sat = (BodyClassification.Satellite)classification;
            AssertThat(sat.Subtype.HasValue)
                .OverrideFailureMessage($"Satellite {satType} produced a null subtype")
                .IsTrue();
        }
    }

    [TestCase]
    public void FromSatelliteType_KnownDefaults()
    {
        AssertThat(BodyClassification.DefaultSatelliteSubtype(OrbitalBodyType.Moon))
            .IsEqual(SatelliteSubtype.RockyMoon);
        AssertThat(BodyClassification.DefaultSatelliteSubtype(OrbitalBodyType.Asteroid))
            .IsEqual(SatelliteSubtype.Carbonaceous);
        AssertThat(BodyClassification.DefaultSatelliteSubtype(OrbitalBodyType.Comet))
            .IsEqual(SatelliteSubtype.ShortPeriod);
    }

    // Family subtype membership (mirrors the SatelliteSubtype enum partition).
    private static readonly SatelliteSubtype[] _asteroidSubtypes =
    {
        SatelliteSubtype.Carbonaceous, SatelliteSubtype.Silicate,
        SatelliteSubtype.Metallic, SatelliteSubtype.IceAsteroid,
    };

    [TestCase]
    [RequireGodotRuntime]
    public void ResolveSatelliteSubtype_EmptyWeights_FallsBackToTypeDefault()
    {
        var rng = new RandomNumberGenerator { Seed = 4242 };
        var empty = new System.Collections.Generic.Dictionary<string, float>();

        AssertThat(SubtypeResolver.ResolveSatelliteSubtype(empty, OrbitalBodyType.Moon, rng))
            .IsEqual(SatelliteSubtype.RockyMoon);
        AssertThat(SubtypeResolver.ResolveSatelliteSubtype(empty, OrbitalBodyType.Asteroid, rng))
            .IsEqual(SatelliteSubtype.Carbonaceous);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResolveSatelliteSubtype_RollsOnlyDeclaredSubtypes()
    {
        var rng = new RandomNumberGenerator { Seed = 99 };
        var weights = new System.Collections.Generic.Dictionary<string, float>();
        foreach (var s in _asteroidSubtypes)
            weights[BiomeIdMapper.SatelliteSubtypeToId(s)] = 1f;

        for (int i = 0; i < 500; i++)
        {
            var rolled = SubtypeResolver.ResolveSatelliteSubtype(weights, OrbitalBodyType.Asteroid, rng);
            AssertThat(System.Array.IndexOf(_asteroidSubtypes, rolled) >= 0)
                .OverrideFailureMessage($"Asteroid rolled undeclared subtype {rolled}")
                .IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResolveSatelliteSubtype_HeavyWeightDominates()
    {
        var rng = new RandomNumberGenerator { Seed = 7 };
        var weights = new System.Collections.Generic.Dictionary<string, float>
        {
            [BiomeIdMapper.SatelliteSubtypeToId(SatelliteSubtype.Metallic)] = 9f,
            [BiomeIdMapper.SatelliteSubtypeToId(SatelliteSubtype.Silicate)] = 1f,
        };

        int metallic = 0;
        const int trials = 1000;
        for (int i = 0; i < trials; i++)
            if (SubtypeResolver.ResolveSatelliteSubtype(weights, OrbitalBodyType.Asteroid, rng)
                == SatelliteSubtype.Metallic)
                metallic++;

        AssertThat(metallic > 750)
            .OverrideFailureMessage($"metallic={metallic}/{trials}, expected ~900")
            .IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetConfigForSubtype_ResolvesForFreshlyBuiltMoon()
    {
        var config = ResourceConfigLoader.LoadPlanetaryResourceConfig();
        AssertThat(config).IsNotNull();

        // A satellite built without an explicit subtype must still resolve a resource config.
        var classification = BodyClassification.FromSatelliteType(OrbitalBodyType.Moon);
        var subtypeConfig = config!.GetConfigForSubtype(classification);

        AssertThat(subtypeConfig)
            .OverrideFailureMessage("Moon satellite resolved no resource config (regression)")
            .IsNotNull();
    }
}
