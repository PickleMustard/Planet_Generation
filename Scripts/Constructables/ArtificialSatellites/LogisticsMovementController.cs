using System;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// Controller for logistics unit movement implementing hybrid simulation.
/// Uses real-time Kepler propagation when the unit is observed (on-screen),
/// simplified position calculation when off-screen for performance,
/// and warp capability for time-skip scenarios.
/// </summary>
public partial class LogisticsMovementController : Node
{
    // =========================================================================
    // EXPORTED PROPERTIES
    // =========================================================================

    [ExportCategory("Hybrid Simulation")]
    [ExportGroup("Visibility Detection")]
    [Export]
    private bool _enableHybridSimulation = true;

    [Export]
    private float _visibilityCheckInterval = 0.5f;

    [Export]
    private float _offScreenCalculationInterval = 1.0f;

    [ExportCategory("Warp")]
    [ExportGroup("Time Skip")]
    [Export]
    private bool _warpEnabled = true;

    [Export]
    private float _minimumWarpTime = 10.0f;

    // =========================================================================
    // PRIVATE FIELDS
    // =========================================================================

    private LogisticsUnit? _logisticsUnit;

    // Simulation state
    private SimulationMode _currentSimulationMode = SimulationMode.FullKepler;
    private bool _isObserved;
    private float _visibilityCheckTimer;
    private float _offScreenTimer;

    // Transfer state
    private bool _isTransferring;
    private TrajectorySolution? _activeTrajectory;
    private float _transferTime;
    private float _timeInTransfer;
    private Vector3 _departurePosition;
    private Vector3 _targetPosition;
    private IOrbitalBody? _originBody;
    private IOrbitalBody? _destinationBody;

    // Central body reference frame offset.
    // Lambert positions/velocities are relative to the central body.
    // We store the central body so we can translate between reference frames.
    private IOrbitalBody? _centralBody;

    // The ship's actual global departure position (for Lerp fallback)
    private Vector3 _departurePositionGlobal;

    // Orbital state (for Kepler propagation)
    private Vector3 _orbitalPosition;
    private Vector3 _orbitalVelocity;
    private float _gravitationalParameter;
    private float _orbitEpoch;

    // Burn profile state (fuel consumption during transfer)
    private BurnProfile? _burnProfile;
    private TransitPhase _currentTransitPhase = TransitPhase.Coasting;
    private float _fuelConsumedThisTransfer;

    // Cached camera for visibility checks
    private Camera3D? _activeCamera;

    // ========================================================================
    // ENUMS
    // ========================================================================

    /// <summary>
    /// Simulation mode for hybrid simulation.
    /// </summary>
    public enum SimulationMode
    {
        /// <summary>Full Kepler propagation - used when unit is observed</summary>
        FullKepler,

        /// <summary>Simplified calculation - used when unit is off-screen</summary>
        Simplified,

        /// <summary>Warp/teleport - instant position update</summary>
        Warp,
    }

    // ========================================================================
    // PROPERTIES
    // ========================================================================

    public SimulationMode CurrentSimulationMode => _currentSimulationMode;

    public bool IsObserved => _isObserved;

    public bool IsTransferring => _isTransferring;

    [ExportCategory("Transfer")]
    [Export]
    public bool WarpEnabled
    {
        get => _warpEnabled;
        set => _warpEnabled = value;
    }

    [Export]
    public float TransferProgress
    {
        get =>
            _activeTrajectory != null && _activeTrajectory.TimeOfFlight > 0
                ? Mathf.Clamp(_timeInTransfer / _activeTrajectory.TimeOfFlight, 0f, 1f)
                : 0f;
        set => _timeInTransfer = value * _activeTrajectory!.TimeOfFlight;
    }

    /// <summary>
    /// The current burn phase during an active transfer (Accelerating, Coasting, or Decelerating).
    /// Only meaningful when <see cref="IsTransferring"/> is true.
    /// </summary>
    public TransitPhase CurrentTransitPhase => _currentTransitPhase;

    /// <summary>
    /// The active burn profile for the current transfer, or null if not transferring.
    /// </summary>
    public BurnProfile? ActiveBurnProfile => _burnProfile;

    /// <summary>
    /// Total fuel consumed during the current transfer so far, in kg.
    /// </summary>
    public float FuelConsumedThisTransfer => _fuelConsumedThisTransfer;

    public override void _Ready()
    {
        GameLogger.Info("LogisticsMovementController: Initializing");

        // Get parent LogisticsUnit
        _logisticsUnit = GetParent<LogisticsUnit>();
        if (_logisticsUnit == null)
        {
            GameLogger.Error("LogisticsMovementController: Parent is not a LogisticsUnit!");
            return;
        }

        // Initialize orbital state from parent
        InitializeOrbitalState();

        GameLogger.Info("LogisticsMovementController: Ready");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_logisticsUnit == null || !_logisticsUnit.IsActive)
            return;

        float deltaFloat = (float)delta;

        var state = _logisticsUnit.State;
        if (state == LogisticsUnitState.Idle || state == LogisticsUnitState.Planning)
        {
            ProcessOrbit(deltaFloat);
        }
        else if (_isTransferring)
        {
            ProcessTransfer(deltaFloat);
        }
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Initiates a transfer to the specified destination using a trajectory.
    /// </summary>
    /// <param name="trajectory">The trajectory solution to execute.</param>
    /// <param name="originBody">The origin celestial body.</param>
    /// <param name="destinationBody">The destination celestial body.</param>
    /// <returns>True if transfer initiated successfully.</returns>
    public bool InitiateTransfer(
        TrajectorySolution trajectory,
        IOrbitalBody originBody,
        IOrbitalBody destinationBody
    )
    {
        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Starting transfer initiation - "
                + $"trajectoryOrigin={trajectory?.OriginBody}, trajectoryDestination={trajectory?.DestinationBody}, "
                + $"originBody={originBody}, destinationBody={destinationBody}, unit={_logisticsUnit?.Name}"
        );

        if (trajectory == null)
        {
            GameLogger.Warning(
                "LogisticsMovementController: Cannot initiate transfer - trajectory is null"
            );
            return false;
        }

        if (originBody == null || destinationBody == null)
        {
            GameLogger.Warning(
                "LogisticsMovementController: Cannot initiate transfer - origin or destination body is null"
            );
            return false;
        }

        // Store transfer data
        _activeTrajectory = trajectory;
        _originBody = originBody;
        _destinationBody = destinationBody;
        _transferTime = trajectory.TimeOfFlight;
        _timeInTransfer = 0f;

        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Stored trajectory data - "
                + $"transferTime={_transferTime:F2}s, departureDeltaV={trajectory.DepartureDeltaV:F2}m/s, "
                + $"arrivalDeltaV={trajectory.ArrivalDeltaV:F2}m/s, totalDeltaV={trajectory.DeltaVRequired:F2}m/s, "
                + $"semiMajorAxis={trajectory.SemiMajorAxis:F2}m, eccentricity={trajectory.Eccentricity:F4}, "
                + $"originBand={trajectory.OriginBandIndex}, destinationBand={trajectory.DestinationBandIndex}"
        );

        UnsetHostBody();

        // Find the central body (the gravitational center that the Lambert solution is relative to).
        // This is the body whose mu was used for the solve — typically the parent star.
        _centralBody = FindCentralBody(originBody);
        Vector3 centralBodyPos = _centralBody?.BodyPosition ?? Vector3.Zero;
        Vector3 centralBodyVel = _centralBody?.Velocity ?? Vector3.Zero;

        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Central body determined - "
                + $"centralBody={_centralBody}, centralBodyPos={centralBodyPos}, centralBodyVel={centralBodyVel}"
        );

        // The Lambert solver computed positions/velocities relative to the central body.
        // PredictedOriginPosition and PredictedDestinationPosition are in GLOBAL coordinates
        // (they were produced by PredictBodyPosition which uses GlobalPosition).
        //
        // For Kepler propagation, we need the departure position RELATIVE to the central body.
        // The Lambert initial velocity is already in the central-body-centered frame.
        _departurePosition = trajectory.PredictedOriginPosition;
        _targetPosition = trajectory.PredictedDestinationPosition;

        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Trajectory positions - "
                + $"departurePositionGlobal={_departurePosition}, targetPositionGlobal={_targetPosition}, "
                + $"initialVelocity={trajectory.InitialVelocity}, initialSpeed={trajectory.InitialVelocity.Length():F2}m/s"
        );

        // Store the ship's actual global position as the Lerp start point
        _departurePositionGlobal = _logisticsUnit!.GlobalPosition;

        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Reference frame positions - "
                + $"departurePositionGlobalActual={_departurePositionGlobal}, "
                + $"shipCurrentGlobalPos={_logisticsUnit.GlobalPosition}"
        );

        _gravitationalParameter = trajectory.GravitationalParameter;

        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Element-based propagation - "
                + $"a={trajectory.SemiMajorAxis:F2}m, e={trajectory.Eccentricity:F4}, "
                + $"i={trajectory.Inclination:F2}°, Ω={trajectory.AscendingNodeLongitude:F2}°, "
                + $"ω={trajectory.ArgumentOfPeriapsis:F2}°, M₀={trajectory.MeanAnomaly:F2}°, "
                + $"μ={_gravitationalParameter:E2} m³/s²"
        );

        _currentSimulationMode = SimulationMode.FullKepler;
        _isTransferring = true;
        _logisticsUnit.State = LogisticsUnitState.InTransit;

        // Build the burn profile from the trajectory's ΔV split and the ship's engine
        _fuelConsumedThisTransfer = 0f;
        _currentTransitPhase = TransitPhase.Accelerating;

        var engine = _logisticsUnit.CurrentEngine;
        float totalMass = _logisticsUnit.GetTotalMass();

        GameLogger.Info(
            $"LogisticsMovementController.InitiateTransfer: Burn profile setup - "
                + $"engineIsp={engine?.BaseSpecificImpulse ?? 0:F2}s, engineThrust={engine?.BaseThrust ?? 0:F2}N, "
                + $"exhaustVelocity={engine?.ExhaustVelocity ?? 0:F2}m/s, totalMass={totalMass:F2}kg"
        );

        if (engine != null)
        {
            _burnProfile = BurnProfile.Calculate(trajectory, engine, totalMass);
            if (_burnProfile == null)
            {
                GameLogger.Warning(
                    "LogisticsMovementController: Failed to calculate burn profile — "
                        + "fuel will not be consumed during this transfer"
                );
            }
            else
            {
                GameLogger.Info(
                    $"LogisticsMovementController.InitiateTransfer: Burn profile calculated - "
                        + $"accelBurnDuration={_burnProfile.AccelBurnDuration:F2}s, "
                        + $"coastDuration={_burnProfile.CoastDuration:F2}s, "
                        + $"decelBurnDuration={_burnProfile.DecelBurnDuration:F2}s, "
                        + $"totalFuelBudget={_burnProfile.TotalFuelBudget:F2}kg, "
                        + $"accelFuelRate={_burnProfile.AccelFuelRate:F4}kg/s, "
                        + $"decelFuelRate={_burnProfile.DecelFuelRate:F4}kg/s, "
                        + $"accelFuelBudget={_burnProfile.AccelFuelBudget:F2}kg, "
                        + $"decelFuelBudget={_burnProfile.DecelFuelBudget:F2}kg"
                );
            }
        }
        else
        {
            GameLogger.Warning(
                "LogisticsMovementController: No engine installed — "
                    + "fuel will not be consumed during this transfer"
            );
            _burnProfile = null;
        }

        GameLogger.Info(
            $"LogisticsMovementController: Transfer initiated - "
                + $"TOF: {_transferTime:F1}s, ΔV: {trajectory.DeltaVRequired:F2} m/s "
                + $"(depart: {trajectory.DepartureDeltaV:F2}, arrive: {trajectory.ArrivalDeltaV:F2}), "
                + $"Central body: {_centralBody!.ToString() ?? "origin"}, "
                + $"Origin band: {trajectory.OriginBandIndex}, Target band: {trajectory.DestinationBandIndex}"
                + (_burnProfile != null ? $", Burn profile: {_burnProfile.GetDescription()}" : "")
        );

        return true;
    }

    /// <summary>
    /// Cancels any active transfer.
    /// </summary>
    public void CancelTransfer()
    {
        if (!_isTransferring)
            return;

        GameLogger.Info(
            $"LogisticsMovementController: Transfer cancelled at T+{_timeInTransfer:F1}s — "
                + $"fuel consumed: {_fuelConsumedThisTransfer:F2}kg (not refunded)"
        );

        _isTransferring = false;
        _activeTrajectory = null;
        _timeInTransfer = 0f;

        // Reset burn profile state — fuel already consumed is NOT refunded
        _burnProfile = null;
        _currentTransitPhase = TransitPhase.Coasting;
        _fuelConsumedThisTransfer = 0f;

        if (_logisticsUnit != null)
        {
            _logisticsUnit.State = LogisticsUnitState.Idle;
        }

        // Default to FullKepler — visibility-based LOD can be layered on later
        _currentSimulationMode = SimulationMode.FullKepler;
    }

    /// <summary>
    /// Initializes orbital state from the parent logistics unit.
    /// </summary>
    private void InitializeOrbitalState()
    {
        if (_logisticsUnit == null)
            return;

        // Get the parent celestial body
        var parentBody = _logisticsUnit.GetParent<Node3D>();
        if (parentBody is CelestialBody celestialBody)
        {
            _gravitationalParameter = OrbitalMath.GRAVITATIONAL_CONSTANT * celestialBody.Mass;
            _orbitalPosition = _logisticsUnit.GlobalPosition;
            _orbitalVelocity = Vector3.Zero; // Would need to be calculated from orbital parameters
            _orbitEpoch = 0f;
        }
    }

    /// <summary>
    /// Processes sinusoidal circular orbit for Idle/Planning states.
    /// </summary>
    private void ProcessOrbit(float delta)
    {
        Node3D? parentBody = _logisticsUnit!.GetParent<Node3D>();
        if (parentBody == null)
            return;

        // Update orbital angle
        _logisticsUnit.OrbitalAngle += _logisticsUnit.OrbitalSpeed * (float)delta;

        // Keep angle in valid range [0, 2*PI]
        if (_logisticsUnit!.OrbitalAngle > Mathf.Tau)
            _logisticsUnit.OrbitalAngle -= Mathf.Tau;

        // Calculate position: parent position + orbital offset
        float cos = Mathf.Cos(_logisticsUnit!.OrbitalAngle);
        float sin = Mathf.Sin(_logisticsUnit!.OrbitalAngle);

        // Position in XZ plane
        _logisticsUnit.GlobalPosition =
            parentBody.GlobalPosition
            + new Vector3(
                cos * _logisticsUnit.OrbitalRadius,
                0,
                sin * _logisticsUnit.OrbitalRadius
            );

        // Calculate and store velocity (tangent to orbit)
        float linearSpeed = _logisticsUnit.OrbitalRadius * _logisticsUnit.OrbitalSpeed;
        _logisticsUnit.Velocity = new Vector3(-sin * linearSpeed, 0f, cos * linearSpeed);
    }

    // ========================================================================
    // PRIVATE METHODS - TRANSFER PROCESSING
    // ========================================================================

    /// <summary>
    /// Processes the active transfer for one frame.
    /// </summary>
    private void ProcessTransfer(float delta)
    {
        if (_logisticsUnit == null || _activeTrajectory == null)
            return;

        // Update time in transfer
        _timeInTransfer += delta;

        //GameLogger.Info(
        //    $"LogisticsMovementController.ProcessTransfer: Frame update - "
        //        + $"delta={delta:F4}s, timeInTransfer={_timeInTransfer:F2}s, "
        //        + $"transferTime={_transferTime:F2}s, progress={(_timeInTransfer / _transferTime * 100f):F1}%, "
        //        + $"currentGlobalPos={_logisticsUnit.GlobalPosition}"
        //);

        // ---- Fuel consumption based on burn profile ----
        if (_burnProfile != null)
        {
            TransitPhase previousPhase = _currentTransitPhase;
            _currentTransitPhase = _burnProfile.GetPhaseAtTime(_timeInTransfer);

            if (_currentTransitPhase != previousPhase)
            {
                GameLogger.Info(
                    $"LogisticsMovementController: Transit phase changed — "
                        + $"{previousPhase} → {_currentTransitPhase} "
                        + $"at T+{_timeInTransfer:F1}s, fuel consumed so far: {_fuelConsumedThisTransfer:F2}kg"
                );
            }

            float fuelRate = _burnProfile.GetFuelRateAtTime(_timeInTransfer);
            //GameLogger.Info(
            //    $"LogisticsMovementController.ProcessTransfer: Fuel status - "
            //        + $"phase={_currentTransitPhase}, fuelRate={fuelRate:F4}kg/s, "
            //        + $"currentFuel={_logisticsUnit.Fuel:F2}kg, fuelConsumedSoFar={_fuelConsumedThisTransfer:F2}kg"
            //);

            if (fuelRate > 0f)
            {
                if (_logisticsUnit.HasFuel())
                {
                    float frameFuel = fuelRate * delta;
                    _logisticsUnit.ConsumeFuel(frameFuel);
                    _fuelConsumedThisTransfer += frameFuel;

                    //GameLogger.Info(
                    //    $"LogisticsMovementController.ProcessTransfer: Fuel consumed - "
                    //        + $"frameFuel={frameFuel:F4}kg, totalConsumed={_fuelConsumedThisTransfer:F2}kg, "
                    //        + $"remainingFuel={_logisticsUnit.Fuel:F2}kg"
                    //);
                }
                else
                {
                    // Ship ran out of fuel mid-burn — strand it
                    HandleStranded();
                    return;
                }
            }
        }

        // ---- Position update ----
        // The central body may have moved since transfer started (it orbits too).
        // Get its current global position for the reference frame offset.
        Vector3 centralBodyPos = _centralBody?.BodyPosition ?? Vector3.Zero;
        Vector3 centralBodyVel = _centralBody?.Velocity ?? Vector3.Zero;

        //GameLogger.Info(
        //    $"LogisticsMovementController.ProcessTransfer: Central body state - "
        //        + $"centralBodyPos={centralBodyPos}, centralBodyVel={centralBodyVel}, "
        //        + $"gravitationalParameter={_gravitationalParameter:E2}"
        //);

        //GameLogger.Info(
        //    $"LogisticsMovementController.ProcessTransfer: Element propagation inputs - "
        //        + $"a={_activeTrajectory.SemiMajorAxis:F2}, e={_activeTrajectory.Eccentricity:F4}, "
        //        + $"timeInTransfer={_timeInTransfer:F2}s, mu={_gravitationalParameter:E2}"
        //);

        // Propagate position from the transfer orbit's classical elements.
        // The elements on _activeTrajectory describe the exact conic (ellipse or hyperbola)
        // of the transfer arc. We advance the mean anomaly by elapsed time to find the
        // current position on that conic.
        Vector3 positionRelative = KeplerianMechanics.PropagateFromElements(
            _activeTrajectory.SemiMajorAxis,
            _activeTrajectory.Eccentricity,
            _activeTrajectory.Inclination,
            _activeTrajectory.AscendingNodeLongitude,
            _activeTrajectory.ArgumentOfPeriapsis,
            _activeTrajectory.MeanAnomaly,
            _gravitationalParameter,
            _timeInTransfer
        );

        // Translate back to global coordinates
        Vector3 newGlobalPosition = positionRelative;

        //GameLogger.Info(
        //    $"LogisticsMovementController.ProcessTransfer: Kepler propagation result - "
        //        + $"positionRelative={positionRelative}, newGlobalPosition={newGlobalPosition}, "
        //        + $"displacementFromDeparture={(newGlobalPosition - _departurePositionGlobal).Length():F2}m"
        //);

        _logisticsUnit.GlobalPosition = newGlobalPosition;

        // Check for arrival
        if (_timeInTransfer >= _transferTime)
        {
            HandleArrival();
        }
    }

    /// <summary>
    /// Handles arrival at the destination.
    /// Uses the target orbit band from the trajectory solution to place the ship
    /// into the correct orbit band at the destination body.
    /// </summary>
    private void HandleArrival()
    {
        if (_logisticsUnit == null || _destinationBody == null)
        {
            CancelTransfer();
            return;
        }

        int targetBand = _activeTrajectory?.DestinationBandIndex ?? -1;
        GameLogger.Info(
            $"LogisticsMovementController: Arriving at {_destinationBody}"
                + (targetBand >= 0 ? $", target band: {targetBand}" : "")
        );

        if (_destinationBody is Node dest)
            SetHostBody(dest);
        CompleteTransfer(_destinationBody, targetBand);
    }

    /// <summary>
    /// Handles the ship running out of fuel mid-transit. The ship is left at its current
    /// position in the system container (adrift in space) and transitions to Stranded state.
    /// Fuel already consumed is NOT refunded — the burns already happened.
    /// </summary>
    private void HandleStranded()
    {
        if (_logisticsUnit == null)
            return;

        string phaseName = _currentTransitPhase.ToString();
        float budgeted = _burnProfile?.TotalFuelBudget ?? 0f;

        GameLogger.Warning(
            $"LogisticsMovementController: Ship stranded — fuel exhausted during {phaseName} phase "
                + $"at T+{_timeInTransfer:F1}s/{_transferTime:F1}s. "
                + $"Fuel consumed: {_fuelConsumedThisTransfer:F2}kg of {budgeted:F2}kg budgeted."
        );

        // Stop the transfer but leave the ship at its current global position.
        // It stays parented to the system container (set by UnsetHostBody during InitiateTransfer).
        _isTransferring = false;
        _activeTrajectory = null;
        _burnProfile = null;
        _currentTransitPhase = TransitPhase.Coasting;

        _logisticsUnit.TransitionTo(LogisticsUnitState.Stranded);

        // Reset simulation mode — ship is adrift, no orbital motion
        _currentSimulationMode = SimulationMode.Simplified;

        // Reset origin/destination but keep central body for potential rescue calculations
        _originBody = null;
        _destinationBody = null;
    }

    /// <summary>
    /// Completes the transfer and resets state.
    /// </summary>
    /// <param name="finalBody">The celestial body the ship is arriving at.</param>
    /// <param name="targetBandIndex">Target orbit band index (-1 = use default/band 0).</param>
    private void CompleteTransfer(IOrbitalBody finalBody, int targetBandIndex = -1)
    {
        if (_logisticsUnit == null)
            return;

        // Log fuel consumption summary before resetting state
        float budgeted = _burnProfile?.TotalFuelBudget ?? 0f;
        float drift = budgeted > 0f ? _fuelConsumedThisTransfer - budgeted : 0f;
        GameLogger.Info(
            $"LogisticsMovementController: Transfer complete — "
                + $"fuel consumed: {_fuelConsumedThisTransfer:F2}kg of {budgeted:F2}kg budgeted "
                + $"(drift: {drift:+0.00;-0.00;0}kg)"
        );

        // Reset controller's own transfer state
        _isTransferring = false;
        _activeTrajectory = null;
        _timeInTransfer = 0f;
        _originBody = null;
        _destinationBody = null;
        _centralBody = null;

        // Reset burn profile state
        _burnProfile = null;
        _currentTransitPhase = TransitPhase.Coasting;
        _fuelConsumedThisTransfer = 0f;

        // Set the unit's band index to the target band before reinitializing orbit
        if (targetBandIndex >= 0 && targetBandIndex < finalBody.GetBandCount())
        {
            _logisticsUnit.BandIndex = targetBandIndex;
            GameLogger.Info(
                $"LogisticsMovementController: Ship entering band {targetBandIndex} "
                    + $"at {finalBody}"
            );
        }
        else if (finalBody.GetBandCount() > 0)
        {
            // Default to band 0 if target band is invalid
            _logisticsUnit.BandIndex = 0;
            GameLogger.Debug($"LogisticsMovementController: Defaulting to band 0 at {finalBody}");
        }

        // Transition the unit to Idle
        _logisticsUnit.State = LogisticsUnitState.Idle;

        // Clean up the unit's own stale transfer fields and reinitialize orbit
        _logisticsUnit.OnTransferComplete(finalBody);

        // Update controller's orbital state for new parent
        if (finalBody != null)
        {
            _gravitationalParameter = OrbitalMath.GRAVITATIONAL_CONSTANT * finalBody.Mass;
            _orbitalPosition = _logisticsUnit.GlobalPosition;
            _orbitalVelocity = Vector3.Zero;
            _orbitEpoch = 0f;
        }

        // Default to FullKepler — visibility-based LOD can be layered on later
        _currentSimulationMode = SimulationMode.FullKepler;
    }

    // ========================================================================
    // PRIVATE METHODS - CENTRAL BODY LOOKUP
    // ========================================================================

    /// <summary>
    /// Finds the gravitationally dominant body in the system for the given origin body.
    /// This is the body whose gravitational parameter was used by the Lambert solver.
    /// Uses the same logic as TrajectoryPlanner.FindCentralBody().
    /// </summary>
    private IOrbitalBody? FindCentralBody(IOrbitalBody origin)
    {
        if (origin == null)
            return null;

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var bodies = tree.GetNodesInGroup("CelestialBody");
        if (bodies == null || bodies.Count == 0)
            return null;

        float maxInfluence = 0f;
        CelestialBody? dominantBody = null;
        Vector3 testPosition = origin.BodyPosition;

        foreach (Node node in bodies)
        {
            if (node is CelestialBody body && body != origin)
            {
                float distanceSq = testPosition.DistanceSquaredTo(body.GlobalPosition);
                if (distanceSq > 0.001f)
                {
                    float influence = OrbitalMath.GRAVITATIONAL_CONSTANT * body.Mass / distanceSq;
                    if (influence > maxInfluence)
                    {
                        maxInfluence = influence;
                        dominantBody = body;
                    }
                }
            }
        }

        return dominantBody;
    }

    private void UnsetHostBody()
    {
        Node systemContainer = GetNode<Node>("/root/system/system_container");
        if (systemContainer != null)
        {
            _logisticsUnit!.ReparentToHostBody(systemContainer);
        }
        else
        {
            GD.PrintErr("Could not find system container");
        }
    }

    private void SetHostBody(Node newHost)
    {
        _logisticsUnit!.ReparentToHostBody(newHost);
    }
}
