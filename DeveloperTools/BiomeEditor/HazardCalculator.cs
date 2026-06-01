#if DEBUG
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using ProceduralGeneration.SubtypeSystem;
using Structures.Enums;

namespace DeveloperTools.BiomeEditor;

/// <summary>
/// Pure read-only hazard heuristic exposed alongside biome editing UI.
/// Combines subtype severity, deviation of mid-atmosphere from 1 atm, and a per-biome
/// constant into a [0, 10] hazard score. Editor-only — runtime ignores this score.
/// Values come from <see cref="SubtypeDatabase"/> / <see cref="BiomeDatabase"/>.
/// </summary>
public static class HazardCalculator
{
    public static float Compute(RockyPlanetSubtype subtype, float atmosphereMin, float atmosphereMax, string biomeId)
    {
        float midAtm = (atmosphereMin + atmosphereMax) * 0.5f;
        float atmosphereHazard = Mathf.Abs(midAtm - 1.0f) * 1.5f;
        float subtypeBase = SubtypeBaseHazard(subtype);
        float biomeHazard = BiomeHazardWeight(biomeId);
        return Mathf.Clamp(subtypeBase + atmosphereHazard + biomeHazard, 0f, 10f);
    }

    public static float SubtypeBaseHazard(RockyPlanetSubtype subtype)
    {
        var def = SubtypeDatabase.Instance.GetById(BiomeIdMapper.RockyPlanetSubtypeToId(subtype));
        return def?.BaseHazard ?? 0f;
    }

    public static float BiomeHazardWeight(string biomeId)
    {
        var def = BiomeDatabase.Instance.GetById(biomeId);
        return def?.HazardWeight ?? 0f;
    }
}
#endif
