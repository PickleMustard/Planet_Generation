using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using Structures.GameState;

namespace ProceduralGeneration.BiomeSystem;

public class TemperatePlanetBiomeAssigner : IBiomeAssigner
{
    private const float MAX_MOISTURE = 2.7f;

    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.9f) return Structures.Enums.Biome.BiomeType.Icecap;
        if (normalizedHeight > 0.68f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.4f && (latitude > 0.8f || latitude < -0.8f)) return Structures.Enums.Biome.BiomeType.Tundra;
        if (normalizedHeight > 0.4f && (latitude > 0.7f || latitude < -0.7f)) return Structures.Enums.Biome.BiomeType.Taiga;
        if (normalizedHeight < 0.05f) return Structures.Enums.Biome.BiomeType.Ocean;
        if (normalizedHeight < 0.07f) return Structures.Enums.Biome.BiomeType.Coastal;
        if (normalizedHeight < 0.3f && moisture < 0.2f) return Structures.Enums.Biome.BiomeType.Desert;
        if (normalizedHeight < 0.3f && moisture < 0.5f) return Structures.Enums.Biome.BiomeType.Grassland;
        if (normalizedHeight > 0.3f && moisture < 0.7f) return Structures.Enums.Biome.BiomeType.Forest;
        return Structures.Enums.Biome.BiomeType.Rainforest;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 9f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;
        float randomVariation = rng.RandfRange(-0.4f, 0.2f);
        return MAX_MOISTURE - (baseMoisture + latitudeFactor + sizeFactor + randomVariation) / MAX_MOISTURE;
    }
}

public class ScouredPlanetBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.8f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.4f) return Structures.Enums.Biome.BiomeType.StoneDesert;
        if (normalizedHeight > 0.1f) return Structures.Enums.Biome.BiomeType.SandDesert;
        return Structures.Enums.Biome.BiomeType.ScouredPlain;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return rng.RandfRange(0.0f, 0.1f);
    }
}

public class DesertPlanetBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.8f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.5f) return Structures.Enums.Biome.BiomeType.StoneDesert;
        if (normalizedHeight > 0.2f) return Structures.Enums.Biome.BiomeType.SandDesert;
        if (normalizedHeight > 0.05f) return Structures.Enums.Biome.BiomeType.Desert;
        return Structures.Enums.Biome.BiomeType.StoneDesert;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return rng.RandfRange(0.0f, 0.15f);
    }
}

public class IcePlanetBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.8f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.6f) return Structures.Enums.Biome.BiomeType.Icecap;
        if (normalizedHeight > 0.4f) return Structures.Enums.Biome.BiomeType.Glacier;
        if (normalizedHeight > 0.2f && moisture < 0.3f) return Structures.Enums.Biome.BiomeType.Desert;
        if (normalizedHeight > 0.2f) return Structures.Enums.Biome.BiomeType.Tundra;
        return Structures.Enums.Biome.BiomeType.FrozenPlain;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return rng.RandfRange(0.7f, 0.9f);
    }
}

public class CoolPlanetBiomeAssigner : IBiomeAssigner
{
    private const float MAX_MOISTURE = 2.7f;

    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.85f) return Structures.Enums.Biome.BiomeType.Icecap;
        if (normalizedHeight > 0.65f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.4f && (latitude > 0.6f || latitude < -0.6f)) return Structures.Enums.Biome.BiomeType.Glacier;
        if (normalizedHeight > 0.4f && (latitude > 0.4f || latitude < -0.4f)) return Structures.Enums.Biome.BiomeType.Tundra;
        if (normalizedHeight < 0.05f) return Structures.Enums.Biome.BiomeType.Ocean;
        if (normalizedHeight < 0.07f) return Structures.Enums.Biome.BiomeType.Coastal;
        if (moisture < 0.3f) return Structures.Enums.Biome.BiomeType.FrozenPlain;
        if (moisture < 0.6f) return Structures.Enums.Biome.BiomeType.Tundra;
        return Structures.Enums.Biome.BiomeType.Taiga;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 9f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;
        float randomVariation = rng.RandfRange(-0.4f, 0.2f);
        return MAX_MOISTURE - (baseMoisture + latitudeFactor + sizeFactor + randomVariation + 0.3f) / MAX_MOISTURE;
    }
}

public class TropicalPlanetBiomeAssigner : IBiomeAssigner
{
    private const float MAX_MOISTURE = 2.7f;

    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.7f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight < 0.05f) return Structures.Enums.Biome.BiomeType.Ocean;
        if (normalizedHeight < 0.08f) return Structures.Enums.Biome.BiomeType.Coastal;
        if (moisture < 0.15f) return Structures.Enums.Biome.BiomeType.Desert;
        if (moisture > 0.6f) return Structures.Enums.Biome.BiomeType.Rainforest;
        if (moisture > 0.35f) return Structures.Enums.Biome.BiomeType.Forest;
        if (moisture > 0.15f) return Structures.Enums.Biome.BiomeType.Swamp;
        return Structures.Enums.Biome.BiomeType.Grassland;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 9f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;
        float randomVariation = rng.RandfRange(-0.2f, 0.3f);
        return MAX_MOISTURE - (baseMoisture + latitudeFactor + sizeFactor + randomVariation) / MAX_MOISTURE;
    }
}

public class OceanPlanetBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.85f) return Structures.Enums.Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.75f) return Structures.Enums.Biome.BiomeType.Grassland;
        if (normalizedHeight > 0.65f) return Structures.Enums.Biome.BiomeType.Coastal;
        if (normalizedHeight > 0.35f) return Structures.Enums.Biome.BiomeType.ShallowOcean;
        return Structures.Enums.Biome.BiomeType.DeepOcean;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return rng.RandfRange(0.8f, 1.0f);
    }
}

public class RustedPlanetBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.8f) return Structures.Enums.Biome.BiomeType.RustedMountain;
        if (normalizedHeight > 0.55f) return Structures.Enums.Biome.BiomeType.RustedDesert;
        if (normalizedHeight > 0.3f) return Structures.Enums.Biome.BiomeType.RustedPlain;
        if (normalizedHeight > 0.1f) return Structures.Enums.Biome.BiomeType.SandDesert;
        return Structures.Enums.Biome.BiomeType.RustedPlain;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return rng.RandfRange(0.0f, 0.2f);
    }
}

public class VolcanicPlanetBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.8f) return Structures.Enums.Biome.BiomeType.VolcanicPeak;
        if (normalizedHeight > 0.55f) return Structures.Enums.Biome.BiomeType.ObsidianField;
        if (normalizedHeight > 0.3f) return Structures.Enums.Biome.BiomeType.AshPlain;
        if (normalizedHeight > 0.1f) return Structures.Enums.Biome.BiomeType.VolcanicPlain;
        return Structures.Enums.Biome.BiomeType.LavaOcean;
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return rng.RandfRange(0.0f, 0.1f);
    }
}

public class DefaultBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        return BiomeAssigner.AssignBiome(generator, height, moisture, latitude);
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return BiomeAssigner.CalculateMoisture(continent, rng, baseMoisture);
    }
}
