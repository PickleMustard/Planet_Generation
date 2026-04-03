using Godot;
using Structures.Enums;

namespace ProceduralGeneration.ColorSystem;

public interface IColorMapper
{
    Color GetBiomeColor(Biome.BiomeType biome, float height);
}
