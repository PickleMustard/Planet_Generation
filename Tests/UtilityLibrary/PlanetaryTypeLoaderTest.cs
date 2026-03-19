using GdUnit4;
using static GdUnit4.Assertions;

using Godot;

using Structures.Enums;
using UtilityLibrary;

namespace Tests;

[TestSuite]
public class PlanetaryTypeLoaderTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void LoadDominantTypes_ReturnsCorrectCount()
    {
        var types = PlanetaryTypeLoader.GetDominantBodyTypes();

        AssertThat(types).IsNotNull();
        AssertThat(types.Count).IsEqual(3);

        // Verify expected internal names
        AssertThat(types[0].Name).IsEqual("Star");
        AssertThat(types[1].Name).IsEqual("NeutronStar");
        AssertThat(types[2].Name).IsEqual("BlackHole");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoadPlanetaryTypes_ReturnsCorrectCount()
    {
        var types = PlanetaryTypeLoader.GetPlanetaryBodyTypes();

        AssertThat(types).IsNotNull();
        AssertThat(types.Count).IsEqual(4);

        AssertThat(types[0].Name).IsEqual("RockyPlanet");
        AssertThat(types[1].Name).IsEqual("DwarfPlanet");
        AssertThat(types[2].Name).IsEqual("GasGiant");
        AssertThat(types[3].Name).IsEqual("IceGiant");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoadSatelliteTypes_ReturnsCorrectCount()
    {
        var types = PlanetaryTypeLoader.GetSatelliteBodyTypes();

        AssertThat(types).IsNotNull();
        AssertThat(types.Count).IsEqual(6);

        AssertThat(types[0].Name).IsEqual("Asteroid");
        AssertThat(types[1].Name).IsEqual("Comet");
        AssertThat(types[2].Name).IsEqual("Moon");
        AssertThat(types[3].Name).IsEqual("DwarfPlanet");
        AssertThat(types[4].Name).IsEqual("Rings");
        AssertThat(types[5].Name).IsEqual("Satellite");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoadSatelliteBeltTypes_ReturnsCorrectCount()
    {
        var types = PlanetaryTypeLoader.GetSatelliteBeltTypes();

        AssertThat(types).IsNotNull();
        AssertThat(types.Count).IsEqual(3);

        AssertThat(types[0].Name).IsEqual("AsteroidBelt");
        AssertThat(types[1].Name).IsEqual("IceBelt");
        AssertThat(types[2].Name).IsEqual("Comet");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DisplayNames_AreHumanFriendly()
    {
        var dominant = PlanetaryTypeLoader.GetDominantBodyTypes();
        AssertThat(dominant[1].DisplayName).IsEqual("Neutron Star");
        AssertThat(dominant[2].DisplayName).IsEqual("Black Hole");

        var planetary = PlanetaryTypeLoader.GetPlanetaryBodyTypes();
        AssertThat(planetary[0].DisplayName).IsEqual("Rocky Planet");
        AssertThat(planetary[1].DisplayName).IsEqual("Dwarf Planet");
        AssertThat(planetary[2].DisplayName).IsEqual("Gas Giant");
        AssertThat(planetary[3].DisplayName).IsEqual("Ice Giant");

        var belts = PlanetaryTypeLoader.GetSatelliteBeltTypes();
        AssertThat(belts[0].DisplayName).IsEqual("Asteroid Belt");
        AssertThat(belts[1].DisplayName).IsEqual("Ice Belt");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetDisplayName_ReturnsCorrectMapping()
    {
        var types = PlanetaryTypeLoader.GetDominantBodyTypes();

        var displayName = PlanetaryTypeLoader.GetDisplayName(types, "NeutronStar");
        AssertThat(displayName).IsEqual("Neutron Star");

        var displayName2 = PlanetaryTypeLoader.GetDisplayName(types, "BlackHole");
        AssertThat(displayName2).IsEqual("Black Hole");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetInternalName_ReturnsCorrectMapping()
    {
        var types = PlanetaryTypeLoader.GetPlanetaryBodyTypes();

        var internalName = PlanetaryTypeLoader.GetInternalName(types, "Rocky Planet");
        AssertThat(internalName).IsEqual("RockyPlanet");

        var internalName2 = PlanetaryTypeLoader.GetInternalName(types, "Gas Giant");
        AssertThat(internalName2).IsEqual("GasGiant");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetDisplayName_FallsBackToInternalName_WhenNotFound()
    {
        var types = PlanetaryTypeLoader.GetDominantBodyTypes();

        var displayName = PlanetaryTypeLoader.GetDisplayName(types, "NonExistentType");
        AssertThat(displayName).IsEqual("NonExistentType");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetIndexByName_ReturnsCorrectIndex()
    {
        var types = PlanetaryTypeLoader.GetPlanetaryBodyTypes();

        AssertThat(PlanetaryTypeLoader.GetIndexByName(types, "RockyPlanet")).IsEqual(0);
        AssertThat(PlanetaryTypeLoader.GetIndexByName(types, "IceGiant")).IsEqual(3);
        AssertThat(PlanetaryTypeLoader.GetIndexByName(types, "NonExistent")).IsEqual(-1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ToCelestialBodyType_ConvertsCorrectly()
    {
        AssertThat(PlanetaryTypeLoader.ToCelestialBodyType("Star"))
            .IsEqual(CelestialBodyType.Star);
        AssertThat(PlanetaryTypeLoader.ToCelestialBodyType("RockyPlanet"))
            .IsEqual(CelestialBodyType.RockyPlanet);
        AssertThat(PlanetaryTypeLoader.ToCelestialBodyType("BlackHole"))
            .IsEqual(CelestialBodyType.BlackHole);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ToSatelliteBodyType_ConvertsCorrectly()
    {
        AssertThat(PlanetaryTypeLoader.ToSatelliteBodyType("Moon"))
            .IsEqual(SatelliteBodyType.Moon);
        AssertThat(PlanetaryTypeLoader.ToSatelliteBodyType("Asteroid"))
            .IsEqual(SatelliteBodyType.Asteroid);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ToSatelliteGroupType_ConvertsCorrectly()
    {
        AssertThat(PlanetaryTypeLoader.ToSatelliteGroupType("AsteroidBelt"))
            .IsEqual(SatelliteGroupTypes.AsteroidBelt);
        AssertThat(PlanetaryTypeLoader.ToSatelliteGroupType("IceBelt"))
            .IsEqual(SatelliteGroupTypes.IceBelt);
        AssertThat(PlanetaryTypeLoader.ToSatelliteGroupType("Comet"))
            .IsEqual(SatelliteGroupTypes.Comet);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ToDominantBodyType_ConvertsCorrectly()
    {
        AssertThat(PlanetaryTypeLoader.ToDominantBodyType("Star"))
            .IsEqual(DominantBodyType.Star);
        AssertThat(PlanetaryTypeLoader.ToDominantBodyType("NeutronStar"))
            .IsEqual(DominantBodyType.NeutronStar);
        AssertThat(PlanetaryTypeLoader.ToDominantBodyType("BlackHole"))
            .IsEqual(DominantBodyType.BlackHole);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ToPlanetaryBodyType_ConvertsCorrectly()
    {
        AssertThat(PlanetaryTypeLoader.ToPlanetaryBodyType("RockyPlanet"))
            .IsEqual(PlanetaryBodyType.RockyPlanet);
        AssertThat(PlanetaryTypeLoader.ToPlanetaryBodyType("GasGiant"))
            .IsEqual(PlanetaryBodyType.GasGiant);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void YamlFilesExist()
    {
        AssertThat(FileAccess.FileExists("res://Configuration/planetary_types/dominant_bodies.yml"))
            .IsTrue();
        AssertThat(
            FileAccess.FileExists("res://Configuration/planetary_types/planetary_bodies.yml")
        )
            .IsTrue();
        AssertThat(
            FileAccess.FileExists("res://Configuration/planetary_types/satellite_bodies.yml")
        )
            .IsTrue();
        AssertThat(
            FileAccess.FileExists("res://Configuration/planetary_types/satellite_belts.yml")
        )
            .IsTrue();
    }
}
