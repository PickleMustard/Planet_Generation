using System;
using Structures.Enums;

namespace ProceduralGeneration;

public static class SubtypeParser
{
    public static object? Parse(CelestialBodyType bodyType, string subtypeString)
    {
        if (string.IsNullOrWhiteSpace(subtypeString))
        {
            return null;
        }

        try
        {
            return bodyType switch
            {
                CelestialBodyType.Star => Enum.Parse(typeof(StarSubtype), subtypeString),
                CelestialBodyType.RockyPlanet => Enum.Parse(typeof(RockyPlanetSubtype), subtypeString),
                CelestialBodyType.GasGiant => Enum.Parse(typeof(GasGiantSubtype), subtypeString),
                CelestialBodyType.IceGiant => Enum.Parse(typeof(IceGiantSubtype), subtypeString),
                CelestialBodyType.DwarfPlanet => Enum.Parse(typeof(DwarfPlanetSubtype), subtypeString),
                CelestialBodyType.BlackHole => Enum.Parse(typeof(BlackHoleSubtype), subtypeString),
                CelestialBodyType.NeutronStar => Enum.Parse(typeof(NeutronStarSubtype), subtypeString),
                _ => null
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static object? ParseSatelliteSubtype(string subtypeString)
    {
        if (string.IsNullOrWhiteSpace(subtypeString))
        {
            return null;
        }

        try
        {
            return Enum.Parse(typeof(SatelliteSubtype), subtypeString);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static object? ParseBeltSubtype(string subtypeString)
    {
        if (string.IsNullOrWhiteSpace(subtypeString))
        {
            return null;
        }

        try
        {
            return Enum.Parse(typeof(BeltSubtype), subtypeString);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
