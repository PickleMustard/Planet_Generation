#if DEBUG
using Godot;
using Structures.Enums;

namespace DeveloperTools.BiomeEditor;

/// <summary>
/// Pure read-only hazard heuristic exposed alongside biome editing UI.
/// Combines subtype severity, deviation of mid-atmosphere from 1 atm, and a per-biome
/// constant into a [0, 10] hazard score. Editor-only — runtime ignores this score.
/// </summary>
public static class HazardCalculator
{
    public static float Compute(RockyPlanetSubtype subtype, float atmosphereMin, float atmosphereMax, Biome.BiomeType biome)
    {
        float midAtm = (atmosphereMin + atmosphereMax) * 0.5f;
        float atmosphereHazard = Mathf.Abs(midAtm - 1.0f) * 1.5f;
        float subtypeBase = SubtypeBaseHazard(subtype);
        float biomeHazard = BiomeHazardWeight(biome);
        return Mathf.Clamp(subtypeBase + atmosphereHazard + biomeHazard, 0f, 10f);
    }

    public static float SubtypeBaseHazard(RockyPlanetSubtype subtype) => subtype switch
    {
        RockyPlanetSubtype.Temperate => 1f,
        RockyPlanetSubtype.Tropical => 2f,
        RockyPlanetSubtype.Ocean => 2f,
        RockyPlanetSubtype.Cool => 3f,
        RockyPlanetSubtype.Desert => 4f,
        RockyPlanetSubtype.Rusted => 5f,
        RockyPlanetSubtype.Ice => 6f,
        RockyPlanetSubtype.Scoured => 7f,
        RockyPlanetSubtype.Volcanic => 9f,
        _ => 0f,
    };

    public static float BiomeHazardWeight(Biome.BiomeType biome) => biome switch
    {
        Biome.BiomeType.Rainforest => 0f,
        Biome.BiomeType.Grassland => 0f,
        Biome.BiomeType.Forest => 0.5f,
        Biome.BiomeType.Coastal => 0.5f,
        Biome.BiomeType.Ocean => 1f,
        Biome.BiomeType.ShallowOcean => 1.5f,
        Biome.BiomeType.Tundra => 2f,
        Biome.BiomeType.Taiga => 2f,
        Biome.BiomeType.Desert => 2.5f,
        Biome.BiomeType.SandDesert => 3f,
        Biome.BiomeType.Icecap => 3f,
        Biome.BiomeType.Glacier => 3f,
        Biome.BiomeType.FrozenPlain => 3.5f,
        Biome.BiomeType.StoneDesert => 3.5f,
        Biome.BiomeType.Mountain => 4f,
        Biome.BiomeType.RustedPlain => 4f,
        Biome.BiomeType.RustedDesert => 4.5f,
        Biome.BiomeType.ScouredPlain => 5f,
        Biome.BiomeType.RustedMountain => 5f,
        Biome.BiomeType.AshPlain => 6f,
        Biome.BiomeType.ObsidianField => 7f,
        Biome.BiomeType.VolcanicPlain => 7f,
        Biome.BiomeType.LavaOcean => 9f,
        Biome.BiomeType.VolcanicPeak => 10f,
        _ => 0f,
    };
}
#endif
