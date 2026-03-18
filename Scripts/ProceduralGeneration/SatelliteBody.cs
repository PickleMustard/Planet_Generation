using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Constructables.ArtificialSatellites;
using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.MeshGeneration.ResourceGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;
using UtilityLibrary;

namespace ProceduralGeneration.PlanetGeneration;

[GlobalClass]
public partial class SatelliteBody : Node3D, IOrbitalBody
{
    private float _mass;
    private float _radius;

    [Export]
    public Vector3 Velocity;

    [Export]
    public float Mass
    {
        get => _mass;
        set => _mass = value;
    }

    [Export]
    public float Radius
    {
        get => _radius;
        set => _radius = value;
    }

    [Export]
    public Vector3 accelerationVector;
    bool isSatelliteGroup = false;
    SatelliteBodyType SatelliteType;
    UnifiedCelestialMesh? Mesh;
    Octree<Point>? Oct;
    public Godot.Collections.Dictionary? bodyDict;
    StructureDatabase? StrDb;

    /// <summary>
    /// Resource deposits available on this satellite body.
    /// Key is the resource ID, value is the deposit information.
    /// </summary>
    public Dictionary<string, ResourceDeposit> Resources { get; set; } = new();

    // Orbit System
    [Export]
    public OrbitConfiguration? OrbitConfig { get; private set; }

    [Export]
    public Godot.Collections.Array<OrbitBand> OrbitBands { get; private set; } = new();

    [Export]
    public Node3D SatellitesContainer { get; private set; } = null!;
    private Dictionary<int, int> _bandSatelliteCounts = new();

    public class Builder
    {
        internal Vector3 _velocity;
        internal float _mass;
        internal float _size;
        internal Vector3 _totalForce = Vector3.Zero;
        internal bool _isSatelliteGroup = false;
        internal SatelliteBodyType _satelliteType;
        internal UnifiedCelestialMesh? _mesh;
        internal Octree<Point>? _oct;
        internal Godot.Collections.Dictionary? _bodyDict;
        internal StructureDatabase? _strDb;

        // Orbital parameters
        internal float _apogee;
        internal float _perigee;
        internal float _startingAngle;
        internal float _verticalOffset;

        public Builder WithVelocity(Vector3 velocity)
        {
            _velocity = velocity;
            return this;
        }

        public Builder WithMass(float mass)
        {
            _mass = mass;
            return this;
        }

        public Builder WithSize(float size)
        {
            _size = size;
            return this;
        }

        public Builder WithTotalForce(Vector3 totalForce)
        {
            _totalForce = totalForce;
            return this;
        }

        public Builder WithIsSatelliteGroup(bool isSatelliteGroup)
        {
            _isSatelliteGroup = isSatelliteGroup;
            return this;
        }

        public Builder WithSatelliteType(SatelliteBodyType satelliteType)
        {
            _satelliteType = satelliteType;
            return this;
        }

        public Builder WithMesh(UnifiedCelestialMesh mesh)
        {
            _mesh = mesh;
            return this;
        }

        public Builder WithBodyDict(Godot.Collections.Dictionary bodyDict)
        {
            _bodyDict = bodyDict;
            return this;
        }

        public Builder WithOrbitalParameters(
            float apogee,
            float perigee,
            float startingAngle,
            float verticalOffset
        )
        {
            _apogee = apogee;
            _perigee = perigee;
            _startingAngle = startingAngle;
            _verticalOffset = verticalOffset;
            return this;
        }

        private void Validate()
        {
            if (_mesh == null)
                throw new InvalidOperationException("Mesh is required");
        }

        public SatelliteBody Build()
        {
            Validate();
            return new SatelliteBody(this);
        }

        public Builder FromBodyDict(
            CelestialBodyType parentType,
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
        )
        {
            _bodyDict = bodyDict;
            _mesh = mesh;

            var type = (String)bodyDict["type"];
            var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
            var mass = (float)baseTemplates["mass"];
            var size = (int)baseTemplates["size"];

            _satelliteType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), type);
            _mass = mass;
            _size = size;

            // Read orbital parameters
            _apogee = baseTemplates.ContainsKey("apogee") ? (float)baseTemplates["apogee"] : 500f;
            _perigee = baseTemplates.ContainsKey("perigee")
                ? (float)baseTemplates["perigee"]
                : 300f;
            _startingAngle = baseTemplates.ContainsKey("starting_angle")
                ? (float)baseTemplates["starting_angle"]
                : 0f;
            _verticalOffset = baseTemplates.ContainsKey("vertical_offset")
                ? (float)baseTemplates["vertical_offset"]
                : 0f;

            var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
            _strDb = new StructureDatabase(rand.RandiRange(0, 100000));
            _oct = new Octree<Point>(new Aabb(Vector3.Zero, new Vector3(size, size, size)));

            return this;
        }

        public static SatelliteBody BuildFromBodyDict(
            CelestialBodyType parentType,
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
        )
        {
            return new Builder().FromBodyDict(parentType, bodyDict, mesh).Build();
        }
    }

    private SatelliteBody(Builder builder)
    {
        Velocity = builder._velocity;
        Mass = builder._mass;
        Radius = builder._size;
        accelerationVector = builder._totalForce;
        isSatelliteGroup = builder._isSatelliteGroup;
        SatelliteType = builder._satelliteType;
        Mesh = builder._mesh;
        Oct = builder._oct;
        bodyDict = builder._bodyDict;
        var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        Oct = new Octree<Point>(new Aabb(Vector3.Zero, new Vector3(Radius, Radius, Radius)));

        if (Mesh != null)
        {
            Mesh.size = Radius;
            this.CallDeferred("add_child", Mesh);
        }
    }

    //public SatelliteBody(
    //    CelestialBodyType parentType,
    //    Godot.Collections.Dictionary bodyDict,
    //    UnifiedCelestialMesh mesh
    //)
    //{
    //    this.bodyDict = bodyDict;
    //    var type = (String)bodyDict["type"];
    //    var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
    //    var mass = (float)baseTemplates["mass"];
    //    var velocity = (Vector3)baseTemplates["satellite_velocity"];
    //    var size = (int)baseTemplates["size"];
    //    var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
    //    StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
    //    Oct = new Octree<Point>(new Aabb(Vector3.Zero, new Vector3(size, size, size)));

    //    this.SatelliteType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), type);
    //    this.Mass = mass;
    //    this.Velocity = velocity;
    //    this.Mesh = mesh;
    //    mesh.size = size;
    //    this.AddChild(mesh);

    //    switch (SatelliteType)
    //    {
    //        case SatelliteBodyType.Asteroid:
    //            break;
    //    }
    //}

    public SatelliteBody(
        CelestialBodyType parentType,
        String satType,
        float mass,
        float size,
        Vector3 velocity,
        UnifiedCelestialMesh mesh
    )
    {
        this.bodyDict = null;
        this.Mesh = mesh;
        this.Radius = size;
        this.AddChild(mesh);
        this.SatelliteType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), satType);
        this.Mass = mass;
        this.Velocity = velocity;
        var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        Oct = new Octree<Point>(new Aabb(Vector3.Zero, new Vector3(size, size, size)));
    }

    /// <summary>
    /// Calculates the position and velocity of a satellite given orbital parameters.
    /// </summary>
    /// <param name="apogee">Farthest distance from parent body</param>
    /// <param name="perigee">Closest distance to parent body</param>
    /// <param name="startingAngle">Starting angle in degrees (0-360)</param>
    /// <param name="verticalOffset">Orbital inclination/vertical offset in degrees (-90 to 90)</param>
    /// <param name="parentMass">Mass of the parent body for velocity calculation</param>
    /// <returns>Tuple of (position, velocity)</returns>
    public static (Vector3 position, Vector3 velocity) CalculateOrbitalState(
        float apogee,
        float perigee,
        float startingAngle,
        float verticalOffset,
        float parentMass
    )
    {
        // Convert angles to radians
        float angleRad = Mathf.DegToRad(startingAngle);
        float inclinationRad = Mathf.DegToRad(verticalOffset);

        // Create orbital plane basis vectors from starting angle and inclination
        // pHat is the direction of the starting angle in the orbital plane
        // qHat incorporates the inclination (vertical offset)
        Vector3 pHat = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)).Normalized();
        Vector3 qHat = new Vector3(
            -Mathf.Sin(angleRad) * Mathf.Cos(inclinationRad),
            Mathf.Sin(inclinationRad),
            Mathf.Cos(angleRad) * Mathf.Cos(inclinationRad)
        ).Normalized();

        // Calculate eccentricity
        float eccentricity = OrbitalMath.CalculateEccentricity(apogee, perigee);

        // Calculate position on the orbit
        Vector3 position = OrbitalMath.CalculateOrbitalPosition(
            pHat,
            qHat,
            apogee,
            perigee,
            angleRad,
            eccentricity
        );

        // Calculate velocity at this position
        Vector3 velocity = OrbitalMath.CalculateEllipticalOrbitalVelocity(
            pHat,
            qHat,
            parentMass,
            apogee,
            perigee,
            angleRad,
            false
        );

        return (position, velocity);
    }

    public override void _Ready()
    {
        base._Ready();
    }

    /// <summary>
    /// Initializes the orbit system based on the satellite's size (treated as radius).
    /// Creates orbit bands and sets up the satellites container.
    /// </summary>
    public void InitializeOrbitSystem()
    {
        // Use Size as the body radius for satellites
        float bodyRadius = Mesh!.size;

        // Create orbit configuration from mass
        OrbitConfig = OrbitConfiguration.CreateFromMass(Mass, bodyRadius);

        // Create all orbit bands
        OrbitBands = OrbitConfig.CreateAllOrbitBands(bodyRadius);

        // Initialize satellite counts for each band
        _bandSatelliteCounts.Clear();
        for (int i = 0; i < OrbitBands.Count; i++)
        {
            _bandSatelliteCounts[i] = 0;
        }

        // Create the satellites container
        SatellitesContainer = new Node3D { Name = "SatellitesContainer" };
        AddChild(SatellitesContainer);

        GameLogger.Debug(
            $"SatelliteBody OrbitSystem initialized: {OrbitBands.Count} bands for mass {Mass}"
        );
    }

    /// <summary>
    /// Creates a station satellite in the specified orbit band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band (0-based)</param>
    /// <param name="name">Name for the station</param>
    /// <returns>The created station satellite, or null if invalid band</returns>
    public StationSatellite? CreateStation(int bandIndex, string name)
    {
        if (!CanAddToBand(bandIndex))
        {
            GameLogger.Warning($"Cannot add station to band {bandIndex}: band is full or invalid");
            return null;
        }

        var station = new StationSatellite { Name = name };

        SatellitesContainer.AddChild(station);
        station.Initialize(this, bandIndex);

        _bandSatelliteCounts[bandIndex]++;

        GameLogger.Debug($"Created station '{name}' in band {bandIndex}");
        return station;
    }

    /// <summary>
    /// Creates a ship in a random valid orbit band.
    /// </summary>
    /// <param name="name">Name for the ship</param>
    /// <returns>The created ship satellite, or null if no valid band available</returns>
    public StationSatellite? CreateShip(string name)
    {
        // Find a band with available capacity
        for (int i = 0; i < OrbitBands.Count; i++)
        {
            if (CanAddToBand(i))
            {
                return CreateStation(i, name);
            }
        }

        GameLogger.Warning($"Cannot create ship '{name}': no available bands");
        return null;
    }

    /// <summary>
    /// Returns the number of available orbit bands.
    /// </summary>
    public int GetBandCount()
    {
        return OrbitBands?.Count ?? 0;
    }

    /// <summary>
    /// Checks if a satellite can be added to the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <returns>True if the band exists and has capacity</returns>
    public bool CanAddToBand(int bandIndex)
    {
        if (OrbitBands == null || bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            return false;
        }

        int currentCount = _bandSatelliteCounts.ContainsKey(bandIndex)
            ? _bandSatelliteCounts[bandIndex]
            : 0;
        int capacity = OrbitBands[bandIndex].Capacity;

        return currentCount < capacity;
    }

    /// <summary>
    /// Gets the current count of satellites in a band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <returns>Number of satellites in the band, or -1 if band is invalid</returns>
    public int GetBandSatelliteCount(int bandIndex)
    {
        if (bandIndex < 0 || bandIndex >= OrbitBands?.Count)
        {
            return -1;
        }

        return _bandSatelliteCounts.ContainsKey(bandIndex) ? _bandSatelliteCounts[bandIndex] : 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        accelerationVector = new Vector3(0.0f, 0.0f, 0.0f);
        var parent = GetParent() as CelestialBody;
        if (parent == null)
        {
            GD.PrintErr("SatelliteBody._PhysicsProcess: Parent body is null");
            return;
        }
        float distance = this.GlobalPosition.DistanceTo(parent.GlobalPosition);
        Vector3 direction = (parent.GlobalPosition - this.GlobalPosition);

        float acceleration =
            OrbitalMath.GRAVITATIONAL_CONSTANT * parent.Mass / (distance * distance);
        accelerationVector += direction.Normalized() * acceleration;

        Velocity += accelerationVector * (float)delta;
        GlobalPosition += Velocity * (float)delta;
    }

    public async Task GenerateMesh()
    {
        StartMeshGeneration(
            onCompleted: (_) => { },
            onFailed: (_, error) => GD.PrintErr($"Mesh generation failed: {error}")
        );
        while (Mesh?.Mesh == null)
        {
            await Task.Delay(10);
        }
    }

    public void StartMeshGeneration(
        Action<SatelliteBody>? onCompleted = null,
        Action<SatelliteBody, string>? onFailed = null
    )
    {
        GD.Print($"Generating satellite: {Name}, bodyDict: {bodyDict}");
        Godot.Collections.Dictionary meshParams = new Godot.Collections.Dictionary();
        // Check if custom mesh data is available in the body dictionary
        if (bodyDict != null)
        {
            meshParams.Add("Type", bodyDict["type"]);

            // Use provided name, or pick from possible_names if available
            if (bodyDict.ContainsKey("name"))
            {
                meshParams.Add("name", bodyDict["name"]);
            }
            else if (bodyDict.ContainsKey("possible_names"))
            {
                var name = PickName((Godot.Collections.Dictionary)bodyDict["possible_names"]);
                meshParams.Add("name", name);
            }
            else
            {
                // Fallback to type name
                meshParams.Add("name", SatelliteType.ToString());
            }
            if (
                bodyDict.ContainsKey("base_mesh")
                && bodyDict["base_mesh"].Obj is Godot.Collections.Dictionary customMesh
            )
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            // Check for spherical_harmonics_settings first, fall back to scaling
            if (
                bodyDict.ContainsKey("spherical_harmonics_settings")
                && bodyDict["spherical_harmonics_settings"].Obj
                    is Godot.Collections.Dictionary shSettings
            )
            {
                CalculateSphericalHarmonicsFromParams(shSettings, meshParams);
            }
            else if (
                bodyDict.ContainsKey("scaling_settings")
                && bodyDict["scaling_settings"].Obj is Godot.Collections.Dictionary scaling
            )
            {
                CalculateScalingFromParams(scaling, meshParams);
            }
            if (
                bodyDict.ContainsKey("noise_settings")
                && bodyDict["noise_settings"].Obj is Godot.Collections.Dictionary noise
            )
            {
                CalculateNoiseSettingsFromParams(noise, meshParams);
            }
        }
        else
        {
            var t = TemplateHelpers.GetSatelliteBodyDefaults(SatelliteType);
            var name = PickName((Godot.Collections.Dictionary)t["possible_names"]);
            meshParams.Add("name", name);
            meshParams.Add("type", Enum.GetName(typeof(SatelliteBodyType), SatelliteType)!);
            var template = (Godot.Collections.Dictionary)t["template"];
            var position = (Vector3)template["position"];
            var velocity = (Vector3)template["velocity"];
            meshParams.Add("position", position);
            meshParams.Add("velocity", velocity);
            var size = (int)template["size"];
            var mass = (float)template["mass"];
            meshParams.Add("size", size);
            meshParams.Add("mass", mass);
            if (
                t.ContainsKey("base_mesh")
                && t["base_mesh"].Obj is Godot.Collections.Dictionary customMesh
            )
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            // Check for spherical_harmonics_settings first, fall back to scaling
            if (
                t.ContainsKey("spherical_harmonics_settings")
                && t["spherical_harmonics_settings"].Obj is Godot.Collections.Dictionary shSettings
            )
            {
                CalculateSphericalHarmonicsFromParams(shSettings, meshParams);
            }
            else if (
                t.ContainsKey("scaling_settings")
                && t["scaling_settings"].Obj is Godot.Collections.Dictionary scaling
            )
            {
                CalculateScalingFromParams(scaling, meshParams);
            }
            if (
                t.ContainsKey("noise_settings")
                && t["noise_settings"].Obj is Godot.Collections.Dictionary noise
            )
            {
                CalculateNoiseSettingsFromParams(noise, meshParams);
            }
        }
        this.CallDeferred("set_name", (String)meshParams["name"]);
        if (Mass > 0)
        {
            meshParams["mass"] = Mass;
        }
        if (Radius > 0)
        {
            meshParams["size"] = Radius;
        }
        Mesh!.ConfigureFrom(StrDb!, meshParams);
        Mesh.StartMeshGeneration(
            Oct!,
            onCompleted: (mesh) =>
            {
                GenerateResources();
                onCompleted?.Invoke(this);
            },
            onFailed: (mesh, error) =>
            {
                GD.PrintErr($"Mesh generation failed for {meshParams["name"]}: {error}");
                onFailed?.Invoke(this, error);
            }
        );
    }

    /// <summary>
    /// Generates resources for this satellite based on its configuration.
    /// </summary>
    public void GenerateResources()
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        Godot.Collections.Dictionary? resourceConfig = null;

        if (bodyDict != null && bodyDict.ContainsKey("resources"))
        {
            resourceConfig = bodyDict["resources"].AsGodotDictionary();
        }
        else
        {
            var template = TemplateHelpers.GetSatelliteBodyDefaults(SatelliteType);
            if (template.ContainsKey("resources"))
            {
                resourceConfig = template["resources"].AsGodotDictionary();
            }
        }

        if (resourceConfig != null)
        {
            Resources = SatelliteResourceGenerator.GenerateResources(resourceConfig, rng);
            GD.Print($"SatelliteBody '{Name}' generated {Resources.Count} resource deposits");
        }
    }

    public String PickName(Godot.Collections.Dictionary nameDict)
    {
        GD.Print($"SatelliteBody.PickName: {nameDict}");
        if (nameDict == null || nameDict.Count == 0)
            return "";

        var categories = new Godot.Collections.Array(nameDict.Keys);
        if (categories.Count == 0)
            return "";

        var random = UtilityLibrary.Randomizer.rng;
        var selectedCategory = (string)categories[random.RandiRange(0, categories.Count - 1)];

        var names = (Godot.Collections.Array)nameDict[selectedCategory];
        if (names == null || names.Count == 0)
            return "";

        return (string)names[random.RandiRange(0, names.Count - 1)];
    }

    private void CalculateBaseMeshFromParams(
        Godot.Collections.Dictionary definedMesh,
        Godot.Collections.Dictionary meshParams
    )
    {
        meshParams.Add("subdivisions", (int)definedMesh["subdivisions"]);
        var vpeArray = (Godot.Collections.Array<Godot.Collections.Array<int>>)
            definedMesh["vertices_per_edge"];
        int[] vertices_per_edge = new int[(int)definedMesh["subdivisions"]];
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        GD.Print($"VPE Array: {vpeArray}");
        for (int i = 0; i < vertices_per_edge.Length; i++)
        {
            if (vpeArray.Count - 1 > i) //Defined subdivisions
            {
                vertices_per_edge[i] = rng.RandiRange(vpeArray[i][0], vpeArray[i][1]);
            }
            else
            {
                vertices_per_edge[i] = rng.RandiRange(
                    vpeArray[vpeArray.Count - 1][0],
                    vpeArray[vpeArray.Count - 1][1]
                );
            }
        }
        meshParams.Add("vertices_per_edge", vertices_per_edge);
        meshParams.Add("num_abberations", (int)definedMesh["num_abberations"]);
        meshParams.Add("num_deformation_cycles", (int)definedMesh["num_deformation_cycles"]);
    }

    private void CalculateScalingFromParams(
        Godot.Collections.Dictionary definedScaling,
        Godot.Collections.Dictionary meshParams
    )
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var scalingDict = new Godot.Collections.Dictionary();
        float[] xScaleRange = (float[])definedScaling["scaling_range_x"];
        scalingDict.Add("scaling_range_x", rng.RandfRange(xScaleRange[0], xScaleRange[1]));
        float[] yScaleRange = (float[])definedScaling["scaling_range_y"];
        scalingDict.Add("scaling_range_y", rng.RandfRange(yScaleRange[0], yScaleRange[1]));
        float[] zScaleRange = (float[])definedScaling["scaling_range_z"];
        scalingDict.Add("scaling_range_z", rng.RandfRange(zScaleRange[0], zScaleRange[1]));
        meshParams.Add("scaling_settings", scalingDict);
    }

    private void CalculateSphericalHarmonicsFromParams(
        Godot.Collections.Dictionary definedSH,
        Godot.Collections.Dictionary meshParams
    )
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var shDict = new Godot.Collections.Dictionary();

        // Randomize amplitude from range
        float[] amplitudeRange = (float[])definedSH["amplitude_range"];
        shDict.Add("amplitude", rng.RandfRange(amplitudeRange[0], amplitudeRange[1]));

        meshParams.Add("spherical_harmonics", shDict);
    }

    private void CalculateNoiseSettingsFromParams(
        Godot.Collections.Dictionary definedNoise,
        Godot.Collections.Dictionary meshParams
    )
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var noiseDict = new Godot.Collections.Dictionary();
        float[] amplitude = (float[])definedNoise["amplitude_range"];
        noiseDict.Add("amplitude", rng.RandfRange(amplitude[0], amplitude[1]));
        float[] scaling = (float[])definedNoise["scaling_range"];
        noiseDict.Add("scaling", rng.RandfRange(scaling[0], scaling[1]));
        int[] octaves = (int[])definedNoise["octave_range"];
        noiseDict.Add("octaves", rng.RandiRange(octaves[0], octaves[1]));
        // Pass through lacunarity and gain if present in config
        if (definedNoise.ContainsKey("lacunarity"))
        {
            noiseDict.Add("lacunarity", (float)definedNoise["lacunarity"]);
        }
        if (definedNoise.ContainsKey("gain"))
        {
            noiseDict.Add("gain", (float)definedNoise["gain"]);
        }
        meshParams.Add("noise_settings", noiseDict);
    }
}
