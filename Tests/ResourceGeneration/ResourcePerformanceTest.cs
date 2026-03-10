using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;

namespace Tests.ResourceGeneration;

[TestSuite]
public class ResourcePerformanceTest
{
    private const int PERFORMANCE_ITERATIONS = 100;

    private Godot.Collections.Dictionary CreateMockSatelliteConfig()
    {
        return new Godot.Collections.Dictionary
        {
            ["main"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "iron_ore",
                    ["base_weight"] = 1.0f,
                    ["distance_weight"] = 1.0f
                }
            }
        };
    }

    private Godot.Collections.Dictionary CreateMockContinentConfig()
    {
        return new Godot.Collections.Dictionary
        {
            ["primary_count"] = new Godot.Collections.Array { 1, 3 },
            ["secondary_count"] = new Godot.Collections.Array { 0, 3 },
            ["primary"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "iron_ore",
                    ["base_weight"] = 1.0f
                }
            }
        };
    }

    private Dictionary<int, Continent> CreateMockContinentsWithCells(int continentCount, int cellsPerContinent)
    {
        var continents = new Dictionary<int, Continent>();
        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        for (int i = 0; i < continentCount; i++)
        {
            var cells = new List<VoronoiCell>();
            float heightSum = 0f;

            for (int j = 0; j < cellsPerContinent; j++)
            {
                var mockPoints = new Point[]
                {
                    new Point(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)), 0),
                    new Point(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)), 0),
                    new Point(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)), 0)
                };

                var cell = new VoronoiCell(j, mockPoints, Array.Empty<Triangle>(), Array.Empty<Edge>());
                cell.Height = rng.RandfRange(-0.5f, 0.5f);
                heightSum += cell.Height;
                cells.Add(cell);
            }

            float avgHeight = heightSum / cellsPerContinent;

            var continent = new Continent(
                StartingIndex: i,
                cells: cells,
                boundaryCells: new HashSet<VoronoiCell>(),
                points: new HashSet<Point>(),
                ConvexHull: new List<Point>(),
                averagedCenter: Vector3.Zero,
                uAxis: Vector3.Right,
                vAxis: Vector3.Up,
                movementDirection: Vector2.Zero,
                velocity: 0f,
                rotation: 0f,
                elevation: Continent.CRUST_TYPE.Continental,
                averageHeight: avgHeight,
                averageMoisture: 0.5f,
                neighborContinents: new HashSet<int>(),
                stressAccumulation: 0f,
                neighborStress: new Dictionary<int, float>(),
                boundaryTypes: new Dictionary<int, Continent.BOUNDARY_TYPE>()
            );

            continents[i] = continent;
        }

        return continents;
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DatabaseLoadTime()
    {
        var sw = Stopwatch.StartNew();
        var db = ResourceDatabase.Instance;
        var resources = db.GetAllResources();
        sw.Stop();

        const double maxLoadTimeMs = 50.0;
        AssertThat(sw.Elapsed.TotalMilliseconds).IsLess(maxLoadTimeMs);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SatelliteGenerationPerformance()
    {
        var config = CreateMockSatelliteConfig();
        int totalDeposits = 0;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)i;
            var deposits = SatelliteResourceGenerator.GenerateResources(config, rng);
            totalDeposits += deposits.Count;
        }
        sw.Stop();

        const double maxTimeMs = 100.0;
        AssertThat(sw.Elapsed.TotalMilliseconds).IsLess(maxTimeMs);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ContinentGenerationPerformance()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 99999;

        var continents = CreateMockContinentsWithCells(10, 20);
        var config = CreateMockContinentConfig();

        var sw = Stopwatch.StartNew();
        ContinentResourceGenerator.GenerateResources(continents, config, rng, null!);
        sw.Stop();

        const double maxTimeMs = 100.0;
        AssertThat(sw.Elapsed.TotalMilliseconds).IsLess(maxTimeMs);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ThreadPoolVsDirect()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 11111;

        var config = CreateMockContinentConfig();

        var continents1 = CreateMockContinentsWithCells(5, 10);
        var continents2 = CreateMockContinentsWithCells(5, 10);

        var sw1 = Stopwatch.StartNew();
        ContinentResourceGenerator.GenerateResources(continents1, config, rng, null!);
        sw1.Stop();

        var sw2 = Stopwatch.StartNew();
        ContinentResourceGenerator.GenerateResources(continents2, config, rng, null!);
        sw2.Stop();

        AssertThat(continents1.Values.Sum(c => c.ContinentalResources.Count)).IsGreater(0);
        AssertThat(continents2.Values.Sum(c => c.ContinentalResources.Count)).IsGreater(0);
    }
}
