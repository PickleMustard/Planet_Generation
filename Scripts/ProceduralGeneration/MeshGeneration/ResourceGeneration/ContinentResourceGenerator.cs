using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace ProceduralGeneration.MeshGeneration.ResourceGeneration;

/// <summary>
/// Generates resources for continental terrain based on biome, elevation, and tectonic factors.
/// </summary>
/// <remarks>
/// The generator uses a weighted selection system with:
/// - Base weights from configuration
/// - Biome affinity modifiers from ResourceDefinition
/// - Elevation weight curves for terrain-appropriate distribution
/// - Soft penalty curves for global resource balancing
/// 
/// Resources are assigned at both continent and Voronoi cell levels.
/// </remarks>
public static class ContinentResourceGenerator
{
    private static readonly float[] ElevationPoints = { -1.0f, -0.3f, 0.0f, 0.4f, 0.8f };

    /// <summary>
    /// Generates resources for all continents based on configuration.
    /// </summary>
    /// <param name="continents">Dictionary of continents to populate with resources.</param>
    /// <param name="resourceConfig">Configuration containing primary/secondary resource pools and settings.</param>
    /// <param name="rng">Random number generator for deterministic generation.</param>
    /// <param name="mesh">The celestial mesh for biome calculations.</param>
    public static void GenerateResources(
        Dictionary<int, Continent> continents,
        Godot.Collections.Dictionary resourceConfig,
        RandomNumberGenerator rng,
        UnifiedCelestialMesh mesh = null)
    {
        GD.Print($"[ResourceDebug] ContinentResourceGenerator.GenerateResources: continents null: {continents == null}, count: {continents?.Count ?? 0}, resourceConfig null: {resourceConfig == null}");

        if (continents == null || continents.Count == 0 || resourceConfig == null)
        {
            GD.PrintErr("[ResourceDebug] GenerateResources early return due to null/empty inputs");
            return;
        }

        int primaryMin = GetIntRange(resourceConfig, "primary_count", (1, 3)).Item1;
        int primaryMax = GetIntRange(resourceConfig, "primary_count", (1, 3)).Item2;
        int secondaryMin = GetIntRange(resourceConfig, "secondary_count", (0, 5)).Item1;
        int secondaryMax = GetIntRange(resourceConfig, "secondary_count", (0, 5)).Item2;
        float penaltyFactor = GetFloat(resourceConfig, "balance_penalty_factor", 0.8f);
        float balanceThreshold = GetFloat(resourceConfig, "balance_threshold", 0.15f);

        GD.Print($"[ResourceDebug] Resource config: primary [{primaryMin},{primaryMax}], secondary [{secondaryMin},{secondaryMax}]");
        GD.Print($"[ResourceDebug] Resource config keys: {string.Join(", ", resourceConfig.Keys)}");

        var globalTotals = new Dictionary<string, float>();
        int totalResources = continents.Count * (primaryMax + secondaryMax);

        foreach (var kvp in continents)
        {
            var continent = kvp.Value;
            GenerateContinentResources(
                continent,
                resourceConfig,
                rng,
                mesh,
                primaryMin,
                primaryMax,
                secondaryMin,
                secondaryMax,
                globalTotals,
                totalResources,
                penaltyFactor,
                balanceThreshold
            );
        }

        GD.Print($"[ResourceDebug] Finished generating continental resources, now distributing to cells");

        foreach (var kvp in continents)
        {
            DistributeResourcesToCells(kvp.Value, rng, mesh);
        }

        GD.Print($"[ResourceDebug] Finished distributing resources to all cells");
    }

    private static void GenerateContinentResources(
        Continent continent,
        Godot.Collections.Dictionary resourceConfig,
        RandomNumberGenerator rng,
        UnifiedCelestialMesh mesh,
        int primaryMin,
        int primaryMax,
        int secondaryMin,
        int secondaryMax,
        Dictionary<string, float> globalTotals,
        int totalResources,
        float penaltyFactor,
        float balanceThreshold)
    {
        GD.Print($"[ResourceDebug] GenerateContinentResources: continent index {continent.StartingIndex}, cells: {continent.cells?.Count ?? 0}");

        continent.ContinentalResources.Clear();
        continent.ResourceAbundance.Clear();

        float avgElevation = continent.averageHeight;
        float normalizedElevation = Mathf.Clamp((avgElevation + 1f) / 2f, 0f, 1f);
        GD.Print($"[ResourceDebug] Continent {continent.StartingIndex}: avgHeight={avgElevation}, normalizedElev={normalizedElevation}");

        var biomeAffinities = CalculateAverageBiomeAffinities(continent, mesh);

        if (resourceConfig.TryGetValue("primary", out var primaryVariant) &&
            primaryVariant.As<Godot.Collections.Array>() is Godot.Collections.Array primaryResources)
        {
            int primaryCount = rng.RandiRange(primaryMin, primaryMax);
            GD.Print($"[ResourceDebug] Continent {continent.StartingIndex}: selecting {primaryCount} primary resources from {primaryResources.Count} pool");
            SelectResources(
                primaryResources,
                continent,
                rng,
                primaryCount,
                globalTotals,
                totalResources,
                penaltyFactor,
                balanceThreshold,
                normalizedElevation,
                biomeAffinities,
                isPrimary: true
            );
        }
        else
        {
            GD.Print($"[ResourceDebug] Continent {continent.StartingIndex}: no primary resources found in config");
        }

        if (resourceConfig.TryGetValue("secondary", out var secondaryVariant) &&
            secondaryVariant.As<Godot.Collections.Array>() is Godot.Collections.Array secondaryResources)
        {
            int secondaryCount = rng.RandiRange(secondaryMin, secondaryMax);
            GD.Print($"[ResourceDebug] Continent {continent.StartingIndex}: selecting {secondaryCount} secondary resources from {secondaryResources.Count} pool");
            SelectResources(
                secondaryResources,
                continent,
                rng,
                secondaryCount,
                globalTotals,
                totalResources,
                penaltyFactor,
                balanceThreshold,
                normalizedElevation,
                biomeAffinities,
                isPrimary: false
            );
        }
        else
        {
            GD.Print($"[ResourceDebug] Continent {continent.StartingIndex}: no secondary resources found in config");
        }

        GD.Print($"[ResourceDebug] Continent {continent.StartingIndex}: assigned {continent.ContinentalResources.Count} resources: {string.Join(", ", continent.ContinentalResources.Keys)}");
    }

    private static void SelectResources(
        Godot.Collections.Array resourcePool,
        Continent continent,
        RandomNumberGenerator rng,
        int count,
        Dictionary<string, float> globalTotals,
        int totalResources,
        float penaltyFactor,
        float balanceThreshold,
        float normalizedElevation,
        Dictionary<string, float> biomeAffinities,
        bool isPrimary)
    {
        var weightedResources = new List<(Godot.Collections.Dictionary config, float weight)>();

        GD.Print($"[ResourceDebug] SelectResources: pool size {resourcePool.Count}, selecting {count}, isPrimary={isPrimary}");

        foreach (var resourceVariant in resourcePool)
        {
            if (resourceVariant.AsGodotDictionary() is Godot.Collections.Dictionary resourceConfig)
            {
                string resourceId = resourceConfig.GetValueOrDefault("resource_id", "").AsString();
                if (string.IsNullOrEmpty(resourceId))
                {
                    GD.Print($"[ResourceDebug] SelectResources: skipping empty resource_id");
                    continue;
                }

                if (!ResourceDatabase.Instance.ValidateResourceExists(resourceId))
                {
                    GD.Print($"[ResourceDebug] SelectResources: resource '{resourceId}' not in database, skipping");
                    continue;
                }

                float baseWeight = GetFloat(resourceConfig, "base_weight", 1.0f);
                float elevationWeight = GetElevationWeight(resourceConfig, normalizedElevation);
                float biomeWeight = GetBiomeWeight(resourceId, biomeAffinities);
                float globalPenalty = CalculateGlobalPenalty(resourceId, globalTotals, totalResources, balanceThreshold, penaltyFactor);

                float finalWeight = baseWeight * elevationWeight * biomeWeight * globalPenalty;

                GD.Print($"[ResourceDebug] SelectResources: {resourceId} - base={baseWeight}, elev={elevationWeight}, biome={biomeWeight}, penalty={globalPenalty}, final={finalWeight}");

                if (finalWeight > 0)
                {
                    weightedResources.Add((resourceConfig, finalWeight));
                }
            }
        }

        GD.Print($"[ResourceDebug] SelectResources: {weightedResources.Count} valid weighted resources");

        for (int i = 0; i < count && weightedResources.Count > 0; i++)
        {
            var selected = SelectWeightedResource(weightedResources, rng);
            if (selected == null)
                break;

            string resourceId = selected.GetValueOrDefault("resource_id", "").AsString();
            if (string.IsNullOrEmpty(resourceId) || continent.ContinentalResources.ContainsKey(resourceId))
                continue;

            float abundance = isPrimary
                ? rng.RandfRange(0.6f, 1.0f)
                : rng.RandfRange(0.2f, 0.6f);

            continent.ContinentalResources[resourceId] = abundance;
            continent.ResourceAbundance[resourceId] = abundance;
            GD.Print($"[ResourceDebug] SelectResources: assigned {resourceId} with abundance {abundance}");

            if (!globalTotals.ContainsKey(resourceId))
                globalTotals[resourceId] = 0f;
            globalTotals[resourceId] += abundance;

            weightedResources.RemoveAll(r => r.config.GetValueOrDefault("resource_id", "").AsString() == resourceId);
        }
    }

    private static void DistributeResourcesToCells(Continent continent, RandomNumberGenerator rng, UnifiedCelestialMesh mesh)
    {
        GD.Print($"[ResourceDebug] DistributeResourcesToCells: continent {continent.StartingIndex}, cells null: {continent.cells == null}, cell count: {continent.cells?.Count ?? 0}, continental resources: {continent.ContinentalResources?.Count ?? 0}");

        if (continent.cells == null || continent.ContinentalResources.Count == 0)
        {
            GD.Print($"[ResourceDebug] DistributeResourcesToCells: early return - cells null: {continent.cells == null}, resource count: {continent.ContinentalResources?.Count ?? 0}");
            return;
        }

        int cellsWithResources = 0;
        foreach (var cell in continent.cells)
        {
            cell.Resources.Clear();

            float cellHeight = cell.Height;
            float normalizedCellHeight = Mathf.Clamp((cellHeight + 1f) / 2f, 0f, 1f);

            foreach (var resourceKvp in continent.ContinentalResources)
            {
                string resourceId = resourceKvp.Key;
                float continentAbundance = resourceKvp.Value;

                if (!ResourceDatabase.Instance.TryGetResource(resourceId, out var resourceDef))
                {
                    GD.PrintErr($"[ResourceDebug] DistributeResourcesToCells: resource '{resourceId}' not found in database");
                    continue;
                }

                float elevationFactor = 1.0f;
                if (normalizedCellHeight < resourceDef.MinElevation || normalizedCellHeight > resourceDef.MaxElevation)
                {
                    elevationFactor = 0.3f;
                }

                float variation = rng.RandfRange(0.7f, 1.3f);
                float cellAbundance = Mathf.Clamp(continentAbundance * elevationFactor * variation, 0f, 1f);

                if (cellAbundance > 0.1f)
                {
                    cell.Resources[resourceId] = cellAbundance;
                }
            }

            if (cell.Resources.Count > 0)
                cellsWithResources++;
        }

        GD.Print($"[ResourceDebug] DistributeResourcesToCells: continent {continent.StartingIndex} - {cellsWithResources}/{continent.cells.Count} cells now have resources");
    }

    private static Dictionary<string, float> CalculateAverageBiomeAffinities(Continent continent, UnifiedCelestialMesh mesh)
    {
        var affinities = new Dictionary<string, float>();

        if (continent.cells == null || continent.cells.Count == 0)
            return affinities;

        var biomeCounts = new Dictionary<Biome.BiomeType, int>();

        foreach (var cell in continent.cells)
        {
            var biome = GetCellBiome(cell, mesh);
            if (!biomeCounts.ContainsKey(biome))
                biomeCounts[biome] = 0;
            biomeCounts[biome]++;
        }

        foreach (var kvp in biomeCounts)
        {
            affinities[kvp.Key.ToString()] = (float)kvp.Value / continent.cells.Count;
        }

        return affinities;
    }

    private static Biome.BiomeType GetCellBiome(VoronoiCell cell, UnifiedCelestialMesh mesh)
    {
        if (mesh == null)
            return Biome.BiomeType.Grassland;

        float moisture = cell.Height * 0.5f;
        return BiomeAssigner.AssignBiome(mesh, cell.Height, moisture, 0f);
    }

    private static float GetElevationWeight(Godot.Collections.Dictionary config, float normalizedElevation)
    {
        if (!config.TryGetValue("elevation_weight_curve", out var curveVariant))
            return 1.0f;

        var curve = curveVariant.As<Godot.Collections.Array>();
        if (curve == null || curve.Count != 5)
            return 1.0f;

        float[] weights = new float[5];
        for (int i = 0; i < 5; i++)
        {
            weights[i] = curve[i].AsSingle();
        }

        return InterpolateCurve(ElevationPoints, weights, normalizedElevation * 2f - 1f);
    }

    private static float GetBiomeWeight(string resourceId, Dictionary<string, float> biomeAffinities)
    {
        if (!ResourceDatabase.Instance.TryGetResource(resourceId, out var resourceDef))
            return 1.0f;

        if (resourceDef.BiomeAffinity == null || resourceDef.BiomeAffinity.Count == 0)
            return 1.0f;

        float totalWeight = 0f;
        float totalAffinity = 0f;

        foreach (var biomeKvp in biomeAffinities)
        {
            if (TryParseBiomeType(biomeKvp.Key, out Biome.BiomeType biomeType))
            {
                if (resourceDef.BiomeAffinity.TryGetValue(biomeType, out float affinity))
                {
                    totalWeight += affinity * biomeKvp.Value;
                    totalAffinity += biomeKvp.Value;
                }
            }
        }

        if (totalAffinity > 0)
            return totalWeight / totalAffinity;

        return 1.0f;
    }

    private static float CalculateGlobalPenalty(
        string resourceId,
        Dictionary<string, float> globalTotals,
        int totalResources,
        float threshold,
        float penaltyFactor)
    {
        if (!globalTotals.TryGetValue(resourceId, out float currentTotal))
            return 1.0f;

        if (totalResources <= 0)
            return 1.0f;

        float currentFraction = currentTotal / totalResources;

        if (currentFraction > threshold)
        {
            float excess = currentFraction - threshold;
            return Mathf.Pow(penaltyFactor, excess * 10f);
        }

        return 1.0f;
    }

    private static Godot.Collections.Dictionary SelectWeightedResource(
        List<(Godot.Collections.Dictionary config, float weight)> weightedResources,
        RandomNumberGenerator rng)
    {
        if (weightedResources.Count == 0)
            return null;

        float totalWeight = weightedResources.Sum(r => r.weight);
        if (totalWeight <= 0)
            return weightedResources[0].config;

        float roll = rng.RandfRange(0f, totalWeight);
        float accumulated = 0f;

        foreach (var (config, weight) in weightedResources)
        {
            accumulated += weight;
            if (roll <= accumulated)
                return config;
        }

        return weightedResources[^1].config;
    }

    private static float InterpolateCurve(float[] points, float[] values, float x)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            if (x >= points[i] && x <= points[i + 1])
            {
                float t = (x - points[i]) / (points[i + 1] - points[i]);
                return Mathf.Lerp(values[i], values[i + 1], t);
            }
        }

        if (x < points[0])
            return values[0];
        return values[^1];
    }

    private static bool TryParseBiomeType(string name, out Biome.BiomeType biomeType)
    {
        biomeType = Biome.BiomeType.Tundra;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        string normalized = name.Replace("_", "").Replace(" ", "").Trim();

        foreach (Biome.BiomeType type in Enum.GetValues(typeof(Biome.BiomeType)))
        {
            string enumName = type.ToString().Replace("_", "");
            if (string.Equals(normalized, enumName, StringComparison.OrdinalIgnoreCase))
            {
                biomeType = type;
                return true;
            }
        }

        return false;
    }

    private static float GetFloat(Godot.Collections.Dictionary dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        if (value.VariantType == Variant.Type.Float)
            return value.AsSingle();
        if (value.VariantType == Variant.Type.Int)
            return (float)value.AsInt64();

        return fallback;
    }

    private static (int, int) GetIntRange(Godot.Collections.Dictionary dict, string key, (int, int) fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var arr = dict[key].As<Godot.Collections.Array>();
        if (arr != null && arr.Count >= 2)
        {
            int a = arr[0].AsInt32();
            int b = arr[1].AsInt32();
            return (Mathf.Min(a, b), Mathf.Max(a, b));
        }

        return fallback;
    }
}
