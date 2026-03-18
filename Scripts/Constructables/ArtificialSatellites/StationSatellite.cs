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
    public bool IsActive { get; set; } = true;

    private float _orbitalAngle;
    private float _orbitalRadius;
    private float _orbitalSpeed;
    private float _bodyRadius;
    private bool _isInitialized;

    private const float DefaultOrbitalSpeed = 0.5f;

    // Visual components
    private MeshInstance3D? _meshInstance;
    private float _rotationSpeed = 0.5f;

    public void Initialize(Node3D parentBody, int bandIndex)
    {
        // Reparent to the specified body if needed
        if (GetParent() != parentBody)
        {
            GetParent()?.RemoveChild(this);
            parentBody.AddChild(this);
        }

        this.BandIndex = bandIndex;
        this.Id = Guid.NewGuid().ToString();

        // Calculate orbital parameters based on band index
        CalculateOrbitalParameters();

        // Random starting angle for variety
        var rand = Randomizer.GetRandomNumberGenerator();
        _orbitalAngle = rand.RandfRange(0f, Mathf.Tau);

        _isInitialized = true;

        GameLogger.Debug(
            $"StationSatellite initialized: {Name}, Band {BandIndex}, Radius {_orbitalRadius}"
        );
    }

    private void CalculateOrbitalParameters()
    {
        Node3D? parentBody = GetParent<Node3D>();
        if (parentBody == null)
        {
            GameLogger.Warning(
                "StationSatellite: Cannot calculate orbital parameters without parent body"
            );
            return;
        }

        // Get body radius from parent's scale (assuming sphere)
        _bodyRadius = parentBody.Scale.X;

        // Try to get the actual band radius from parent's OrbitBands
        // CelestialBody and SatelliteBody both have OrbitBands property
        var orbitBands = GetOrbitBandsFromParent(parentBody);

        if (orbitBands != null && BandIndex >= 0 && BandIndex < orbitBands.Count)
        {
            // Use the actual band radius that was pre-calculated
            _orbitalRadius = orbitBands[BandIndex].Radius;
        }
        else
        {
            // Fallback to old behavior if we can't get the bands (shouldn't happen normally)
            float[] bandMultipliers = OrbitConfiguration.GetDefaultBandMultipliers(4);
            int clampedBand = Mathf.Clamp(BandIndex, 0, bandMultipliers.Length - 1);
            _orbitalRadius = _bodyRadius * bandMultipliers[clampedBand];
            GameLogger.Warning($"StationSatellite: Could not access orbit bands, using fallback calculation");
        }

        // Calculate orbital speed based on physics
        // Using simplified orbital velocity: v = sqrt(GM/r)
        // For simplicity, using base orbital speed scaled by band
        float baseOrbitalSpeed = DefaultOrbitalSpeed;

        // Inner bands orbit faster than outer bands
        int clampedBandForSpeed = Mathf.Clamp(BandIndex, 0, 3);
        _orbitalSpeed = baseOrbitalSpeed / (1f + clampedBandForSpeed * 0.5f);

        GameLogger.Debug($"Orbital params: Radius={_orbitalRadius}, Speed={_orbitalSpeed}");
    }

    /// <summary>
    /// Helper method to get OrbitBands from either CelestialBody or SatelliteBody.
    /// Uses dynamic to avoid circular dependency between assemblies.
    /// </summary>
    private Godot.Collections.Array<OrbitBand>? GetOrbitBandsFromParent(Node3D parent)
    {
        try
        {
            // Both CelestialBody and SatelliteBody have an OrbitBands property
            // Use dynamic to avoid compile-time circular dependency
            dynamic parentDynamic = parent;
            return parentDynamic.OrbitBands;
        }
        catch (Exception e)
        {
            GameLogger.Warning($"StationSatellite: Could not access orbit bands from parent: {e.Message}");
            return null;
        }
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
        // Using simple circular orbit in XZ plane
        float offsetX = Mathf.Cos(_orbitalAngle) * _orbitalRadius;
        float offsetZ = Mathf.Sin(_orbitalAngle) * _orbitalRadius;

        // Set position relative to parent
        GlobalPosition = parentBody.GlobalPosition + new Vector3(offsetX, 0, offsetZ);
    }

    public void OnDestroy()
    {
        GameLogger.Debug($"StationSatellite destroying: {Name}");

        _isInitialized = false;
    }

    public override void _ExitTree()
    {
        OnDestroy();
        base._ExitTree();
    }

    public override void _Ready()
    {
        base._Ready();

        // Create visual representation
        CreateVisualRepresentation();

        // If initialized before _Ready (via scene instantiation), recalculate
        Node3D? parentBody = GetParent<Node3D>();
        if (parentBody != null && !_isInitialized && BandIndex >= 0)
        {
            CalculateOrbitalParameters();

            var rand = Randomizer.GetRandomNumberGenerator();
            _orbitalAngle = rand.RandfRange(0f, Mathf.Tau);

            _isInitialized = true;
        }
    }

    private void CreateVisualRepresentation()
    {
        // Create MeshInstance3D for visual representation
        _meshInstance = new MeshInstance3D
        {
            Name = "StationMesh"
        };

        // Create a cylinder mesh (stations look like cylinders)
        var cylinderMesh = new CylinderMesh
        {
            TopRadius = 0.3f,
            BottomRadius = 0.5f,
            Height = 1.0f,
            RadialSegments = 16,
            Rings = 4
        };
        _meshInstance.Mesh = cylinderMesh;

        // Create a blue/gray material for the station
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.5f, 0.6f), // Blue-gray color
            Metallic = 0.7f,
            Roughness = 0.3f
        };
        _meshInstance.MaterialOverride = material;

        // Add mesh instance as child
        AddChild(_meshInstance);

        // Add a second smaller cylinder on top for antenna/tower look
        var topMesh = new MeshInstance3D
        {
            Name = "StationAntenna"
        };
        var topCylinder = new CylinderMesh
        {
            TopRadius = 0.1f,
            BottomRadius = 0.2f,
            Height = 0.5f,
            RadialSegments = 12,
            Rings = 2
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
