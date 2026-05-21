using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;

namespace Tests.ResourceGeneration;

[TestSuite]
public class ResourceIntegrationTest
{
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
                },
                new Godot.Collections.Dictionary
                {
                    ["resource_id"] = "gold_ore",
                    ["base_weight"] = 0.5f,
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
    public void EndToEndSatelliteBody()
    {
        var mesh = new UnifiedCelestialMesh();
        mesh.Name = "TestAsteroid";

        var bodyDict = new Godot.Collections.Dictionary
        {
            ["type"] = "Asteroid",
            ["name"] = "TestAsteroid_001",
            ["template"] = new Godot.Collections.Dictionary
            {
                ["mass"] = 5.0f,
                ["size"] = 15,
                ["satellite_velocity"] = Vector3.Zero
            },
            ["resources"] = CreateMockSatelliteConfig()
        };

        var satellite = SatelliteBody.Builder.BuildFromBodyDict(PlanetaryBodyType.RockyPlanet, bodyDict, mesh);

        AssertThat(satellite).IsNotNull();
        AssertThat(satellite.Resources).IsNotNull();

        satellite.GenerateResources();

        AssertThat(satellite.Resources.Count).IsGreater(0);

        foreach (var kvp in satellite.Resources)
        {
            AssertThat(ResourceDatabase.Instance.ValidateResourceExists(kvp.Key)).IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EndToEndCelestialBody()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 77777;

        var continents = CreateMockContinentsWithCells(3, 4);
        var config = CreateMockContinentConfig();

        ContinentResourceGenerator.GenerateResources(continents, config, rng, null!);

        int totalCellResources = 0;
        foreach (var kvp in continents)
        {
            AssertThat(kvp.Value.ContinentalResources.Count).IsGreater(0);
            foreach (var cell in kvp.Value.cells)
                totalCellResources += cell.Resources.Count;
        }

        AssertThat(totalCellResources).IsGreater(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResourceGenerationIntegration()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = 88888;

        var continents = CreateMockContinentsWithCells(1, 3);
        var config = CreateMockContinentConfig();

        ContinentResourceGenerator.GenerateResources(continents, config, rng, null!);

        var continent = continents.Values.First();
        bool hasResources = false;
        foreach (var cell in continent.cells)
        {
            if (cell.Resources.Count > 0)
            {
                hasResources = true;
                break;
            }
        }
        AssertThat(hasResources).IsTrue();
    }
}
