using System;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using UtilityLibrary.GameMath.Orbital;
using UtilityLibrary.NameGeneration;

namespace ProceduralGeneration.PlanetGeneration;

public class SatelliteBeltBody
{
    public float RingApogee { get; private set; }
    public float RingPerigee { get; private set; }
    public Vector3 RingVelocity { get; private set; }
    public float SizeMin { get; private set; }
    public float SizeMax { get; private set; }
    public float MassMin { get; private set; }
    public float MassMax { get; private set; }
    public float BeltNumber { get; private set; }
    public SatelliteGroupTypes GroupType { get; private set; }
    public Godot.Collections.Array<string>? SatelliteNames { get; private set; }

    public class Builder
    {
        internal SatelliteGroupTypes _beltType;
        internal float _ringApogee;
        internal float _ringPerigee;
        internal Vector3 _ringVelocity;
        internal float _sizeMin;
        internal float _sizeMax;
        internal float _massMin;
        internal float _massMax;
        internal int _upperRange;
        internal int _lowerRange;
        internal int _beltNumber;
        internal Godot.Collections.Array<string>? _satelliteNames;

        public Builder WithRingApogee(float ringApogee)
        {
            _ringApogee = ringApogee;
            return this;
        }

        public Builder WithRingPerigee(float ringPerigee)
        {
            _ringPerigee = ringPerigee;
            return this;
        }

        public Builder WithRingVelocity(Vector3 ringVelocity)
        {
            _ringVelocity = ringVelocity;
            return this;
        }

        public Builder WithSizeMin(float sizeMin)
        {
            _sizeMin = sizeMin;
            return this;
        }

        public Builder WithSizeMax(float sizeMax)
        {
            _sizeMax = sizeMax;
            return this;
        }

        public Builder WithMassMin(float massMin)
        {
            _massMin = massMin;
            return this;
        }

        public Builder WithMassMax(float massMax)
        {
            _massMax = massMax;
            return this;
        }

        public Builder WithUpperRange(int upperRange)
        {
            _upperRange = upperRange;
            return this;
        }

        public Builder WithLowerRange(int lowerRange)
        {
            _lowerRange = lowerRange;
            return this;
        }

        public Builder WithRingType(SatelliteGroupTypes groupType)
        {
            _beltType = groupType;
            return this;
        }

        public SatelliteBeltBody Build()
        {
            return new SatelliteBeltBody(this);
        }

        public Builder FromBodyDict(
            OrbitalBodyType parentType,
            Godot.Collections.Dictionary bodyDict
        )
        {
            GD.Print($"Building belt from body: {bodyDict}");
            var type = (String)bodyDict["type"];
            _beltType = (SatelliteGroupTypes)Enum.Parse(typeof(SatelliteGroupTypes), type);
            _ringApogee = (float)bodyDict["ring_apogee"];
            _ringPerigee = (float)bodyDict["ring_perigee"];
            _ringVelocity = (Vector3)bodyDict["ring_velocity"];
            _sizeMin = (float)bodyDict["size_min"];
            _sizeMax = (float)bodyDict["size_max"];
            _massMin = (float)bodyDict["mass_min"];
            _massMax = (float)bodyDict["mass_max"];
            _upperRange = (int)bodyDict["upper_range"];
            _lowerRange = (int)bodyDict["lower_range"];

            // Use pre-resolved belt_number from template loader if available
            if (bodyDict.ContainsKey("belt_number"))
            {
                _beltNumber = (int)bodyDict["belt_number"];
            }
            else
            {
                _beltNumber = GD.RandRange(_lowerRange, _upperRange);
            }

            // Use pre-generated satellite names if available
            if (bodyDict.ContainsKey("satellite_names"))
            {
                _satelliteNames = (Godot.Collections.Array<string>)bodyDict["satellite_names"];
            }

            GD.Print(
                $"Built belt consisting of {_ringApogee}, {_ringPerigee}, {_ringVelocity}, {_sizeMin}, {_sizeMax}, {_massMin}, {_massMax}, {_upperRange}, {_lowerRange}, {_beltNumber}"
            );
            return this;
        }

        public static SatelliteBeltBody BuildFromBodyDict(
            OrbitalBodyType parentType,
            Godot.Collections.Dictionary bodyDict
        )
        {
            return new Builder().FromBodyDict(parentType, bodyDict).Build();
        }
    }

    private SatelliteBeltBody(Builder builder)
    {
        RingApogee = builder._ringApogee;
        RingPerigee = builder._ringPerigee;
        RingVelocity = builder._ringVelocity;
        SizeMin = builder._sizeMin;
        SizeMax = builder._sizeMax;
        MassMin = builder._massMin;
        MassMax = builder._massMax;
        BeltNumber = builder._beltNumber;
        GroupType = builder._beltType;
        SatelliteNames = builder._satelliteNames;
    }

    public Godot.Collections.Array<CelestialBody> GenerateSatelliteBelt(CelestialBody parent)
    {
        Godot.Collections.Array<CelestialBody> satellites =
            new Godot.Collections.Array<CelestialBody>();
        try
        {
            var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();

            // Generate pHat and qHat ONCE for the entire belt (shared orbital plane)
            float theta = rng.RandfRange(0f, Mathf.Pi * 2f); // Azimuthal angle [0, 2π]
            float phi = rng.RandfRange(0f, Mathf.Pi); // Polar angle [0, π]
            Vector3 pHat = new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta),
                Mathf.Cos(phi)
            ).Normalized();

            // Generate qHat as 90-degree rotation in orbital plane
            Vector3 upReference = Vector3.Up;
            if (Mathf.Abs(pHat.Dot(upReference)) > 0.99f)
            {
                upReference = Vector3.Right;
            }
            Vector3 qHat = -pHat.Cross(upReference).Normalized();

            // Store base qHat for inclination variation
            Vector3 baseQHat = qHat;

            for (int i = 0; i < BeltNumber; i++)
            {
                var satellite = CreateSatellite(parent, pHat, baseQHat, i);
                satellites.Add(satellite);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error generating orbital belt: {ex.Message}\n{ex.StackTrace}\n");
        }
        return satellites;
    }

    private CelestialBody CreateSatellite(CelestialBody parent, Vector3 pHat, Vector3 baseQHat, int index)
    {
        var satelliteType = DetermineSatelliteType(GroupType);
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();

        // Mirror the celestial path: roll a concrete subtype from AU-weighted config so the
        // belt satellite carries a non-null subtype for resource and mesh-param lookups.
        float effectiveAU =
            parent.GetDistanceFromCenterAU()
            + OrbitalMath.ConvertUnitsToAU((RingApogee + RingPerigee) / 2f);
        string? parentSubtypeId =
            ProceduralGeneration.ColorSystem.BiomeIdMapper.ClassificationToSubtypeId(
                parent.Classification
            );
        var subtype =
            (
                new AUProbabilityManager(rng).SelectClassification(
                    satelliteType,
                    effectiveAU,
                    parentSubtypeId
                ) as Structures.BodyClassification.Satellite
            )?.Subtype;

        var size = rng.RandfRange(SizeMin, SizeMax);
        var mass = rng.RandfRange(MassMin, MassMax);

        // Apply slight inclination variation while keeping orbital plane shared
        // This creates a belt that's mostly flat but with minor variations
        float inclinationVariation = rng.RandfRange(-0.05f, 0.05f); // ~3 degrees variation
        Vector3 qHat = new Vector3(
            baseQHat.X,
            baseQHat.Y + inclinationVariation,
            baseQHat.Z
        ).Normalized();

        var angle = (float)rng.RandfRange(0f, Mathf.Pi * 2f);
        float eccentricity = OrbitalMath.CalculateEccentricity(RingApogee, RingPerigee);

        // Validate orbital parameters to prevent invalid positions
        if (RingApogee <= 0f || RingPerigee <= 0f)
        {
            GD.PrintErr(
                $"Invalid orbital parameters for satellite belt: RingApogee={RingApogee}, RingPerigee={RingPerigee}. Using default values."
            );
            RingApogee = Mathf.Max(RingApogee, 1f);
            RingPerigee = Mathf.Max(RingPerigee, 1f);
        }

        // Clamp eccentricity to valid range [0, 1) to prevent NaN in sqrt
        eccentricity = Mathf.Clamp(eccentricity, 0f, 0.99f);

        Vector3 position = OrbitalMath.CalculateOrbitalPosition(
            pHat,
            qHat,
            RingApogee,
            RingPerigee,
            angle,
            eccentricity
        );

        // Get the default template for this satellite type
        var template = TemplateHelpers.GetSatelliteBodyDefaults(satelliteType);

        // Override mass and size with belt-specific values
        var templateDict = (Godot.Collections.Dictionary)template["template"];
        templateDict["mass"] = mass;
        templateDict["size"] = size;

        // Calculate proper orbital velocity at this specific position
        Vector3 calculatedVelocity = OrbitalMath.CalculateEllipticalOrbitalVelocity(
            pHat,
            qHat,
            parent.Mass, // Mass of parent body
            RingApogee,
            RingPerigee,
            angle,
            false // counter-clockwise by default
        );

        // Apply ring_velocity as a multiplier (use its magnitude)
        float velocityMultiplier = RingVelocity.Length();
        if (velocityMultiplier > 0.001f) // avoid division by zero
        {
            calculatedVelocity *= velocityMultiplier;
        }

        templateDict["satellite_velocity"] = calculatedVelocity;
        templateDict["base_position"] = position;

        // Build the body dict with type and template
        var bodyDict = new Godot.Collections.Dictionary();
        bodyDict["type"] = satelliteType.ToString();
        bodyDict["template"] = templateDict;

        // Include mesh settings if present in template
        if (template.ContainsKey("base_mesh"))
            bodyDict["base_mesh"] = template["base_mesh"];
        if (template.ContainsKey("spherical_harmonics_settings"))
            bodyDict["spherical_harmonics_settings"] = template["spherical_harmonics_settings"];
        if (template.ContainsKey("noise_settings"))
            bodyDict["noise_settings"] = template["noise_settings"];
        if (template.ContainsKey("resources"))
            bodyDict["resources"] = template["resources"];

        // Include possible names so SatelliteBody can pick a unique name
        if (template.ContainsKey("possible_names"))
            bodyDict["possible_names"] = template["possible_names"];

        // Use pre-generated name from template loader if available
        if (SatelliteNames != null && index < SatelliteNames.Count)
        {
            bodyDict["name"] = SatelliteNames[index];
        }
        else if (!bodyDict.ContainsKey("name") || string.IsNullOrEmpty((string)bodyDict["name"]))
        {
            bodyDict["name"] = NameGenerator.GenerateSatelliteName(satelliteType);
        }

        var mesh = new UnifiedCelestialMesh();
        var satellite = new CelestialBody.Builder()
            .WithSatelliteType(satelliteType, subtype)
            .WithSize(size)
            .WithMass(mass)
            .WithVelocity(calculatedVelocity)
            .WithBodyDict(bodyDict)
            .WithDepth(parent.Depth + 1)
            .WithForceAnalyticalOrbit(true)
            .WithMesh(mesh)
            .Build();
        satellite.EffectiveAU =
            parent.EffectiveAU + OrbitalMath.ConvertUnitsToAU((RingApogee + RingPerigee) / 2f);
        // Place the belt member at its computed orbital position (local; retained after parenting).
        satellite.Position = position;

        return satellite;
    }

    private OrbitalBodyType DetermineSatelliteType(SatelliteGroupTypes beltType)
    {
        return beltType switch
        {
            SatelliteGroupTypes.AsteroidBelt => OrbitalBodyType.Asteroid,
            SatelliteGroupTypes.Comet => OrbitalBodyType.Comet,
            SatelliteGroupTypes.IceBelt => OrbitalBodyType.Comet,
            _ => OrbitalBodyType.Asteroid,
        };
    }
}
