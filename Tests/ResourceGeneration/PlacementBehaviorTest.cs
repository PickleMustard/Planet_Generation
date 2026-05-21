using GdUnit4;
using Godot;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using static GdUnit4.Assertions;

namespace Tests.ResourceGeneration;

[TestSuite]
public class PlacementBehaviorTest
{
    [TestCase]
    public void DefaultPlacementBehavior_ValidCell_ReturnsTrue()
    {
        var requirements = new BuildingDefinition.PlacementRequirements
        {
            MinElevation = 0.0f,
            MaxElevation = 1.0f,
            MaxSlope = 45.0f,
            AllowAnyBiome = true
        };

        var behavior = new DefaultPlacementBehavior(requirements);
        var cell = CreateTestCell(height: 0.5f);

        AssertThat(behavior.IsValidPlacement(cell)).IsTrue();
    }

    [TestCase]
    public void DefaultPlacementBehavior_ElevationOutOfRange_ReturnsFalse()
    {
        var requirements = new BuildingDefinition.PlacementRequirements
        {
            MinElevation = 0.2f,
            MaxElevation = 0.8f,
            AllowAnyBiome = true
        };

        var behavior = new DefaultPlacementBehavior(requirements);
        var lowCell = CreateTestCell(height: 0.1f);
        var highCell = CreateTestCell(height: 0.9f);

        AssertThat(behavior.IsValidPlacement(lowCell)).IsFalse();
        AssertThat(behavior.IsValidPlacement(highCell)).IsFalse();
    }

    [TestCase]
    public void DefaultPlacementBehavior_InvalidSlope_ReturnsFalse()
    {
        var requirements = new BuildingDefinition.PlacementRequirements
        {
            MinElevation = 0.0f,
            MaxElevation = 1.0f,
            MaxSlope = -1.0f, // Invalid slope
            AllowAnyBiome = true
        };

        var behavior = new DefaultPlacementBehavior(requirements);
        var cell = CreateTestCell(height: 0.5f);

        AssertThat(behavior.IsValidPlacement(cell)).IsFalse();
    }

    [TestCase]
    public void DefaultPlacementBehavior_EdgeElevation_ReturnsTrue()
    {
        var requirements = new BuildingDefinition.PlacementRequirements
        {
            MinElevation = 0.2f,
            MaxElevation = 0.8f,
            AllowAnyBiome = true
        };

        var behavior = new DefaultPlacementBehavior(requirements);
        var minCell = CreateTestCell(height: 0.2f);
        var maxCell = CreateTestCell(height: 0.8f);

        AssertThat(behavior.IsValidPlacement(minCell)).IsTrue();
        AssertThat(behavior.IsValidPlacement(maxCell)).IsTrue();
    }

    [TestCase]
    public void GeothermalVentPlacementBehavior_CellWithVent_ReturnsTrue()
    {
        var behavior = new GeothermalVentPlacementBehavior();
        var cell = CreateTestCell();
        cell.HasGeothermalVent = true;

        AssertThat(behavior.IsValidPlacement(cell)).IsTrue();
    }

    [TestCase]
    public void GeothermalVentPlacementBehavior_CellWithoutVent_ReturnsFalse()
    {
        var behavior = new GeothermalVentPlacementBehavior();
        var cell = CreateTestCell();
        // Default HasGeothermalVent == false

        AssertThat(behavior.IsValidPlacement(cell)).IsFalse();
    }

    [TestCase]
    public void GeothermalVentPlacementBehavior_VolcanicWithoutVent_ReturnsFalse()
    {
        // Volcanic biome alone is no longer sufficient — placement gates on the
        // procedurally-generated HasGeothermalVent flag.
        var behavior = new GeothermalVentPlacementBehavior();
        var cell = CreateTestCell(
            height: 0.6f,
            biome: "biome_volcanic_peak",
            stress: 0.7f
        );

        AssertThat(behavior.IsValidPlacement(cell)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingConfigLoader_CustomBehavior_LoadedEagerly()
    {
        // This test requires a real behavior file to test loading
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );

        var geothermalBuilding = definitions.Find(b => b.IdName == "geothermal_plant_example");
        AssertThat(geothermalBuilding).IsNotNull();
        AssertThat(geothermalBuilding!.Placement.ConfigurableBehavior).IsNotNull();
        AssertThat(geothermalBuilding.Placement.ConfigurableBehavior)
            .IsInstanceOf<GeothermalVentPlacementBehavior>();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BuildingConfigLoader_ClassNameBehavior_LoadedEagerly()
    {
        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(
            "res://Configuration/Buildings/example_building.yaml"
        );

        var classNameBuilding = definitions.Find(b => b.IdName == "behavior_by_class_name_example");
        AssertThat(classNameBuilding).IsNotNull();
        AssertThat(classNameBuilding!.Placement.ConfigurableBehavior).IsNotNull();
        AssertThat(classNameBuilding.Placement.ConfigurableBehavior)
            .IsInstanceOf<GeothermalVentPlacementBehavior>();
    }

    [TestCase]
    public void BuildingDefinition_ConfigurableBehavior_DefaultIsNull()
    {
        var requirements = new BuildingDefinition.PlacementRequirements();
        AssertThat(requirements.ConfigurableBehavior).IsNull();
    }

    private VoronoiCell CreateTestCell(
        float height = 0.5f,
        string biome = "biome_grassland",
        float stress = 0.0f)
    {
        var cell = new VoronoiCell(
            0,
            System.Array.Empty<Point>(),
            System.Array.Empty<Triangle>(),
            System.Array.Empty<Edge>()
        )
        {
            Height = height,
            Biome = biome,
            Stress = stress
        };
        return cell;
    }
}
