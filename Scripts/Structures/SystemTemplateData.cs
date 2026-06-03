using Godot.Collections;

namespace Structures;

/// <summary>
/// Holds the four sections of a loaded system template.
/// Each section contains an array of dictionaries matching the ToParams() output
/// of the corresponding GUI item (DominantBodyItem, SatelliteBeltItem, PlanetaryBodyItem,
/// SatelliteItem). Satellites are flattened to a top-level section; each carries a
/// <c>parent</c> string naming the body it orbits (a planetary body or another satellite).
/// </summary>
public record SystemTemplateData(
    Array<Dictionary> Dominant,
    Array<Dictionary> Belts,
    Array<Dictionary> Planetary,
    Array<Dictionary> Satellites
);
