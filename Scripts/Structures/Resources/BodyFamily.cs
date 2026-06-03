using System;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Top-level taxonomy of celestial body families. Each family has a distinct
/// generation pipeline. New subtypes are data-driven via <see cref="SubtypeDefinition"/>,
/// but family membership is fixed because it determines which engine pipeline runs.
/// </summary>
public enum BodyFamily
{
    RockyPlanet,
    GasGiant,
    IceGiant,
    DwarfPlanet,
    Star,
    NeutronStar,
    BlackHole,
    Satellite,
    Belt,
}

/// <summary>
/// Maps the flat <see cref="OrbitalBodyType"/> taxonomy onto the fixed
/// <see cref="BodyFamily"/> pipeline categories. Moon/Asteroid/Comet collapse to
/// <see cref="BodyFamily.Satellite"/> so the existing biome/subtype pipeline keeps working.
/// </summary>
public static class OrbitalBodyTypeExtensions
{
    public static BodyFamily ToFamily(this OrbitalBodyType type) => type switch
    {
        OrbitalBodyType.Star => BodyFamily.Star,
        OrbitalBodyType.RockyPlanet => BodyFamily.RockyPlanet,
        OrbitalBodyType.GasGiant => BodyFamily.GasGiant,
        OrbitalBodyType.IceGiant => BodyFamily.IceGiant,
        OrbitalBodyType.DwarfPlanet => BodyFamily.DwarfPlanet,
        OrbitalBodyType.BlackHole => BodyFamily.BlackHole,
        OrbitalBodyType.NeutronStar => BodyFamily.NeutronStar,
        OrbitalBodyType.Moon => BodyFamily.Satellite,
        OrbitalBodyType.Asteroid => BodyFamily.Satellite,
        OrbitalBodyType.Comet => BodyFamily.Satellite,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown OrbitalBodyType: {type}"),
    };

    /// <summary>True for body types that anchor an N-body system (stars, black holes).</summary>
    public static bool IsDominant(this OrbitalBodyType type) =>
        type is OrbitalBodyType.Star or OrbitalBodyType.BlackHole or OrbitalBodyType.NeutronStar;

    /// <summary>True for the satellite family (Moon/Asteroid/Comet).</summary>
    public static bool IsSatellite(this OrbitalBodyType type) =>
        type is OrbitalBodyType.Moon or OrbitalBodyType.Asteroid or OrbitalBodyType.Comet;
}
