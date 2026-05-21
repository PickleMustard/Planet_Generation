using Godot;

namespace ProceduralGeneration.ColorSystem;

public interface IColorMapper
{
    /// <summary>
    /// Returns the rendering color for the given stable biome ID (e.g. <c>biome_forest</c>)
    /// at the given world-unit height.
    /// </summary>
    Color GetBiomeColor(string biomeId, float height);
}
