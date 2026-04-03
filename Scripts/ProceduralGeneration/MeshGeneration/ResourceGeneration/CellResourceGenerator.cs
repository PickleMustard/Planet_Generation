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
/// Generates resources per VoronoiCell using the new group-based eligibility system.
/// Body subtypes define resources via resource groups, biome probability modifiers
/// control where resources generate via resource ID weight lookups.
/// </summary>
public static class CellResourceGenerator
{
    /// <summary>
    /// Generates resources for all cells across all continents using group-based matching.
    /// </summary>
    /// <param name="continents">Continents containing VoronoiCells to populate.</param>
    /// <param name="planetaryResourceConfig">Planetary resource configuration with resolved resource sets.</param>
    /// <param name="biomeResourceConfig">Biome resource weight configuration.</param>
    /// <param name="bodyType">The celestial body type.</param>
    /// <param name="bodySubtype">The body subtype enum value.</param>
    /// <param name="rng">Random number generator for deterministic generation.</param>
    /// <param name="maxResourcesPerCell">Maximum number of resources per cell.</param>
    public static void GenerateResources(
        Dictionary<int, Continent> continents,
        PlanetaryResourceConfig planetaryResourceConfig,
        BiomeResourceConfig biomeResourceConfig,
        CelestialBodyType bodyType,
        object? bodySubtype,
        RandomNumberGenerator rng,
        int maxResourcesPerCell = 3
    )
    {
        if (continents == null || continents.Count == 0)
        {
            GameLogger.Warning("CellResourceGenerator: No continents provided");
            return;
        }

        if (planetaryResourceConfig == null)
        {
            GameLogger.Error("CellResourceGenerator: PlanetaryResourceConfig is null");
            return;
        }

        if (biomeResourceConfig == null)
        {
            GameLogger.Error("CellResourceGenerator: BiomeResourceConfig is null");
            return;
        }

        // Step 1: Resolve body subtype config
        var subtypeConfig = planetaryResourceConfig.GetConfigForSubtype(bodyType, bodySubtype);
        if (subtypeConfig == null)
        {
            GameLogger.Warning(
                $"CellResourceGenerator: No resource config for {bodyType}/{bodySubtype}"
            );
            return;
        }

        var resolvedResources = subtypeConfig.ResolvedResources;
        float baseResourceWeight = subtypeConfig.GetResourceWeight();

        GameLogger.Info(
            $"CellResourceGenerator: Body {bodyType}/{bodySubtype}, resolvedResources: [{string.Join(", ", resolvedResources)}], baseWeight: {baseResourceWeight}"
        );

        // Step 2: Pre-filter eligible resources by resolved set membership
        var eligibleResources = GetEligibleResources(subtypeConfig);

        if (eligibleResources.Count == 0)
        {
            GameLogger.Warning(
                "CellResourceGenerator: No eligible resources found for body subtype config"
            );
            return;
        }

        GameLogger.Info($"CellResourceGenerator: {eligibleResources.Count} eligible resources");

        // Step 3: Generate resources per cell
        int totalCells = 0;
        int cellsWithResources = 0;

        foreach (var kvp in continents)
        {
            var continent = kvp.Value;
            if (continent.cells == null)
                continue;

            foreach (var cell in continent.cells)
            {
                totalCells++;
                cell.Resources.Clear();

                int assigned = GenerateCellResources(
                    cell,
                    eligibleResources,
                    biomeResourceConfig,
                    baseResourceWeight,
                    rng,
                    maxResourcesPerCell
                );

                if (assigned > 0)
                    cellsWithResources++;
            }

            // Step 4: Aggregate to continent level for backward compatibility
            continent.AggregateResourcesFromCells();
        }

        GameLogger.Info(
            $"CellResourceGenerator: {cellsWithResources}/{totalCells} cells have resources"
        );
    }

    /// <summary>
    /// Generates resources for satellite bodies using group-based matching.
    /// For bodies without cells/biomes, uses subtype config only.
    /// </summary>
    public static Dictionary<string, ResourceDeposit> GenerateSatelliteResources(
        PlanetaryResourceConfig planetaryResourceConfig,
        object? satelliteSubtype,
        RandomNumberGenerator rng,
        int maxResources = 3
    )
    {
        var results = new Dictionary<string, ResourceDeposit>();

        if (planetaryResourceConfig == null)
        {
            GameLogger.Error("CellResourceGenerator: PlanetaryResourceConfig is null");
            return results;
        }

        var subtypeConfig = planetaryResourceConfig.GetConfigForSatelliteSubtype(satelliteSubtype);
        if (subtypeConfig == null)
        {
            GameLogger.Warning(
                $"CellResourceGenerator: No resource config for satellite subtype {satelliteSubtype}"
            );
            return results;
        }

        var resolvedResources = subtypeConfig.ResolvedResources;
        float baseResourceWeight = subtypeConfig.GetResourceWeight();

        var eligibleResources = GetEligibleResources(subtypeConfig);
        if (eligibleResources.Count == 0)
            return results;

        // Build weighted pool using base resource weight and resolved count ratio (no biome modifiers)
        var pool = new List<(string id, float score)>();
        float resolvedCountRatio = eligibleResources.Count > 0 
            ? (float)resolvedResources.Count / eligibleResources.Count 
            : 0.5f;

        foreach (var resource in eligibleResources)
        {
            float score = baseResourceWeight * (0.5f + resolvedCountRatio * 0.5f);
            pool.Add((resource.IdName!, score));
        }

        // Select resources
        int count = Math.Min(maxResources, pool.Count);
        var selected = SelectWeightedResources(pool, rng, count);

        foreach (var (resourceId, score) in selected)
        {
            float abundance = Mathf.Clamp(score * rng.RandfRange(0.5f, 1.5f), 0.1f, 1.0f);
            float accessibility = rng.RandfRange(0.4f, 1.0f);
            results[resourceId] = new ResourceDeposit
            {
                ResourceId = resourceId,
                Abundance = abundance,
                Accessibility = accessibility,
            };
        }

        return results;
    }

    /// <summary>
    /// Generates resources for a single Voronoi cell based on eligible resources and biome modifiers.
    /// </summary>
    private static int GenerateCellResources(
        VoronoiCell cell,
        List<ResourceDefinition> eligibleResources,
        BiomeResourceConfig biomeResourceConfig,
        float baseResourceWeight,
        RandomNumberGenerator rng,
        int maxResources
    )
    {
        var pool = new List<(string id, float score)>();

        foreach (var resource in eligibleResources)
        {
            float score = ComputeResourceScore(
                resource.IdName!,
                cell.Biome,
                biomeResourceConfig,
                baseResourceWeight
            );

            if (score > 0.05f)
            {
                pool.Add((resource.IdName!, score));
            }
        }

        if (pool.Count == 0)
            return 0;

        int count = Math.Min(maxResources, pool.Count);
        var selected = SelectWeightedResources(pool, rng, count);

        foreach (var (resourceId, score) in selected)
        {
            float abundance = Mathf.Clamp(score * rng.RandfRange(0.5f, 1.5f), 0.1f, 1.0f);
            cell.Resources[resourceId] = abundance;
        }

        return selected.Count;
    }

    /// <summary>
    /// Gets eligible resources from the resolved set in the subtype config.
    /// Returns only resources that are in ResolvedResources and exist in the database.
    /// </summary>
    private static List<ResourceDefinition> GetEligibleResources(SubtypeResourceConfig subtypeConfig)
    {
        var eligible = new List<ResourceDefinition>();
        var allResources = ResourceDatabase.Instance.GetAllResources();
        var resolvedSet = subtypeConfig.ResolvedResources;

        foreach (var kvp in allResources)
        {
            if (resolvedSet.Contains(kvp.Key))
            {
                eligible.Add(kvp.Value);
            }
        }

        return eligible;
    }

    /// <summary>
    /// Computes the resource score using biome-based weight modifiers.
    /// Uses single resource ID lookup instead of tag intersection.
    /// </summary>
    private static float ComputeResourceScore(
        string resourceId,
        Biome.BiomeType biome,
        BiomeResourceConfig biomeResourceConfig,
        float baseResourceWeight
    )
    {
        float modifier = biomeResourceConfig.GetWeightModifier(biome, resourceId);
        return baseResourceWeight * modifier;
    }

    /// <summary>
    /// Selects weighted random resources from a pool.
    /// </summary>
    private static List<(string id, float score)> SelectWeightedResources(
        List<(string id, float score)> pool,
        RandomNumberGenerator rng,
        int count
    )
    {
        var selected = new List<(string id, float score)>();
        var remaining = new List<(string id, float score)>(pool);

        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            float totalWeight = remaining.Sum(r => r.score);
            if (totalWeight <= 0f)
                break;

            float roll = rng.RandfRange(0f, totalWeight);
            float accumulated = 0f;

            int selectedIdx = remaining.Count - 1;
            for (int j = 0; j < remaining.Count; j++)
            {
                accumulated += remaining[j].score;
                if (roll <= accumulated)
                {
                    selectedIdx = j;
                    break;
                }
            }

            selected.Add(remaining[selectedIdx]);
            remaining.RemoveAt(selectedIdx);
        }

        return selected;
    }
}
