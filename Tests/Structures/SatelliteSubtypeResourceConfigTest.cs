using GdUnit4;
using Godot;
using ProceduralGeneration;
using Structures;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.Structures;

/// <summary>
/// Regression coverage for the "No resource config for satellite classification" warning:
/// satellites must carry a non-null subtype so resource/mesh-param lookups resolve. Also exercises
/// the unified 1D subtype selection (cumulative effective AU + per-parent-subtype modifiers).
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

    [BeforeTest]
    public void ClearConfigCache() => AUProbabilityLoader.ClearCache();

    // Rolls a concrete satellite subtype from the unified 1D selector. The immediate parent's
    // subtype id (nullable) selects optional per-parent-subtype weight modifiers.
    private static SatelliteSubtype Roll(
        AUProbabilityManager mgr, OrbitalBodyType fam, string? parentSubtypeId, float effectiveAU
    ) => ((BodyClassification.Satellite)mgr.SelectClassification(fam, effectiveAU, parentSubtypeId))
        .Subtype!.Value;

    [TestCase]
    [RequireGodotRuntime]
    public void SelectClassification_ReturnsNonNullSatelliteSubtype()
    {
        var rng = new RandomNumberGenerator { Seed = 4242 };
        var auManager = new AUProbabilityManager(rng);

        var subtype =
            (auManager.SelectClassification(OrbitalBodyType.Moon, 1.0f)
                as BodyClassification.Satellite)?.Subtype;

        AssertThat(subtype.HasValue).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SelectClassification_AsteroidNeverRollsMoonSubtype()
    {
        var rng = new RandomNumberGenerator { Seed = 99 };
        var mgr = new AUProbabilityManager(rng);

        for (int i = 0; i < 500; i++)
        {
            float effectiveAU = (i % 7) * 2f; // sweep cumulative distance
            var rolled = Roll(mgr, OrbitalBodyType.Asteroid, null, effectiveAU);
            AssertThat(System.Array.IndexOf(_asteroidSubtypes, rolled) >= 0)
                .OverrideFailureMessage($"Asteroid rolled non-asteroid subtype {rolled}")
                .IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SelectClassification_EffectiveAuSelectsDifferentRanges()
    {
        var rng = new RandomNumberGenerator { Seed = 7 };
        var mgr = new AUProbabilityManager(rng);

        // Inner system (effectiveAU < 3): IronMoon/DesertMoon are inner-only; outer-only subtypes
        // (SulfurMoon/IcyMoon/CapturedAsteroid/CarbonMoon) must not appear.
        bool sawInnerExclusive = false;
        for (int i = 0; i < 400; i++)
        {
            var r = Roll(mgr, OrbitalBodyType.Moon, null, 1.0f);
            AssertThat(
                    r != SatelliteSubtype.SulfurMoon
                    && r != SatelliteSubtype.IcyMoon
                    && r != SatelliteSubtype.CapturedAsteroid
                    && r != SatelliteSubtype.CarbonMoon)
                .OverrideFailureMessage($"Inner system yielded outer-only subtype {r}")
                .IsTrue();
            if (r == SatelliteSubtype.IronMoon || r == SatelliteSubtype.DesertMoon) sawInnerExclusive = true;
        }
        AssertThat(sawInnerExclusive).IsTrue();

        // Outer system (effectiveAU >= 3): SulfurMoon reachable; inner-only IronMoon/DesertMoon not.
        bool sawOuterExclusive = false;
        for (int i = 0; i < 400; i++)
        {
            var r = Roll(mgr, OrbitalBodyType.Moon, null, 5.0f);
            AssertThat(r != SatelliteSubtype.IronMoon && r != SatelliteSubtype.DesertMoon)
                .OverrideFailureMessage($"Outer system yielded inner-only subtype {r}")
                .IsTrue();
            if (r == SatelliteSubtype.SulfurMoon) sawOuterExclusive = true;
        }
        AssertThat(sawOuterExclusive).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SelectClassification_ParentSubtypeModifierBoostsVolcanic()
    {
        // The ice-giant parent subtype id carries a VolcanicMoon: 2.0 modifier; a null parent
        // subtype applies no modifier. Same inner-system base range.
        const string iceGiantParentId = "subtype_ice_giant_standard_neptune";

        int iceVolcanic = 0;
        var iceRng = new RandomNumberGenerator { Seed = 555 };
        var iceMgr = new AUProbabilityManager(iceRng);
        for (int i = 0; i < 2000; i++)
            if (Roll(iceMgr, OrbitalBodyType.Moon, iceGiantParentId, 1.0f) == SatelliteSubtype.VolcanicMoon)
                iceVolcanic++;

        int baseVolcanic = 0;
        var baseRng = new RandomNumberGenerator { Seed = 555 };
        var baseMgr = new AUProbabilityManager(baseRng);
        for (int i = 0; i < 2000; i++)
            if (Roll(baseMgr, OrbitalBodyType.Moon, null, 1.0f) == SatelliteSubtype.VolcanicMoon)
                baseVolcanic++;

        AssertThat(iceVolcanic > baseVolcanic)
            .OverrideFailureMessage($"IceGiant-parent volcanic ({iceVolcanic}) not > unmodified ({baseVolcanic})")
            .IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SelectClassification_FallsBackToDefaultWhenNoRangeMatches()
    {
        var rng = new RandomNumberGenerator { Seed = 1 };
        var mgr = new AUProbabilityManager(rng);

        // Negative effectiveAU matches no range -> family default subtype.
        var r = Roll(mgr, OrbitalBodyType.Moon, null, -1.0f);
        AssertThat(r).IsEqual(SatelliteSubtype.RockyMoon);
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
