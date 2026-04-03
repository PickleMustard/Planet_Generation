using Godot;
using Structures.Enums;

namespace ProceduralGeneration.ColorSystem;

public class TemperatePlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Tundra => new Color(0.85f, 0.85f, 0.8f),
            Biome.BiomeType.Icecap => Colors.White,
            Biome.BiomeType.Desert => new Color(0.9f, 0.8f, 0.5f),
            Biome.BiomeType.Grassland => new Color(0.5f, 0.8f, 0.3f),
            Biome.BiomeType.Forest => new Color(0.2f, 0.6f, 0.2f),
            Biome.BiomeType.Rainforest => new Color(0.1f, 0.4f, 0.1f),
            Biome.BiomeType.Taiga => new Color(0.4f, 0.5f, 0.3f),
            Biome.BiomeType.Ocean => new Color(0.1f, 0.3f, 0.7f),
            Biome.BiomeType.Coastal => new Color(0.8f, 0.7f, 0.4f),
            Biome.BiomeType.Mountain => new Color(0.6f, 0.5f, 0.4f),
            _ => Colors.Gray,
        };
    }
}

public class ScouredPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Mountain => new Color(0.20f, 0.20f, 0.22f),
            Biome.BiomeType.StoneDesert => new Color(0.45f, 0.44f, 0.43f),
            Biome.BiomeType.SandDesert => new Color(0.58f, 0.56f, 0.54f),
            Biome.BiomeType.ScouredPlain => new Color(0.65f, 0.64f, 0.62f),
            _ => new Color(0.50f, 0.49f, 0.48f),
        };
    }
}

public class DesertPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Mountain => new Color(0.55f, 0.50f, 0.42f),
            Biome.BiomeType.StoneDesert => new Color(0.68f, 0.62f, 0.50f),
            Biome.BiomeType.SandDesert => new Color(0.92f, 0.82f, 0.55f),
            Biome.BiomeType.Desert => new Color(0.88f, 0.78f, 0.42f),
            _ => new Color(0.85f, 0.75f, 0.48f),
        };
    }
}

public class IcePlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Mountain => new Color(0.62f, 0.65f, 0.72f),
            Biome.BiomeType.Icecap => new Color(0.92f, 0.95f, 1.0f),
            Biome.BiomeType.Glacier => new Color(0.68f, 0.78f, 0.90f),
            Biome.BiomeType.Tundra => new Color(0.58f, 0.60f, 0.52f),
            Biome.BiomeType.Desert => new Color(0.70f, 0.68f, 0.65f),
            Biome.BiomeType.FrozenPlain => new Color(0.82f, 0.88f, 0.95f),
            _ => new Color(0.75f, 0.80f, 0.88f),
        };
    }
}

public class CoolPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Icecap => new Color(0.90f, 0.92f, 0.95f),
            Biome.BiomeType.Mountain => new Color(0.55f, 0.55f, 0.60f),
            Biome.BiomeType.Glacier => new Color(0.75f, 0.82f, 0.90f),
            Biome.BiomeType.Tundra => new Color(0.65f, 0.62f, 0.55f),
            Biome.BiomeType.Taiga => new Color(0.32f, 0.40f, 0.30f),
            Biome.BiomeType.FrozenPlain => new Color(0.72f, 0.74f, 0.72f),
            Biome.BiomeType.Ocean => new Color(0.10f, 0.22f, 0.42f),
            Biome.BiomeType.Coastal => new Color(0.55f, 0.55f, 0.48f),
            _ => new Color(0.55f, 0.56f, 0.55f),
        };
    }
}

public class TropicalPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Mountain => new Color(0.45f, 0.38f, 0.28f),
            Biome.BiomeType.Rainforest => new Color(0.0f, 0.42f, 0.05f),
            Biome.BiomeType.Forest => new Color(0.08f, 0.52f, 0.10f),
            Biome.BiomeType.Swamp => new Color(0.25f, 0.40f, 0.12f),
            Biome.BiomeType.Grassland => new Color(0.35f, 0.75f, 0.15f),
            Biome.BiomeType.Desert => new Color(0.90f, 0.78f, 0.40f),
            Biome.BiomeType.Ocean => new Color(0.0f, 0.22f, 0.65f),
            Biome.BiomeType.Coastal => new Color(0.75f, 0.70f, 0.40f),
            _ => new Color(0.15f, 0.50f, 0.10f),
        };
    }
}

public class OceanPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Mountain => new Color(0.48f, 0.52f, 0.38f),
            Biome.BiomeType.Grassland => new Color(0.45f, 0.65f, 0.25f),
            Biome.BiomeType.Coastal => new Color(0.60f, 0.72f, 0.45f),
            Biome.BiomeType.ShallowOcean => new Color(0.12f, 0.38f, 0.62f),
            Biome.BiomeType.DeepOcean => new Color(0.02f, 0.08f, 0.35f),
            Biome.BiomeType.Ocean => new Color(0.05f, 0.20f, 0.50f),
            _ => new Color(0.05f, 0.18f, 0.48f),
        };
    }
}

public class RustedPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        float t = Mathf.Clamp(height * 0.5f, 0f, 1f);

        return biome switch
        {
            Biome.BiomeType.RustedMountain => new Color(
                Mathf.Lerp(0.45f, 0.55f, t),
                Mathf.Lerp(0.18f, 0.22f, t),
                Mathf.Lerp(0.08f, 0.12f, t)),
            Biome.BiomeType.RustedDesert => new Color(
                Mathf.Lerp(0.65f, 0.75f, t),
                Mathf.Lerp(0.25f, 0.32f, t),
                Mathf.Lerp(0.10f, 0.15f, t)),
            Biome.BiomeType.RustedPlain => new Color(
                Mathf.Lerp(0.78f, 0.88f, t),
                Mathf.Lerp(0.38f, 0.45f, t),
                Mathf.Lerp(0.15f, 0.20f, t)),
            Biome.BiomeType.SandDesert => new Color(0.82f, 0.55f, 0.28f),
            _ => new Color(0.72f, 0.35f, 0.18f),
        };
    }
}

public class VolcanicPlanetColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.VolcanicPeak => new Color(0.12f, 0.08f, 0.06f),
            Biome.BiomeType.ObsidianField => new Color(0.08f, 0.08f, 0.10f),
            Biome.BiomeType.AshPlain => new Color(0.25f, 0.22f, 0.20f),
            Biome.BiomeType.VolcanicPlain => new Color(0.18f, 0.12f, 0.08f),
            Biome.BiomeType.LavaOcean => new Color(0.85f, 0.18f, 0.02f),
            _ => new Color(0.20f, 0.12f, 0.08f),
        };
    }
}

public class DefaultColorMapper : IColorMapper
{
    public Color GetBiomeColor(Biome.BiomeType biome, float height)
    {
        return biome switch
        {
            Biome.BiomeType.Tundra => new Color(0.85f, 0.85f, 0.8f),
            Biome.BiomeType.Icecap => Colors.White,
            Biome.BiomeType.Desert => new Color(0.9f, 0.8f, 0.5f),
            Biome.BiomeType.Grassland => new Color(0.5f, 0.8f, 0.3f),
            Biome.BiomeType.Forest => new Color(0.2f, 0.6f, 0.2f),
            Biome.BiomeType.Rainforest => new Color(0.1f, 0.4f, 0.1f),
            Biome.BiomeType.Taiga => new Color(0.4f, 0.5f, 0.3f),
            Biome.BiomeType.Ocean => new Color(0.1f, 0.3f, 0.7f),
            Biome.BiomeType.Coastal => new Color(0.8f, 0.7f, 0.4f),
            Biome.BiomeType.Mountain => new Color(0.6f, 0.5f, 0.4f),
            _ => Colors.Gray,
        };
    }
}
