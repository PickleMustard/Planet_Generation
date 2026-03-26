using System;
using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

public partial class StationSatellite : Node3D, IArtificialSatellite
{
    [Export]
    public string Id { get; private set; } = string.Empty;

    [Export]
    public int BandIndex { get; set; }

    [Export]
    public bool IsStationary { get; set; } = true;

    [Export]
    public bool IsActive { get; set; } = true;

    [Export]
    public Vector3 Velocity
    {
        get => _velocity;
        set => _velocity = value;
    }

    [Export]
    public Vector3 UnitPosition
    {
        get => Position;
        set => Position = value;
    }

    // Visual components
    private MeshInstance3D? _meshInstance;
    private float _rotationSpeed = 0.5f;

    #region OrbitalParameters

    /// <summary>Current orbital angle in radians.</summary>
    public float OrbitalAngle => _orbitalAngle;

    /// <summary>Current orbital radius in meters.</summary>
    public float OrbitalRadius => _orbitalRadius;

    /// <summary>Current orbital angular speed in rad/s.</summary>
    public float OrbitalSpeed => _orbitalSpeed;

    // Orbital state fields
    private float _orbitalAngle;
    private float _orbitalRadius;
    private float _orbitalSpeed;
    private float _hostMass;
    private bool _isInitialized;
    private Vector3 _velocity;

    /// <summary>
    /// Initializes the station's orbit using the host body's orbital parameters.
    /// Uses band-based or continuous placement based on the host body's configuration.
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="bandIndex">Index of the orbit band (for band-based bodies).</param>
    public void InitializeOrbit(IOrbitalBody hostBody, int bandIndex)
    {
        if (hostBody == null)
        {
            GameLogger.Warning("StationSatellite: Cannot initialize orbit - host body is null");
            return;
        }

        // Get random starting angle
        var rand = Randomizer.GetRandomNumberGenerator();
        float startingAngle = rand.RandfRange(0f, Mathf.Tau);

        OrbitalParameters parameters;

        if (hostBody.UsesBandPlacement)
        {
            // Band-based placement
            parameters = hostBody.GetOrbitalParametersForBand(bandIndex, startingAngle);
            this.BandIndex = bandIndex;
        }
        else
        {
            // Continuous placement - calculate radius from band index as a fallback
            float radius = CalculateRadiusForBand(hostBody, bandIndex);
            parameters = hostBody.GetOrbitalParametersAtRadius(radius, startingAngle);
            this.BandIndex = -1;
        }

        // Store orbital parameters
        _orbitalRadius = parameters.Radius;
        _orbitalSpeed = parameters.AngularSpeed;
        _orbitalAngle = startingAngle;
        _hostMass = parameters.HostMass;
        _velocity = parameters.InitialVelocity;

        // Set initial position
        GlobalPosition =
            (GetParent<Node3D>()?.GlobalPosition ?? Vector3.Zero) + parameters.InitialPosition;

        _isInitialized = true;

        GameLogger.Debug(
            $"StationSatellite initialized: {Name}, Band {BandIndex}, Radius {_orbitalRadius:F2}, Speed {_orbitalSpeed:F6}"
        );
    }

    /// <summary>
    /// Initializes the station's orbit at a specific radius (for continuous placement).
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="radius">Desired orbital radius in meters.</param>
    public void InitializeOrbitAtRadius(IOrbitalBody hostBody, float radius)
    {
        if (hostBody == null)
        {
            GameLogger.Warning("StationSatellite: Cannot initialize orbit - host body is null");
            return;
        }

        // Get random starting angle
        var rand = Randomizer.GetRandomNumberGenerator();
        float startingAngle = rand.RandfRange(0f, Mathf.Tau);

        OrbitalParameters parameters = hostBody.GetOrbitalParametersAtRadius(radius, startingAngle);

        // Store orbital parameters
        _orbitalRadius = parameters.Radius;
        _orbitalSpeed = parameters.AngularSpeed;
        _orbitalAngle = startingAngle;
        _hostMass = parameters.HostMass;
        _velocity = parameters.InitialVelocity;
        this.BandIndex = -1;

        // Set initial position
        GlobalPosition =
            (GetParent<Node3D>()?.GlobalPosition ?? Vector3.Zero) + parameters.InitialPosition;

        _isInitialized = true;

        GameLogger.Debug(
            $"StationSatellite initialized at radius: {Name}, Radius {_orbitalRadius:F2}, Speed {_orbitalSpeed:F6}"
        );
    }

    /// <summary>
    /// Backward-compatible wrapper that casts Node3D to IOrbitalBody and calls InitializeOrbit.
    /// </summary>
    /// <param name="parentBody">The parent body to orbit around.</param>
    /// <param name="bandIndex">Index of the orbit band.</param>
    public void Initialize(Node3D parentBody, int bandIndex)
    {
        if (parentBody is not IOrbitalBody orbitalBody)
        {
            GameLogger.Warning(
                $"StationSatellite: Parent body {parentBody?.Name} does not implement IOrbitalBody"
            );
            return;
        }

        // Ensure we're parented to the body
        if (GetParent() != parentBody)
        {
            GetParent()?.RemoveChild(this);
            parentBody.AddChild(this);
        }

        InitializeOrbit(orbitalBody, bandIndex);
    }

    /// <summary>
    /// Calculates a fallback radius for band index on continuous-placement bodies.
    /// </summary>
    private float CalculateRadiusForBand(IOrbitalBody hostBody, int bandIndex)
    {
        float bodyRadius = hostBody.Radius;
        float[] multipliers = Structures.GameState.OrbitConfiguration.GetDefaultBandMultipliers(4);
        int clampedBand = Mathf.Clamp(bandIndex, 0, multipliers.Length - 1);
        return bodyRadius * multipliers[clampedBand];
    }

    #endregion


    #region GodotBuiltin

    public override void _EnterTree()
    {
        this.Id = Guid.NewGuid().ToString();
    }

    public override void _ExitTree()
    {
        GameLogger.Debug($"StationSatellite destroying: {Name}");
        _isInitialized = false;
        base._ExitTree();
    }

    public override void _Ready()
    {
        base._Ready();

        // Create visual representation
        CreateVisualRepresentation();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isInitialized || !IsActive)
            return;

        Node3D? parentBody = GetParent<Node3D>();
        if (parentBody == null)
            return;

        // Update orbital angle
        _orbitalAngle += _orbitalSpeed * (float)delta;

        // Keep angle in valid range [0, 2*PI]
        if (_orbitalAngle > Mathf.Tau)
            _orbitalAngle -= Mathf.Tau;

        // Calculate position: parent position + orbital offset
        float cos = Mathf.Cos(_orbitalAngle);
        float sin = Mathf.Sin(_orbitalAngle);

        // Position in XZ plane
        GlobalPosition =
            parentBody.GlobalPosition + new Vector3(cos * _orbitalRadius, 0, sin * _orbitalRadius);

        // Calculate and store velocity (tangent to orbit)
        float linearSpeed = _orbitalRadius * _orbitalSpeed;
        _velocity = new Vector3(-sin * linearSpeed, 0f, cos * linearSpeed);
    }

    #endregion

    private void CreateVisualRepresentation()
    {
        // Create MeshInstance3D for visual representation
        _meshInstance = new MeshInstance3D { Name = "StationMesh" };

        // Create a cylinder mesh (stations look like cylinders)
        var cylinderMesh = new CylinderMesh
        {
            TopRadius = 0.3f,
            BottomRadius = 0.5f,
            Height = 1.0f,
            RadialSegments = 16,
            Rings = 4,
        };
        _meshInstance.Mesh = cylinderMesh;

        // Create a blue/gray material for the station
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.5f, 0.6f), // Blue-gray color
            Metallic = 0.7f,
            Roughness = 0.3f,
        };
        _meshInstance.MaterialOverride = material;

        // Add mesh instance as child
        AddChild(_meshInstance);

        // Add a second smaller cylinder on top for antenna/tower look
        var topMesh = new MeshInstance3D { Name = "StationAntenna" };
        var topCylinder = new CylinderMesh
        {
            TopRadius = 0.1f,
            BottomRadius = 0.2f,
            Height = 0.5f,
            RadialSegments = 12,
            Rings = 2,
        };
        topMesh.Mesh = topCylinder;
        topMesh.MaterialOverride = material;
        topMesh.Position = new Vector3(0, 0.6f, 0);

        _meshInstance.AddChild(topMesh);

        GameLogger.Debug($"StationSatellite visuals created for: {Name}");
    }

    public override void _Process(double delta)
    {
        // Rotate the station for visual interest
        if (_meshInstance != null && IsActive)
        {
            _meshInstance.RotateY(_rotationSpeed * (float)delta);
        }
    }
}
