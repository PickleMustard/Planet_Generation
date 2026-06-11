using System.Collections.Generic;
using Godot;
using ProceduralGeneration.ColorSystem;
using Structures;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;

namespace ProceduralGeneration;

/// <summary>
/// Resolves a body's <see cref="BodyClassification"/> from its SystemTemplate entry using
/// per-body subtype weights. Replaces the former global AU-band probability system: subtype
/// distribution is now authored per body in the system template, not derived from orbital
/// distance. Priority:
/// (1) inline <c>subtype: &lt;id&gt;</c> string;
/// (2) <c>subtype_weights</c> map → weighted roll;
/// (3) per-type default subtype.
/// </summary>
public static class SubtypeResolver
{
    /// <summary>
    /// Weighted random pick over a subtype-id → weight map. Returns the chosen id, or
    /// <paramref name="fallback"/> when the map is empty / all-zero.
    /// </summary>
    public static string SelectFromWeights(
        IReadOnlyDictionary<string, float> weights,
        RandomNumberGenerator rng,
        string fallback = ""
    )
    {
        if (weights == null || weights.Count == 0)
            return fallback;

        float total = 0f;
        foreach (var kvp in weights)
        {
            if (kvp.Value > 0)
                total += kvp.Value;
        }
        if (total <= 0f)
            return fallback;

        float roll = rng.Randf() * total;
        float cumulative = 0f;
        string? last = null;
        foreach (var kvp in weights)
        {
            if (kvp.Value <= 0)
                continue;
            cumulative += kvp.Value;
            last = kvp.Key;
            if (roll <= cumulative)
                return kvp.Key;
        }
        return last ?? fallback;
    }

    /// <summary>
    /// Per-type fallback subtype, used when a body declares neither <c>subtype</c> nor
    /// <c>subtype_weights</c>. Returns a boxed per-family subtype enum value.
    /// </summary>
    public static object GetDefaultSubtype(OrbitalBodyType bodyType) => bodyType switch
    {
        OrbitalBodyType.RockyPlanet => RockyPlanetSubtype.Temperate,
        OrbitalBodyType.GasGiant => GasGiantSubtype.StandardJupiter,
        OrbitalBodyType.IceGiant => IceGiantSubtype.StandardNeptune,
        OrbitalBodyType.DwarfPlanet => DwarfPlanetSubtype.IcyKuiper,
        OrbitalBodyType.Star => StarSubtype.MainSequence,
        OrbitalBodyType.BlackHole => BlackHoleSubtype.StellarMass,
        OrbitalBodyType.NeutronStar => NeutronStarSubtype.Pulsar,
        OrbitalBodyType.Moon => SatelliteSubtype.RockyMoon,
        OrbitalBodyType.Asteroid => SatelliteSubtype.Carbonaceous,
        OrbitalBodyType.Comet => SatelliteSubtype.ShortPeriod,
        _ => StarSubtype.MainSequence,
    };

    /// <summary>
    /// Resolves a dominant/celestial body's classification from its template dict (the priority
    /// chain documented on the class). Always returns a non-null classification.
    /// </summary>
    public static BodyClassification Resolve(
        Godot.Collections.Dictionary body,
        OrbitalBodyType type,
        RandomNumberGenerator rng
    )
    {
        var family = type.ToFamily();

        // (1) inline explicit subtype id.
        if (body.ContainsKey("subtype"))
        {
            string id = body["subtype"].AsString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                var cls = ClassificationFromId(id, type, family);
                if (cls != null)
                    return cls;

                // Back-compat: yaml may still carry the legacy enum-name form ("Temperate").
                return SubtypeParser.ParseClassification(type, id);
            }
        }

        // (2) weighted roll over subtype_weights.
        if (body.ContainsKey("subtype_weights"))
        {
            var weights = ReadWeights((Godot.Collections.Dictionary)body["subtype_weights"]);
            string chosen = SelectFromWeights(weights, rng, fallback: "");
            if (!string.IsNullOrEmpty(chosen))
            {
                var cls = ClassificationFromId(chosen, type, family);
                if (cls != null)
                    return cls;
                GameLogger.Warning(
                    $"SubtypeResolver: subtype id '{chosen}' did not map to a {family} classification — using default"
                );
            }
        }

        // (3) per-type default.
        return BodyClassification.FromType(type, GetDefaultSubtype(type));
    }

    /// <summary>
    /// Rolls a concrete <see cref="SatelliteSubtype"/> for a satellite or belt member from a
    /// subtype-weights map. Falls back to the per-type default when the map is empty/unmapped so
    /// the member always carries a non-null subtype (resource/mesh-param lookups key off it).
    /// </summary>
    public static SatelliteSubtype ResolveSatelliteSubtype(
        IReadOnlyDictionary<string, float> weights,
        OrbitalBodyType satType,
        RandomNumberGenerator rng
    )
    {
        string chosen = SelectFromWeights(weights, rng, fallback: "");
        if (!string.IsNullOrEmpty(chosen))
        {
            var s = BiomeIdMapper.IdToSatelliteSubtype(chosen);
            if (s.HasValue)
                return s.Value;
        }
        return BodyClassification.DefaultSatelliteSubtype(satType);
    }

    /// <summary>Reads a yaml <c>subtype_weights</c> dictionary into a typed id → weight map.</summary>
    public static Dictionary<string, float> ReadWeights(Godot.Collections.Dictionary raw)
    {
        var weights = new Dictionary<string, float>(System.StringComparer.Ordinal);
        if (raw == null)
            return weights;
        foreach (var key in raw.Keys)
        {
            string id = key.AsString();
            if (string.IsNullOrEmpty(id))
                continue;
            weights[id] = (float)raw[key];
        }
        return weights;
    }

    private static BodyClassification? ClassificationFromId(
        string id,
        OrbitalBodyType type,
        BodyFamily family
    )
    {
        if (family == BodyFamily.Satellite)
        {
            var s = BiomeIdMapper.IdToSatelliteSubtype(id);
            return s.HasValue ? BodyClassification.FromSatelliteType(type, s.Value) : null;
        }
        return BiomeIdMapper.IdToBodyClassification(id, family);
    }
}
