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
    public abstract string OrbitalType { get; }
    public abstract bool IsDominant { get; }
    public abstract bool UsesBandPlacement { get; }

    /// <summary>
    /// Returns the flat <see cref="OrbitalBodyType"/> for this classification.
    /// </summary>
    public OrbitalBodyType Type => this switch
    {
        Star => OrbitalBodyType.Star,
        BlackHole => OrbitalBodyType.BlackHole,
        NeutronStar => OrbitalBodyType.NeutronStar,
        RockyPlanet => OrbitalBodyType.RockyPlanet,
        GasGiant => OrbitalBodyType.GasGiant,
        IceGiant => OrbitalBodyType.IceGiant,
        DwarfPlanet => OrbitalBodyType.DwarfPlanet,
        Satellite sat => sat.SatType,
        Belt b => b.GroupType switch
        {
            SatelliteGroupTypes.AsteroidBelt => OrbitalBodyType.Asteroid,
            SatelliteGroupTypes.IceBelt => OrbitalBodyType.Comet,
            SatelliteGroupTypes.Comet => OrbitalBodyType.Comet,
            _ => OrbitalBodyType.Asteroid,
        },
        _ => throw new InvalidOperationException($"Unknown classification: {GetType().Name}"),
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
    /// Constructs a BodyClassification from an OrbitalBodyType and a boxed subtype enum.
    /// Moon/Asteroid/Comet collapse onto the Satellite variant.
    /// </summary>
    public static BodyClassification FromType(OrbitalBodyType type, object? subtype)
    {
        return type switch
        {
            OrbitalBodyType.Star => new Star(subtype as StarSubtype? ?? StarSubtype.MainSequence),
            OrbitalBodyType.BlackHole => new BlackHole(subtype as BlackHoleSubtype? ?? BlackHoleSubtype.StellarMass),
            OrbitalBodyType.NeutronStar => new NeutronStar(subtype as NeutronStarSubtype? ?? NeutronStarSubtype.Pulsar),
            OrbitalBodyType.RockyPlanet => new RockyPlanet(subtype as RockyPlanetSubtype? ?? RockyPlanetSubtype.Temperate),
            OrbitalBodyType.GasGiant => new GasGiant(subtype as GasGiantSubtype? ?? GasGiantSubtype.StandardJupiter),
            OrbitalBodyType.IceGiant => new IceGiant(subtype as IceGiantSubtype? ?? IceGiantSubtype.StandardNeptune),
            OrbitalBodyType.DwarfPlanet => new DwarfPlanet(subtype as DwarfPlanetSubtype? ?? DwarfPlanetSubtype.IcyKuiper),
            OrbitalBodyType.Moon or OrbitalBodyType.Asteroid or OrbitalBodyType.Comet =>
                new Satellite(type, subtype as SatelliteSubtype? ?? DefaultSatelliteSubtype(type)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown OrbitalBodyType: {type}"),
        };
    }

    /// <summary>
    /// Constructs a Satellite classification from an OrbitalBodyType (Moon/Asteroid/Comet) and
    /// optional subtype. When no subtype is supplied, falls back to a sane per-type default so the
    /// classification never carries a null subtype (resource/mesh-param lookups key off it).
    /// </summary>
    public static BodyClassification FromSatelliteType(OrbitalBodyType satType, SatelliteSubtype? subtype = null)
    {
        return new Satellite(satType, subtype ?? DefaultSatelliteSubtype(satType));
    }

    /// <summary>
    /// Maps a satellite body type to a reasonable default subtype, used as a safety net
    /// for construction paths that don't perform AU-weighted subtype selection.
    /// </summary>
    public static SatelliteSubtype DefaultSatelliteSubtype(OrbitalBodyType satType) => satType switch
    {
        OrbitalBodyType.Moon => SatelliteSubtype.RockyMoon,
        OrbitalBodyType.Asteroid => SatelliteSubtype.Carbonaceous,
        OrbitalBodyType.Comet => SatelliteSubtype.ShortPeriod,
        _ => SatelliteSubtype.RockyMoon,
    };

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
        public override string TypeName => nameof(OrbitalBodyType.Star);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => true;
        public override bool UsesBandPlacement => false;
    }

    public sealed record BlackHole(BlackHoleSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(OrbitalBodyType.BlackHole);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => true;
        public override bool UsesBandPlacement => false;
    }

    public sealed record NeutronStar(NeutronStarSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(OrbitalBodyType.NeutronStar);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => true;
        public override bool UsesBandPlacement => false;
    }

    public sealed record RockyPlanet(RockyPlanetSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(OrbitalBodyType.RockyPlanet);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record GasGiant(GasGiantSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(OrbitalBodyType.GasGiant);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record IceGiant(IceGiantSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(OrbitalBodyType.IceGiant);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record DwarfPlanet(DwarfPlanetSubtype Subtype) : BodyClassification
    {
        public override string TypeName => nameof(OrbitalBodyType.DwarfPlanet);
        public override string OrbitalType => "CelestialBody";
        public override bool IsDominant => false;
        public override bool UsesBandPlacement => true;
    }

    public sealed record Satellite(OrbitalBodyType SatType, SatelliteSubtype? Subtype = null) : BodyClassification
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
