using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace ProceduralGeneration.MeshGeneration.ResourceGeneration;

/// <summary>
/// Generates resources per VoronoiCell as spatially-coherent fields.
///
/// Each eligible resource owns a deterministic spherical noise field; neighbouring cells
/// sample nearby points on that field, so resources form gradual, overlapping patches
/// instead of independent per-cell random draws. A resource group's per-biome
/// <see cref="AvailabilityLevel"/> sets the field threshold (patch coverage) and an
/// abundance scale. Physical impossibilities (frozen resources in hot biomes, aquatic
/// resources in arid biomes) are gated in code from biome/resource tags.
/// </summary>
public static class CellResourceGenerator
{
    /// <summary>Noise frequency for resource patch fields. Larger patches span more cells.</summary>
    private const float PATCH_FREQUENCY = 1.2f;

    /// <summary>Direction-space sampling scale for the spherical noise lookup.</summary>
    private const float PATCH_SCALE = 2.0f;

    /// <summary>Minimum abundance written for a resource that clears its threshold.</summary>
    private const float MIN_ABUNDANCE = 0.05f;

    /// <summary>
    /// Per-level patch threshold (on remapped [0,1] noise) and abundance scale.
    /// Lower threshold → larger, more frequent patches. Tunable.
    /// </summary>
    private static readonly Dictionary<AvailabilityLevel, (float threshold, float abundance)> LevelTable =
        new()
        {
            [AvailabilityLevel.Abundant] = (0.30f, 1.00f),
            [AvailabilityLevel.Frequent] = (0.45f, 0.85f),
            [AvailabilityLevel.Normal] = (0.60f, 0.70f),
            [AvailabilityLevel.Scarce] = (0.75f, 0.55f),
            [AvailabilityLevel.Rare] = (0.86f, 0.40f),
        };

    // Biome tag groups that drive the physical hard gates.
    private static readonly HashSet<string> HotBiomeTags = new(StringComparer.Ordinal)
        { "hot", "volcanic", "lethal" };
    private static readonly HashSet<string> AridBiomeTags = new(StringComparer.Ordinal)
        { "arid", "hot", "volcanic", "lethal" };

    // Resource tags that mark a resource as frozen-only / aquatic-only.
    private static readonly HashSet<string> FrozenResourceTags = new(StringComparer.Ordinal)
        { "frozen" };
    private static readonly HashSet<string> AquaticResourceTags = new(StringComparer.Ordinal)
        { "aquatic", "marine", "water_world", "water_needed" };

    /// <summary>
    /// Generates resource fields for all cells across all continents.
    /// </summary>
    /// <param name="continents">Continents containing VoronoiCells to populate.</param>
    /// <param name="planetaryResourceConfig">Resolved subtype eligibility + resource groups.</param>
    /// <param name="biomeResourceConfig">Per-biome group availability levels.</param>
    /// <param name="classification">The celestial body classification (selects subtype config).</param>
    /// <param name="rng">RNG; its Seed drives deterministic per-resource fields.</param>
    /// <param name="maxResourcesPerCell">Unused by the field path (kept for compatibility).</param>
    public static void GenerateResources(
        Dictionary<int, Continent> continents,
        PlanetaryResourceConfig planetaryResourceConfig,
        BiomeResourceConfig biomeResourceConfig,
        BodyClassification classification,
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
        var subtypeConfig = planetaryResourceConfig.GetConfigForSubtype(classification);
        if (subtypeConfig == null)
        {
            GameLogger.Warning($"CellResourceGenerator: No resource config for {classification}");
            return;
        }

        float baseResourceWeight = subtypeConfig.GetResourceWeight();

        // Step 2: Eligible resources for this subtype
        var eligibleResources = GetEligibleResources(subtypeConfig);
        if (eligibleResources.Count == 0)
        {
            GameLogger.Warning(
                "CellResourceGenerator: No eligible resources found for body subtype config"
            );
            return;
        }

        GameLogger.Info(
            $"CellResourceGenerator: Body {classification}, {eligibleResources.Count} eligible resources, baseWeight: {baseResourceWeight}"
        );

        // Step 3: Build per-resource lookups (groups, fields, gate flags)
        var resourceToGroups = BuildResourceToGroups(eligibleResources, planetaryResourceConfig.ResourceGroups);
        int baseSeed = unchecked((int)rng.Seed);

        var fields = new Dictionary<string, FastNoiseLite>(eligibleResources.Count);
        var frozenOnly = new HashSet<string>(StringComparer.Ordinal);
        var aquaticOnly = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in eligibleResources)
        {
            string id = resource.IdName!;
            fields[id] = NoiseHelpers.CreateSurfaceNoise(
                unchecked(baseSeed ^ StableHash(id)),
                PATCH_FREQUENCY,
                octaves: 4
            );

            if (resource.Tags != null)
            {
                if (resource.Tags.Overlaps(FrozenResourceTags))
                    frozenOnly.Add(id);
                if (resource.Tags.Overlaps(AquaticResourceTags))
                    aquaticOnly.Add(id);
            }
        }

        var biomeDb = BiomeDatabase.Instance;

        // Step 4: Generate per cell
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
                    resourceToGroups,
                    fields,
                    frozenOnly,
                    aquaticOnly,
                    biomeResourceConfig,
                    biomeDb,
                    baseResourceWeight
                );

                if (assigned > 0)
                    cellsWithResources++;
            }

            continent.AggregateResourcesFromCells();
        }

        GameLogger.Info(
            $"CellResourceGenerator: {cellsWithResources}/{totalCells} cells have resources"
        );

        LogAbundanceCategoryHistogram(continents);
    }

    /// <summary>
    /// Logs how many generated deposits fall into each richness category, for tuning the bands in
    /// <c>abundance_categories.yaml</c>. The stored abundance % is the source of truth; the
    /// category is a pure mapping via <see cref="AbundanceCategoryTable"/>.
    /// </summary>
    private static void LogAbundanceCategoryHistogram(Dictionary<int, Continent> continents)
    {
        var counts = new Dictionary<AbundanceCategory, int>();
        foreach (var kvp in continents)
        {
            if (kvp.Value.cells == null)
                continue;
            foreach (var cell in kvp.Value.cells)
            {
                if (cell.Resources == null)
                    continue;
                foreach (var res in cell.Resources)
                {
                    var cat = AbundanceCategoryTable.Categorize(res.Value);
                    counts[cat] = counts.GetValueOrDefault(cat) + 1;
                }
            }
        }

        GameLogger.Info(
            "CellResourceGenerator: abundance categories — "
            + $"Rare {counts.GetValueOrDefault(AbundanceCategory.Rare)}, "
            + $"Scarce {counts.GetValueOrDefault(AbundanceCategory.Scarce)}, "
            + $"Uncommon {counts.GetValueOrDefault(AbundanceCategory.Uncommon)}, "
            + $"Common {counts.GetValueOrDefault(AbundanceCategory.Common)}, "
            + $"Frequent {counts.GetValueOrDefault(AbundanceCategory.Frequent)}, "
            + $"Abundant {counts.GetValueOrDefault(AbundanceCategory.Abundant)}, "
            + $"Plentiful {counts.GetValueOrDefault(AbundanceCategory.Plentiful)}"
        );
    }

    /// <summary>
    /// Generates resources for a single cell by sampling each eligible resource's field.
    /// No per-cell cap — every resource clearing its threshold is placed, so overlapping
    /// patches naturally produce rich cells and gaps produce sparse cells.
    /// </summary>
    private static int GenerateCellResources(
        VoronoiCell cell,
        List<ResourceDefinition> eligibleResources,
        Dictionary<string, List<string>> resourceToGroups,
        Dictionary<string, FastNoiseLite> fields,
        HashSet<string> frozenOnly,
        HashSet<string> aquaticOnly,
        BiomeResourceConfig biomeResourceConfig,
        BiomeDatabase? biomeDb,
        float baseResourceWeight
    )
    {
        // Resolve biome tags once per cell for the physical gates.
        bool biomeHot = false;
        bool biomeArid = false;
        var biomeDef = biomeDb?.GetById(cell.Biome);
        if (biomeDef?.Tags != null)
        {
            foreach (var tag in biomeDef.Tags)
            {
                if (HotBiomeTags.Contains(tag))
                    biomeHot = true;
                if (AridBiomeTags.Contains(tag))
                    biomeArid = true;
            }
        }

        Vector3 dir = cell.Center.Normalized();
        int assigned = 0;

        foreach (var resource in eligibleResources)
        {
            string id = resource.IdName!;

            // Physical hard gates — frozen resources cannot form in hot biomes,
            // aquatic resources cannot form in arid biomes.
            if (biomeHot && frozenOnly.Contains(id))
                continue;
            if (biomeArid && aquaticOnly.Contains(id))
                continue;

            // Most permissive availability across this resource's groups in this biome.
            var level = ResolveLevel(id, resourceToGroups, biomeResourceConfig, cell.Biome);
            if (level == null)
                continue; // group(s) not listed for this biome → None

            var (threshold, abundScale) = LevelTable[level.Value];

            float s = NoiseHelpers.Remap01(
                NoiseHelpers.SampleSphericalNoise(fields[id], dir, PATCH_SCALE)
            );

            if (s <= threshold)
                continue;

            float t = (s - threshold) / (1.0f - threshold);
            float abundance = Mathf.Clamp(t * abundScale * baseResourceWeight, MIN_ABUNDANCE, 1.0f);
            cell.Resources[id] = abundance;
            assigned++;
        }

        return assigned;
    }

    /// <summary>
    /// Picks the most permissive (lowest-threshold) availability level among all groups a
    /// resource belongs to that the biome lists. Returns null if no group is listed.
    /// </summary>
    private static AvailabilityLevel? ResolveLevel(
        string resourceId,
        Dictionary<string, List<string>> resourceToGroups,
        BiomeResourceConfig biomeResourceConfig,
        string biomeId
    )
    {
        if (!resourceToGroups.TryGetValue(resourceId, out var groups))
            return null;

        AvailabilityLevel? best = null;
        float bestThreshold = float.MaxValue;

        foreach (var group in groups)
        {
            var level = biomeResourceConfig.GetGroupAvailability(biomeId, group);
            if (level == null)
                continue;

            float threshold = LevelTable[level.Value].threshold;
            if (threshold < bestThreshold)
            {
                bestThreshold = threshold;
                best = level;
            }
        }

        return best;
    }

    /// <summary>
    /// Maps each eligible resource ID to the groups (within the provided group definitions)
    /// that contain it. A resource may belong to more than one group.
    /// </summary>
    private static Dictionary<string, List<string>> BuildResourceToGroups(
        List<ResourceDefinition> eligibleResources,
        Dictionary<string, List<string>> resourceGroups
    )
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var resource in eligibleResources)
            map[resource.IdName!] = new List<string>();

        if (resourceGroups != null)
        {
            foreach (var groupKvp in resourceGroups)
            {
                if (groupKvp.Value == null)
                    continue;
                foreach (var resourceId in groupKvp.Value)
                {
                    if (map.TryGetValue(resourceId, out var groups))
                        groups.Add(groupKvp.Key);
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Generates resources for satellite bodies (no cells / biomes) using subtype config only.
    /// Unchanged field-less path: a single weighted deposit set with no spatial field.
    /// </summary>
    public static Dictionary<string, ResourceDeposit> GenerateSatelliteResources(
        PlanetaryResourceConfig planetaryResourceConfig,
        BodyClassification? classification,
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

        var subtypeConfig = classification != null
            ? planetaryResourceConfig.GetConfigForSubtype(classification)
            : null;
        if (subtypeConfig == null)
        {
            GameLogger.Warning(
                $"CellResourceGenerator: No resource config for satellite classification {classification}"
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
    /// Deterministic FNV-1a 32-bit hash of a string (stable across runs/processes, unlike
    /// <see cref="string.GetHashCode()"/>). Used to derive a per-resource noise seed.
    /// </summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char c in s)
            {
                hash ^= c;
                hash *= prime;
            }
            return (int)hash;
        }
    }

    /// <summary>
    /// Selects weighted random resources from a pool (used by the satellite path).
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
