using GdUnit4;
using static GdUnit4.Assertions;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Enums;
using Godot;

namespace Tests.Structures.GameState;

[TestSuite]
public class VoronoiCellBiomeTest
{
    [TestCase]
    public void CalculateCellBiome_EmptyPoints_ReturnsDefault()
    {
        // Arrange
        var cell = new VoronoiCell(0, new Point[0], new Triangle[0], new Edge[0]);

        // Act
        cell.CalculateCellBiome();

        // Assert
        AssertThat(cell.Biome).IsEqual(Biome.BiomeType.Grassland);
    }

    [TestCase]
    public void CalculateCellBiome_SingleBiome_ReturnsThatBiome()
    {
        // Arrange
        var points = new Point[3];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = Biome.BiomeType.Forest;
        }
        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        // Act
        cell.CalculateCellBiome();

        // Assert
        AssertThat(cell.Biome).IsEqual(Biome.BiomeType.Forest);
    }

    [TestCase]
    public void CalculateCellBiome_MajorityBiome_ReturnsMajority()
    {
        // Arrange
        var points = new Point[5];
        // 3 Forest points
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = Biome.BiomeType.Forest;
        }
        // 2 Desert points
        for (int i = 3; i < 5; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = Biome.BiomeType.Desert;
        }
        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        // Act
        cell.CalculateCellBiome();

        // Assert
        AssertThat(cell.Biome).IsEqual(Biome.BiomeType.Forest);
    }

    [TestCase]
    public void CalculateCellBiome_Tie_UsesPriority()
    {
        // Arrange
        var points = new Point[4];
        // 2 Forest points (priority 90)
        points[0] = new Point(Vector3.Zero);
        points[0].Biome = Biome.BiomeType.Forest;
        points[1] = new Point(Vector3.Zero);
        points[1].Biome = Biome.BiomeType.Forest;

        // 2 Desert points (priority 40)
        points[2] = new Point(Vector3.Zero);
        points[2].Biome = Biome.BiomeType.Desert;
        points[3] = new Point(Vector3.Zero);
        points[3].Biome = Biome.BiomeType.Desert;

        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        // Act
        cell.CalculateCellBiome();

        // Assert - Forest should win due to higher priority
        AssertThat(cell.Biome).IsEqual(Biome.BiomeType.Forest);
    }

    [TestCase]
    public void CalculateCellBiome_MultipleTies_UsesHighestPriority()
    {
        // Arrange
        var points = new Point[6];
        // 2 Forest points (priority 90)
        points[0] = new Point(Vector3.Zero);
        points[0].Biome = Biome.BiomeType.Forest;
        points[1] = new Point(Vector3.Zero);
        points[1].Biome = Biome.BiomeType.Forest;

        // 2 Desert points (priority 40)
        points[2] = new Point(Vector3.Zero);
        points[2].Biome = Biome.BiomeType.Desert;
        points[3] = new Point(Vector3.Zero);
        points[3].Biome = Biome.BiomeType.Desert;

        // 2 Grassland points (priority 70)
        points[4] = new Point(Vector3.Zero);
        points[4].Biome = Biome.BiomeType.Grassland;
        points[5] = new Point(Vector3.Zero);
        points[5].Biome = Biome.BiomeType.Grassland;

        var cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        // Act
        cell.CalculateCellBiome();

        // Assert - Forest should win due to highest priority
        AssertThat(cell.Biome).IsEqual(Biome.BiomeType.Forest);
    }

    [TestCase]
    public void ToString_IncludesBiomeInformation()
    {
        // Arrange
        var points = new Point[3];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = Biome.BiomeType.Mountain;
        }
        var cell = new VoronoiCell(42, points, new Triangle[0], new Edge[0]);
        cell.CalculateCellBiome();

        // Act
        var result = cell.ToString();

        // Assert
        AssertThat(result).Contains("Biome: Mountain");
        AssertThat(result).Contains("42"); // Cell index
    }

    [TestCase]
    public void BiomeProperty_ImplementsInterface()
    {
        // Arrange
        var points = new Point[3];
        for (int i = 0; i < 3; i++)
        {
            points[i] = new Point(Vector3.Zero);
            points[i].Biome = Biome.BiomeType.Ocean;
        }
        IVoronoiCell cell = new VoronoiCell(0, points, new Triangle[0], new Edge[0]);

        // Act & Assert - Should compile and implement interface
        var biome = cell.Biome;
        AssertThat(biome).IsEqual(Biome.BiomeType.Grassland); // Default value before calculation

        // Cast back to VoronoiCell to test calculation
        var voronoiCell = (VoronoiCell)cell;
        voronoiCell.CalculateCellBiome();
        AssertThat(voronoiCell.Biome).IsEqual(Biome.BiomeType.Ocean);
    }
}