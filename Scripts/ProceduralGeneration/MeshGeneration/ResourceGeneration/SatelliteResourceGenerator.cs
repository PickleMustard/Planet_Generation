using System.Collections.Generic;
using Godot;
using Structures.Resources;

namespace ProceduralGeneration.MeshGeneration.ResourceGeneration;

/// <summary>
/// Generates resources for satellite bodies (asteroids, moons, etc.).
/// Uses weighted random selection from configured resource pools.
/// </summary>
/// <remarks>
/// The generator uses a main/secondary resource pool system:
/// - Main resources: High-value resources with at least one guaranteed
/// - Secondary resources: Optional resources that may or may not be present
/// 
/// Weights control the probability of each resource being selected.
/// </remarks>
public static class SatelliteResourceGenerator
{
    /// <summary>
    /// Generates resource deposits for a satellite body based on configuration.
    /// </summary>
    /// <param name="resourceConfig">Dictionary containing 'main' and 'secondary' resource lists.</param>
    /// <param name="rng">Random number generator for deterministic generation.</param>
    /// <returns>Dictionary mapping resource IDs to ResourceDeposits.</returns>
    public static Dictionary<string, ResourceDeposit> GenerateResources(
        Godot.Collections.Dictionary resourceConfig,
        RandomNumberGenerator rng)
    {
        var deposits = new Dictionary<string, ResourceDeposit>();

        if (resourceConfig == null || resourceConfig.Count == 0)
        {
            return deposits;
        }

        if (resourceConfig.TryGetValue("main", out var mainVariant) &&
            mainVariant.As<Godot.Collections.Array>() is Godot.Collections.Array mainResources)
        {
            var selectedMain = SelectWeightedResource(mainResources, rng);
            if (selectedMain != null)
            {
                var deposit = CreateDepositFromConfig(selectedMain, rng, highAbundance: true);
                if (deposit != null && !string.IsNullOrEmpty(deposit.ResourceId))
                {
                    deposits[deposit.ResourceId] = deposit;
                }
            }
        }

        if (resourceConfig.TryGetValue("secondary", out var secondaryVariant) &&
            secondaryVariant.As<Godot.Collections.Array>() is Godot.Collections.Array secondaryResources)
        {
            int secondaryCount = rng.RandfRange(0f, 1f) < 0.6f ? 1 : (rng.RandfRange(0f, 1f) < 0.3f ? 2 : 0);

            for (int i = 0; i < secondaryCount && secondaryResources.Count > 0; i++)
            {
                var selectedSecondary = SelectWeightedResource(secondaryResources, rng);
                if (selectedSecondary != null)
                {
                    var deposit = CreateDepositFromConfig(selectedSecondary, rng, highAbundance: false);
                    if (deposit != null && !string.IsNullOrEmpty(deposit.ResourceId) && !deposits.ContainsKey(deposit.ResourceId))
                    {
                        deposits[deposit.ResourceId] = deposit;
                    }
                }
            }
        }

        ValidateDeposits(deposits);

        return deposits;
    }

    private static Godot.Collections.Dictionary? SelectWeightedResource(
        Godot.Collections.Array resources,
        RandomNumberGenerator rng)
    {
        if (resources == null || resources.Count == 0)
            return null;

        float totalWeight = 0f;
        var validResources = new List<(Godot.Collections.Dictionary config, float weight)>();

        foreach (var resourceVariant in resources)
        {
            if (resourceVariant.AsGodotDictionary() is Godot.Collections.Dictionary resourceConfig)
            {
                float weight = GetWeight(resourceConfig);
                if (weight > 0)
                {
                    totalWeight += weight;
                    validResources.Add((resourceConfig, weight));
                }
            }
        }

        if (validResources.Count == 0 || totalWeight <= 0)
            return null;

        float roll = rng.RandfRange(0f, totalWeight);
        float accumulated = 0f;

        foreach (var (config, weight) in validResources)
        {
            accumulated += weight;
            if (roll <= accumulated)
            {
                return config;
            }
        }

        return validResources[0].config;
    }

    private static ResourceDeposit? CreateDepositFromConfig(
        Godot.Collections.Dictionary config,
        RandomNumberGenerator rng,
        bool highAbundance)
    {
        if (config == null)
            return null;

        string resourceId = config.GetValueOrDefault("resource_id", "").AsString();
        if (string.IsNullOrEmpty(resourceId))
            return null;

        if (!ResourceDatabase.Instance.ValidateResourceExists(resourceId))
        {
            GD.PrintErr($"SatelliteResourceGenerator: Unknown resource ID '{resourceId}'");
            return null;
        }

        float baseAbundance = highAbundance ? 0.7f : 0.4f;
        float abundanceVariation = highAbundance ? 0.25f : 0.35f;
        float abundance = Mathf.Clamp(baseAbundance + rng.RandfRange(-abundanceVariation, abundanceVariation), 0.1f, 1.0f);

        float baseAccessibility = 0.7f;
        float accessibilityVariation = 0.25f;
        float accessibility = Mathf.Clamp(baseAccessibility + rng.RandfRange(-accessibilityVariation, accessibilityVariation), 0.2f, 1.0f);

        return new ResourceDeposit(resourceId, abundance, accessibility);
    }

    private static float GetWeight(Godot.Collections.Dictionary config)
    {
        if (config == null)
            return 0f;

        float baseWeight = config.GetValueOrDefault("base_weight", 1.0f).AsSingle();
        float distanceWeight = config.GetValueOrDefault("distance_weight", 1.0f).AsSingle();

        return baseWeight * distanceWeight;
    }

    private static void ValidateDeposits(Dictionary<string, ResourceDeposit> deposits)
    {
        foreach (var kvp in deposits)
        {
            if (!ResourceDatabase.Instance.ValidateResourceExists(kvp.Key))
            {
                GD.PrintErr($"SatelliteResourceGenerator: Generated deposit for unknown resource '{kvp.Key}'");
            }
        }
    }
}
