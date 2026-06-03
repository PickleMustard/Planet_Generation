using System;
using System.Text.RegularExpressions;
using Structures;
using Structures.Enums;
using Structures.Resources;

namespace ProceduralGeneration.ColorSystem;

/// <summary>
/// Legacy bridge that converts the soon-to-be-deleted enums
/// (<see cref="Biome.BiomeType"/>, <see cref="RockyPlanetSubtype"/>, ...) into the stable
/// string IDs used by <c>BiomeDatabase</c> / <c>SubtypeDatabase</c> and back. Delete once all
/// callers operate on string IDs directly.
/// </summary>
public static class BiomeIdMapper
{
    private static readonly Regex _camelBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);

    public static string BiomeTypeToId(Biome.BiomeType biome) =>
        "biome_" + ToSnakeCase(biome.ToString());

    /// <summary>
    /// Inverse of <see cref="BiomeTypeToId"/>. Returns null if no enum value matches.
    /// </summary>
    public static Biome.BiomeType? IdToBiomeType(string biomeId)
    {
        if (string.IsNullOrEmpty(biomeId) || !biomeId.StartsWith("biome_"))
            return null;
        string pascal = SnakeToPascal(biomeId.Substring("biome_".Length));
        return Enum.TryParse<Biome.BiomeType>(pascal, ignoreCase: true, out var v) ? v : null;
    }

    public static string RockyPlanetSubtypeToId(RockyPlanetSubtype subtype) =>
        "subtype_rocky_" + ToSnakeCase(subtype.ToString());

    /// <summary>
    /// Inverse of <see cref="RockyPlanetSubtypeToId"/>. Returns null if no enum value matches.
    /// </summary>
    public static RockyPlanetSubtype? IdToRockyPlanetSubtype(string subtypeId)
    {
        if (string.IsNullOrEmpty(subtypeId) || !subtypeId.StartsWith("subtype_rocky_"))
            return null;
        string pascal = SnakeToPascal(subtypeId.Substring("subtype_rocky_".Length));
        return Enum.TryParse<RockyPlanetSubtype>(pascal, ignoreCase: true, out var v) ? v : null;
    }

    public static string GasGiantSubtypeToId(GasGiantSubtype subtype) =>
        "subtype_gas_giant_" + ToSnakeCase(subtype.ToString());

    public static GasGiantSubtype? IdToGasGiantSubtype(string id) =>
        TryParseSuffix<GasGiantSubtype>(id, "subtype_gas_giant_");

    public static string IceGiantSubtypeToId(IceGiantSubtype subtype) =>
        "subtype_ice_giant_" + ToSnakeCase(subtype.ToString());

    public static IceGiantSubtype? IdToIceGiantSubtype(string id) =>
        TryParseSuffix<IceGiantSubtype>(id, "subtype_ice_giant_");

    public static string DwarfPlanetSubtypeToId(DwarfPlanetSubtype subtype) =>
        "subtype_dwarf_planet_" + ToSnakeCase(subtype.ToString());

    public static DwarfPlanetSubtype? IdToDwarfPlanetSubtype(string id) =>
        TryParseSuffix<DwarfPlanetSubtype>(id, "subtype_dwarf_planet_");

    public static string StarSubtypeToId(StarSubtype subtype) =>
        "subtype_star_" + ToSnakeCase(subtype.ToString());

    public static StarSubtype? IdToStarSubtype(string id) =>
        TryParseSuffix<StarSubtype>(id, "subtype_star_");

    public static string NeutronStarSubtypeToId(NeutronStarSubtype subtype) =>
        "subtype_neutron_star_" + ToSnakeCase(subtype.ToString());

    public static NeutronStarSubtype? IdToNeutronStarSubtype(string id) =>
        TryParseSuffix<NeutronStarSubtype>(id, "subtype_neutron_star_");

    public static string BlackHoleSubtypeToId(BlackHoleSubtype subtype) =>
        "subtype_black_hole_" + ToSnakeCase(subtype.ToString());

    public static BlackHoleSubtype? IdToBlackHoleSubtype(string id) =>
        TryParseSuffix<BlackHoleSubtype>(id, "subtype_black_hole_");

    public static string SatelliteSubtypeToId(SatelliteSubtype subtype) =>
        "subtype_satellite_" + ToSnakeCase(subtype.ToString());

    public static SatelliteSubtype? IdToSatelliteSubtype(string id) =>
        TryParseSuffix<SatelliteSubtype>(id, "subtype_satellite_");

    public static string BeltSubtypeToId(BeltSubtype subtype) =>
        "subtype_belt_" + ToSnakeCase(subtype.ToString());

    public static BeltSubtype? IdToBeltSubtype(string id) =>
        TryParseSuffix<BeltSubtype>(id, "subtype_belt_");

    /// <summary>
    /// Resolves a subtype id + family pair to a typed <see cref="BodyClassification"/>. Used by
    /// the Planet Generator scene to force a manual classification at generation time. Returns
    /// null when the family is not a dominant-body family or the id segment does not map to a
    /// known enum value.
    /// </summary>
    public static BodyClassification? IdToBodyClassification(string id, BodyFamily family)
    {
        return family switch
        {
            BodyFamily.RockyPlanet when IdToRockyPlanetSubtype(id) is { } s
                => new BodyClassification.RockyPlanet(s),
            BodyFamily.GasGiant when IdToGasGiantSubtype(id) is { } s
                => new BodyClassification.GasGiant(s),
            BodyFamily.IceGiant when IdToIceGiantSubtype(id) is { } s
                => new BodyClassification.IceGiant(s),
            BodyFamily.DwarfPlanet when IdToDwarfPlanetSubtype(id) is { } s
                => new BodyClassification.DwarfPlanet(s),
            BodyFamily.Star when IdToStarSubtype(id) is { } s
                => new BodyClassification.Star(s),
            BodyFamily.NeutronStar when IdToNeutronStarSubtype(id) is { } s
                => new BodyClassification.NeutronStar(s),
            BodyFamily.BlackHole when IdToBlackHoleSubtype(id) is { } s
                => new BodyClassification.BlackHole(s),
            _ => null,
        };
    }

    /// <summary>
    /// Maps any boxed per-family subtype enum value to its stable subtype id string. Returns null
    /// for unrecognized objects. Lets callers that hold an <c>object</c> subtype (e.g.
    /// <c>SubtypeProbability.Subtype</c>) resolve an id without a per-family switch.
    /// </summary>
    public static string? SubtypeObjectToId(object? subtype) => subtype switch
    {
        RockyPlanetSubtype s => RockyPlanetSubtypeToId(s),
        GasGiantSubtype s => GasGiantSubtypeToId(s),
        IceGiantSubtype s => IceGiantSubtypeToId(s),
        DwarfPlanetSubtype s => DwarfPlanetSubtypeToId(s),
        StarSubtype s => StarSubtypeToId(s),
        NeutronStarSubtype s => NeutronStarSubtypeToId(s),
        BlackHoleSubtype s => BlackHoleSubtypeToId(s),
        SatelliteSubtype s => SatelliteSubtypeToId(s),
        BeltSubtype s => BeltSubtypeToId(s),
        _ => null,
    };

    /// <summary>
    /// Subtype id for a classification's current subtype, or null when it carries none.
    /// </summary>
    public static string? ClassificationToSubtypeId(BodyClassification classification) =>
        SubtypeObjectToId(classification?.SubtypeAsObject);

    private static T? TryParseSuffix<T>(string id, string prefix) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix)) return null;
        string pascal = SnakeToPascal(id.Substring(prefix.Length));
        return Enum.TryParse<T>(pascal, ignoreCase: true, out var v) ? v : null;
    }

    private static string ToSnakeCase(string pascalCase) =>
        _camelBoundary.Replace(pascalCase, "$1_$2").ToLowerInvariant();

    private static string SnakeToPascal(string snake)
    {
        if (string.IsNullOrEmpty(snake)) return string.Empty;
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p.Substring(1));
        }
        return sb.ToString();
    }
}
