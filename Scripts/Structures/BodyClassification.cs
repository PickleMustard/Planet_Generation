using System;
using Structures.Enums;

namespace Structures;

/// <summary>
/// Type-safe discriminated union for body classification.
/// Each sealed record variant carries its own correctly-typed subtype enum,
/// enabling exhaustive pattern matching and eliminating all object? casts.
/// Existing enums are kept for YAML deserialization and config lookups.
/// </summary>
public abstract record BodyClassification
{
    public abstract string TypeName { get; }
    public abstract string OrbitalType {get;}
    public abstract bool IsDominant { get; }
    public abstract bool UsesBandPlacement { get; }

    /// <summary>
    /// Backward-compat: returns the CelestialBodyType if this is a celestial body classification.
    /// </summary>
    public CelestialBodyType? AsCelestialBodyType => this switch
    {
        Star => CelestialBodyType.Star,
        BlackHole => CelestialBodyType.BlackHole,
        NeutronStar => CelestialBodyType.NeutronStar,
        RockyPlanet => CelestialBodyType.RockyPlanet,
        GasGiant => CelestialBodyType.GasGiant,
        IceGiant => CelestialBodyType.IceGiant,
        DwarfPlanet => CelestialBodyType.DwarfPlanet,
        _ => null,
    };

    /// <summary>
    /// Returns the subtype as an object for backward compatibility with systems
    /// that haven't been migrated yet.
    /// </summary>
    public object? SubtypeAsObject => this switch
    {
        Star s => s.Subtype,
        BlackHole bh => bh.Subtype,
        NeutronStar ns => ns.Subtype,
        RockyPlanet rp => rp.Subtype,
        GasGiant gg => gg.Subtype,
        IceGiant ig => ig.Subtype,
        DwarfPlanet dp => dp.Subtype,
        Satellite sat => sat.Subtype,
        Belt b => b.Subtype,
        _ => null,
    };

    /// <summary>
    /// Constructs a BodyClassification from a CelestialBodyType and a boxed subtype enum.
    /// Used for migration from the old (CelestialBodyType, object?) pair.
    /// </summary>
    public static BodyClassification FromLegacy(CelestialBodyType type, object? subtype)
    {
        return type switch
        {
            CelestialBodyType.Star => new Star(subtype as StarSubtype? ?? StarSubtype.MainSequence),
            CelestialBodyType.BlackHole => new BlackHole(subtype as BlackHoleSubtype? ?? BlackHoleSubtype.StellarMass),
            CelestialBodyType.NeutronStar => new NeutronStar(subtype as NeutronStarSubtype? ?? NeutronStarSubtype.Pulsar),
            CelestialBodyType.RockyPlanet => new RockyPlanet(subtype as RockyPlanetSubtype? ?? RockyPlanetSubtype.Temperate),
            CelestialBodyType.GasGiant => new GasGiant(subtype as GasGiantSubtype? ?? GasGiantSubtype.StandardJupiter),
            CelestialBodyType.IceGiant => new IceGiant(subtype as IceGiantSubtype? ?? IceGiantSubtype.StandardNeptune),
            CelestialBodyType.DwarfPlanet => new DwarfPlanet(subtype as DwarfPlanetSubtype? ?? DwarfPlanetSubtype.IcyKuiper),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown CelestialBodyType: {type}"),
        };
    }

    /// <summary>
    /// Constructs a Satellite classification from a SatelliteBodyType and optional subtype.
    /// </summary>
    public static BodyClassification FromSatelliteType(SatelliteBodyType satType, SatelliteSubtype? subtype = null)
    {
        return new Satellite(satType, subtype);
    }

    /// <summary>
    /// Constructs a Belt classification from a SatelliteGroupTypes and optional subtype.
    /// </summary>
    public static BodyClassification FromBeltType(SatelliteGroupTypes groupType, BeltSubtype? subtype = null)
    {
        return new Belt(groupType, subtype);
    }

    // --- Sealed record variants ---

    public sealed record Star(StarSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.Star);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => true;
        public override bool UsesBandPlacement => false;
    }

    public sealed record BlackHole(BlackHoleSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.BlackHole);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => true;
        public override bool UsesBandPlacement => false;
    }

    public sealed record NeutronStar(NeutronStarSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.NeutronStar);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => true;
        public override bool UsesBandPlacement => false;
    }

    public sealed record RockyPlanet(RockyPlanetSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.RockyPlanet);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record GasGiant(GasGiantSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.GasGiant);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record IceGiant(IceGiantSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.IceGiant);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record DwarfPlanet(DwarfPlanetSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(CelestialBodyType.DwarfPlanet);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record Satellite(SatelliteBodyType SatType, SatelliteSubtype? Subtype = null) : BodyClassification
    {
        public override string TypeName => SatType.ToString();
        public override string OrbitalType => "SatelliteBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record Belt(SatelliteGroupTypes GroupType, BeltSubtype? Subtype = null) : BodyClassification
    {
        public override string TypeName => GroupType.ToString();
        public override string OrbitalType => "SatelliteBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => false;
    }
}
