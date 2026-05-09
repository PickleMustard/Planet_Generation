using System;
using Godot;
using Godot.Collections;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables;

public partial class StationSatellite : Node3D, IArtificialSatellite, IConstructable
{
    [Signal]
    public delegate void OnCompletionEventHandler();

    [Signal]
    public delegate void OnConstructionBlockedEventHandler();

    [Signal]
    public delegate void OnConstructionResumedEventHandler();

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

    public IOrbitalBody ParentBody
    {
        get => _parentBody;
        set => _parentBody = value;
    }

    // Visual components
    protected MeshInstance3D? _meshInstance;
    protected Node3D? _modelRoot;
    private float _rotationSpeed = 0.5f;
    protected IOrbitalBody? _parentBody;

    // Construction state
    protected ConstructionState? _constructionState;
    protected StationDefinition? _stationDefinition;
    protected bool _isUnderConstruction;
    protected StandardMaterial3D? _originalMaterial;

    // Self-ticking construction progress reporting
    private float _progressTimer;
    private const float PROGRESS_SIGNAL_INTERVAL = 0.5f;

    /// <summary>Whether this station type can build ships (from station definition).</summary>
    public bool CanBuildShips => _stationDefinition?.CanBuildShips ?? false;

    /// <summary>The station type from the definition.</summary>
    public string StationType => _stationDefinition?.StationType ?? "";


    #region IConstructable

    [ExportGroup("Construction")]
    [Export]
    public float workRequired
    {
        get => _constructionState?.WorkRequired ?? 0f;
        set
        {
            if (_constructionState != null)
                _constructionState.WorkRequired = value;
        }
    }

    [Export]
    public float workDone
    {
        get => _constructionState?.WorkDone ?? 0f;
        set
        {
            if (_constructionState != null)
                _constructionState.WorkDone = value;
        }
    }

    [Export]
    public float ConstructionProgress
    {
        get => _constructionState?.GetProgress() ?? 0f;
        set { }
    }

    [Export]
    public string ConstructionStatusText
    {
        get => _constructionState?.Status.ToString() ?? "None";
        set { }
    }

    [Export]
    public Dictionary<string, int> requiredResources
    {
        get
        {
            var dict = new Dictionary<string, int>();
            if (_constructionState != null)
                foreach (var kvp in _constructionState.RequiredResources)
                    dict[kvp.Key] = kvp.Value;
            return dict;
        }
        set
        {
            if (_constructionState != null)
            {
                _constructionState.RequiredResources.Clear();
                foreach (var kvp in value)
                    _constructionState.RequiredResources[kvp.Key] = kvp.Value;
            }
        }
    }

    [Export]
    public Dictionary<string, int> availableResources
    {
        get
        {
            var dict = new Dictionary<string, int>();
            if (_constructionState != null)
                foreach (var kvp in _constructionState.AvailableResources)
                    dict[kvp.Key] = kvp.Value;
            return dict;
        }
        set
        {
            if (_constructionState != null)
            {
                _constructionState.AvailableResources.Clear();
                foreach (var kvp in value)
                    _constructionState.AvailableResources[kvp.Key] = kvp.Value;
            }
        }
    }

    public bool CanConstruct(Dictionary LocationDetails)
    {
        return true;
    }

    public IConstructable StartConstruction(Dictionary LocationDetails)
    {
        if (_constructionState == null)
            return this;

        if (LocationDetails.TryGetValue("parent_body", out var parentBody))
        {
            LocationDetails.TryGetValue("parent_type", out var parentType);
            if ((String)parentType == "CelestialBody")
                _parentBody = (CelestialBody)parentBody;
            else if ((String)parentBody == "SatelliteBody")
                _parentBody = (SatelliteBody)parentBody;
        }

        _isUnderConstruction = true;
        IsActive = false;

        // Apply translucent construction material
        ApplyConstructionMaterial();

        _constructionState.TryStart();

        GameLogger.Info(
            $"StationSatellite {Name}: Construction started ({_constructionState.WorkRequired}s)"
        );
        return this;
    }

    public bool DefineTemplate(Dictionary templateData)
    {
        return true;
    }

    public bool UpdateConfiguration(Dictionary updateData)
    {
        return true;
    }

    public bool CancelConstruction()
    {
        if (_constructionState == null)
            return false;

        _constructionState.Cancel();
        _isUnderConstruction = false;

        GameLogger.Info($"StationSatellite {Name}: Construction cancelled");
        return true;
    }

    public string GetStatus()
    {
        return _constructionState?.Status.ToString() ?? "None";
    }

    public float GetProgress()
    {
        return _constructionState?.GetProgress() ?? 0f;
    }

    public void UpdateProgress(float delta)
    {
        if (_constructionState == null || !_isUnderConstruction)
            return;

        var previousStatus = _constructionState.Status;
        _constructionState.UpdateProgress(delta);

        if (_constructionState.StatusChanged)
        {
            if (
                _constructionState.Status == ConstructionStatus.Blocked
                && previousStatus == ConstructionStatus.InProgress
            )
            {
                EmitSignal(SignalName.OnConstructionBlocked);
            }
            else if (
                _constructionState.Status == ConstructionStatus.InProgress
                && previousStatus == ConstructionStatus.Blocked
            )
            {
                EmitSignal(SignalName.OnConstructionResumed);
            }
            else if (_constructionState.Status == ConstructionStatus.Complete)
            {
                OnConstructionComplete();
            }
        }
    }

    public bool CheckRequiredResourcesAvailable()
    {
        return _constructionState?.CheckRequiredResourcesAvailable() ?? true;
    }

    public bool CanDemolish() => !_isUnderConstruction;

    public bool DemolishConstructable() => false;

    public bool CanDestroy() => true;

    public bool DestroyConstructable()
    {
        QueueFree();
        return true;
    }

    #endregion

    /// <summary>
    /// Configures this station with a station definition for construction.
    /// </summary>
    public virtual void SetStationDefinition(StationDefinition definition)
    {
        _stationDefinition = definition;
        _constructionState = new ConstructionState(
            definition.ConstructionTime,
            definition.RequiredResources
        );

        float bodyRadius = _parentBody?.Radius ?? 1.0f;
        Node3D? model = definition.Visual?.CreateModelInstance(bodyRadius);
        InstallModel(model, definition.Name);
    }

    /// <summary>
    /// Delivers resources to this station's construction site.
    /// </summary>
    public void DeliverResources(string resourceId, int amount)
    {
        _constructionState?.DeliverResources(resourceId, amount);
    }

    /// <summary>Whether this station is currently under construction.</summary>
    public bool IsUnderConstruction => _isUnderConstruction;

    protected virtual void OnConstructionComplete()
    {
        _isUnderConstruction = false;

        // Restore original material
        RestoreOriginalMaterial();

        EmitSignal(SignalName.OnCompletion);
        GameLogger.Info($"StationSatellite {Name}: Construction complete");
    }

    private void ApplyConstructionMaterial()
    {
        if (_meshInstance?.MaterialOverride is StandardMaterial3D existingMat)
        {
            _originalMaterial = existingMat;
        }

        var constructionMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.5f, 0.6f, 0.3f),
            Metallic = 0.7f,
            Roughness = 0.3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        if (_meshInstance != null)
            _meshInstance.MaterialOverride = constructionMat;
    }

    private void RestoreOriginalMaterial()
    {
        if (_meshInstance != null && _originalMaterial != null)
            _meshInstance.MaterialOverride = _originalMaterial;
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
    protected bool _isInitialized;
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

        if (_modelRoot == null)
            InstallModel(null, Name);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Self-tick construction when under construction
        if (_isUnderConstruction)
        {
            TickConstruction(dt);
            return;
        }

        // Tick subclass operational behavior (ship queues, building budgets, etc.)
        TickOperational(dt);

        if (!_isInitialized || !IsActive)
            return;

        Node3D? parentBody = GetParent<Node3D>();
        if (parentBody == null)
            return;

        // Update orbital angle
        _orbitalAngle += _orbitalSpeed * dt;

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

    /// <summary>
    /// Called each physics tick while the station is under construction.
    /// Base implementation advances construction progress and emits periodic progress signals.
    /// </summary>
    protected virtual void TickConstruction(float delta)
    {
        UpdateProgress(delta);

        // Periodic progress reporting
        _progressTimer += delta;
        if (_progressTimer >= PROGRESS_SIGNAL_INTERVAL)
        {
            _progressTimer = 0f;
            ConstructionManager.Instance?.NotifyProgressUpdate(
                Name.ToString(),
                GetProgress(),
                GetStatus()
            );
        }

        if (_constructionState?.Status == ConstructionStatus.Complete)
        {
            OnConstructionComplete();
            ConstructionManager.Instance?.NotifyConstructionComplete(this);
        }
    }

    /// <summary>
    /// Called each physics tick when the station is NOT under construction.
    /// Override in subclasses for operational behavior (e.g. ship build queues, building work budgets).
    /// </summary>
    protected virtual void TickOperational(float delta) { }

    #endregion

    /// <summary>
    /// Installs a model node as the station's visual. If <paramref name="model"/> is null, the
    /// generic station default prefab is used. Frees any previously installed model.
    /// </summary>
    private void InstallModel(Node3D? model, string? logLabel)
    {
        if (_modelRoot != null)
        {
            _modelRoot.QueueFree();
            _modelRoot = null;
            _meshInstance = null;
        }

        model ??= DefaultModelRegistry.InstantiateStationDefault();
        if (model == null)
        {
            GameLogger.Error($"StationSatellite {Name}: Failed to obtain any model for '{logLabel}'");
            return;
        }

        AddChild(model);
        _modelRoot = model;
        _meshInstance = NodeUtils.FindMeshInstanceRecursive(model);

        if (_meshInstance != null)
        {
            _originalMaterial = _meshInstance.MaterialOverride?.Duplicate() as StandardMaterial3D;
        }
        else
        {
            GameLogger.Warning($"StationSatellite {Name}: No MeshInstance3D found in model for '{logLabel}'");
        }

        GameLogger.Debug($"StationSatellite {Name}: Visual installed for '{logLabel}'");
    }

    public override void _Process(double delta)
    {
        if (_modelRoot != null && IsActive)
        {
            _modelRoot.RotateY(_rotationSpeed * (float)delta);
        }
    }
}
