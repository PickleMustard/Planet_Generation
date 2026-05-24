using System.Collections.Generic;
using Godot;
using ProceduralGeneration.ColorSystem;
using Structures;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;

namespace ProceduralGeneration.SubtypeSystem;

/// <summary>
/// Resolves per-subtype generation parameter ranges (mesh, tectonic, spherical harmonics) into
/// concrete rolled values that <see cref="MeshGeneration.UnifiedCelestialMesh.ConfigureFrom"/>
/// can consume.
///
/// Each Apply* method only writes keys for which the subtype declares a range. Knobs the subtype
/// does not declare are left untouched — the consumer falls back to its <c>[Export]</c> default.
///
/// Mirrors the lookup pattern used by <see cref="BiomeSystem.BiomeAssignerFactory"/>: classification
/// is mapped to a subtype id string, then resolved against <see cref="SubtypeDatabase"/>; missing
/// subtypes fall through to <see cref="SubtypeDatabase.GetDefaultForFamily"/>.
/// </summary>
public static class SubtypeGenParamResolver
{
    /// <summary>
    /// Writes base-mesh top-level keys (<c>subdivisions</c>, <c>num_abberations</c>,
    /// <c>num_deformation_cycles</c>) into <paramref name="meshParams"/>. Existing keys are not
    /// overwritten so explicit overrides from bodyDict take precedence.
    /// </summary>
    public static void ApplyMeshParams(
        Godot.Collections.Dictionary meshParams,
        BodyClassification classification,
        RandomNumberGenerator rng,
        bool useMidpoint = false)
    {
        var def = Resolve(classification);
        if (def == null) return;

        TryWriteInt(meshParams, "subdivisions", def.MeshRanges, "subdivisions", rng, useMidpoint);
        TryWriteInt(meshParams, "num_abberations", def.MeshRanges, "num_abberations", rng, useMidpoint);
        TryWriteInt(meshParams, "num_deformation_cycles", def.MeshRanges, "num_deformation_cycles", rng, useMidpoint);
        TryWriteVerticesPerEdge(meshParams, def, rng, useMidpoint);
    }

    /// <summary>
    /// Rolls one vertex-count per subdivision level and writes the resulting <c>int[]</c> into
    /// <paramref name="meshParams"/> as <c>vertices_per_edge</c>. Level count comes from the
    /// already-rolled <c>subdivisions</c> when present, otherwise from
    /// <see cref="SubtypeDefinition.VerticesPerEdgeRanges"/>.Count. When the rolled level index
    /// exceeds the configured list, the last entry is reused — mirrors the legacy clamp.
    /// </summary>
    private static void TryWriteVerticesPerEdge(
        Godot.Collections.Dictionary target,
        SubtypeDefinition def,
        RandomNumberGenerator rng,
        bool useMidpoint)
    {
        if (target.ContainsKey("vertices_per_edge")) return;
        if (def.VerticesPerEdgeRanges.Count == 0) return;

        int levels;
        if (target.ContainsKey("subdivisions"))
            levels = (int)target["subdivisions"];
        else
            levels = def.VerticesPerEdgeRanges.Count;

        if (levels <= 0) return;

        int[] result = new int[levels];
        for (int i = 0; i < levels; i++)
        {
            int idx = System.Math.Min(i, def.VerticesPerEdgeRanges.Count - 1);
            var r = def.VerticesPerEdgeRanges[idx];
            int min = (int)Mathf.Round(r.Min);
            int max = (int)Mathf.Round(r.Max);
            result[i] = useMidpoint
                ? (int)Mathf.Round((min + max) * 0.5f)
                : rng.RandiRange(min, max);
        }
        target.Add("vertices_per_edge", result);
    }

    /// <summary>
    /// Writes a nested <c>tectonic</c> sub-dict carrying the ten tectonic knobs. Only knobs the
    /// subtype declares are emitted; missing knobs fall through to UnifiedCelestialMesh defaults.
    /// </summary>
    public static void ApplyTectonicParams(
        Godot.Collections.Dictionary meshParams,
        BodyClassification classification,
        RandomNumberGenerator rng,
        bool useMidpoint = false)
    {
        if (meshParams.ContainsKey("tectonic")) return;
        var def = Resolve(classification);
        if (def == null || def.TectonicRanges.Count == 0) return;

        var sub = new Godot.Collections.Dictionary();
        TryWriteInt(sub, "num_continents", def.TectonicRanges, "num_continents", rng, useMidpoint);
        TryWriteFloat(sub, "stress_scale", def.TectonicRanges, "stress_scale", rng, useMidpoint);
        TryWriteFloat(sub, "shear_scale", def.TectonicRanges, "shear_scale", rng, useMidpoint);
        TryWriteFloat(sub, "max_propagation_distance", def.TectonicRanges, "max_propagation_distance", rng, useMidpoint);
        TryWriteFloat(sub, "propagation_falloff", def.TectonicRanges, "propagation_falloff", rng, useMidpoint);
        TryWriteFloat(sub, "inactive_stress_threshold", def.TectonicRanges, "inactive_stress_threshold", rng, useMidpoint);
        TryWriteFloat(sub, "general_height_scale", def.TectonicRanges, "general_height_scale", rng, useMidpoint);
        TryWriteFloat(sub, "general_shear_scale", def.TectonicRanges, "general_shear_scale", rng, useMidpoint);
        TryWriteFloat(sub, "general_compression_scale", def.TectonicRanges, "general_compression_scale", rng, useMidpoint);
        TryWriteFloat(sub, "general_transform_scale", def.TectonicRanges, "general_transform_scale", rng, useMidpoint);

        if (sub.Count > 0) meshParams.Add("tectonic", sub);
    }

    /// <summary>
    /// Writes a nested <c>spherical_harmonics</c> sub-dict carrying the amplitude (and, once
    /// Phase 3 lands, per-band scales).
    /// </summary>
    public static void ApplySphericalHarmonicsParams(
        Godot.Collections.Dictionary meshParams,
        BodyClassification classification,
        RandomNumberGenerator rng,
        bool useMidpoint = false)
    {
        if (meshParams.ContainsKey("spherical_harmonics")) return;
        var def = Resolve(classification);
        if (def == null || def.SphericalHarmonicsRanges.Count == 0) return;

        var sub = new Godot.Collections.Dictionary();
        TryWriteFloat(sub, "amplitude", def.SphericalHarmonicsRanges, "amplitude", rng, useMidpoint);
        TryWriteFloat(sub, "band_scale_l0", def.SphericalHarmonicsRanges, "band_scale_l0", rng, useMidpoint);
        TryWriteFloat(sub, "band_scale_l1", def.SphericalHarmonicsRanges, "band_scale_l1", rng, useMidpoint);
        TryWriteFloat(sub, "band_scale_l2", def.SphericalHarmonicsRanges, "band_scale_l2", rng, useMidpoint);
        TryWriteFloat(sub, "band_scale_l3", def.SphericalHarmonicsRanges, "band_scale_l3", rng, useMidpoint);

        if (sub.Count > 0) meshParams.Add("spherical_harmonics", sub);
    }

    // ── Lookup ──────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up the SubtypeDefinition for a classification, falling back to the family default
    /// when the specific subtype is unregistered. Returns null when SubtypeDatabase is unloaded
    /// or the classification has no representable subtype id.
    /// </summary>
    private static SubtypeDefinition? Resolve(BodyClassification classification)
    {
        try
        {
            var db = SubtypeDatabase.Instance;
            if (!db.IsLoaded) return null;

            string? id = TryGetSubtypeId(classification);
            if (!string.IsNullOrEmpty(id))
            {
                var byId = db.GetById(id);
                if (byId != null) return byId;
            }

            BodyFamily? family = TryGetFamily(classification);
            return family.HasValue ? db.GetDefaultForFamily(family.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetSubtypeId(BodyClassification c) => c switch
    {
        BodyClassification.RockyPlanet rp => BiomeIdMapper.RockyPlanetSubtypeToId(rp.Subtype),
        BodyClassification.GasGiant gg => BiomeIdMapper.GasGiantSubtypeToId(gg.Subtype),
        BodyClassification.IceGiant ig => BiomeIdMapper.IceGiantSubtypeToId(ig.Subtype),
        BodyClassification.DwarfPlanet dp => BiomeIdMapper.DwarfPlanetSubtypeToId(dp.Subtype),
        BodyClassification.Star s => BiomeIdMapper.StarSubtypeToId(s.Subtype),
        BodyClassification.NeutronStar ns => BiomeIdMapper.NeutronStarSubtypeToId(ns.Subtype),
        BodyClassification.BlackHole bh => BiomeIdMapper.BlackHoleSubtypeToId(bh.Subtype),
        BodyClassification.Satellite sat when sat.Subtype.HasValue
            => BiomeIdMapper.SatelliteSubtypeToId(sat.Subtype.Value),
        BodyClassification.Belt b when b.Subtype.HasValue
            => BiomeIdMapper.BeltSubtypeToId(b.Subtype.Value),
        _ => null,
    };

    private static BodyFamily? TryGetFamily(BodyClassification c) => c switch
    {
        BodyClassification.RockyPlanet => BodyFamily.RockyPlanet,
        BodyClassification.GasGiant => BodyFamily.GasGiant,
        BodyClassification.IceGiant => BodyFamily.IceGiant,
        BodyClassification.DwarfPlanet => BodyFamily.DwarfPlanet,
        BodyClassification.Star => BodyFamily.Star,
        BodyClassification.NeutronStar => BodyFamily.NeutronStar,
        BodyClassification.BlackHole => BodyFamily.BlackHole,
        BodyClassification.Satellite => BodyFamily.Satellite,
        BodyClassification.Belt => BodyFamily.Belt,
        _ => null,
    };

    // ── Roll helpers ────────────────────────────────────────────────────

    private static void TryWriteInt(
        Godot.Collections.Dictionary target,
        string outKey,
        Dictionary<string, FloatRange> ranges,
        string rangeKey,
        RandomNumberGenerator rng,
        bool useMidpoint = false)
    {
        if (target.ContainsKey(outKey)) return;
        if (!ranges.TryGetValue(rangeKey, out var r)) return;
        int rolled = useMidpoint
            ? (int)Mathf.Round(r.Midpoint)
            : rng.RandiRange((int)Mathf.Round(r.Min), (int)Mathf.Round(r.Max));
        target.Add(outKey, rolled);
    }

    private static void TryWriteFloat(
        Godot.Collections.Dictionary target,
        string outKey,
        Dictionary<string, FloatRange> ranges,
        string rangeKey,
        RandomNumberGenerator rng,
        bool useMidpoint = false)
    {
        if (target.ContainsKey(outKey)) return;
        if (!ranges.TryGetValue(rangeKey, out var r)) return;
        target.Add(outKey, useMidpoint ? r.Midpoint : r.Roll(rng));
    }
}
