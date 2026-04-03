using Structures.Enums;

namespace ProceduralGeneration.BiomeSystem;

public static class BiomeAssignerFactory
{
    public static IBiomeAssigner GetAssigner(CelestialBodyType type, object? subtype = null)
    {
        return type switch
        {
            CelestialBodyType.RockyPlanet => GetRockyPlanetAssigner(subtype as RockyPlanetSubtype?),
            _ => new DefaultBiomeAssigner()
        };
    }

    private static IBiomeAssigner GetRockyPlanetAssigner(RockyPlanetSubtype? subtype)
    {
        return subtype switch
        {
            RockyPlanetSubtype.Scoured => new ScouredPlanetBiomeAssigner(),
            RockyPlanetSubtype.Desert => new DesertPlanetBiomeAssigner(),
            RockyPlanetSubtype.Temperate => new TemperatePlanetBiomeAssigner(),
            RockyPlanetSubtype.Ice => new IcePlanetBiomeAssigner(),
            RockyPlanetSubtype.Cool => new CoolPlanetBiomeAssigner(),
            RockyPlanetSubtype.Tropical => new TropicalPlanetBiomeAssigner(),
            RockyPlanetSubtype.Ocean => new OceanPlanetBiomeAssigner(),
            RockyPlanetSubtype.Rusted => new RustedPlanetBiomeAssigner(),
            RockyPlanetSubtype.Volcanic => new VolcanicPlanetBiomeAssigner(),
            _ => new TemperatePlanetBiomeAssigner()
        };
    }
}
