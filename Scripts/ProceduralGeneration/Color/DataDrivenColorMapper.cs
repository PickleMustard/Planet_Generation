using Godot;
using ProceduralGeneration.BiomeSystem;
using Structures.Enums;
using UtilityLibrary;

namespace ProceduralGeneration.ColorSystem;

/// <summary>
/// Single <see cref="IColorMapper"/> that resolves cell color from the data-driven
/// <see cref="BiomeDatabase"/>. Replaces all hand-coded per-subtype mappers
/// (<c>TemperatePlanetColorMapper</c>, <c>DesertPlanetColorMapper</c>, ...) — color
/// tables now live in <c>Configuration/Biomes/biomes.yaml</c> per biome, keyed by
/// subtype ID.
/// </summary>
public sealed class DataDrivenColorMapper : IColorMapper
{
    private readonly string _subtypeId;

    /// <summary>
    /// <paramref name="subtypeId"/> is the stable subtype ID (e.g. <c>subtype_rocky_temperate</c>).
    /// Pass an empty string to bypass overrides and always return the biome's default color
    /// — that's the new fallback equivalent of the old <c>DefaultColorMapper</c>.
    /// </summary>
    public DataDrivenColorMapper(string subtypeId = "")
    {
        _subtypeId = subtypeId ?? string.Empty;
    }

    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        string biomeId = BiomeIdMapper.BiomeTypeToId(biome);
        var def = BiomeDatabase.Instance.GetById(biomeId);
        if (def == null)
        {
            GameLogger.Warning($"DataDrivenColorMapper: no biome '{biomeId}' in BiomeDatabase");
            return Colors.Gray;
        }
        return def.GetColorForSubtype(_subtypeId);
    }
}
