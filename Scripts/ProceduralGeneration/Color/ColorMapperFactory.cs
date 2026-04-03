using Structures.Enums;

namespace ProceduralGeneration.ColorSystem;

public static class ColorMapperFactory
{
    public static IColorMapper GetMapper(CelestialBodyType type, object? subtype = null)
    {
        return type switch
        {
            CelestialBodyType.RockyPlanet => GetRockyPlanetMapper(subtype as RockyPlanetSubtype?),
            _ => new DefaultColorMapper()
        };
    }

    private static IColorMapper GetRockyPlanetMapper(RockyPlanetSubtype? subtype)
    {
        return subtype switch
        {
            RockyPlanetSubtype.Scoured => new ScouredPlanetColorMapper(),
            RockyPlanetSubtype.Desert => new DesertPlanetColorMapper(),
            RockyPlanetSubtype.Temperate => new TemperatePlanetColorMapper(),
            RockyPlanetSubtype.Ice => new IcePlanetColorMapper(),
            RockyPlanetSubtype.Cool => new CoolPlanetColorMapper(),
            RockyPlanetSubtype.Tropical => new TropicalPlanetColorMapper(),
            RockyPlanetSubtype.Ocean => new OceanPlanetColorMapper(),
            RockyPlanetSubtype.Rusted => new RustedPlanetColorMapper(),
            RockyPlanetSubtype.Volcanic => new VolcanicPlanetColorMapper(),
            _ => new TemperatePlanetColorMapper()
        };
    }
}
