#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Enumerates the valid subtype-id strings for a body family — the one utility the subtype editor
/// needs that didn't already exist. Each list is <c>Enum.GetValues</c> routed through the matching
/// <c>&lt;Family&gt;SubtypeToId</c> mapper so the ids always agree with the parser/round-trip path.
/// </summary>
public static class SubtypeFamilies
{
    /// <summary>Subtype ids for a body's own <c>subtype</c>/<c>subtype_weights</c> slot.</summary>
    public static IReadOnlyList<string> IdsForBody(BodyNode node)
    {
        if (node.Category == BodyCategory.Belt)
            return BeltIds;

        return node.Type switch
        {
            "Star" => StarIds,
            "RockyPlanet" => RockyIds,
            "GasGiant" => GasIds,
            "IceGiant" => IceIds,
            "DwarfPlanet" => DwarfIds,
            "BlackHole" => BlackHoleIds,
            "NeutronStar" => NeutronIds,
            _ => SatelliteIds, // Moon / Asteroid / Comet
        };
    }

    /// <summary>Per-member asteroid subtype ids for a belt's <c>member_subtype_weights</c>.</summary>
    public static IReadOnlyList<string> MemberIds => SatelliteIds;

    private static readonly List<string> StarIds =
        Map<StarSubtype>(BiomeIdMapper.StarSubtypeToId);
    private static readonly List<string> RockyIds =
        Map<RockyPlanetSubtype>(BiomeIdMapper.RockyPlanetSubtypeToId);
    private static readonly List<string> GasIds =
        Map<GasGiantSubtype>(BiomeIdMapper.GasGiantSubtypeToId);
    private static readonly List<string> IceIds =
        Map<IceGiantSubtype>(BiomeIdMapper.IceGiantSubtypeToId);
    private static readonly List<string> DwarfIds =
        Map<DwarfPlanetSubtype>(BiomeIdMapper.DwarfPlanetSubtypeToId);
    private static readonly List<string> BlackHoleIds =
        Map<BlackHoleSubtype>(BiomeIdMapper.BlackHoleSubtypeToId);
    private static readonly List<string> NeutronIds =
        Map<NeutronStarSubtype>(BiomeIdMapper.NeutronStarSubtypeToId);
    private static readonly List<string> SatelliteIds =
        Map<SatelliteSubtype>(BiomeIdMapper.SatelliteSubtypeToId);
    private static readonly List<string> BeltIds =
        Map<BeltSubtype>(BiomeIdMapper.BeltSubtypeToId);

    private static List<string> Map<TEnum>(Func<TEnum, string> toId) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(toId).OrderBy(s => s).ToList();
}
#endif
