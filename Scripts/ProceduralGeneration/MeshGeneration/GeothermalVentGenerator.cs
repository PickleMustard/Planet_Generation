using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using UtilityLibrary;

namespace ProceduralGeneration.MeshGeneration;

/// <summary>
/// Procedural placement of geothermal vents onto Voronoi cells. Runs after biome
/// assignment, before resource generation. Vents are binary per cell and gate
/// geothermal-plant placement.
///
/// Distribution:
/// - Base probability is biome-keyed. Deep ocean and volcanic terrain are most
///   likely; mountains and plains are least likely.
/// - Cells incident to a divergent tectonic edge get a bonus; cells incident
///   to a convergent edge get a penalty.
/// - A second pass adds a small bonus to cells adjacent to already-placed
///   vents, allowing light clustering without runaway growth.
/// </summary>
public static class GeothermalVentGenerator
{
    private const float DEEP_OCEAN_BASE = 0.15f;
    private const float OCEAN_BASE = 0.05f;
    private const float VOLCANIC_BASE = 0.10f;
    private const float MOUNTAIN_BASE = 0.02f;
    private const float PLAIN_BASE = 0.02f;
    private const float DEFAULT_BASE = 0.03f;

    private const float DIVERGENT_BONUS = 0.10f;
    private const float CONVERGENT_PENALTY = 0.04f;

    private const float ADJACENCY_BONUS_PER_VENT = 0.05f;
    private const float ADJACENCY_BONUS_CAP = 0.20f;

    /// <summary>
    /// Mutates <c>cell.HasGeothermalVent</c> across every cell in the database.
    /// </summary>
    public static void Generate(StructureDatabase strDb, RandomNumberGenerator rand)
    {
        if (strDb?.VoronoiCells == null || strDb.VoronoiCells.Count == 0)
            return;

        // Pass 1: intrinsic roll per cell.
        var intrinsicProbs = new Dictionary<VoronoiCell, float>(strDb.VoronoiCells.Count);
        foreach (var cell in strDb.VoronoiCells)
        {
            float prob = Mathf.Clamp(IntrinsicProbability(cell), 0f, 1f);
            intrinsicProbs[cell] = prob;
            if (rand.Randf() < prob)
                cell.HasGeothermalVent = true;
        }

        // Pass 2: adjacency-boosted re-roll on cells that didn't get a vent.
        // Use a snapshot of pass-1 vent assignments so the second pass doesn't
        // chain (avoids runaway growth).
        var pass1Vents = new HashSet<VoronoiCell>();
        foreach (var cell in strDb.VoronoiCells)
            if (cell.HasGeothermalVent)
                pass1Vents.Add(cell);

        int newVents = 0;
        foreach (var cell in strDb.VoronoiCells)
        {
            if (cell.HasGeothermalVent)
                continue;
            int neighborVents = 0;
            var neighbors = UnifiedCelestialMesh.GetCellNeighbors(cell, strDb);
            foreach (var n in neighbors)
            {
                if (n != cell && pass1Vents.Contains(n))
                    neighborVents++;
            }
            if (neighborVents == 0)
                continue;
            float bonus = Mathf.Min(neighborVents * ADJACENCY_BONUS_PER_VENT, ADJACENCY_BONUS_CAP);
            float prob = Mathf.Clamp(intrinsicProbs[cell] + bonus, 0f, 1f);
            if (rand.Randf() < prob)
            {
                cell.HasGeothermalVent = true;
                newVents++;
            }
        }

        int total = pass1Vents.Count + newVents;
        GameLogger.Info(
            $"GeothermalVentGenerator: placed {total} vents "
            + $"({pass1Vents.Count} intrinsic + {newVents} adjacency) "
            + $"across {strDb.VoronoiCells.Count} cells"
        );
    }

    private static float IntrinsicProbability(VoronoiCell cell)
    {
        float baseProb = BaseFromBiome(cell.Biome);
        float boundaryMod = BoundaryModifier(cell);
        return baseProb + boundaryMod;
    }

    private static float BaseFromBiome(Biome.BiomeType biome)
    {
        switch (biome)
        {
            case Biome.BiomeType.DeepOcean:
                return DEEP_OCEAN_BASE;
            case Biome.BiomeType.Ocean:
            case Biome.BiomeType.ShallowOcean:
                return OCEAN_BASE;
            case Biome.BiomeType.VolcanicPeak:
            case Biome.BiomeType.VolcanicPlain:
            case Biome.BiomeType.LavaOcean:
            case Biome.BiomeType.AshPlain:
            case Biome.BiomeType.ObsidianField:
                return VOLCANIC_BASE;
            case Biome.BiomeType.Mountain:
            case Biome.BiomeType.RustedMountain:
                return MOUNTAIN_BASE;
            case Biome.BiomeType.Grassland:
            case Biome.BiomeType.Desert:
            case Biome.BiomeType.SandDesert:
            case Biome.BiomeType.StoneDesert:
            case Biome.BiomeType.Tundra:
            case Biome.BiomeType.Taiga:
            case Biome.BiomeType.FrozenPlain:
            case Biome.BiomeType.RustedPlain:
            case Biome.BiomeType.ScouredPlain:
                return PLAIN_BASE;
            default:
                return DEFAULT_BASE;
        }
    }

    private static float BoundaryModifier(VoronoiCell cell)
    {
        if (cell.Edges == null)
            return 0f;
        bool hasDivergent = false;
        bool hasConvergent = false;
        foreach (var edge in cell.Edges)
        {
            if (edge == null)
                continue;
            if (edge.Type == EdgeType.divergent)
                hasDivergent = true;
            else if (edge.Type == EdgeType.convergent)
                hasConvergent = true;
        }
        float mod = 0f;
        if (hasDivergent)
            mod += DIVERGENT_BONUS;
        if (hasConvergent)
            mod -= CONVERGENT_PENALTY;
        return mod;
    }
}
