using System;
using System.Text.RegularExpressions;
using Structures.Enums;

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

    public static string IceGiantSubtypeToId(IceGiantSubtype subtype) =>
        "subtype_ice_giant_" + ToSnakeCase(subtype.ToString());

    public static string DwarfPlanetSubtypeToId(DwarfPlanetSubtype subtype) =>
        "subtype_dwarf_planet_" + ToSnakeCase(subtype.ToString());

    public static string StarSubtypeToId(StarSubtype subtype) =>
        "subtype_star_" + ToSnakeCase(subtype.ToString());

    public static string NeutronStarSubtypeToId(NeutronStarSubtype subtype) =>
        "subtype_neutron_star_" + ToSnakeCase(subtype.ToString());

    public static string BlackHoleSubtypeToId(BlackHoleSubtype subtype) =>
        "subtype_black_hole_" + ToSnakeCase(subtype.ToString());

    public static string SatelliteSubtypeToId(SatelliteSubtype subtype) =>
        "subtype_satellite_" + ToSnakeCase(subtype.ToString());

    public static string BeltSubtypeToId(BeltSubtype subtype) =>
        "subtype_belt_" + ToSnakeCase(subtype.ToString());

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
