using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using PlanetGeneration;
using Structures.Enums;
using ProceduralGeneration.MeshGeneration;
using UtilityLibrary;

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

        public Builder FromBodyDict(CelestialBodyType parentType, Godot.Collections.Dictionary bodyDict)
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
            _beltNumber = GD.RandRange(_lowerRange, _upperRange);

            GD.Print($"Built belt consisting of {_ringApogee}, {_ringPerigee}, {_ringVelocity}, {_sizeMin}, {_sizeMax}, {_massMin}, {_massMax}, {_upperRange}, {_lowerRange}, {_beltNumber}");
            return this;
        }

        public static SatelliteBeltBody BuildFromBodyDict(CelestialBodyType parentType, Godot.Collections.Dictionary bodyDict)
        {
            return new Builder()
                .FromBodyDict(parentType, bodyDict)
                .Build();
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
    }

    public Godot.Collections.Array<SatelliteBody> GenerateSatelliteBelt(CelestialBody parent)
    {
        Godot.Collections.Array<SatelliteBody> satellites = new Godot.Collections.Array<SatelliteBody>();
        try
        {
            for (int i = 0; i < BeltNumber; i++)
            {
                var satellite = CreateSatellite();
                satellites.Add(satellite);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error generating orbital belt: {ex.Message}\n{ex.StackTrace}\n");
        }
        return satellites;
    }

    private SatelliteBody CreateSatellite()
    {
        var satelliteType = DetermineSatelliteType(GroupType);
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();

        var size = rng.RandfRange(SizeMin, SizeMax);
        var mass = rng.RandfRange(MassMin, MassMax);
        var angle = (float)rng.RandfRange(0, Mathf.Pi * 2);
        float eccentricity = OrbitalMath.CalculateEccentricity(RingApogee, RingPerigee);
        Vector3 position = OrbitalMath.CalculateOrbitalPosition(RingApogee, RingPerigee, angle, eccentricity);

        var mesh = new UnifiedCelestialMesh();
        var satellite = new SatelliteBody.Builder()
            .WithSatelliteType(satelliteType)
            .WithMass(mass)
            .WithSize(size)
            .WithVelocity(RingVelocity)
            .WithMesh(mesh)
            .Build();

        satellite.Position = position;
        return satellite;
    }

    private SatelliteBodyType DetermineSatelliteType(SatelliteGroupTypes beltType)
    {
        return beltType switch
        {
            SatelliteGroupTypes.AsteroidBelt => SatelliteBodyType.Asteroid,
            SatelliteGroupTypes.Comet => SatelliteBodyType.Comet,
            SatelliteGroupTypes.IceBelt => SatelliteBodyType.Comet,
            _ => SatelliteBodyType.Asteroid,
        };
    }

    private bool SupportsBeltGeneration(CelestialBodyType parentType)
    {
        return parentType == CelestialBodyType.Star || parentType == CelestialBodyType.BlackHole;
    }
}
