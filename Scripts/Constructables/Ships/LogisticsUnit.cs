using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables;

public partial class LogisticsUnit : Node3D, IArtificialSatellite, IConstructable
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
    public Vector3 UnitPosition
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
    public int BandIndex { get; set; }

    [Export]
    public bool IsActive { get; set; } = true;

    [Export]
    public bool IsStationary { get; set; } = false;

    // Construction state
    private ConstructionState? _constructionState;
    private ShipDefinition? _shipDefinition;
    private bool _isUnderConstruction;

    /// <summary>Whether this unit is currently under construction.</summary>
    public bool IsUnderConstruction => _isUnderConstruction;

    /// <summary>The construction-yard station building this ship, if any.</summary>
    public StationSatellite? ConstructingStation { get; set; }

    #region IConstructable

    [ExportGroup("Construction")]

    [Export]
    public float workRequired
    {
        get => _constructionState?.WorkRequired ?? 0f;
        set { if (_constructionState != null) _constructionState.WorkRequired = value; }
    }

    [Export]
    public float workDone
    {
        get => _constructionState?.WorkDone ?? 0f;
        set { if (_constructionState != null) _constructionState.WorkDone = value; }
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
    public Godot.Collections.Dictionary<string, int> requiredResources
    {
        get
        {
            var dict = new Godot.Collections.Dictionary<string, int>();
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
    public Godot.Collections.Dictionary<string, int> availableResources
    {
        get
        {
            var dict = new Godot.Collections.Dictionary<string, int>();
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

    public bool CanConstruct(Godot.Collections.Dictionary LocationDetails)
    {
        // Check if parent station can build ships
        if (LocationDetails.ContainsKey("parent_station"))
        {
            var parentStation = LocationDetails["parent_station"].As<StationSatellite>();
            if (parentStation != null && !parentStation.CanBuildShips)
            {
                GameLogger.Warning($"LogisticsUnit: Parent station '{parentStation.Name}' cannot build ships");
                return false;
            }
        }
        return true;
    }

    public IConstructable StartConstruction(Godot.Collections.Dictionary LocationDetails)
    {
        if (_constructionState == null) return this;

        _isUnderConstruction = true;
        IsActive = false;
        ProcessMode = ProcessModeEnum.Disabled;

        _constructionState.TryStart();

        GameLogger.Info($"LogisticsUnit {Name}: Construction started ({_constructionState.WorkRequired}s)");
        return this;
    }

    public bool DefineTemplate(Godot.Collections.Dictionary templateData) => true;
    public bool UpdateConfiguration(Godot.Collections.Dictionary updateData) => true;

    public bool CancelConstruction()
    {
        if (_constructionState == null) return false;

        _constructionState.Cancel();
        _isUnderConstruction = false;

        GameLogger.Info($"LogisticsUnit {Name}: Construction cancelled");
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
        if (_constructionState == null || !_isUnderConstruction) return;

        var previousStatus = _constructionState.Status;
        _constructionState.UpdateProgress(delta);

        if (_constructionState.StatusChanged)
        {
            if (_constructionState.Status == ConstructionStatus.Blocked
                && previousStatus == ConstructionStatus.InProgress)
            {
                EmitSignal(SignalName.OnConstructionBlocked);
            }
            else if (_constructionState.Status == ConstructionStatus.InProgress
                && previousStatus == ConstructionStatus.Blocked)
            {
                EmitSignal(SignalName.OnConstructionResumed);
            }
            else if (_constructionState.Status == ConstructionStatus.Complete)
            {
                _isUnderConstruction = false;
                EmitSignal(SignalName.OnCompletion);
                GameLogger.Info($"LogisticsUnit {Name}: Construction complete");
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
    /// Configures this unit with a ship definition for construction.
    /// </summary>
    public void SetShipDefinition(ShipDefinition definition)
    {
        _shipDefinition = definition;
        _constructionState = new ConstructionState(
            definition.ConstructionTime,
            definition.RequiredResources
        );
    }

    /// <summary>
    /// Delivers resources to this unit's construction site.
    /// </summary>
    public void DeliverResources(string resourceId, int amount)
    {
        _constructionState?.DeliverResources(resourceId, amount);
    }

    #region OrbitalParameters

    /// <summary>Current orbital angle in radians.</summary>
    public float OrbitalAngle
    {
        get => _orbitalAngle;
        set => _orbitalAngle = value;
    }

    /// <summary>Current orbital radius in meters.</summary>
    public float OrbitalRadius
    {
        get => _orbitalRadius;
        set => _orbitalRadius = value;
    }

    /// <summary>Current orbital angular speed in rad/s.</summary>
    public float OrbitalSpeed
    {
        get => _orbitalSpeed;
        set => _orbitalSpeed = value;
    }

    // Orbital state fields
    private float _orbitalAngle;
    private float _orbitalRadius;
    private float _orbitalSpeed;
    private float _hostMass;
    private bool _isInitialized;
    private Vector3 _velocity;

    /// <summary>
    /// Initializes the logistics unit's orbit using the host body's orbital parameters.
    /// Uses band-based or continuous placement based on the host body's configuration.
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="bandIndex">Index of the orbit band (for band-based bodies).</param>
    public void InitializeOrbit(IOrbitalBody hostBody, int bandIndex)
    {
        if (hostBody == null)
        {
            GameLogger.Warning("LogisticsUnit: Cannot initialize orbit - host body is null");
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
        _state = LogisticsUnitState.Idle;

        // Initialize engine with default values if not set
        if (_currentEngine == null)
        {
            _currentEngine = new Structures.Logistics.EngineDefinition(300f, 1000f);
        }

        GameLogger.Debug(
            $"LogisticsUnit initialized: {Name}, Band {BandIndex}, Radius {_orbitalRadius:F2}, Speed {_orbitalSpeed:F6}"
        );
    }

    /// <summary>
    /// Initializes the logistics unit's orbit at a specific radius (for continuous placement).
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="radius">Desired orbital radius in meters.</param>
    public void InitializeOrbitAtRadius(IOrbitalBody hostBody, float radius)
    {
        if (hostBody == null)
        {
            GameLogger.Warning("LogisticsUnit: Cannot initialize orbit - host body is null");
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
        _state = LogisticsUnitState.Idle;

        // Initialize engine with default values if not set
        if (_currentEngine == null)
        {
            _currentEngine = new Structures.Logistics.EngineDefinition(300f, 1000f);
        }

        GameLogger.Debug(
            $"LogisticsUnit initialized at radius: {Name}, Radius {_orbitalRadius:F2}, Speed {_orbitalSpeed:F6}"
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
                $"LogisticsUnit: Parent body {parentBody?.Name} does not implement IOrbitalBody"
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
#if DEBUG
        RegisterWithDebug();
#endif
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

    // Logistics-specific properties
    [Export]
    private LogisticsUnitState _state = LogisticsUnitState.Idle;

    private Structures.Logistics.EngineDefinition? _currentEngine;

    private Structures.Resources.CargoManifest? _cargo;

    [Export]
    private float _fuel = 100.0f;

    [Export]
    private float _maxFuel = 100.0f;

    /// <summary>
    /// Dry mass of the ship in kg (empty weight without fuel or cargo).
    /// </summary>
    [Export]
    private float _dryMass = 1000.0f;

    // Travel state
    private bool _isTraveling;
    private IOrbitalBody? _destinationBody;
    private float _travelSpeed = 10.0f;

    // Trajectory planning fields (Task #463)
    private TrajectorySolution? _plannedTrajectory;
    private float _transferTime;
    private float _timeInTransfer;
    private Vector3 _departurePosition;
    private Vector3 _targetPosition;
    private Vector3 _initialVelocity;

    // Generated route options for selection (Task #465-468)
    private List<TrajectorySolution>? _availableRouteOptions;
    private CelestialBody? _routeOptionsDestination;

    // Movement controller for hybrid simulation (Task #435)
    private LogisticsMovementController? _movementController;
    private OrbitalScheduleExecutor? _scheduleExecutor;

    // Trajectory preview manager for visualizing planned routes
    private TrajectoryPreviewManager? _trajectoryPreviewManager;

    // Visual components
    private MeshInstance3D? _meshInstance;
    private float _rotationSpeed = 1.0f;

    public LogisticsUnitState State
    {
        get => _state;
        set => _state = value;
    }

    public Structures.Logistics.EngineDefinition? CurrentEngine
    {
        get => _currentEngine;
        set => _currentEngine = value;
    }

    public Structures.Resources.CargoManifest? Cargo
    {
        get => _cargo;
        set => _cargo = value;
    }

    public float Fuel
    {
        get => _fuel;
        set => _fuel = Mathf.Clamp(value, 0, _maxFuel);
    }

    public float MaxFuel => _maxFuel;

    public float CurrentFuelMass => _fuel;

    public float MaxFuelMass => _maxFuel;

    /// <summary>
    /// The movement controller handling hybrid simulation, Kepler propagation, and warp.
    /// </summary>
    public LogisticsMovementController? MovementController => _movementController;

    /// <summary>
    /// The schedule executor for this unit. Drives leg and schedule execution.
    /// </summary>
    public OrbitalScheduleExecutor? ScheduleExecutor => _scheduleExecutor;

    /// <summary>
    /// The trajectory preview manager for visualizing planned routes before execution.
    /// </summary>
    public TrajectoryPreviewManager? PreviewManager => _trajectoryPreviewManager;

    public override void _ExitTree()
    {
        GameLogger.Debug($"LogisticsUnit destroying: {Name}");
        _isInitialized = false;
        _isTraveling = false;
        _destinationBody = null;
        base._ExitTree();
    }

    public override void _EnterTree()
    {
        this.Id = Guid.NewGuid().ToString();
    }

    public override void _Ready()
    {
        base._Ready();

        // Create visual representation
        CreateVisualRepresentation();

        // Initialize movement controller for hybrid simulation
        InitializeMovementController();

        // Initialize trajectory preview manager
        InitializePreviewManager();

        // Initialize orbital schedule executor
        InitializeScheduleExecutor();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isInitialized || !IsActive)
            return;

        CheckFuelState();
    }

    /// <summary>
    /// Dry mass of the ship in kg (empty weight without fuel or cargo).
    /// </summary>
    public float DryMass => _dryMass;

    /// <summary>
    /// Sets the dry mass of the ship.
    /// </summary>
    /// <param name="mass">Dry mass in kg.</param>
    public void SetDryMass(float mass)
    {
        if (mass < 0)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Cannot set negative dry mass: {mass}");
            return;
        }

        _dryMass = mass;
        GameLogger.Debug($"LogisticsUnit {Name}: Dry mass set to {mass}");
    }

    public void SetFuelCapacity(float capacity)
    {
        if (capacity < 0)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot set negative fuel capacity: {capacity}"
            );
            return;
        }

        _maxFuel = capacity;
        _fuel = Mathf.Clamp(_fuel, 0, _maxFuel);
        GameLogger.Debug($"LogisticsUnit {Name}: Fuel capacity set to {capacity}");
    }

    public void Refuel(float amount)
    {
        if (amount < 0)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot refuel with negative amount: {amount}"
            );
            return;
        }

        float previousFuel = _fuel;
        _fuel = Mathf.Clamp(_fuel + amount, 0, _maxFuel);
        float actualRefueled = _fuel - previousFuel;

        GameLogger.Debug(
            $"LogisticsUnit {Name}: Refueled {actualRefueled:F2} (requested: {amount:F2}), current: {_fuel:F2}/{_maxFuel:F2}"
        );
    }

    public void ConsumeFuel(float amount)
    {
        if (amount < 0)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot consume negative fuel amount: {amount}"
            );
            return;
        }

        float previousFuel = _fuel;
        _fuel = Mathf.Clamp(_fuel - amount, 0, _maxFuel);
        float actualConsumed = previousFuel - _fuel;

        GameLogger.Debug(
            $"LogisticsUnit {Name}: Consumed {actualConsumed:F2} (requested: {amount:F2}), remaining: {_fuel:F2}/{_maxFuel:F2}"
        );
    }

    public bool HasFuel()
    {
        return _fuel > 0f;
    }

    public float GetFuelPercentage()
    {
        if (_maxFuel <= 0f)
            return 0f;

        return (_fuel / _maxFuel) * 100f;
    }

    // Rocket equation methods (Task #462)
    /// <summary>
    /// Calculates the remaining delta-v capability using the Tsiolkovsky rocket equation.
    /// Delegates to ThrustPerformanceCalculator for the calculation.
    /// Δv = ve * ln(m0 / m1)
    /// </summary>
    /// <returns>Remaining delta-v in m/s, or 0 if no engine is installed.</returns>
    public float CalculateRemainingDeltaV()
    {
        GameLogger.Info(
            $"LogisticsUnit.CalculateRemainingDeltaV: Starting calculation - "
                + $"dryMass={_dryMass:F2} kg, fuel={_fuel:F2} kg, cargoMass={GetCargoMass():F2} kg, "
                + $"unit={Name}"
        );

        if (_currentEngine == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot calculate delta-v - no engine installed"
            );
            return 0f;
        }

        float exhaustVelocity = _currentEngine.ExhaustVelocity;
        float totalMass = _dryMass + _fuel + GetCargoMass();
        float dryMass = _dryMass + GetCargoMass();

        GameLogger.Info(
            $"LogisticsUnit.CalculateRemainingDeltaV: Engine parameters - "
                + $"exhaustVelocity={exhaustVelocity:F2} m/s, totalMass={totalMass:F2} kg, "
                + $"dryMassNoFuel={dryMass:F2} kg, unit={Name}"
        );

        float deltaV = ThrustPerformanceCalculator.CalculateMaxDeltaV(
            _currentEngine,
            _dryMass,
            _fuel,
            GetCargoMass()
        );

        // Calculate mass ratio for logging
        float massRatio = totalMass / dryMass;

        GameLogger.Info(
            $"LogisticsUnit.CalculateRemainingDeltaV: Result - "
                + $"Δv={deltaV:F2} m/s, massRatio={massRatio:F4}, "
                + $"ln(massRatio)={Mathf.Log(massRatio):F4}, unit={Name}"
        );

        return deltaV;
    }

    /// <summary>
    /// Calculates the fuel mass required to achieve a given delta-v.
    /// Delegates to ThrustPerformanceCalculator for the calculation.
    /// Reverse Tsiolkovsky: m0 = m1 * e^(Δv/ve), Fuel = m0 - m1
    /// </summary>
    /// <param name="deltaV">Desired delta-v in m/s.</param>
    /// <returns>Fuel mass required in the same units as fuel (mass units).</returns>
    public float CalculateFuelForDeltaV(float deltaV)
    {
        GameLogger.Info(
            $"LogisticsUnit.CalculateFuelForDeltaV: Starting calculation - "
                + $"requestedDeltaV={deltaV:F2} m/s, dryMass={_dryMass:F2} kg, "
                + $"cargoMass={GetCargoMass():F2} kg, unit={Name}"
        );

        if (_currentEngine == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot calculate fuel - no engine installed"
            );
            return 0f;
        }

        if (deltaV <= 0f)
        {
            GameLogger.Info(
                $"LogisticsUnit.CalculateFuelForDeltaV: Zero or negative deltaV requested, returning 0 fuel"
            );
            return 0f;
        }

        float exhaustVelocity = _currentEngine.ExhaustVelocity;
        if (exhaustVelocity <= 0f)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot calculate fuel - invalid exhaust velocity"
            );
            return 0f;
        }

        float dryMass = _dryMass + GetCargoMass();
        float deltaVOverVe = deltaV / exhaustVelocity;
        float massRatio = (float)System.Math.Exp(deltaVOverVe);
        float initialMass = dryMass * massRatio;
        float fuelRequired = initialMass - dryMass;

        GameLogger.Info(
            $"LogisticsUnit.CalculateFuelForDeltaV: Result - "
                + $"exhaustVelocity={exhaustVelocity:F2} m/s, dryMassNoFuel={dryMass:F2} kg, "
                + $"deltaV/ve={deltaVOverVe:F4}, massRatio=e^{deltaVOverVe:F4}={massRatio:F4}, "
                + $"initialMass={initialMass:F2} kg, fuelRequired={fuelRequired:F2} kg, "
                + $"unit={Name}"
        );

        return fuelRequired;
    }

    /// <summary>
    /// Gets the total mass of the logistics unit.
    /// </summary>
    /// <returns>Total mass = dry mass (1) + fuel mass + cargo mass.</returns>
    public float GetTotalMass()
    {
        return _dryMass + _fuel + GetCargoMass();
    }

    public bool CanTransitionTo(LogisticsUnitState newState)
    {
        if (_state == newState)
        {
            return true;
        }

        switch (_state)
        {
            case LogisticsUnitState.Idle:
                return newState == LogisticsUnitState.Planning
                    || newState == LogisticsUnitState.Loading
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.Planning:
                return newState == LogisticsUnitState.InTransit
                    || newState == LogisticsUnitState.Idle
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.InTransit:
                return newState == LogisticsUnitState.Arriving
                    || newState == LogisticsUnitState.Stranded
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.Arriving:
                return newState == LogisticsUnitState.Idle
                    || newState == LogisticsUnitState.Unloading
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.Disabled:
                return newState == LogisticsUnitState.Idle;

            case LogisticsUnitState.Stranded:
                return newState == LogisticsUnitState.Idle
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.Loading:
                return newState == LogisticsUnitState.WaitingForTrajectory
                    || newState == LogisticsUnitState.Planning
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.Unloading:
                return newState == LogisticsUnitState.Idle
                    || newState == LogisticsUnitState.Loading
                    || newState == LogisticsUnitState.Disabled;

            case LogisticsUnitState.WaitingForTrajectory:
                return newState == LogisticsUnitState.Planning
                    || newState == LogisticsUnitState.Stranded
                    || newState == LogisticsUnitState.Disabled;

            default:
                return false;
        }
    }

    public bool TransitionTo(LogisticsUnitState newState)
    {
        if (_state == newState)
        {
            GameLogger.Debug($"LogisticsUnit {Name}: Already in state {newState}");
            return true;
        }

        if (!CanTransitionTo(newState))
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot transition from {_state} to {newState}"
            );
            return false;
        }

        LogisticsUnitState previousState = _state;
        _state = newState;

        GameLogger.Info($"LogisticsUnit {Name} transitioned: {previousState} → {newState}");

        return true;
    }

    public bool IsStateValidForOperation()
    {
        return _state != LogisticsUnitState.Disabled
            && _state != LogisticsUnitState.Arriving
            && _state != LogisticsUnitState.Stranded;
    }

    private void CheckFuelState()
    {
        // During transit, the movement controller handles fuel exhaustion
        // and will transition to Stranded if fuel runs out mid-transfer.
        // Don't interfere with that process by forcing Disabled here.
        if (
            !HasFuel()
            && _state != LogisticsUnitState.Disabled
            && _state != LogisticsUnitState.InTransit
            && _state != LogisticsUnitState.Stranded
            && _state != LogisticsUnitState.Loading
            && _state != LogisticsUnitState.Unloading
            && _state != LogisticsUnitState.WaitingForTrajectory
        )
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Out of fuel - transitioning to Disabled state"
            );
            TransitionTo(LogisticsUnitState.Disabled);
        }
        else if (HasFuel() && _state == LogisticsUnitState.Disabled)
        {
            GameLogger.Info($"LogisticsUnit {Name}: Fuel available - can transition to Idle state");
        }
    }

    // Engine management methods
    public void SetEngine(Structures.Logistics.EngineDefinition engine)
    {
        if (engine == null)
        {
            GameLogger.Warning("LogisticsUnit: Cannot set null engine");
            return;
        }

        _currentEngine = engine;
        GameLogger.Debug(
            $"LogisticsUnit {Name}: Engine set - Isp: {engine.BaseSpecificImpulse}, Thrust: {engine.BaseThrust}"
        );
    }

    public void RemoveEngine()
    {
        if (_currentEngine == null)
        {
            GameLogger.Debug($"LogisticsUnit {Name}: No engine to remove");
            return;
        }

        GameLogger.Debug($"LogisticsUnit {Name}: Engine removed");
        _currentEngine = null;
    }

    public void ReparentToHostBody(Node newParent)
    {
        CallDeferred("reparent", newParent);
    }

    public bool ApplyEngineModifier(Structures.Logistics.EngineModifier modifier)
    {
        if (_currentEngine == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot apply modifier - no engine installed"
            );
            return false;
        }

        bool result = _currentEngine.ApplyModifier(modifier);
        if (result)
        {
            GameLogger.Debug(
                $"LogisticsUnit {Name}: Applied engine modifier from {modifier.Source}"
            );
        }
        else
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Failed to apply modifier from {modifier.Source} - source already exists"
            );
        }

        return result;
    }

    public bool RemoveEngineModifier(string sourceId)
    {
        if (_currentEngine == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot remove modifier - no engine installed"
            );
            return false;
        }

        if (string.IsNullOrEmpty(sourceId))
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot remove modifier with null or empty source ID"
            );
            return false;
        }

        bool result = _currentEngine.RemoveModifier(sourceId);
        if (result)
        {
            GameLogger.Debug($"LogisticsUnit {Name}: Removed engine modifier from {sourceId}");
        }
        else
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Failed to remove modifier from {sourceId} - not found"
            );
        }

        return result;
    }

    // Cargo management methods
    public void InitializeCargo()
    {
        if (_cargo == null)
        {
            _cargo = new CargoManifest();
            GameLogger.Debug($"LogisticsUnit {Name}: Cargo manifest initialized");
        }
        else
        {
            GameLogger.Debug($"LogisticsUnit {Name}: Cargo manifest already exists");
        }
    }

    public bool LoadCargo(string resourceId, float quantity)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            GameLogger.Warning("LogisticsUnit: Cannot load cargo with null or empty resource ID");
            return false;
        }

        if (quantity <= 0)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot load cargo with non-positive quantity: {quantity}"
            );
            return false;
        }

        // Initialize cargo manifest if needed
        InitializeCargo();

        if (_cargo == null)
        {
            GameLogger.Error($"LogisticsUnit {Name}: Failed to initialize cargo manifest");
            return false;
        }

        bool result = _cargo.LoadResource(resourceId, quantity);
        if (result)
        {
            GameLogger.Debug($"LogisticsUnit {Name}: Loaded {quantity} of {resourceId}");
        }
        else
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Failed to load {quantity} of {resourceId}");
        }

        return result;
    }

    public bool UnloadCargo(string resourceId, float quantity)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            GameLogger.Warning("LogisticsUnit: Cannot unload cargo with null or empty resource ID");
            return false;
        }

        if (quantity <= 0)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot unload cargo with non-positive quantity: {quantity}"
            );
            return false;
        }

        if (_cargo == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot unload cargo - cargo manifest not initialized"
            );
            return false;
        }

        bool result = _cargo.UnloadResource(resourceId, quantity);
        if (result)
        {
            GameLogger.Debug($"LogisticsUnit {Name}: Unloaded {quantity} of {resourceId}");
        }
        else
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Failed to unload {quantity} of {resourceId}"
            );
        }

        return result;
    }

    public float GetCargoMass()
    {
        if (_cargo == null)
        {
            return 0f;
        }

        return _cargo.TotalCargoMass;
    }

    public void ClearCargo()
    {
        if (_cargo == null)
        {
            GameLogger.Debug(
                $"LogisticsUnit {Name}: No cargo to clear - cargo manifest not initialized"
            );
            return;
        }

        _cargo.Clear();
        GameLogger.Debug($"LogisticsUnit {Name}: Cargo cleared");
    }

    // ============ Orbital Velocity Calculations (Ticket #1) ============

    /// <summary>
    /// Calculates the ship's orbital velocity vector around its parent body.
    /// For a circular orbit in the XZ plane (Y=0):
    /// - Position: r × (cosθ, 0, sinθ)
    /// - Velocity: r × ω × (-sinθ, 0, cosθ)
    /// Returns velocity in global coordinates relative to the parent body's frame.
    /// </summary>
    /// <returns>Orbital velocity vector in m/s, or Vector3.Zero if calculation fails.</returns>
    public Vector3 GetOrbitalVelocity()
    {
        // Velocity is now maintained by _PhysicsProcess each frame
        // Return the current velocity value
        if (_velocity.LengthSquared() > 1e-10f)
        {
            GameLogger.Info(
                $"LogisticsUnit.GetOrbitalVelocity: Using cached velocity - "
                    + $"v={_velocity.Length():F2} m/s, "
                    + $"components=({_velocity.X:F2}, {_velocity.Y:F2}, {_velocity.Z:F2}), "
                    + $"unit={Name}"
            );
            return _velocity;
        }

        // Fallback calculation if velocity hasn't been set yet
        // Validate orbital parameters
        if (_orbitalRadius <= 0f || _orbitalSpeed <= 0f)
        {
            GameLogger.Warning(
                $"LogisticsUnit.GetOrbitalVelocity: Invalid orbital parameters - "
                    + $"radius: {_orbitalRadius}, speed: {_orbitalSpeed}"
            );
            return Vector3.Zero;
        }

        // Log input parameters
        GameLogger.Info(
            $"LogisticsUnit.GetOrbitalVelocity: Calculating orbital velocity - "
                + $"inputs: r={_orbitalRadius:F2}m, ω={_orbitalSpeed:F6} rad/s, θ={_orbitalAngle:F4} rad, "
                + $"hostMass={_hostMass:E2} kg, unit={Name}"
        );

        // Calculate linear speed: v = r × ω
        float linearSpeed = _orbitalRadius * _orbitalSpeed;

        // Calculate velocity components for circular orbit in XZ plane
        // Velocity direction is tangent to the orbit: (-sinθ, 0, cosθ)
        float sinTheta = Mathf.Sin(_orbitalAngle);
        float cosTheta = Mathf.Cos(_orbitalAngle);

        Vector3 velocity = new Vector3(
            -linearSpeed * sinTheta, // X component
            0f, // Y component (XZ plane)
            linearSpeed * cosTheta // Z component
        );
        Velocity = velocity;

        GameLogger.Info(
            $"LogisticsUnit.GetOrbitalVelocity: Calculated result - "
                + $"v={velocity.Length():F2} m/s, "
                + $"components=({velocity.X:F2}, {velocity.Y:F2}, {velocity.Z:F2}), "
                + $"linearSpeed={linearSpeed:F2} m/s, sinθ={sinTheta:F4}, cosθ={cosTheta:F4}, "
                + $"unit={Name}"
        );

        return velocity;
    }

    /// <summary>
    /// Gets the ship's orbital velocity relative to a reference body (e.g., central star).
    /// This accounts for both the ship's orbit around its parent body and the parent body's
    /// orbit around the reference body.
    /// </summary>
    /// <param name="referenceBody">The reference body to compute velocity relative to.</param>
    /// <returns>Total velocity vector relative to the reference body, or Vector3.Zero if calculation fails.</returns>
    public Vector3 GetOrbitalVelocityRelativeTo(IOrbitalBody referenceBody)
    {
        GameLogger.Info(
            $"LogisticsUnit.GetOrbitalVelocityRelativeTo: Starting calculation - "
                + $"referenceBody={referenceBody}, unit={Name}"
        );

        if (referenceBody == null)
        {
            GameLogger.Warning(
                "LogisticsUnit.GetOrbitalVelocityRelativeTo: Reference body is null"
            );
            return Vector3.Zero;
        }

        // Get the ship's orbital velocity around its parent body
        Vector3 shipOrbitalVelocity = GetOrbitalVelocity();
        GameLogger.Info(
            $"LogisticsUnit.GetOrbitalVelocityRelativeTo: Ship orbital velocity - "
                + $"v_ship={shipOrbitalVelocity.Length():F2} m/s, "
                + $"components=({shipOrbitalVelocity.X:F2}, {shipOrbitalVelocity.Y:F2}, {shipOrbitalVelocity.Z:F2})"
        );

        if (shipOrbitalVelocity.LengthSquared() <= 1e-10f)
        {
            GameLogger.Warning(
                "LogisticsUnit.GetOrbitalVelocityRelativeTo: Could not calculate ship orbital velocity"
            );
            return Vector3.Zero;
        }

        // Get the parent celestial body
        IOrbitalBody? parentBody = GetParent<IOrbitalBody>();
        if (parentBody == null || parentBody is not CelestialBody parentCelestialBody)
        {
            GameLogger.Warning(
                "LogisticsUnit.GetOrbitalVelocityRelativeTo: Parent is not a celestial body or is null"
            );
            return Vector3.Zero;
        }

        // If the reference body is the same as the parent body, just return ship's orbital velocity
        if (referenceBody == parentCelestialBody)
        {
            GameLogger.Info(
                $"LogisticsUnit.GetOrbitalVelocityRelativeTo: Reference body is parent - "
                    + $"returning ship velocity only, v={shipOrbitalVelocity.Length():F2} m/s"
            );
            return shipOrbitalVelocity;
        }

        // Log parent body velocity
        GameLogger.Info(
            $"LogisticsUnit.GetOrbitalVelocityRelativeTo: Parent body velocity - "
                + $"v_parent={parentCelestialBody.Velocity.Length():F2} m/s, "
                + $"components=({parentCelestialBody.Velocity.X:F2}, {parentCelestialBody.Velocity.Y:F2}, {parentCelestialBody.Velocity.Z:F2}), "
                + $"parent={parentCelestialBody.Name}"
        );

        // Calculate total velocity: ship's orbital velocity + parent body's orbital velocity
        // Both velocities are in global coordinates, so we can add them directly
        Vector3 totalVelocity = shipOrbitalVelocity + parentCelestialBody.Velocity;

        GameLogger.Info(
            $"LogisticsUnit.GetOrbitalVelocityRelativeTo: Final result - "
                + $"v_ship={shipOrbitalVelocity.Length():F2} m/s, "
                + $"v_parent={parentCelestialBody.Velocity.Length():F2} m/s, "
                + $"v_total={totalVelocity.Length():F2} m/s, "
                + $"totalComponents=({totalVelocity.X:F2}, {totalVelocity.Y:F2}, {totalVelocity.Z:F2}), "
                + $"relativeTo={referenceBody}"
        );

        return totalVelocity;
    }

    /// <summary>
    /// Predicts the ship's position at a future time based on its current orbital motion.
    /// Assumes circular orbit in XZ plane around the parent body.
    /// </summary>
    /// <param name="timeDelta">Time delta in seconds to predict forward.</param>
    /// <returns>Predicted global position, or current position if calculation fails.</returns>
    public Vector3 PredictShipPositionAtTime(float timeDelta)
    {
        Node3D? parentBody = GetParent<Node3D>();
        if (parentBody == null)
        {
            GameLogger.Warning("LogisticsUnit.PredictShipPositionAtTime: No parent body");
            return GlobalPosition;
        }

        // Validate orbital parameters
        if (_orbitalRadius <= 0f || _orbitalSpeed <= 0f)
        {
            GameLogger.Warning(
                $"LogisticsUnit.PredictShipPositionAtTime: Invalid orbital parameters - "
                    + $"radius: {_orbitalRadius}, speed: {_orbitalSpeed}"
            );
            return GlobalPosition;
        }

        // Log input parameters
        GameLogger.Info(
            $"LogisticsUnit.PredictShipPositionAtTime: Inputs - "
                + $"timeDelta={timeDelta:F2}s, currentAngle={_orbitalAngle:F4} rad, "
                + $"orbitalSpeed={_orbitalSpeed:F6} rad/s, orbitalRadius={_orbitalRadius:F2}m, "
                + $"currentGlobalPos={GlobalPosition}, unit={Name}"
        );

        // Calculate new orbital angle: θ_new = θ_current + ω × Δt
        float angleIncrement = _orbitalSpeed * timeDelta;
        float newOrbitalAngle = _orbitalAngle + angleIncrement;

        // Normalize angle to [0, 2π)
        float normalizedAngle = NormalizeAngle(newOrbitalAngle);

        // Calculate position relative to parent body
        float cosAngle = Mathf.Cos(newOrbitalAngle);
        float sinAngle = Mathf.Sin(newOrbitalAngle);
        float offsetX = cosAngle * _orbitalRadius;
        float offsetZ = sinAngle * _orbitalRadius;
        Vector3 relativePosition = new Vector3(offsetX, 0, offsetZ);

        // Return global position (assuming parent body doesn't move significantly in this time)
        Vector3 predictedPosition = parentBody.GlobalPosition + relativePosition;

        GameLogger.Info(
            $"LogisticsUnit.PredictShipPositionAtTime: Result - "
                + $"angleIncrement={angleIncrement:F4} rad, "
                + $"newAngle={newOrbitalAngle:F4} rad (normalized={normalizedAngle:F4}), "
                + $"cos={cosAngle:F4}, sin={sinAngle:F4}, "
                + $"relativePos=({relativePosition.X:F2}, {relativePosition.Y:F2}, {relativePosition.Z:F2}), "
                + $"parentPos={parentBody.GlobalPosition}, "
                + $"predictedPos={predictedPosition}, unit={Name}"
        );

        return predictedPosition;
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 2π).
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        angle %= Mathf.Tau;
        if (angle < 0f)
        {
            angle += Mathf.Tau;
        }
        return angle;
    }

    /// <summary>
    /// Processes orbital motion during Idle/Planning states.
    /// </summary>
    private void ProcessOrbit(double delta)
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

    // ============ End of Ticket #1 Methods ============

    public void InitiateTravel(IOrbitalBody destinationBody, float speed)
    {
        if (destinationBody == null)
        {
            GameLogger.Warning("LogisticsUnit: Cannot initiate travel - destination body is null");
            return;
        }

        _destinationBody = destinationBody;
        _travelSpeed = speed;
        _isTraveling = true;
        _state = LogisticsUnitState.Planning;

        GameLogger.Info(
            $"LogisticsUnit {Name} initiating travel to {destinationBody} at speed {speed}"
        );
    }

    public void HandleTravel(double delta)
    {
        if (_destinationBody == null)
        {
            GameLogger.Warning("LogisticsUnit: Traveling but destination body is null");
            _isTraveling = false;
            _state = LogisticsUnitState.Idle;
            return;
        }

        // Update state to in transit
        if (_state != LogisticsUnitState.InTransit)
        {
            _state = LogisticsUnitState.InTransit;
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
            GameLogger.Info($"LogisticsUnit {Name} arrived at {_destinationBody}");

            // Reparent to the destination body
            Node3D? currentParent = GetParent<Node3D>();
            if (currentParent != null)
            {
                currentParent.RemoveChild(this);
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
                    $"LogisticsUnit: Destination body does not implement IOrbitalBody"
                );
            }

            _isTraveling = false;
            _destinationBody = null;

            GameLogger.Debug($"LogisticsUnit {Name} entered orbit around destination");
        }
    }

    // Lambert-based trajectory execution methods (Task #463)
    /// <summary>
    /// Plans a route to the specified destination using a simplified Lambert-like calculation.
    /// </summary>
    /// <param name="destination">Target destination node.</param>
    /// <param name="departureTime">Departure time in seconds from now.</param>
    /// <returns>True if route was planned successfully, false otherwise.</returns>
    public bool PlanRoute(Node3D destination, float departureTime)
    {
        if (destination == null)
        {
            GameLogger.Warning("LogisticsUnit: Cannot plan route - destination is null");
            return false;
        }

        if (_currentEngine == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Cannot plan route - no engine installed");
            return false;
        }

        // Store departure and target positions
        _departurePosition = GlobalPosition;
        _targetPosition = destination.GlobalPosition;

        // Calculate transfer vector
        Vector3 transferVector = _targetPosition - _departurePosition;
        float transferDistance = transferVector.Length();

        if (transferDistance <= 0f)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot plan route - zero distance to destination"
            );
            return false;
        }

        // Simplified Lambert-like calculation for initial velocity
        // Estimate time of flight based on distance and average speed
        float averageSpeed = _currentEngine.EffectiveThrust / GetTotalMass();
        _transferTime = transferDistance / Mathf.Max(averageSpeed, 1f);

        // Clamp transfer time to reasonable bounds
        _transferTime = Mathf.Clamp(_transferTime, 10f, 86400f); // 10 seconds to 1 day

        // Calculate initial velocity for the transfer
        // Using simplified ballistic trajectory assumption
        Vector3 direction = transferVector.Normalized();
        _initialVelocity = direction * (transferDistance / Mathf.Max(_transferTime, 1f));

        // Create the planned trajectory
        _plannedTrajectory = new TrajectorySolution
        {
            InitialVelocity = _initialVelocity,
            FinalVelocity = Vector3.Zero, // Will be calculated at arrival
            TimeOfFlight = _transferTime,
            SemiMajorAxis = transferDistance / 2f,
            Eccentricity = 0.5f, // Simplified elliptical assumption
            Revolutions = 0,
            TransferType = TransferType.Direct,
        };

        // Calculate required delta-v
        _plannedTrajectory.DeltaVRequired = _initialVelocity.Length();

        // Calculate fuel needed for this trajectory
        float fuelNeeded = CalculateFuelForDeltaV(_plannedTrajectory.DeltaVRequired);

        GameLogger.Info(
            $"LogisticsUnit.PlanRoute: Route planned with detailed inputs - "
                + $"departurePos={_departurePosition}, targetPos={_targetPosition}, "
                + $"transferVector=({transferVector.X:F2}, {transferVector.Y:F2}, {transferVector.Z:F2}), "
                + $"transferDistance={transferDistance:F2}m, averageSpeed={averageSpeed:F2}m/s, "
                + $"engineThrust={_currentEngine?.EffectiveThrust ?? 0:F2}N, totalMass={GetTotalMass():F2}kg, "
                + $"direction=({direction.X:F4}, {direction.Y:F4}, {direction.Z:F4}), "
                + $"Results: TOF={_transferTime:F1}s, Δv={_plannedTrajectory.DeltaVRequired:F2}m/s, "
                + $"initialVelocity=({_initialVelocity.X:F2}, {_initialVelocity.Y:F2}, {_initialVelocity.Z:F2}), "
                + $"semiMajorAxis={_plannedTrajectory.SemiMajorAxis:F2}m, ecc={_plannedTrajectory.Eccentricity:F4}, "
                + $"fuelNeeded={fuelNeeded:F2}kg, unit={Name}, destination={destination.Name}"
        );

        // Transition to Planning state
        TransitionTo(LogisticsUnitState.Planning);

        return true;
    }

    // ============ TrajectoryPlanner Integration (Task #465-468) ============

    /// <summary>
    /// Gets available trajectory options to a destination celestial body.
    /// Uses the TrajectoryPlanner to generate multiple Lambert solutions.
    /// Accounts for the ship's actual position in its orbit band and the
    /// target orbit band at the destination.
    /// </summary>
    /// <param name="destination">Target destination celestial body.</param>
    /// <param name="departureTime">Departure time in seconds from now (default: 0).</param>
    /// <param name="numOptions">Number of options to generate (default: 20).</param>
    /// <param name="targetBandIndex">Target orbit band at destination (-1 = auto-select closest).</param>
    /// <returns>List of trajectory options ranked by most efficient (lowest delta-v).</returns>
    public List<TrajectorySolution> GetRouteOptions(
        IOrbitalBody destination,
        float departureTime = 0f,
        int numOptions = 20,
        int targetBandIndex = -1
    )
    {
        if (destination == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: GetRouteOptions - destination is null");
            return new List<TrajectorySolution>();
        }

        if (_currentEngine == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: GetRouteOptions - no engine installed");
            return new List<TrajectorySolution>();
        }

        // Find origin body (the body this unit is currently at/orbiting)
        var origin = FindCurrentCelestialBody();

        if (origin == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: GetRouteOptions - cannot determine origin body"
            );
            return new List<TrajectorySolution>();
        }

        GameLogger.Info(
            $"LogisticsUnit {Name}: Getting route options from {origin.Name} (band {BandIndex}) "
                + $"to {destination} (target band: {(targetBandIndex >= 0 ? targetBandIndex.ToString() : "auto")})"
        );

        return TrajectoryPlanner.Instance.GetOptions(
            this,
            origin,
            destination,
            departureTime,
            numOptions,
            TrajectorySolution.RankingCriteria.MostEfficient,
            targetBandIndex
        );
    }

    /// <summary>
    /// Plans a route to the specified destination using the TrajectoryPlanner.
    /// Automatically selects the most efficient trajectory option.
    /// </summary>
    /// <param name="destination">Target destination celestial body.</param>
    /// <param name="departureTime">Departure time in seconds from now (default: 0).</param>
    /// <returns>True if route was planned successfully, false otherwise.</returns>
    public bool PlanRoute(
        ProceduralGeneration.PlanetGeneration.CelestialBody destination,
        float departureTime = 0f
    )
    {
        var options = GetRouteOptions(destination, departureTime);

        if (options.Count == 0)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: No viable trajectory options found to {destination.Name}"
            );
            return false;
        }

        // Select the first (most efficient) option
        return PlanRoute(options[0]);
    }

    /// <summary>
    /// Plans a route using a specific trajectory solution selected by the player.
    /// </summary>
    /// <param name="selectedTrajectory">The trajectory solution to use.</param>
    /// <returns>True if route was planned successfully, false otherwise.</returns>
    public bool PlanRoute(TrajectorySolution selectedTrajectory)
    {
        if (selectedTrajectory == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: PlanRoute - trajectory is null");
            return false;
        }

        if (_currentEngine == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: PlanRoute - no engine installed");
            return false;
        }

        // Calculate fuel needed
        float fuelNeeded = CalculateFuelForDeltaV(selectedTrajectory.DeltaVRequired);

        if (fuelNeeded > _fuel)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: PlanRoute - insufficient fuel "
                    + $"(required: {fuelNeeded:F2}, available: {_fuel:F2})"
            );
            return false;
        }

        // Store the planned trajectory
        _plannedTrajectory = selectedTrajectory;
        _transferTime = selectedTrajectory.TimeOfFlight;
        _departurePosition = selectedTrajectory.PredictedOriginPosition;
        _targetPosition = selectedTrajectory.PredictedDestinationPosition;
        _initialVelocity = selectedTrajectory.InitialVelocity;

        // Set legacy destination field so all code paths can find it
        _destinationBody = selectedTrajectory.DestinationBody;

        GameLogger.Info(
            $"LogisticsUnit {Name}: Route planned from {selectedTrajectory.OriginBody!.BodyName} to {selectedTrajectory.DestinationBody!.BodyName}, "
                + $"TOF: {_transferTime:F1}s, Δv: {selectedTrajectory.DeltaVRequired:F2} m/s, v Initial {selectedTrajectory.InitialVelocity} m/s, v Final {selectedTrajectory.FinalVelocity} m/s"
                + $"Fuel required: {fuelNeeded:F2}"
        );

        // Transition to Planning state
        TransitionTo(LogisticsUnitState.Planning);

        return true;
    }

    /// <summary>
    /// Gets the available delta-v budget based on current fuel and engine.
    /// Uses Tsiolkovsky rocket equation: Δv = Isp × g₀ × ln(m_initial / m_final)
    /// </summary>
    /// <returns>Available delta-v in m/s.</returns>
    public float GetAvailableDeltaV()
    {
        float totalMass = GetTotalMass();
        float fuelMass = _fuel;

        if (fuelMass <= 0f || totalMass <= 0f)
        {
            return 0f;
        }

        float dryMass = totalMass - fuelMass;

        if (_currentEngine == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: GetAvailableDeltaV - no engine installed");
            return 0f;
        }

        float exhaustVelocity = _currentEngine.ExhaustVelocity;

        if (exhaustVelocity <= 0f)
        {
            return 0f;
        }

        // Tsiolkovsky: Δv = Isp × g₀ × ln(m_initial / m_final)
        float deltaV = exhaustVelocity * Mathf.Log(totalMass / dryMass);

        return deltaV;
    }

    /// <summary>
    /// Finds the celestial body this unit is currently at or orbiting.
    /// </summary>
    /// <returns>The current celestial body, or null if not found.</returns>
    private ProceduralGeneration.PlanetGeneration.CelestialBody? FindCurrentCelestialBody()
    {
        // Check parent chain for a CelestialBody
        Node? parent = GetParent();

        while (parent != null)
        {
            if (parent is ProceduralGeneration.PlanetGeneration.CelestialBody celestialBody)
            {
                return celestialBody;
            }
            parent = parent.GetParent();
        }

        // Fallback: check if we can find a body near our position
        // This is a simplified approach - in a full implementation, we'd query the spatial system
        return null;
    }

    /// <summary>
    /// Executes the planned trajectory, starting the transfer.
    /// </summary>
    /// <returns>True if trajectory execution started successfully, false if no trajectory planned.</returns>
    public bool ExecuteTrajectory()
    {
        if (_plannedTrajectory == null)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot execute trajectory - no route planned"
            );
            return false;
        }

        if (!HasFuel())
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Cannot execute trajectory - out of fuel");
            return false;
        }

        // Calculate fuel needed for this trajectory
        float fuelNeeded = CalculateFuelForDeltaV(_plannedTrajectory.DeltaVRequired);
        if (fuelNeeded > _fuel)
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Cannot execute trajectory - insufficient fuel "
                    + $"(required: {fuelNeeded:F2}, available: {_fuel:F2})"
            );
            return false;
        }

        // Use the movement controller if available (hybrid simulation)
        if (
            _movementController != null
            && _plannedTrajectory.OriginBody != null
            && _plannedTrajectory.DestinationBody != null
        )
        {
            bool success = _movementController.InitiateTransfer(
                _plannedTrajectory,
                _plannedTrajectory.OriginBody,
                _plannedTrajectory.DestinationBody
            );

            if (success)
            {
                GameLogger.Info(
                    $"LogisticsUnit {Name}: Transfer initiated via movement controller, "
                        + $"transfer time: {_plannedTrajectory.TimeOfFlight:F1}s"
                );
                return true;
            }
        }

        // Fallback to legacy behavior if controller not available
        // Initialize transfer timing
        _timeInTransfer = 0f;

        // Set state to InTransit
        if (!TransitionTo(LogisticsUnitState.InTransit))
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Cannot transition to InTransit state");
            return false;
        }

        // Disable normal orbital movement during transfer
        _isTraveling = false;

        GameLogger.Info(
            $"LogisticsUnit {Name}: Executing trajectory (legacy), transfer time: {_transferTime:F1}s"
        );

        return true;
    }

    /// <summary>
    /// Applies a burn for the specified delta time, consuming fuel and updating position.
    /// </summary>
    /// <param name="delta">Time step in seconds.</param>
    /// <returns>True if burn was applied, false if not enough fuel or transfer complete.</returns>
    [Obsolete(
        "Use LogisticsMovementController with BurnProfile for profile-based fuel burn during transit. "
            + "This legacy method does not account for the three-phase burn model (Accelerating/Coasting/Decelerating)."
    )]
    public bool ApplyBurn(double delta)
    {
        if (_plannedTrajectory == null)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Cannot apply burn - no trajectory planned");
            return false;
        }

        if (_state != LogisticsUnitState.InTransit)
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Cannot apply burn - not in transit state");
            return false;
        }

        // Check if we have fuel
        if (!HasFuel())
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Burn aborted - out of fuel");
            TransitionTo(LogisticsUnitState.Disabled);
            return false;
        }

        // Calculate fuel consumption for this burn
        float thrust = _currentEngine?.EffectiveThrust ?? 1000f;
        float mass = GetTotalMass();
        float acceleration = thrust / mass;

        // Fuel consumed = thrust * delta / (exhaust_velocity * specific_impulse_factor)
        // Simplified: fuel = thrust * delta / (ve * 10) to avoid excessive fuel burn
        float ve = _currentEngine?.ExhaustVelocity ?? 3000f;
        float fuelConsumed = (float)(thrust * delta / ve);

        // Consume fuel (with minimum to avoid divide by zero)
        if (ve > 0f && fuelConsumed > 0f)
        {
            ConsumeFuel(fuelConsumed);
        }

        // Update position using simplified Kepler propagation
        // This is a simplified ballistic trajectory update
        _timeInTransfer += (float)delta;

        // Calculate progress along the transfer (0 to 1)
        float progress = Mathf.Clamp(_timeInTransfer / _transferTime, 0f, 1f);

        // Interpolate position along the transfer path
        Vector3 newPosition = _departurePosition.Lerp(_targetPosition, progress);

        // Apply initial velocity direction with some velocity decay as we approach target
        Vector3 currentVelocity = _initialVelocity * (1f - progress * 0.5f);
        newPosition += currentVelocity * (float)delta * 0.1f;

        GlobalPosition = newPosition;

        // Check for arrival
        if (_timeInTransfer >= _transferTime)
        {
            return HandleArrival();
        }

        return true;
    }

    /// <summary>
    /// Handles arrival at the destination, transitioning to Arriving state.
    /// </summary>
    /// <returns>True if arrival was handled successfully.</returns>
    private bool HandleArrival()
    {
        GameLogger.Info($"LogisticsUnit {Name}: Transfer complete, arriving at destination");

        // Transition to Arriving state
        if (!TransitionTo(LogisticsUnitState.Arriving))
        {
            GameLogger.Warning($"LogisticsUnit {Name}: Failed to transition to Arriving state");
            return false;
        }

        // Find the target body (we need to re-parent to it)
        Node3D? targetBody = GetNearestBody();
        if (targetBody != null)
        {
            // Reparent to the destination body
            Node3D? currentParent = GetParent<Node3D>();
            if (currentParent != null && currentParent != targetBody)
            {
                currentParent.RemoveChild(this);
                targetBody.CallDeferred("add_child", this);
            }

            // Recalculate orbital parameters for the new parent using IOrbitalBody
            if (targetBody is IOrbitalBody orbitalBody)
            {
                InitializeOrbit(orbitalBody, 0);
            }
            else
            {
                GameLogger.Warning($"LogisticsUnit: Target body does not implement IOrbitalBody");
            }

            GameLogger.Info($"LogisticsUnit {Name}: Successfully arrived and entered orbit");
        }
        else
        {
            GameLogger.Warning(
                $"LogisticsUnit {Name}: Arrived but could not find target body to orbit"
            );
        }

        // Clear the planned trajectory
        _plannedTrajectory = null;
        _timeInTransfer = 0f;

        // Transition to Idle after arrival processing
        TransitionTo(LogisticsUnitState.Idle);

        return true;
    }

    /// <summary>
    /// Resets internal transfer state after the movement controller completes a transfer.
    /// Called by LogisticsMovementController.CompleteTransfer() to keep the unit's own
    /// fields in sync with the controller-managed lifecycle.
    /// </summary>
    /// <param name="newParentBody">The celestial body the unit is now orbiting.</param>
    public void OnTransferComplete(IOrbitalBody? newParentBody)
    {
        // Clear trajectory planning state
        _plannedTrajectory = null;
        _timeInTransfer = 0f;
        _transferTime = 0f;
        _departurePosition = Vector3.Zero;
        _targetPosition = Vector3.Zero;
        _initialVelocity = Vector3.Zero;
        _destinationBody = null;
        _isTraveling = false;

        // Clear stored route options
        _availableRouteOptions = null;
        _routeOptionsDestination = null;

        // Recalculate orbital parameters for the new parent
        if (newParentBody != null)
        {
            if (newParentBody is IOrbitalBody orbitalBody)
            {
                InitializeOrbit(orbitalBody, 0);
            }
            else
            {
                GameLogger.Warning(
                    $"LogisticsUnit: New parent body does not implement IOrbitalBody"
                );
            }
        }

        GameLogger.Debug(
            $"LogisticsUnit {Name}: Transfer state cleared after controller-managed arrival"
        );
    }

    /// <summary>
    /// Gets the nearest body node from current position.
    /// </summary>
    /// <returns>The nearest Node3D parent, or null if none found.</returns>
    private Node3D? GetNearestBody()
    {
        Node3D? nearest = null;
        float nearestDistance = float.MaxValue;

        Node3D? parent = GetParent<Node3D>();
        if (parent != null)
        {
            float dist = GlobalPosition.DistanceTo(parent.GlobalPosition);
            nearest = parent;
            nearestDistance = dist;
        }

        return nearest;
    }

    /// <summary>
    /// Initializes the movement controller for hybrid simulation.
    /// </summary>
    private void InitializeMovementController()
    {
        _movementController = new LogisticsMovementController { Name = "MovementController" };
        CallDeferred("add_child", _movementController);
        GameLogger.Debug($"LogisticsUnit {Name}: Movement controller initialized");
    }

    private void InitializePreviewManager()
    {
        _trajectoryPreviewManager = new TrajectoryPreviewManager { Name = "PreviewManager" };
        CallDeferred("add_child", _trajectoryPreviewManager);
        GameLogger.Debug($"LogisticsUnit {Name}: Preview manager initialized");
    }

    private void InitializeScheduleExecutor()
    {
        _scheduleExecutor = new OrbitalScheduleExecutor { Name = "ScheduleExecutor" };
        CallDeferred("add_child", _scheduleExecutor);
        GameLogger.Debug($"LogisticsUnit {Name}: Schedule executor initialized");
    }

    /// <summary>
    /// Assigns an orbital transfer schedule to this unit.
    /// </summary>
    public bool AssignSchedule(OrbitalTransferSchedule schedule)
    {
        return _scheduleExecutor?.AssignSchedule(schedule) ?? false;
    }

    /// <summary>
    /// Starts the assigned orbital transfer schedule.
    /// </summary>
    public bool StartSchedule()
    {
        return _scheduleExecutor?.StartSchedule() ?? false;
    }

    /// <summary>
    /// Stops the active schedule. Can be resumed with StartSchedule().
    /// </summary>
    public void StopSchedule()
    {
        _scheduleExecutor?.StopSchedule();
    }

    /// <summary>
    /// Cancels and clears the active schedule.
    /// </summary>
    public void CancelSchedule()
    {
        _scheduleExecutor?.CancelSchedule();
    }

    /// <summary>
    /// Executes a single leg as a non-repeating schedule.
    /// </summary>
    public bool ExecuteSingleLeg(Leg leg)
    {
        return _scheduleExecutor?.ExecuteSingleLeg(leg) ?? false;
    }

    /// <summary>
    /// The currently active orbital transfer schedule, if any.
    /// </summary>
    public OrbitalTransferSchedule? ActiveSchedule => _scheduleExecutor?.ActiveSchedule;

    private void CreateVisualRepresentation()
    {
        // Create MeshInstance3D for visual representation
        _meshInstance = new MeshInstance3D { Name = "LogisticsUnitMesh" };

        // Create a box mesh (logistics units look like elongated cargo containers)
        var boxMesh = new BoxMesh
        {
            Size = new Vector3(0.5f, 0.2f, 1.0f),
            SubdivideWidth = 2,
            SubdivideHeight = 1,
            SubdivideDepth = 3,
        };
        _meshInstance.Mesh = boxMesh;

        // Create a metallic material for the logistics unit (silver/light gray)
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.7f, 0.8f), // Silver/light gray
            Metallic = 0.8f,
            Roughness = 0.2f,
        };
        _meshInstance.MaterialOverride = material;

        // Add mesh instance as child
        CallDeferred("add_child", _meshInstance);

        // Add a small cone at the front for a pointed nose
        var noseMesh = new MeshInstance3D { Name = "LogisticsUnitNose" };
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

        _meshInstance.CallDeferred("add_child", noseMesh);

        GameLogger.Debug($"LogisticsUnit visuals created for: {Name}");
    }

    public override void _Process(double delta)
    {
        // Rotate the logistics unit for visual interest
        if (_meshInstance != null && IsActive)
        {
            _meshInstance.RotateY(_rotationSpeed * (float)delta);
        }
    }
}
