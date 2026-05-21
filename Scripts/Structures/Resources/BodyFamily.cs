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
