using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Constructables;
using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.SubtypeSystem;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Transfers;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;
#if DEBUG
using Debug;
#endif

namespace ProceduralGeneration.PlanetGeneration;

///<class>CelestialBody</class>
///<summary>A CelestialBody is a single point in space that has a mass and velocity.
///It has mass and gravity that will be used to calculate the attrational force on other objects.
///Its position can be modified by the forces acting upon it</summary>
#if DEBUG
[DebugData("CelestialBody", Category = "Game")]
#endif
[GlobalClass]
public partial class CelestialBody : Node3D, IOrbitalBody, ISelectableBody
{
    private float _mass;
    private float _radius;
    private Vector3 _velocity;

    public static Barycenter barycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 0f);

    /// <summary>
    /// When true, an NBodyCoordinator handles physics integration.
    /// Per-body _PhysicsProcess becomes a no-op.
    /// </summary>
    public static bool CoordinatorActive { get; set; } = false;

    public Builder builder()
    {
        return new Builder();
    }

    [Export]
    public Vector3 BodyPosition
    {
        get => Position;
        set => Position = value;
    }

    [Export]
    public Vector3 Velocity
    {
        get => _velocity;
        set => _velocity = value;
    }

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
    public string BodyName
    {
        get => Name;
        set => Name = value;
    }

    [Export]
    public string? BodyType
    {
        get => Classification?.TypeName;
        set
        {
            // Deserialization: reconstruct Classification from stored type name
            if (value != null && Enum.TryParse<CelestialBodyType>(value, out var cbt))
            {
                Classification = BodyClassification.FromLegacy(cbt, null);
            }
        }
    }

    [Export]
    public Vector3 TotalForce;
    private Vector3 _savedForce;

    public BodyClassification Classification { get; set; } = null!;

    /// <summary>
    /// Deterministic seed used by <see cref="ProceduralGeneration.SubtypeSystem.SubtypeGenParamResolver"/>
    /// to roll mesh / tectonic / spherical-harmonic values inside the subtype's declared ranges.
    /// Zero means "fall back to the global Randomizer" (non-deterministic). Set explicitly to make
    /// repeated regenerations reproduce identical bodies (e.g. live preview reroll).
    /// </summary>
    public ulong BodySeed { get; set; } = 0UL;

    /// <summary>
    /// Atmospheric pressure in atmospheres (1.0 = Earth standard). Sampled at
    /// generation from the body type's YAML range. Used by wind power gating
    /// and scaling; reserved for future atmosphere processing.
    /// </summary>
    [Export]
    public float Atmosphere { get; set; } = 0f;

    /// <summary>
    /// The body this one orbits. Null for system-root bodies (stars, black holes
    /// orbiting the barycenter). Wired by <see cref="SystemGenerator"/>.
    /// </summary>
    public IOrbitalBody? OrbitalParent { get; set; }

    [Export]
    public BodyBillboardTextures BillboardTextures { get; private set; } = null!;

    /// <summary>
    /// Backward-compat computed property. Returns the CelestialBodyType from Classification.
    /// </summary>
    public CelestialBodyType Type => Classification.AsCelestialBodyType!.Value;

    public UnifiedCelestialMesh Mesh { get; private set; }
    public Octree<Point> Oct;
    private Godot.Collections.Dictionary? bodyDict;
    private StructureDatabase StrDb;

    /// <summary>
    /// The camera anchor (Node3D) attached to this body for positioning the camera.
    /// Created on-demand via GetOrCreateCameraAnchor().
    /// </summary>
    public Node3D? CameraAnchor { get; private set; }

    /// <summary>
    /// Stores the current look direction for the camera anchor when no explicit target is provided.
    /// Used to maintain orientation when repositioning the anchor without a LookAt target.
    /// </summary>
    private Vector3 _cameraAnchorLookDir = Vector3.Forward;

    // Orbit System
    [Export]
    public OrbitConfiguration? OrbitConfig { get; private set; }

    [Export]
    public Godot.Collections.Array<OrbitBand> OrbitBands { get; private set; } = new();

    [Export]
    public Node3D SatellitesContainer { get; private set; } = null!;
    public BuildingConstructionManager? BuildingConstructionMgr { get; private set; }
    public BodyEconomyManager? EconomyMgr { get; private set; }
    public Constructables.Power.BodyPowerGridManager? PowerGridMgr { get; private set; }
    public Node ConstructionManager
    {
        get => BuildingConstructionMgr!;
    }
    private Godot.Collections.Dictionary<int, int> _bandSatelliteCounts = new();

    // Transfer endpoint registry (lightweight — no transfer state)
    private readonly Dictionary<string, TransferStationDefinition> _endpointDefs = new();

    private readonly Dictionary<string, GodotObject> _endpointOwners = new();
    #region OrbitalParameters

    /// <summary>
    /// Indicates whether this body uses discrete band-based placement.
    /// Delegates to Classification.
    /// </summary>
    public bool UsesBandPlacement => Classification.UsesBandPlacement;

    /// <summary>
    /// Gets orbital parameters for a satellite placed in the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band.</param>
    /// <param name="startingAngle">Starting orbital angle in radians.</param>
    /// <returns>Complete orbital parameters including position and velocity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when band index is invalid.</exception>
    public OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle)
    {
        if (bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bandIndex),
                $"Band index {bandIndex} out of range. Available bands: {OrbitBands.Count}"
            );
        }

        OrbitBand band = OrbitBands[bandIndex];

        return OrbitalParameters.CreateCircular(
            radius: band.Radius,
            angularSpeed: band.AngularSpeed,
            startingAngle: startingAngle,
            hostMass: Mass,
            bandIndex: bandIndex
        );
    }

    /// <summary>
    /// Gets orbital parameters for a satellite placed at an arbitrary radius (continuous placement).
    /// </summary>
    /// <param name="radius">Desired orbital radius in meters.</param>
    /// <param name="startingAngle">Starting orbital angle in radians.</param>
    /// <returns>Complete orbital parameters including position and velocity.</returns>
    public OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle)
    {
        // Calculate physics-based angular speed: ω = sqrt(G*M/r^3)
        float angularSpeed = OrbitalParameters.CalculateAngularSpeed(Mass, radius);

        return OrbitalParameters.CreateCircular(
            radius: radius,
            angularSpeed: angularSpeed,
            startingAngle: startingAngle,
            hostMass: Mass,
            bandIndex: -1 // Continuous placement
        );
    }

    #endregion

    public CelestialBody(Godot.Collections.Dictionary bodyDict, UnifiedCelestialMesh mesh)
    {
        this.bodyDict = bodyDict;
        this.BillboardTextures = new BodyBillboardTextures();
        var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
        var type = (String)bodyDict["type"];
        var mass = (float)baseTemplates["mass"];
        var velocity = (Vector3)baseTemplates["velocity"];
        var size = Mathf.RoundToInt((float)baseTemplates["size"]);
        Vector3 aabbSize = new Vector3(size, size, size) * 1.2f;
        Vector3 aabbBegin = Vector3.Zero - aabbSize;
        var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
        Oct = new Octree<Point>(new Aabb(aabbBegin, aabbSize * 2f));

        var celestialBodyType = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), type);
        this.Classification = BodyClassification.FromLegacy(celestialBodyType, null);
        this.Mass = mass;
        this.Velocity = velocity;
        this.Mesh = mesh;
        this.Atmosphere = SampleAtmosphere(celestialBodyType, rand);
        mesh.size = size;
        this.AddChild(mesh);

        if (Classification is BodyClassification.Star)
        {
            OmniLight3D emision = new OmniLight3D();
            emision.OmniRange = 4096f;
            emision.OmniAttenuation = .14f;
            this.AddChild(emision);
        }
    }

    private static float SampleAtmosphere(CelestialBodyType type, RandomNumberGenerator rand)
    {
        var (min, max) = UtilityLibrary.DataLoading.TemplateHelpers.GetAtmosphereRange(type);
        if (max <= min)
            return min;
        return rand.RandfRange(min, max);
    }

    private CelestialBody(Builder builder)
    {
        this.Velocity = builder._velocity ?? Vector3.Zero;
        this.Mass = builder._mass ?? 0f;
        this.Classification =
            builder._classification
            ?? BodyClassification.FromLegacy(CelestialBodyType.RockyPlanet, null);
        this.Mesh = builder._mesh;
        this.bodyDict = builder._bodyDict;
        this.TotalForce = Vector3.Zero;
        this.Name = builder._name ?? "";
        this.BillboardTextures = new BodyBillboardTextures();

        var atmRand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var cbt = this.Classification.AsCelestialBodyType;
        this.Atmosphere = cbt.HasValue ? SampleAtmosphere(cbt.Value, atmRand) : 0f;

        if (this.Mesh != null)
        {
            this.AddChild(this.Mesh);
        }

        if (this.bodyDict != null)
        {
            var baseTemplates = (Godot.Collections.Dictionary)this.bodyDict["template"];
            var size = (int)baseTemplates["size"];
            Vector3 aabbSize = new Vector3(size, size, size) * 1.2f;
            Vector3 aabbBegin = Vector3.Zero - aabbSize;
            var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
            StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
            Oct = new Octree<Point>(new Aabb(aabbBegin, aabbSize * 2f));

            if (this.Mesh != null)
            {
                this.Mesh.size = size;
            }
        }
        else
        {
            var rand = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
            StrDb = new StructureDatabase(rand.RandiRange(0, 100000));
            Oct = new Octree<Point>(new Aabb(Vector3.One * -10, Vector3.One * 20));
        }

        if (Classification is BodyClassification.Star)
        {
            OmniLight3D emision = new OmniLight3D();
            emision.OmniRange = 4096f;
            emision.OmniAttenuation = .14f;
            this.AddChild(emision);
        }
    }

    public override void _Ready()
    {
        AddToGroup("CelestialBody");
        barycenter.RegisterBody();
    }

    /// <summary>
    /// Initializes the orbit system based on the body's mass.
    /// Creates orbit bands for band-based bodies, or empty bands for continuous placement bodies.
    /// Sets up the satellites container for all body types.
    /// </summary>
    public void InitializeOrbitSystem()
    {
        // Calculate body radius from scale (assuming sphere)
        float bodyRadius = Mesh!.size;

        // Create orbit configuration from mass
        OrbitConfig = OrbitConfiguration.CreateFromMass(Mass, bodyRadius);

        // Create orbit bands based on placement type
        if (UsesBandPlacement)
        {
            // Band-based: create actual bands with physics-derived velocities
            OrbitBands = OrbitConfig.CreateAllOrbitBands(bodyRadius, Mass);

            // Initialize satellite counts for each band
            _bandSatelliteCounts.Clear();
            for (int i = 0; i < OrbitBands.Count; i++)
            {
                _bandSatelliteCounts[i] = 0;
            }
        }
        else
        {
            // Continuous placement: empty bands, no discrete slots
            OrbitBands = new Godot.Collections.Array<OrbitBand>();
            _bandSatelliteCounts.Clear();
        }

        // Create the satellites container (always needed)
        SatellitesContainer = new Node3D { Name = "SatellitesContainer" };
        CallDeferred("add_child", SatellitesContainer);

        // Create the centralized building construction manager
        BuildingConstructionMgr = new BuildingConstructionManager
        {
            Name = "BuildingConstructionManager",
        };
        CallDeferred("add_child", BuildingConstructionMgr);

        // Create per-body economy manager
        EconomyMgr = new BodyEconomyManager { Name = "BodyEconomyManager" };
        CallDeferred("add_child", EconomyMgr);

        PowerGridMgr = new Constructables.Power.BodyPowerGridManager
        {
            Name = "BodyPowerGridManager",
            Body = this,
        };
        CallDeferred("add_child", PowerGridMgr);

        GameLogger.Debug(
            $"OrbitSystem initialized: {OrbitBands.Count} bands for mass {Mass}, UsesBandPlacement={UsesBandPlacement}"
        );
    }

    /// <summary>
    /// Increments the satellite count for the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when band index is invalid</exception>
    public void IncrementBandCount(int bandIndex)
    {
        if (bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bandIndex),
                $"Band index {bandIndex} out of range. Available bands: {OrbitBands.Count}"
            );
        }

        if (!_bandSatelliteCounts.ContainsKey(bandIndex))
        {
            _bandSatelliteCounts[bandIndex] = 0;
        }
        _bandSatelliteCounts[bandIndex]++;
    }

    /// <summary>
    /// Decrements the satellite count for the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when band index is invalid</exception>
    public void DecrementBandCount(int bandIndex)
    {
        if (bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bandIndex),
                $"Band index {bandIndex} out of range. Available bands: {OrbitBands.Count}"
            );
        }

        if (_bandSatelliteCounts.ContainsKey(bandIndex) && _bandSatelliteCounts[bandIndex] > 0)
        {
            _bandSatelliteCounts[bandIndex]--;
        }
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

    /// <summary>
    /// Gets the orbital radius for a specific orbit band index.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band (0-based).</param>
    /// <returns>Orbital radius in the same units as the body radius, or -1 if invalid.</returns>
    public float GetOrbitBandRadius(int bandIndex)
    {
        if (OrbitBands == null || bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            GameLogger.Warning(
                $"CelestialBody.GetOrbitBandRadius: Invalid band index {bandIndex} "
                    + $"(available: {OrbitBands?.Count ?? 0})"
            );
            return -1f;
        }

        return OrbitBands[bandIndex].Radius;
    }

    /// <summary>
    /// Gets the angular orbital speed for a specific orbit band.
    /// Inner bands orbit faster than outer bands.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band (0-based).</param>
    /// <returns>Angular speed in rad/s from physics calculation, or -1 if invalid.</returns>
    public float GetOrbitalSpeedForBand(int bandIndex)
    {
        if (OrbitBands == null || bandIndex < 0 || bandIndex >= OrbitBands.Count)
        {
            GameLogger.Warning(
                $"CelestialBody.GetOrbitalSpeedForBand: Invalid band index {bandIndex} "
                    + $"(available: {OrbitBands?.Count ?? 0})"
            );
            return -1f;
        }

        // Return the physics-derived angular speed from the band
        return OrbitBands[bandIndex].AngularSpeed;
    }

    /// <summary>
    /// Finds the closest orbit band index to a ship arriving from interplanetary space.
    /// Selects the band that minimizes the difference between the ship's approach velocity
    /// and the band's orbital velocity, reducing insertion delta-v.
    /// </summary>
    /// <param name="approachSpeed">The ship's speed relative to this body at arrival.</param>
    /// <returns>The optimal band index, or 0 if no bands available.</returns>
    public int GetClosestBandForApproach(float approachSpeed)
    {
        if (OrbitBands == null || OrbitBands.Count == 0)
        {
            return 0;
        }

        int bestBand = 0;
        float bestDifference = float.MaxValue;

        for (int i = 0; i < OrbitBands.Count; i++)
        {
            float bandRadius = OrbitBands[i].Radius;
            float bandAngularSpeed = GetOrbitalSpeedForBand(i);
            if (bandAngularSpeed < 0f)
                continue;

            float bandLinearSpeed = bandRadius * bandAngularSpeed;
            float difference = Mathf.Abs(approachSpeed - bandLinearSpeed);

            if (difference < bestDifference)
            {
                bestDifference = difference;
                bestBand = i;
            }
        }

        GameLogger.Debug(
            $"CelestialBody.GetClosestBandForApproach: approach={approachSpeed:F2} m/s, "
                + $"best band={bestBand}"
        );

        return bestBand;
    }

    public override void _PhysicsProcess(double delta)
    {
        // When NBodyCoordinator is active, it handles synchronized integration
        // for all CelestialBodies. Skip per-body physics to avoid double-updating.
        if (CoordinatorActive)
            return;

        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
        float totalMass = 0f;
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        float deltaT = (float)delta;
        foreach (CelestialBody body in bodies)
        {
            if (body != this)
            {
                Vector3 direction = (body.GlobalPosition - this.GlobalPosition);
                float distanceSq = direction.LengthSquared();

                // Guard against division by zero when bodies overlap or are at the
                // same position. Without this, distanceSq == 0 produces force = Inf,
                // and Inf * Normalized(Zero) = NaN, which permanently corrupts the
                // velocity and position of every body in the system.
                if (distanceSq < 1e-6f)
                    continue;

                float force = OrbitalMath.GRAVITATIONAL_CONSTANT * Mass * body.Mass / distanceSq;
                TotalForce += direction.Normalized() * force;
                totalMass += body.Mass;
            }
        }

        Vector3 acceleration = _savedForce / Mass;
        Velocity += 0.5f * acceleration * deltaT;
        GlobalPosition += Velocity * deltaT;
        Vector3 newAcceleration = TotalForce / Mass;
        Velocity += 0.5f * newAcceleration * deltaT;
        _savedForce = TotalForce;
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
        Action<CelestialBody>? onCompleted = null,
        Action<CelestialBody, string>? onFailed = null
    )
    {
        // Set classification before building meshParams so SubtypeGenParamResolver and
        // ConfigureFrom both see the final value.
        Mesh!.Classification = Classification;

        Godot.Collections.Dictionary meshParams = new Godot.Collections.Dictionary();
        // Check if custom mesh data is available in the body dictionary
        if (bodyDict != null)
        {
            meshParams.Add("type", bodyDict["type"]);
            meshParams.Add("name", bodyDict["name"]);
            if (
                bodyDict.ContainsKey("base_mesh")
                && bodyDict["base_mesh"].Obj is Godot.Collections.Dictionary customMesh
            )
            {
                CalculateBaseMeshFromParams(customMesh, meshParams);
            }
            if (
                bodyDict.ContainsKey("tectonics")
                && bodyDict["tectonics"].Obj is Godot.Collections.Dictionary tectonics
            )
            {
                CalculateTectonicMeshFromParams(tectonics, meshParams);
            }
            if (
                bodyDict.ContainsKey("spherical_harmonics_settings")
                && bodyDict["spherical_harmonics_settings"].Obj
                    is Godot.Collections.Dictionary shSettings
            )
            {
                CalculateSphericalHarmonicsFromParams(shSettings, meshParams);
            }
            if (
                bodyDict.ContainsKey("resources")
                && bodyDict["resources"].Obj is Godot.Collections.Dictionary resources
            )
            {
                meshParams.Add("resources", resources);
            }
            // Resource generation is now handled via ResourceGenerationConfigDatabase in the mesh pipeline
        }

        // Fill in any remaining mesh / tectonic / spherical-harmonic knobs from the body's
        // subtype ranges. Explicit overrides above take precedence (resolver skips keys that
        // are already present). Knobs with no range declared fall through to the mesh's
        // [Export] defaults.
        if (Classification != null)
        {
            var rng = ResolveBodyRng();
            // The Planet Generator preview scene sets _use_midpoint to render the centre of
            // each range instead of rolling — gives a stable preview after every YAML edit.
            bool useMidpoint = bodyDict != null
                && bodyDict.ContainsKey("_use_midpoint")
                && bodyDict["_use_midpoint"].AsBool();
            SubtypeGenParamResolver.ApplyMeshParams(meshParams, Classification, rng, useMidpoint);
            SubtypeGenParamResolver.ApplyTectonicParams(meshParams, Classification, rng, useMidpoint);
            SubtypeGenParamResolver.ApplySphericalHarmonicsParams(meshParams, Classification, rng, useMidpoint);
        }

        Mesh!.ConfigureFrom(StrDb, meshParams);

        Mesh.StartMeshGeneration(
            this,
            Oct,
            onCompleted: (mesh) =>
            {
                Radius = mesh.size;
                StrDb.FinalizeDB();
                onCompleted?.Invoke(this);
            },
            onFailed: (mesh, error) =>
            {
                GD.PrintErr($"Mesh generation failed for {meshParams["name"]}: {error}");
                onFailed?.Invoke(this, error);
            }
        );
    }

    public Point? FindNearest(Vector3 position)
    {
        var result = FindNearestCell(position);
        return result?.Point;
    }

    public CellSelectionResult? GetFaceFromIndex(int index)
    {
        var result = StrDb.GetCellFromIndex(index);
        if (result is null)
            return null;
        var continent = Mesh!.GetContinent(result.ContinentIndex);
        return new CellSelectionResult
        {
            Point = result.Points[0],
            Cell = result,
            CellContinent = continent,
        };
    }

    public VoronoiCell[] GetRuntimeCellNeighbors(
        VoronoiCell origin,
        bool includeSameContinent = true
    )
    {
        HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
        foreach (Point p in origin.Points)
        {
            if (!StrDb.PlanetMap.TryGetValue(p, out var neighborCells))
                continue;
            foreach (VoronoiCell vc in neighborCells)
            {
                if (includeSameContinent)
                {
                    neighbors.Add(vc);
                }
                else if (vc.ContinentIndex != origin.ContinentIndex)
                {
                    neighbors.Add(vc);
                }
            }
        }
        return neighbors.ToArray();
    }

    /// <summary>
    /// Gets or creates the camera anchor for this body.
    /// Anchor is created as a child Node3D named "CameraAnchor".
    /// </summary>
    /// <returns>The CameraAnchor node.</returns>
    public Node3D GetOrCreateCameraAnchor()
    {
        if (CameraAnchor == null)
        {
            CameraAnchor = new Node3D { Name = "CameraAnchor" };
            AddChild(CameraAnchor);
        }
        return CameraAnchor;
    }

    /// <summary>
    /// Positions the camera anchor at a world-space position looking at an optional target.
    /// If no target is provided, maintains the current look direction or defaults to looking at body center.
    /// </summary>
    /// <param name="worldPosition">The world-space position for the anchor.</param>
    /// <param name="lookAtTarget">Optional world-space target to look at. If null, looks at body center.</param>
    public void PositionCameraAnchor(Vector3 worldPosition, Vector3? lookAtTarget = null)
    {
        if (CameraAnchor == null)
            GetOrCreateCameraAnchor();

        CameraAnchor!.GlobalPosition = worldPosition;

        if (lookAtTarget.HasValue)
        {
            CameraAnchor.LookAt(lookAtTarget.Value);
            _cameraAnchorLookDir = (lookAtTarget.Value - worldPosition).Normalized();
        }
        else if (_cameraAnchorLookDir != Vector3.Zero)
        {
            // Maintain current look direction
            CameraAnchor.LookAt(worldPosition + _cameraAnchorLookDir);
        }
        else
        {
            // Default to looking at body center
            CameraAnchor.LookAt(GlobalPosition);
            _cameraAnchorLookDir = (GlobalPosition - worldPosition).Normalized();
        }

        GameLogger.Debug(
            $"CameraAnchor positioned at {worldPosition}"
                + (lookAtTarget.HasValue ? $" looking at {lookAtTarget.Value}" : "")
        );
    }

    /// <summary>
    /// Rotates the camera anchor around its current position using spherical coordinates.
    /// </summary>
    /// <param name="yaw">Yaw angle in radians (horizontal rotation around Y axis).</param>
    /// <param name="pitch">Pitch angle in radians (vertical rotation around X axis).</param>
    public void UpdateCameraAnchorRotation(float yaw, float pitch)
    {
        if (CameraAnchor == null)
            return;

        Vector3 currentPos = CameraAnchor.GlobalPosition;

        // Convert spherical coordinates to a direction vector
        float cosPitch = Mathf.Cos(pitch);
        Vector3 direction = new Vector3(
            Mathf.Sin(yaw) * cosPitch,
            Mathf.Sin(pitch),
            Mathf.Cos(yaw) * cosPitch
        );

        // Apply rotation to the anchor around its position
        // We rotate the anchor's transform so that the -Z axis points in the new direction
        CameraAnchor.LookAt(currentPos + direction);
        _cameraAnchorLookDir = direction;

        GameLogger.Debug(
            $"CameraAnchor rotated: yaw={Mathf.RadToDeg(yaw):F1}°, pitch={Mathf.RadToDeg(pitch):F1}°"
        );
    }

    /// <summary>
    /// Positions the inspection camera to focus on a specific cell.
    /// Camera is placed along the cell normal, close to the surface.
    /// Uses local Position so the camera tracks the body's orbital movement.
    /// </summary>
    /// <param name="cell">The VoronoiCell to focus on</param>
    public void FocusInspectionCameraOnCell(VoronoiCell cell)
    {
        if (CameraAnchor == null)
            GetOrCreateCameraAnchor();

        Vector3 cellCenter = cell.Center;
        Vector3 normal = cellCenter.Normalized();
        float offset = cellCenter.Length() * 1.3f;

        // Transform to global space accounting for body rotation
        Vector3 camLocation = Position + normal * offset;

        // Position camera anchor above the cell along its outward normal
        PositionCameraAnchor(camLocation, cellCenter + GlobalPosition);
    }

    /// <summary>
    /// Positions the inspection camera to focus on a specific continent.
    /// Camera distance is calculated based on the continent's angular size to ensure
    /// the entire continent is visible within the viewport.
    /// </summary>
    /// <param name="continent">The Continent to focus on</param>
    public void FocusInspectionCameraOnContinent(Continent continent)
    {
        if (CameraAnchor == null && continent == null)
            return;

        // Get normalized center direction (averagedCenter is already normalized)
        Vector3 center = continent.averagedCenter.Normalized();

        // Get actual body radius from a cell center (averagedCenter.Length() is 1.0
        // since it was normalized during generation)
        float bodyRadius = continent.cells.Count > 0 ? continent.cells[0].Center.Length() : 1.0f;

        // Calculate max angular radius from center to boundary cells
        float maxAngle = 0f;
        foreach (var cell in continent.boundaryCells)
        {
            Vector3 cellDir = cell.Center.Normalized();
            float angle = Mathf.Acos(Mathf.Clamp(center.Dot(cellDir), -1f, 1f));
            maxAngle = Mathf.Max(maxAngle, angle);
        }

        // Add 15% margin for visual comfort
        float angularRadius = maxAngle * 1.15f;

        // Calculate camera distance based on FOV and angular size
        float fovRad = Mathf.DegToRad(30.0f); // Default FOV
        float distance = bodyRadius * (1.0f + Mathf.Sin(angularRadius) / Mathf.Tan(fovRad / 2f));

        // Ensure minimum distance
        distance = Mathf.Max(distance, bodyRadius * 1.3f);

        // Position and orient camera anchor
        Vector3 camPosition = Position + center * distance;
        PositionCameraAnchor(camPosition, center * bodyRadius + GlobalPosition);

        GameLogger.Info(
            $"InspectionCamera positioned for continent {continent.StartingIndex} at distance {distance:F2}"
        );
    }

    public CellSelectionResult? FindNearestCell(Vector3 position)
    {
        Vector3 localSpace = (position - this.GlobalPosition);
        Point desired = new Point(localSpace, 0);
        Point? result = Oct.FindNearest(desired);
        if (result is null)
            return null;
        PolygonRendererSDL.DrawPoint(this, 1, result.ToVector3(), 0.05f, Colors.Red);

        var cells = StrDb.PlanetMap[result];

        // Use angular distance (dot product of normalized directions) to find the
        // closest Voronoi cell center. This is curvature-invariant and works correctly
        // for cells at any position on the sphere, unlike the previous AABB approach
        // which degraded near the sphere edges due to axis-aligned boxes poorly
        // approximating curved surface regions.
        Vector3 desiredDir = desired.Position.Normalized();
        float bestDot = -2f;
        VoronoiCell? bestCell = null;
        foreach (var cell in cells)
        {
            Vector3 cellDir = cell.Center.Normalized();
            float dot = desiredDir.Dot(cellDir);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestCell = cell;
            }
        }
        if (bestCell == null)
            return null;
        var continent = Mesh!.GetContinent(bestCell.ContinentIndex);
        return new CellSelectionResult
        {
            Point = result,
            Cell = bestCell,
            CellContinent = continent,
        };
    }

    // CellSelectionResult is now a shared class in ISelectableBody.cs

    public MeshInstance3D CreateDebugWireframe(Aabb aabb)
    {
        Vector3[] corners = new Vector3[]
        {
            aabb.GetEndpoint(0),
            aabb.GetEndpoint(1),
            aabb.GetEndpoint(2),
            aabb.GetEndpoint(3),
            aabb.GetEndpoint(4),
            aabb.GetEndpoint(5),
            aabb.GetEndpoint(6),
            aabb.GetEndpoint(7),
        };
        var lineVertices = new Vector3[]
        {
            // Bottom face
            corners[0],
            corners[1],
            corners[1],
            corners[5],
            corners[5],
            corners[4],
            corners[4],
            corners[0],
            // Top face
            corners[2],
            corners[3],
            corners[3],
            corners[7],
            corners[7],
            corners[6],
            corners[6],
            corners[2],
            // Vertical edges
            corners[0],
            corners[2],
            corners[1],
            corners[3],
            corners[4],
            corners[6],
            corners[5],
            corners[7],
        };

        // 3. Create the ArrayMesh
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = lineVertices;

        // 4. Add the vertices as a surface with a line primitive type
        mesh.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Lines, arrays);

        // 5. Create the MeshInstance3D node
        var meshInstance = new MeshInstance3D { Mesh = mesh, Name = "AABB_Wireframe" };

        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            // Use unshaded mode to make the color constant regardless of lighting
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
        };
        meshInstance.MaterialOverride = material;

        return meshInstance;
    }

    /// <summary>
    /// Returns the per-body RNG used by <see cref="SubtypeGenParamResolver"/> to roll subtype
    /// ranges. If <see cref="BodySeed"/> is non-zero the RNG is seeded from it (deterministic,
    /// so editor re-rolls reproduce identical bodies); otherwise the global <see cref="Randomizer"/>
    /// instance is returned (non-deterministic, system-level).
    /// </summary>
    private RandomNumberGenerator ResolveBodyRng()
    {
        if (BodySeed != 0UL)
        {
            return new RandomNumberGenerator { Seed = BodySeed };
        }
        return Randomizer.GetRandomNumberGenerator();
    }

    private void CalculateTectonicMeshFromParams(
        Godot.Collections.Dictionary definedMesh,
        Godot.Collections.Dictionary meshParams
    )
    {
        var rng = UtilityLibrary.Randomizer.GetRandomNumberGenerator();
        var tectDict = new Godot.Collections.Dictionary();
        int[] numContinents = (int[])definedMesh["num_continents"];
        tectDict.Add("num_continents", rng.RandiRange(numContinents[0], numContinents[1]));
        float[] stressScale = (float[])definedMesh["stress_scale"];
        tectDict.Add("stress_scale", rng.RandfRange(stressScale[0], stressScale[1]));
        float[] shearScale = (float[])definedMesh["shear_scale"];
        tectDict.Add("shear_scale", rng.RandfRange(shearScale[0], shearScale[1]));
        float[] maxPropagationDistance = (float[])definedMesh["max_propagation_distance"];
        tectDict.Add(
            "max_propagation_distance",
            rng.RandfRange(maxPropagationDistance[0], maxPropagationDistance[1])
        );
        float[] propagationFalloff = (float[])definedMesh["propagation_falloff"];
        tectDict.Add(
            "propagation_falloff",
            rng.RandfRange(propagationFalloff[0], propagationFalloff[1])
        );
        float[] inactiveStressThreshold = (float[])definedMesh["inactive_stress_threshold"];
        tectDict.Add(
            "inactive_stress_threshold",
            rng.RandfRange(inactiveStressThreshold[0], inactiveStressThreshold[1])
        );
        float[] generalHeightScale = (float[])definedMesh["general_height_scale"];
        tectDict.Add(
            "general_height_scale",
            rng.RandfRange(generalHeightScale[0], generalHeightScale[1])
        );
        float[] generalShearScale = (float[])definedMesh["general_shear_scale"];
        tectDict.Add(
            "general_shear_scale",
            rng.RandfRange(generalShearScale[0], generalShearScale[1])
        );
        float[] generalCompressionScale = (float[])definedMesh["general_compression_scale"];
        tectDict.Add(
            "general_compression_scale",
            rng.RandfRange(generalCompressionScale[0], generalCompressionScale[1])
        );
        float[] generalTransformScale = (float[])definedMesh["general_transform_scale"];
        tectDict.Add(
            "general_transform_scale",
            rng.RandfRange(generalTransformScale[0], generalTransformScale[1])
        );
        meshParams.Add("tectonic", tectDict);
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

    private Godot.Collections.Dictionary ConvertCustomMeshToParams(
        Godot.Collections.Dictionary customMesh
    )
    {
        var meshParams = new Godot.Collections.Dictionary();
        // Convert custom mesh data to the format expected by Mesh.ConfigureFrom
        if (
            customMesh.ContainsKey("base_mesh")
            && customMesh["base_mesh"].Obj is Godot.Collections.Dictionary baseMesh
        )
        {
            meshParams["subdivisions"] = baseMesh["subdivisions"];
            meshParams["num_abberations"] = baseMesh["num_abberations"];
            meshParams["num_deformation_cycles"] = baseMesh["num_deformation_cycles"];
            meshParams["vertices_per_edge"] = baseMesh["vertices_per_edge"];
        }
        if (
            customMesh.ContainsKey("tectonic")
            && customMesh["tectonic"].Obj is Godot.Collections.Dictionary tectonic
        )
        {
            meshParams["tectonic"] = tectonic;
        }
        // Add other sections if present (scaling, noise_settings, etc.)
        if (customMesh.ContainsKey("scaling"))
        {
            meshParams["scaling"] = customMesh["scaling"];
        }
        if (customMesh.ContainsKey("noise_settings"))
        {
            meshParams["noise_settings"] = customMesh["noise_settings"];
        }
        return meshParams;
    }

    #region TransferEndpointRegistry

    public void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, GodotObject owner)
    {
        if (string.IsNullOrEmpty(endpointId))
            return;

        _endpointDefs[endpointId] = def;
        _endpointOwners[endpointId] = owner;

        int continentIdx = owner is Building b ? b.PrimaryCell?.ContinentIndex ?? -1 : -1;
        SignalBus.Instance?.EmitContinentTransferCapacityChanged(
            continentIdx,
            GetTotalTransferCapacityOnContinent(continentIdx)
        );

        GameLogger.Info(
            $"[CelestialBody] Endpoint '{endpointId[..System.Math.Min(8, endpointId.Length)]}' "
                + $"registered (capacity: {def.CargoCapacity:F0})"
        );
    }

    public void UnregisterTransferEndpoint(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return;

        int? continentIdx = _endpointOwners.TryGetValue(endpointId, out var owner)
            ? (owner as Building)?.PrimaryCell?.ContinentIndex
            : null;

        _endpointDefs.Remove(endpointId);
        _endpointOwners.Remove(endpointId);

        if (continentIdx.HasValue)
            SignalBus.Instance?.EmitContinentTransferCapacityChanged(
                continentIdx.Value,
                GetTotalTransferCapacityOnContinent(continentIdx.Value)
            );
    }

    public bool HasTransferEndpoint(string endpointId)
    {
        return !string.IsNullOrEmpty(endpointId) && _endpointDefs.ContainsKey(endpointId);
    }

    public TransferStationDefinition? GetTransferEndpointDef(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return null;
        _endpointDefs.TryGetValue(endpointId, out var def);
        return def;
    }

    [Obsolete("Use GetTransferEndpointOwner instead")]
    public Building? GetTransferEndpointBuilding(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return null;
        _endpointOwners.TryGetValue(endpointId, out var owner);
        return owner as Building;
    }

    public GodotObject? GetTransferEndpointOwner(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return null;
        _endpointOwners.TryGetValue(endpointId, out var owner);
        return owner;
    }

    public IReadOnlyList<string> GetAllTransferEndpoints()
    {
        return new List<string>(_endpointDefs.Keys);
    }

    public IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex)
    {
        var list = new List<string>();
        foreach (var kvp in _endpointOwners)
        {
            if (kvp.Value is Building b && b.PrimaryCell?.ContinentIndex == continentIndex)
                list.Add(kvp.Key);
        }
        return list;
    }

    public float GetTotalTransferCapacityOnContinent(int continentIndex)
    {
        if (continentIndex < 0)
            return 0f;
        float total = 0f;
        foreach (var id in GetTransferEndpointsOnContinent(continentIndex))
        {
            if (_endpointDefs.TryGetValue(id, out var def))
                total += def.CargoCapacity;
        }
        return total;
    }

    #endregion

    public class Builder
    {
        internal Vector3? _velocity;
        internal float? _mass;
        internal BodyClassification? _classification;
        internal UnifiedCelestialMesh? _mesh;
        internal Godot.Collections.Dictionary? _bodyDict;
        internal string? _name;

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

        public Builder WithClassification(BodyClassification classification)
        {
            _classification = classification;
            return this;
        }

        public Builder WithName(string name)
        {
            _name = name;
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

        public Builder FromBodyDict(
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
        )
        {
            _bodyDict = bodyDict;
            _mesh = mesh;

            if (bodyDict != null)
            {
                var baseTemplates = (Godot.Collections.Dictionary)bodyDict["template"];
                var type = (String)bodyDict["type"];
                var mass = (float)baseTemplates["mass"];
                var velocity = (Vector3)baseTemplates["velocity"];

                var celestialBodyType = (CelestialBodyType)
                    Enum.Parse(typeof(CelestialBodyType), type);
                _classification = BodyClassification.FromLegacy(celestialBodyType, null);
                _mass = mass;
                _velocity = velocity;
                _name = (String)bodyDict["name"];

                if (mesh != null)
                {
                    var size = Mathf.RoundToInt((float)baseTemplates["size"]);
                    mesh.size = size;
                }
            }

            return this;
        }

        private void ValidateRequiredFields()
        {
            if (!_velocity.HasValue)
                throw new InvalidOperationException("Velocity is required");
            if (!_mass.HasValue)
                throw new InvalidOperationException("Mass is required");
            if (_classification == null)
                throw new InvalidOperationException("Classification is required");
            if (_mesh == null)
                throw new InvalidOperationException("Mesh is required");
        }

        public CelestialBody Build()
        {
            ValidateRequiredFields();
            return new CelestialBody(this);
        }

        public static CelestialBody BuildFromBodyDict(
            Godot.Collections.Dictionary bodyDict,
            UnifiedCelestialMesh mesh
        )
        {
            return new Builder().FromBodyDict(bodyDict, mesh).Build();
        }
    }
}
