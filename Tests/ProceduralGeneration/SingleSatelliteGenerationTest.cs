using System;
using GdUnit4;
using Godot;
using Godot.Collections;
using ProceduralGeneration;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using UtilityLibrary.GameMath.Orbital;
using static GdUnit4.Assertions;

namespace Tests.ProceduralGeneration;

/// <summary>
/// Replicates SystemGenerator.GenerateSingleSatellite's body against the real parsed moon dict
/// from Multi-body-test, to surface any exception that would cause the satellite to be silently
/// skipped (0 satellite bodies generated).
/// </summary>
[TestSuite]
public class SingleSatelliteGenerationTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void GenerateSingleSatellite_BuildsLunaFromMultiBodyTest()
    {
        var data = TemplateHelpers.LoadSystemTemplate("Multi-body-test");

        // luna lives in the flattened top-level satellites section, parented to proxima.
        Godot.Collections.Dictionary? sat = null;
        foreach (var entry in data.Satellites)
        {
            if (entry.ContainsKey("parent") && (string)entry["parent"] == "proxima")
            {
                sat = entry;
                break;
            }
        }
        AssertThat(sat).IsNotNull();

        // --- exact steps from GenerateSingleSatellite ---
        var templateDict = (Godot.Collections.Dictionary)sat!["template"];
        float apogee = templateDict.ContainsKey("apogee") ? (float)templateDict["apogee"] : 500f;
        float perigee = templateDict.ContainsKey("perigee") ? (float)templateDict["perigee"] : 300f;
        float startingAngle = templateDict.ContainsKey("starting_angle") ? (float)templateDict["starting_angle"] : 0f;
        float verticalOffset = templateDict.ContainsKey("vertical_offset") ? (float)templateDict["vertical_offset"] : 0f;

        var (position, velocity) = CelestialBody.CalculateOrbitalState(
            apogee, perigee, startingAngle, verticalOffset, 1000f
        );

        var mesh = AutoFree(new UnifiedCelestialMesh());

        var rng = new RandomNumberGenerator { Seed = 1 };
        var satType = (OrbitalBodyType)Enum.Parse(typeof(OrbitalBodyType), (string)sat["type"]);
        // Subtype now resolves from the satellite's own subtype / subtype_weights.
        var classification = SubtypeResolver.Resolve(sat, satType, rng);

        var satBody = AutoFree(
            new CelestialBody.Builder()
                .FromBodyDict(sat, mesh)
                .WithClassification(classification)
                .Build()
        );

        AssertThat(satBody).IsNotNull();
        AssertThat(classification).IsInstanceOf<global::Structures.BodyClassification.Satellite>();
    }
}
