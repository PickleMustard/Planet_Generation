using GdUnit4;
using Godot;
using Godot.Collections;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests;

[TestSuite]
public class NameLoadingTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void TestLoadRockyPlanetNames()
    {
        var defaults = TemplateHelpers.GetCelestialBodyDefaults(
            OrbitalBodyType.RockyPlanet
        );

        AssertThat(defaults).IsNotNull();
        AssertThat(defaults.ContainsKey("possible_names")).IsTrue();

        var possibleNames = (Dictionary)defaults["possible_names"];
        AssertThat(possibleNames).IsNotNull();
        AssertThat(possibleNames.Count).IsGreater(0);

        // Check that expected categories are loaded
        AssertThat(possibleNames.ContainsKey("mythology")).IsTrue();
        AssertThat(possibleNames.ContainsKey("scientists")).IsTrue();
        AssertThat(possibleNames.ContainsKey("explorers")).IsTrue();
        AssertThat(possibleNames.ContainsKey("adjectives")).IsTrue();

        // Check that names are arrays
        var mythologyNames = (string[])possibleNames["mythology"];
        AssertThat(mythologyNames).IsNotNull();
        AssertThat(mythologyNames.Length).IsGreater(0);

        GD.Print($"Loaded {mythologyNames.Length} mythology names for RockyPlanet");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestLoadStarNames()
    {
        var defaults = TemplateHelpers.GetCelestialBodyDefaults(
            OrbitalBodyType.Star
        );

        AssertThat(defaults).IsNotNull();
        AssertThat(defaults.ContainsKey("possible_names")).IsTrue();

        var possibleNames = (Dictionary)defaults["possible_names"];
        AssertThat(possibleNames).IsNotNull();
        AssertThat(possibleNames.Count).IsGreater(0);

        // Check that expected categories are loaded
        AssertThat(possibleNames.ContainsKey("mythology")).IsTrue();
        AssertThat(possibleNames.ContainsKey("scientists")).IsTrue();

        // Check that names are arrays
        var mythologyNames = (string[])possibleNames["mythology"];
        AssertThat(mythologyNames).IsNotNull();
        AssertThat(mythologyNames.Length).IsGreater(0);

        GD.Print($"Loaded {mythologyNames.Length} mythology names for Star");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestLoadMoonNames()
    {
        var defaults = TemplateHelpers.GetSatelliteBodyDefaults(
            OrbitalBodyType.Moon
        );

        AssertThat(defaults).IsNotNull();
        AssertThat(defaults.ContainsKey("possible_names")).IsTrue();

        var possibleNames = (Dictionary)defaults["possible_names"];
        AssertThat(possibleNames).IsNotNull();
        AssertThat(possibleNames.Count).IsGreater(0);

        // Check that expected categories are loaded
        AssertThat(possibleNames.ContainsKey("mythology")).IsTrue();
        AssertThat(possibleNames.ContainsKey("scientists")).IsTrue();

        // Check that names are arrays
        var mythologyNames = (string[])possibleNames["mythology"];
        AssertThat(mythologyNames).IsNotNull();
        AssertThat(mythologyNames.Length).IsGreater(0);

        GD.Print($"Loaded {mythologyNames.Length} mythology names for Moon");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TestNameFileMapping()
    {
        // Verify that different body types map to correct name files
        var rockyPath = "Configuration/names/rockyplanets.yml";
        var centralPath = "Configuration/names/centralbodies.yml";
        var nonrockyPath = "Configuration/names/nonrocky.yml";
        var satellitesPath = "Configuration/names/satellites.yml";

        AssertThat(Godot.FileAccess.FileExists("res://" + rockyPath)).IsTrue();
        AssertThat(Godot.FileAccess.FileExists("res://" + centralPath)).IsTrue();
        AssertThat(Godot.FileAccess.FileExists("res://" + nonrockyPath)).IsTrue();
        AssertThat(Godot.FileAccess.FileExists("res://" + satellitesPath)).IsTrue();

        GD.Print("All name files exist");
    }
}
