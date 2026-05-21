using System;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using Structures.GameState;

namespace ProceduralGeneration.BiomeSystem;

/// <summary>
/// Data-driven biome assigner. Evaluates a list of rules in declared order; first match wins.
/// Moisture is computed either via the Whittaker-style formula or as a uniform random draw,
/// depending on the configured MoistureMode.
/// </summary>
public class ConfigurableBiomeAssigner : IBiomeAssigner
{
    private readonly BiomeAssignerEntry _entry;
    private readonly Biome.BiomeType _fallbackBiome;

    public ConfigurableBiomeAssigner(BiomeAssignerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entry = entry;
        _fallbackBiome = _entry.Rules.Count > 0
            ? _entry.Rules[^1].Biome
            : Biome.BiomeType.Grassland;
    }

    public Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);
        float absLat = Mathf.Abs(latitude);

        foreach (var rule in _entry.Rules)
        {
            var w = rule.When;
            if (w.HeightAbove is float hA && !(normalizedHeight > hA)) continue;
            if (w.HeightBelow is float hB && !(normalizedHeight < hB)) continue;
            if (w.MoistureAbove is float mA && !(moisture > mA)) continue;
            if (w.MoistureBelow is float mB && !(moisture < mB)) continue;
            if (w.AbsLatitudeAbove is float lA && !(absLat > lA)) continue;
            if (w.AbsLatitudeBelow is float lB && !(absLat < lB)) continue;
            return rule.Biome;
        }

        return _fallbackBiome;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        var p = _entry.Moisture;
        switch (p.Mode)
        {
            case MoistureMode.UniformRandom:
                return rng.RandfRange(p.RandomMin, p.RandomMax);

            case MoistureMode.Whittaker:
            default:
                float latitudeFactor = Mathf.Clamp(
                    continent.averagedCenter.Y / (p.LatitudeFactorDivisor == 0f ? 1f : p.LatitudeFactorDivisor),
                    0f, 1f);
                float sizeFactor = continent.cells.Count
                    / (p.SizeFactorDivisor == 0f ? 1f : p.SizeFactorDivisor);
                float randomVariation = rng.RandfRange(p.RandomMin, p.RandomMax);
                float denom = p.MaxMoisture == 0f ? 1f : p.MaxMoisture;
                float numerator = baseMoisture + p.BaseOffset + latitudeFactor + sizeFactor
                                  + randomVariation + p.ConstantOffset;
                return p.MaxMoisture - numerator / denom;
        }
    }
}
