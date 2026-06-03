using System;
using Structures;
using Structures.Enums;

namespace ProceduralGeneration;

public static class SubtypeParser
{
    /// <summary>
    /// Parses a subtype string for a given OrbitalBodyType and returns a BodyClassification.
    /// </summary>
    public static BodyClassification ParseClassification(OrbitalBodyType bodyType, string? subtypeString)
    {
        if (string.IsNullOrWhiteSpace(subtypeString))
        {
            return BodyClassification.FromType(bodyType, null);
        }

        try
        {
            object? subtype = bodyType switch
            {
                OrbitalBodyType.Star => Enum.Parse(typeof(StarSubtype), subtypeString),
                OrbitalBodyType.RockyPlanet => Enum.Parse(typeof(RockyPlanetSubtype), subtypeString),
                OrbitalBodyType.GasGiant => Enum.Parse(typeof(GasGiantSubtype), subtypeString),
                OrbitalBodyType.IceGiant => Enum.Parse(typeof(IceGiantSubtype), subtypeString),
                OrbitalBodyType.DwarfPlanet => Enum.Parse(typeof(DwarfPlanetSubtype), subtypeString),
                OrbitalBodyType.BlackHole => Enum.Parse(typeof(BlackHoleSubtype), subtypeString),
                OrbitalBodyType.NeutronStar => Enum.Parse(typeof(NeutronStarSubtype), subtypeString),
                _ => null
            };
            return BodyClassification.FromType(bodyType, subtype);
        }
        catch (ArgumentException)
        {
            return BodyClassification.FromType(bodyType, null);
        }
    }

    /// <summary>
    /// Parses a satellite subtype string and returns a Satellite BodyClassification.
    /// </summary>
    public static BodyClassification ParseSatelliteClassification(OrbitalBodyType satType, string? subtypeString)
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
