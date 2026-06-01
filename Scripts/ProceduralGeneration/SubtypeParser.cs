using System;
using Structures;
using Structures.Enums;

namespace ProceduralGeneration;

public static class SubtypeParser
{
    /// <summary>
    /// Parses a subtype string for a given CelestialBodyType and returns a BodyClassification.
    /// </summary>
    public static BodyClassification ParseClassification(CelestialBodyType bodyType, string? subtypeString)
    {
        if (string.IsNullOrWhiteSpace(subtypeString))
        {
            return BodyClassification.FromLegacy(bodyType, null);
        }

        try
        {
            object? subtype = bodyType switch
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
            return BodyClassification.FromLegacy(bodyType, subtype);
        }
        catch (ArgumentException)
        {
            return BodyClassification.FromLegacy(bodyType, null);
        }
    }

    /// <summary>
    /// Parses a satellite subtype string and returns a Satellite BodyClassification.
    /// </summary>
    public static BodyClassification ParseSatelliteClassification(SatelliteBodyType satType, string? subtypeString)
    {
        SatelliteSubtype? subtype = null;
        if (!string.IsNullOrWhiteSpace(subtypeString))
        {
            try
            {
                subtype = (SatelliteSubtype)Enum.Parse(typeof(SatelliteSubtype), subtypeString);
            }
            catch (ArgumentException)
            {
                // Fall through with null subtype
            }
        }
        return BodyClassification.FromSatelliteType(satType, subtype);
    }

    /// <summary>
    /// Parses a belt subtype string and returns a Belt BodyClassification.
    /// </summary>
    public static BodyClassification ParseBeltClassification(SatelliteGroupTypes groupType, string? subtypeString)
    {
        BeltSubtype? subtype = null;
        if (!string.IsNullOrWhiteSpace(subtypeString))
        {
            try
            {
                subtype = (BeltSubtype)Enum.Parse(typeof(BeltSubtype), subtypeString);
            }
            catch (ArgumentException)
            {
                // Fall through with null subtype
            }
        }
        return BodyClassification.FromBeltType(groupType, subtype);
    }
}
