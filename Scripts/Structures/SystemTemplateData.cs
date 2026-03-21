using Godot.Collections;

namespace Structures;

/// <summary>
/// Holds the three sections of a loaded system template.
/// Each section contains an array of dictionaries matching the ToParams() output
/// of the corresponding GUI item (DominantBodyItem, SatelliteBeltItem, PlanetaryBodyItem).
/// </summary>
public record SystemTemplateData(
    Array<Dictionary> Dominant,
    Array<Dictionary> Belts,
    Array<Dictionary> Planetary
);
