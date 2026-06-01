using GdUnit4;
using static GdUnit4.Assertions;
using Structures.GameState;
using Structures.MeshGeneration;
using Godot;

namespace Tests.Structures.GameState;

[TestSuite]
public class VoronoiCellBiomeTest
{
    [TestCase]
    public void CalculateCellBiome_EmptyPoints_ReturnsDefault()
    {
        var cell = new VoronoiCell(0, new Point[0], new Triangle[0], new Edge[0]);

        cell.CalculateCellBiome();

        AssertThat(cell.Biome).IsEqual("biome_grassland");
    }

    [TestCase]
    public void CalculateCellBiome_SingleBiome_ReturnsThatBiome()
    {
        var points = new Point[3];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = "biome_forest";
        }
        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        cell.CalculateCellBiome();

        AssertThat(cell.Biome).IsEqual("biome_forest");
    }

    [TestCase]
    public void CalculateCellBiome_MajorityBiome_ReturnsMajority()
    {
        var points = new Point[5];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = "biome_forest";
        }
        for (int i = 3; i < 5; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = "biome_desert";
        }
        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        cell.CalculateCellBiome();

        AssertThat(cell.Biome).IsEqual("biome_forest");
    }

    [TestCase]
    public void CalculateCellBiome_Tie_UsesPriority()
    {
        var points = new Point[4];
        points[0] = new Point(Vector3.Zero);
        points[0].Biome = "biome_forest";
        points[1] = new Point(Vector3.Zero);
        points[1].Biome = "biome_forest";

        points[2] = new Point(Vector3.Zero);
        points[2].Biome = "biome_desert";
        points[3] = new Point(Vector3.Zero);
        points[3].Biome = "biome_desert";

        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        cell.CalculateCellBiome();

        // Forest (priority 90) > Desert (40)
        AssertThat(cell.Biome).IsEqual("biome_forest");
    }

    [TestCase]
    public void CalculateCellBiome_MultipleTies_UsesHighestPriority()
    {
        var points = new Point[6];
        points[0] = new Point(Vector3.Zero);
        points[0].Biome = "biome_forest";
        points[1] = new Point(Vector3.Zero);
        points[1].Biome = "biome_forest";

        points[2] = new Point(Vector3.Zero);
        points[2].Biome = "biome_desert";
        points[3] = new Point(Vector3.Zero);
        points[3].Biome = "biome_desert";

        points[4] = new Point(Vector3.Zero);
        points[4].Biome = "biome_grassland";
        points[5] = new Point(Vector3.Zero);
        points[5].Biome = "biome_grassland";

        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        cell.CalculateCellBiome();

        // Forest (90) > Grassland (70) > Desert (40)
        AssertThat(cell.Biome).IsEqual("biome_forest");
    }

    [TestCase]
    public void ToString_IncludesBiomeInformation()
    {
        var points = new Point[3];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = "biome_mountain";
        }
        var cell = new VoronoiCell(42, points, new Triangle[0], new Edge[0]);
        cell.CalculateCellBiome();

        var result = cell.ToString();

        AssertThat(result).Contains("Biome: biome_mountain");
        AssertThat(result).Contains("42");
    }

    [TestCase]
    public void BiomeProperty_ImplementsInterface()
    {
        var points = new Point[3];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = "biome_ocean";
        }
        IVoronoiCell cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        var biome = cell.Biome;
        AssertThat(biome).IsEqual("biome_grassland");

        var voronoiCell = (VoronoiCell)cell;
        voronoiCell.CalculateCellBiome();
        AssertThat(voronoiCell.Biome).IsEqual("biome_ocean");
    }
}
