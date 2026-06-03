namespace Structures.Enums;

/// <summary>
/// Flat, merged taxonomy of every orbital body type. Generation is routed by the
/// config dictionary, not the class type, so dominant bodies, planets, and satellites
/// share one enum. Moon/Asteroid/Comet are first-class members; a mapping layer
/// (<c>OrbitalBodyTypeExtensions.ToFamily</c>) collapses them to <c>BodyFamily.Satellite</c>
/// for the biome/subtype pipeline.
/// </summary>
public enum OrbitalBodyType
{
    Star,
    RockyPlanet,
    GasGiant,
    IceGiant,
    DwarfPlanet,
    BlackHole,
    NeutronStar,
    Moon,
    Asteroid,
    Comet,
}

public enum GroupingCategories
{
    Balanced,
    Clustered,
    DualGrouping,
    Random,
    HalfAndHalf,
}

public enum SatelliteGroupTypes
{
    AsteroidBelt,
    IceBelt,
    Comet,
}
