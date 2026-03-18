using System;
using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

public partial class ShipSatellite : Node3D, IArtificialSatellite
{
    [Export]
    public string Id { get; private set; } = string.Empty;

    [Export]
    public int BandIndex { get; set; }

    [Export]
    public bool IsActive { get; set; } = true;

    // Travel state
    private bool _isTraveling;
    private Node3D? _destinationBody;
    private float _travelSpeed = 10.0f;

    // Orbital state
    private float _orbitalAngle;
    private float _orbitalRadius;
    private float _orbitalSpeed;
    private float _bodyRadius;
    private bool _isInitialized;

    private const float DefaultOrbitalSpeed = 0.5f;

    // Visual components
    private MeshInstance3D? _meshInstance;
    private float _rotationSpeed = 1.0f;

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
        _isTraveling = false;

        GameLogger.Debug(
            $"ShipSatellite initialized: {Name}, Band {BandIndex}, Radius {_orbitalRadius}"
        );
    }

    private void CalculateOrbitalParameters()
    {
        Node3D? parentBody = GetParent<Node3D>();
        if (parentBody == null)
        {
            GameLogger.Warning(
                "ShipSatellite: Cannot calculate orbital parameters without parent body"
            );
            return;
        }

        // Get body radius from parent's scale (assuming sphere)
        _bodyRadius = parentBody.Scale.X;

        // Try to get the actual band radius from parent's OrbitBands
        var orbitBands = GetOrbitBandsFromParent(parentBody);

        if (orbitBands != null && BandIndex >= 0 && BandIndex < orbitBands.Count)
        {
            // Use the actual band radius that was pre-calculated
            _orbitalRadius = orbitBands[BandIndex].Radius;
        }
        else
        {
            // Fallback to default behavior
            float[] bandMultipliers = OrbitConfiguration.GetDefaultBandMultipliers(4);
            int clampedBand = Mathf.Clamp(BandIndex, 0, bandMultipliers.Length - 1);
            _orbitalRadius = _bodyRadius * bandMultipliers[clampedBand];
            GameLogger.Warning($"ShipSatellite: Could not access orbit bands, using fallback calculation");
        }

        // Calculate orbital speed based on band
        float baseOrbitalSpeed = DefaultOrbitalSpeed;

        // Inner bands orbit faster than outer bands
        int clampedBandForSpeed = Mathf.Clamp(BandIndex, 0, 3);
        _orbitalSpeed = baseOrbitalSpeed / (1f + clampedBandForSpeed * 0.5f);

        GameLogger.Debug($"ShipSatellite orbital params: Radius={_orbitalRadius}, Speed={_orbitalSpeed}");
    }

    /// <summary>
    /// Helper method to get OrbitBands from either CelestialBody or SatelliteBody.
    /// </summary>
    private Godot.Collections.Array<OrbitBand>? GetOrbitBandsFromParent(Node3D parent)
    {
        try
        {
            dynamic parentDynamic = parent;
            return parentDynamic.OrbitBands;
        }
        catch (Exception e)
        {
            GameLogger.Warning($"ShipSatellite: Could not access orbit bands from parent: {e.Message}");
            return null;
        }
    }

    public void InitiateTravel(Node3D destinationBody, float speed)
    {
        if (destinationBody == null)
        {
            GameLogger.Warning("ShipSatellite: Cannot initiate travel - destination body is null");
            return;
        }

        _destinationBody = destinationBody;
        _travelSpeed = speed;
        _isTraveling = true;

        GameLogger.Info($"ShipSatellite {Name} initiating travel to {destinationBody.Name} at speed {speed}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isInitialized || !IsActive)
            return;

        if (_isTraveling)
        {
            HandleTravel(delta);
        }
        else
        {
            HandleOrbit(delta);
        }
    }

    public void HandleTravel(double delta)
    {
        if (_destinationBody == null)
        {
            GameLogger.Warning("ShipSatellite: Traveling but destination body is null");
            _isTraveling = false;
            return;
        }

        // Calculate direction to destination
        Vector3 direction = (_destinationBody.GlobalPosition - GlobalPosition).Normalized();

        // Move toward destination
        GlobalPosition += direction * _travelSpeed * (float)delta;

        // Check if we've arrived (close enough to destination)
        float distanceToDestination = GlobalPosition.DistanceTo(_destinationBody.GlobalPosition);

        // If we're close enough, re-enter orbit around the destination
        if (distanceToDestination <= _destinationBody.Scale.X * 1.5f)
        {
            GameLogger.Info($"ShipSatellite {Name} arrived at {_destinationBody.Name}");

            // Reparent to the destination body
            Node3D? currentParent = GetParent<Node3D>();
            if (currentParent != null)
            {
                currentParent.RemoveChild(this);
            }
            _destinationBody.AddChild(this);

            // Recalculate orbital parameters for the new parent
            CalculateOrbitalParameters();

            // Random starting angle at new location
            var rand = Randomizer.GetRandomNumberGenerator();
            _orbitalAngle = rand.RandfRange(0f, Mathf.Tau);

            _isTraveling = false;
            _destinationBody = null;

            GameLogger.Debug($"ShipSatellite {Name} entered orbit around {_destinationBody?.Name ?? "unknown"}");
        }
    }

    public void HandleOrbit(double delta)
    {
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
        GameLogger.Debug($"ShipSatellite destroying: {Name}");

        _isInitialized = false;
        _isTraveling = false;
        _destinationBody = null;
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
            Name = "ShipMesh"
        };

        // Create a box mesh (ships look like elongated boxes)
        var boxMesh = new BoxMesh
        {
            Size = new Vector3(0.5f, 0.2f, 1.0f),
            SubdivideWidth = 2,
            SubdivideHeight = 1,
            SubdivideDepth = 3
        };
        _meshInstance.Mesh = boxMesh;

        // Create a metallic material for the ship
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.7f, 0.8f), // Silver/light gray
            Metallic = 0.8f,
            Roughness = 0.2f
        };
        _meshInstance.MaterialOverride = material;

        // Add mesh instance as child
        AddChild(_meshInstance);

        // Add a small cone at the front for a pointed nose
        var noseMesh = new MeshInstance3D
        {
            Name = "ShipNose"
        };
        var coneMesh = new CylinderMesh
        {
            TopRadius = 0f,
            BottomRadius = 0.15f,
            Height = 0.3f,
            RadialSegments = 8,
            Rings = 2
        };
        noseMesh.Mesh = coneMesh;
        noseMesh.MaterialOverride = material;
        noseMesh.Position = new Vector3(0, 0, -0.6f);
        noseMesh.RotationDegrees = new Vector3(90, 0, 0);

        _meshInstance.AddChild(noseMesh);

        GameLogger.Debug($"ShipSatellite visuals created for: {Name}");
    }

    public override void _Process(double delta)
    {
        // Rotate the ship for visual interest
        if (_meshInstance != null && IsActive)
        {
            _meshInstance.RotateY(_rotationSpeed * (float)delta);
        }
    }
}
