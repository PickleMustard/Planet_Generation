using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables;

public partial class ShipSatellite : Node3D, IArtificialSatellite
{
    [Export]
    public string Id { get; private set; } = string.Empty;

    [Export]
    public int BandIndex { get; set; }

    [Export]
    public bool IsActive { get; set; } = true;

    [Export]
    public bool IsStationary { get; set; } = false;

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
    /// Initializes the ship's orbit using the host body's orbital parameters.
    /// Uses band-based or continuous placement based on the host body's configuration.
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="bandIndex">Index of the orbit band (for band-based bodies).</param>
    public void InitializeOrbit(IOrbitalBody hostBody, int bandIndex)
    {
        if (hostBody == null)
        {
            GameLogger.Warning("ShipSatellite: Cannot initialize orbit - host body is null");
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
        Node3D? parentNode = GetParent<Node3D>();
        if (parentNode != null)
        {
            GlobalPosition = parentNode.GlobalPosition + parameters.InitialPosition;
        }

        _isInitialized = true;
        _isTraveling = false;

        GameLogger.Debug(
            $"ShipSatellite initialized: {Name}, Band {BandIndex}, Radius {_orbitalRadius:F2}, Speed {_orbitalSpeed:F6}"
        );
    }

    /// <summary>
    /// Initializes the ship's orbit at a specific radius (for continuous placement).
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="radius">Desired orbital radius in meters.</param>
    public void InitializeOrbitAtRadius(IOrbitalBody hostBody, float radius)
    {
        if (hostBody == null)
        {
            GameLogger.Warning("ShipSatellite: Cannot initialize orbit - host body is null");
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
        Node3D? parentNode = GetParent<Node3D>();
        if (parentNode != null)
        {
            GlobalPosition = parentNode.GlobalPosition + parameters.InitialPosition;
        }

        _isInitialized = true;
        _isTraveling = false;

        GameLogger.Debug(
            $"ShipSatellite initialized at radius: {Name}, Radius {_orbitalRadius:F2}, Speed {_orbitalSpeed:F6}"
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
                $"ShipSatellite: Parent body {parentBody?.Name} does not implement IOrbitalBody"
            );
            return;
        }

        // Reparent to the specified body if needed
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

    // Travel state
    private bool _isTraveling;
    private IOrbitalBody? _destinationBody;
    private float _travelSpeed = 10.0f;

    // Visual components
    private MeshInstance3D? _meshInstance;
    private float _rotationSpeed = 1.0f;

    public void InitiateTravel(IOrbitalBody destinationBody, float speed)
    {
        if (destinationBody == null)
        {
            GameLogger.Warning("ShipSatellite: Cannot initiate travel - destination body is null");
            return;
        }

        _destinationBody = destinationBody;
        _travelSpeed = speed;
        _isTraveling = true;

        GameLogger.Info(
            $"ShipSatellite {Name} initiating travel to {destinationBody} at speed {speed}"
        );
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
        Vector3 direction = (_destinationBody.BodyPosition - GlobalPosition).Normalized();

        // Move toward destination
        GlobalPosition += direction * _travelSpeed * (float)delta;

        // Check if we've arrived (close enough to destination)
        float distanceToDestination = GlobalPosition.DistanceTo(_destinationBody.BodyPosition);

        // If we're close enough, re-enter orbit around the destination
        if (distanceToDestination <= _destinationBody.Radius * 1.5f)
        {
            GameLogger.Info($"ShipSatellite {Name} arrived at {_destinationBody}");

            // Reparent to the destination body
            Node3D? currentParent = GetParent<Node3D>();
            if (currentParent != null)
            {
                currentParent.RemoveChild(this);
            }
            if (_destinationBody is null)
            {
                throw new System.NullReferenceException();
            }
            Node3D destinationBodyNode = (Node3D)_destinationBody;
            destinationBodyNode!.CallDeferred("add_child", this);

            // Recalculate orbital parameters for the new parent using IOrbitalBody
            if (_destinationBody is IOrbitalBody orbitalBody)
            {
                InitializeOrbit(orbitalBody, 0);
            }
            else
            {
                GameLogger.Warning(
                    $"ShipSatellite: Destination body does not implement IOrbitalBody"
                );
            }

            _isTraveling = false;
            _destinationBody = null;
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
        float cos = Mathf.Cos(_orbitalAngle);
        float sin = Mathf.Sin(_orbitalAngle);

        // Position in XZ plane
        GlobalPosition =
            parentBody.GlobalPosition + new Vector3(cos * _orbitalRadius, 0, sin * _orbitalRadius);

        // Calculate and store velocity (tangent to orbit)
        float linearSpeed = _orbitalRadius * _orbitalSpeed;
        _velocity = new Vector3(-sin * linearSpeed, 0f, cos * linearSpeed);
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
        CreateVisualRepresentation();
    }

    private void CreateVisualRepresentation()
    {
        // Create MeshInstance3D for visual representation
        _meshInstance = new MeshInstance3D { Name = "ShipMesh" };

        // Create a box mesh (ships look like elongated boxes)
        var boxMesh = new BoxMesh
        {
            Size = new Vector3(0.5f, 0.2f, 1.0f),
            SubdivideWidth = 2,
            SubdivideHeight = 1,
            SubdivideDepth = 3,
        };
        _meshInstance.Mesh = boxMesh;

        // Create a metallic material for the ship
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.7f, 0.8f), // Silver/light gray
            Metallic = 0.8f,
            Roughness = 0.2f,
        };
        _meshInstance.MaterialOverride = material;

        // Add mesh instance as child
        AddChild(_meshInstance);

        // Add a small cone at the front for a pointed nose
        var noseMesh = new MeshInstance3D { Name = "ShipNose" };
        var coneMesh = new CylinderMesh
        {
            TopRadius = 0f,
            BottomRadius = 0.15f,
            Height = 0.3f,
            RadialSegments = 8,
            Rings = 2,
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
