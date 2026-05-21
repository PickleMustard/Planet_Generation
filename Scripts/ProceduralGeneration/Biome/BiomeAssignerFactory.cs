using Structures;
using Structures.Enums;

namespace ProceduralGeneration.BiomeSystem;

public static class BiomeAssignerFactory
{
    public static IBiomeAssigner GetAssigner(BodyClassification classification)
    {
        return classification switch
        {
            BodyClassification.RockyPlanet rp => GetRockyPlanetAssigner(rp.Subtype),
            _ => new DefaultBiomeAssigner()
        };
    }

    private static IBiomeAssigner GetRockyPlanetAssigner(RockyPlanetSubtype subtype)
    {
        var entry = BiomeAssignerConfigDatabase.Instance.GetForSubtype(subtype);
        return new ConfigurableBiomeAssigner(entry);
    }
}
