using Structures;
using Structures.Enums;

namespace ProceduralGeneration.ColorSystem;

public static class ColorMapperFactory
{
    public static IColorMapper GetMapper(BodyClassification classification)
    {
        return classification switch
        {
            BodyClassification.RockyPlanet rp => GetRockyPlanetMapper(rp.Subtype),
            _ => new DefaultColorMapper()
        };
    }

    private static IColorMapper GetRockyPlanetMapper(RockyPlanetSubtype subtype)
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
