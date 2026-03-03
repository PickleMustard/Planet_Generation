using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using Structures.GameState;
using Structures.MeshGeneration;

namespace Tests.ResourceGeneration;

[TestSuite]
public class ContinentGeneratorTest
{
	private Godot.Collections.Dictionary CreateMockContinentConfig()
	{
		return new Godot.Collections.Dictionary
		{
			["primary_count"] = new Godot.Collections.Array { 1, 3 },
			["secondary_count"] = new Godot.Collections.Array { 0, 3 },
			["balance_penalty_factor"] = 0.8f,
			["balance_threshold"] = 0.15f,
			["primary"] = new Godot.Collections.Array
			{
				new Godot.Collections.Dictionary
				{
					["resource_id"] = "iron_ore",
					["base_weight"] = 1.0f,
					["elevation_weight_curve"] = new Godot.Collections.Array { 0.0f, 0.3f, 0.6f, 0.9f, 1.0f }
				},
				new Godot.Collections.Dictionary
				{
					["resource_id"] = "copper_ore",
					["base_weight"] = 0.8f,
					["elevation_weight_curve"] = new Godot.Collections.Array { 0.1f, 0.4f, 0.7f, 0.8f, 0.5f }
				},
				new Godot.Collections.Dictionary
				{
					["resource_id"] = "water_ice",
					["base_weight"] = 0.7f,
					["elevation_weight_curve"] = new Godot.Collections.Array { 1.0f, 0.8f, 0.4f, 0.1f, 0.0f }
				}
			},
			["secondary"] = new Godot.Collections.Array
			{
				new Godot.Collections.Dictionary
				{
					["resource_id"] = "gold_ore",
					["base_weight"] = 0.3f,
					["elevation_weight_curve"] = new Godot.Collections.Array { 0.0f, 0.1f, 0.3f, 0.7f, 0.9f }
				},
				new Godot.Collections.Dictionary
				{
					["resource_id"] = "uranium_ore",
					["base_weight"] = 0.2f,
					["elevation_weight_curve"] = new Godot.Collections.Array { 0.0f, 0.2f, 0.4f, 0.8f, 0.9f }
				}
			}
		};
	}

	private Dictionary<int, Continent> CreateMockContinents(int count)
	{
		var continents = new Dictionary<int, Continent>();
		for (int i = 0; i < count; i++)
			continents[i] = CreateMockContinent(i, 0.5f);
		return continents;
	}

	private Continent CreateMockContinent(int index, float averageHeight)
	{
		return new Continent(
			StartingIndex: index,
			cells: new List<VoronoiCell>(),
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
			averageHeight: averageHeight,
			averageMoisture: 0.5f,
			neighborContinents: new HashSet<int>(),
			stressAccumulation: 0f,
			neighborStress: new Dictionary<int, float>(),
			boundaryTypes: new Dictionary<int, Continent.BOUNDARY_TYPE>()
		);
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
	public void BasicGeneration()
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = 54321;

		var continents = CreateMockContinents(3);
		var config = CreateMockContinentConfig();

		ContinentResourceGenerator.GenerateResources(continents, config, rng, null);

		foreach (var kvp in continents)
		{
			AssertThat(kvp.Value.ContinentalResources).IsNotNull();
			AssertThat(kvp.Value.ContinentalResources.Count).IsGreater(0);
		}
	}

	[TestCase]
	[RequireGodotRuntime]
	public void CellDistribution()
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = 11111;

		var continents = CreateMockContinentsWithCells(2, 5);
		var config = CreateMockContinentConfig();

		ContinentResourceGenerator.GenerateResources(continents, config, rng, null);

		int cellsWithResources = 0;
		foreach (var kvp in continents)
		{
			foreach (var cell in kvp.Value.cells)
			{
				if (cell.Resources.Count > 0)
					cellsWithResources++;
			}
		}

		AssertThat(cellsWithResources).IsGreater(0);
	}

	[TestCase]
	[RequireGodotRuntime]
	public void ElevationWeight()
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = 22222;

		var config = CreateMockContinentConfig();
		var highElevationContinent = CreateMockContinent(1, 0.9f);
		var lowElevationContinent = CreateMockContinent(2, 0.1f);

		var continents = new Dictionary<int, Continent>
		{
			[1] = highElevationContinent,
			[2] = lowElevationContinent
		};

		ContinentResourceGenerator.GenerateResources(continents, config, rng, null);

		AssertThat(highElevationContinent.ContinentalResources.Count).IsGreater(0);
		AssertThat(lowElevationContinent.ContinentalResources.Count).IsGreater(0);
	}

	[TestCase]
	[RequireGodotRuntime]
	public void BiomeWeight()
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = 33333;

		var config = CreateMockContinentConfig();
		var continents = CreateMockContinents(3);

		ContinentResourceGenerator.GenerateResources(continents, config, rng, null);

		bool hasWaterIce = continents.Values.Any(c => c.ContinentalResources.ContainsKey("water_ice"));
		bool hasIronOre = continents.Values.Any(c => c.ContinentalResources.ContainsKey("iron_ore"));

		AssertThat(hasWaterIce || hasIronOre).IsTrue();
	}

	[TestCase]
	[RequireGodotRuntime]
	public void GlobalBalancing()
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = 44444;

		var config = CreateMockContinentConfig();
		config["balance_penalty_factor"] = 0.5f;
		config["balance_threshold"] = 0.1f;

		var continents = CreateMockContinents(10);

		ContinentResourceGenerator.GenerateResources(continents, config, rng, null);

		var resourceCounts = new Dictionary<string, int>();
		foreach (var kvp in continents)
		{
			foreach (var resourceId in kvp.Value.ContinentalResources.Keys)
			{
				if (!resourceCounts.ContainsKey(resourceId))
					resourceCounts[resourceId] = 0;
				resourceCounts[resourceId]++;
			}
		}

		AssertThat(resourceCounts.Count).IsGreater(1);
	}
}
