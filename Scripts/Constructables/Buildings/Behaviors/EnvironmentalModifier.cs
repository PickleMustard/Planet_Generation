using System.Collections.Generic;
using Godot;
using ProceduralGeneration;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Scales a manufacturing recipe's output magnitude by an environment-derived factor
/// (star distance, atmosphere, presence of a vent). Configured via the
/// <c>environmental_modifier:</c> block under a building's ManufacturingBehavior entry.
/// Sampled once at OnRegister; applied multiplicatively to every output deposited
/// at cycle end. Factor of zero blocks the cycle from starting.
/// </summary>
public sealed class EnvironmentalModifier
{
    public enum ModifierType
    {
        None,
        StarDistanceInverseSquare,
        AtmosphereLinear,
        VentPresenceBinary,
    }

    public ModifierType Type { get; init; } = ModifierType.None;
    public float ReferenceDistance { get; init; } = 1000f;
    public float ReferenceAtmosphere { get; init; } = 1f;
    public float MaxScale { get; init; } = 4f;

    /// <summary>
    /// Parses an <c>environmental_modifier:</c> mapping. Returns null when absent or empty.
    /// </summary>
    public static EnvironmentalModifier? FromConfig(Dictionary<string, object> parentConfig)
    {
        if (!parentConfig.TryGetValue("environmental_modifier", out var raw) || raw == null)
            return null;
        if (raw is not Dictionary<object, object> dict)
            return null;

        var flat = new Dictionary<string, object>();
        foreach (var kvp in dict)
        {
            var k = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(k) && kvp.Value != null)
                flat[k] = kvp.Value;
        }

        string typeStr = BehaviorConfigHelper.ReadString(flat, "type", "NONE") ?? "NONE";
        var type = ParseType(typeStr);

        return new EnvironmentalModifier
        {
            Type = type,
            ReferenceDistance = BehaviorConfigHelper.ReadFloat(flat, "reference_distance", 1000f),
            ReferenceAtmosphere = BehaviorConfigHelper.ReadFloat(flat, "reference_atmosphere", 1f),
            MaxScale = BehaviorConfigHelper.ReadFloat(flat, "max_scale", 4f),
        };
    }

    private static ModifierType ParseType(string s)
    {
        return s.Trim().ToUpperInvariant() switch
        {
            "STAR_DISTANCE_INVERSE_SQUARE" => ModifierType.StarDistanceInverseSquare,
            "ATMOSPHERE_LINEAR" => ModifierType.AtmosphereLinear,
            "VENT_PRESENCE_BINARY" => ModifierType.VentPresenceBinary,
            _ => ModifierType.None,
        };
    }

    /// <summary>
    /// Computes the scale factor for the given building's environment. Returns 1.0 for
    /// ModifierType.None, 0 when the required body context is missing.
    /// </summary>
    public float ComputeFactor(Building? owner)
    {
        if (Type == ModifierType.None)
            return 1f;
        if (owner == null)
            return 0f;

        switch (Type)
        {
            case ModifierType.StarDistanceInverseSquare:
                return ComputeStarDistance(owner);
            case ModifierType.AtmosphereLinear:
                return ComputeAtmosphere(owner);
            case ModifierType.VentPresenceBinary:
                return ComputeVent(owner);
            default:
                return 1f;
        }
    }

    private float ComputeStarDistance(Building owner)
    {
        var body = ProducerBodyResolver.FindOwningBody(owner);
        if (body == null)
            return 0f;
        float dist = body.DistanceToParentStar();
        if (dist <= 0f)
            return 0f;
        float ratio = ReferenceDistance / dist;
        return Mathf.Clamp(ratio * ratio, 0f, MaxScale);
    }

    private float ComputeAtmosphere(Building owner)
    {
        var body = ProducerBodyResolver.FindOwningBody(owner);
        if (body == null)
            return 0f;
        float refAtm = ReferenceAtmosphere > 0f ? ReferenceAtmosphere : 1f;
        return Mathf.Clamp(body.Atmosphere / refAtm, 0f, MaxScale);
    }

    private static float ComputeVent(Building owner)
    {
        var cells = owner.OccupiedCells;
        if (cells == null || cells.Count == 0)
            return 0f;
        foreach (var cell in cells)
        {
            if (cell != null && cell.HasGeothermalVent)
                return 1f;
        }
        return 0f;
    }
}
