using System;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

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
    private Vector3 _initialVelocity;
    private CelestialBody? _originBody;
    private CelestialBody? _destinationBody;

    // Central body reference frame offset.
    // Lambert positions/velocities are relative to the central body.
    // We store the central body so we can translate between reference frames.
    private CelestialBody? _centralBody;

    // The ship's departure position relative to the central body (for Kepler propagation)
    private Vector3 _departurePositionRelative;

    // The ship's actual global departure position (for Lerp fallback)
    private Vector3 _departurePositionGlobal;

    // Orbital state (for Kepler propagation)
    private Vector3 _orbitalPosition;
    private Vector3 _orbitalVelocity;
    private float _gravitationalParameter;
    private float _orbitEpoch;

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

    // ========================================================================
    // GODOT LIFECYCLE
    // ========================================================================

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

        ProcessKeplerPropagation(deltaFloat);

        // Process transfer if active
        if (_isTransferring)
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
        CelestialBody originBody,
        CelestialBody destinationBody
    )
    {
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

        UnsetHostBody();

        // Find the central body (the gravitational center that the Lambert solution is relative to).
        // This is the body whose mu was used for the solve — typically the parent star.
        _centralBody = FindCentralBody(originBody);
        Vector3 centralBodyPos = _centralBody?.GlobalPosition ?? Vector3.Zero;

        // The Lambert solver computed positions/velocities relative to the central body.
        // PredictedOriginPosition and PredictedDestinationPosition are in GLOBAL coordinates
        // (they were produced by PredictBodyPosition which uses GlobalPosition).
        //
        // For Kepler propagation, we need the departure position RELATIVE to the central body.
        // The Lambert initial velocity is already in the central-body-centered frame.
        _departurePosition = trajectory.PredictedOriginPosition;
        _targetPosition = trajectory.PredictedDestinationPosition;
        _initialVelocity = trajectory.InitialVelocity;

        // Departure position relative to central body (for Kepler propagation)
        _departurePositionRelative = _departurePosition - centralBodyPos;

        // Store the ship's actual global position as the Lerp start point
        _departurePositionGlobal = _logisticsUnit!.GlobalPosition;

        // Initialize orbital state for Kepler propagation during transfer
        _orbitalPosition = _departurePositionRelative;
        _orbitalVelocity = _initialVelocity;
        _orbitEpoch = 0f;
        _gravitationalParameter = trajectory.GravitationalParameter;

        _isTransferring = true;
        _logisticsUnit.State = LogisticsUnitState.InTransit;

        GameLogger.Info(
            $"LogisticsMovementController: Transfer initiated - "
                + $"TOF: {_transferTime:F1}s, ΔV: {trajectory.DeltaVRequired:F2} m/s, "
                + $"Central body: {_centralBody?.Name ?? "origin"}"
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

        _isTransferring = false;
        _activeTrajectory = null;
        _timeInTransfer = 0f;

        if (_logisticsUnit != null)
        {
            _logisticsUnit.State = LogisticsUnitState.Idle;
        }

        // Reset to orbital simulation
        _currentSimulationMode =
            _enableHybridSimulation && _isObserved
                ? SimulationMode.FullKepler
                : SimulationMode.Simplified;

        GameLogger.Info("LogisticsMovementController: Transfer cancelled");
    }

    /// <summary>
    /// Gets the current position of the unit based on the active simulation mode.
    /// </summary>
    public Vector3 GetCurrentPosition()
    {
        return _logisticsUnit?.GlobalPosition ?? Vector3.Zero;
    }

    // ========================================================================
    // PRIVATE METHODS - VISIBILITY DETECTION
    // ========================================================================

    /// <summary>
    /// Finds the active camera in the scene.
    /// </summary>
    private Camera3D? FindActiveCamera()
    {
        // Try to get camera from viewport
        var viewport = GetViewport();
        if (viewport == null)
            return null;

        var camera3D = viewport.GetCamera3D();
        if (camera3D != null && camera3D.IsCurrent())
            return camera3D;

        // Fallback: search for any Camera3D in the scene
        return GetTree()?.GetFirstNodeInGroup("MainCamera") as Camera3D;
    }

    /// <summary>
    /// Checks if a position is within the camera's frustum.
    /// </summary>
    private bool IsInCameraFrustum(Camera3D camera, Vector3 position)
    {
        if (camera == null)
            return true; // Assume visible if no camera

        // Transform position to camera space
        // In Godot, cameras look along -Z, so objects in front have negative localPos.Z
        Vector3 localPos = camera.GlobalTransform.Inverse() * position;

        // Check if behind the camera (positive Z in camera space = behind)
        if (localPos.Z > 0.1f)
            return false;

        // Get camera projection data
        float fov = camera.Fov;
        float aspectRatio =
            camera.GetViewport().GetVisibleRect().Size.X
            / camera.GetViewport().GetVisibleRect().Size.Y;

        // Calculate frustum boundaries at the distance of the position
        // Use positive distance (negate Z since camera looks along -Z)
        float distance = -localPos.Z;
        float halfHeight = distance * Mathf.Tan(Mathf.DegToRad(fov * 0.5f));
        float halfWidth = halfHeight * aspectRatio;

        // Check if within frustum bounds
        return Mathf.Abs(localPos.X) <= halfWidth && Mathf.Abs(localPos.Y) <= halfHeight;
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
    /// Processes Kepler propagation for the current frame.
    /// </summary>
    private void ProcessKeplerPropagation(float delta)
    {
        if (_logisticsUnit == null)
            return;

        // During transfer, use trajectory-based propagation
        if (_isTransferring && _activeTrajectory != null)
        {
            // Propagation handled in ProcessTransfer
            return;
        }

        // Use OrbitalMath for Kepler propagation
        if (_gravitationalParameter > 0f)
        {
            float timeStep = delta;
            _orbitEpoch += timeStep;

            Vector3 newPosition = OrbitalMath.GetPositionOnOrbit(
                _orbitalPosition,
                _orbitalVelocity,
                _gravitationalParameter,
                _orbitEpoch
            );

            _logisticsUnit.GlobalPosition = newPosition;
            //_orbitalPosition = newPosition;
        }
    }

    /// <summary>
    /// Processes simplified update for off-screen units.
    /// </summary>
    private void ProcessSimplifiedUpdate(float delta)
    {
        if (_logisticsUnit == null)
            return;

        // Update timer
        _offScreenTimer += delta;

        // Only update position periodically
        if (_offScreenTimer >= _offScreenCalculationInterval)
        {
            _offScreenTimer = 0f;

            // During transfer, propagate along trajectory
            if (_isTransferring && _activeTrajectory != null)
            {
                // Simplified: interpolate from ship's actual departure to destination body
                float progress = Mathf.Clamp(_timeInTransfer / _transferTime, 0f, 1f);
                Vector3 destPos = _destinationBody?.GlobalPosition ?? _targetPosition;
                Vector3 newPos = _departurePositionGlobal.Lerp(destPos, progress);
                _logisticsUnit.GlobalPosition = newPos;
            }
            else
            {
                // For orbital motion, could use simplified circular orbit calculation
                // For now, maintain current position (units don't move much while off-screen)
            }
        }
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

        // The central body may have moved since transfer started (it orbits too).
        // Get its current global position for the reference frame offset.
        Vector3 centralBodyPos = _centralBody?.GlobalPosition ?? Vector3.Zero;

        // Calculate current position using Kepler propagation along transfer orbit
        if (_currentSimulationMode == SimulationMode.FullKepler)
        {
            // Kepler propagation in central-body-centered frame.
            // _departurePositionRelative and _initialVelocity are both relative to the
            // central body, so GetPositionOnOrbit gives us a position relative to it.
            Vector3 positionRelative = OrbitalMath.GetPositionOnOrbit(
                _departurePositionRelative,
                _initialVelocity,
                _gravitationalParameter,
                _timeInTransfer
            );

            // Translate back to global coordinates
            _logisticsUnit.GlobalPosition = positionRelative + centralBodyPos;
        }
        else
        {
            // Simplified: linear interpolation from ship's actual departure to the
            // destination body's current position (it may have moved since planning).
            float progress = Mathf.Clamp(_timeInTransfer / _transferTime, 0f, 1f);
            Vector3 destPos = _destinationBody?.GlobalPosition ?? _targetPosition;
            Vector3 currentPosition = _departurePositionGlobal.Lerp(destPos, progress);
            _logisticsUnit.GlobalPosition = currentPosition;
        }

        // Check for arrival
        if (_timeInTransfer >= _transferTime)
        {
            HandleArrival();
        }
    }

    /// <summary>
    /// Handles arrival at the destination.
    /// </summary>
    private void HandleArrival()
    {
        if (_logisticsUnit == null || _destinationBody == null)
        {
            CancelTransfer();
            return;
        }

        GameLogger.Info($"LogisticsMovementController: Arriving at {_destinationBody.Name}");

        SetHostBody(_destinationBody);
        CompleteTransfer(_destinationBody);
    }

    /// <summary>
    /// Completes the transfer and resets state.
    /// </summary>
    private void CompleteTransfer(CelestialBody finalBody)
    {
        if (_logisticsUnit == null)
            return;

        // Reset controller's own transfer state
        _isTransferring = false;
        _activeTrajectory = null;
        _timeInTransfer = 0f;
        _originBody = null;
        _destinationBody = null;
        _centralBody = null;

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

        // Reset simulation mode
        _currentSimulationMode =
            _enableHybridSimulation && _isObserved
                ? SimulationMode.FullKepler
                : SimulationMode.Simplified;

        GameLogger.Info("LogisticsMovementController: Transfer complete");
    }

    // ========================================================================
    // PRIVATE METHODS - CENTRAL BODY LOOKUP
    // ========================================================================

    /// <summary>
    /// Finds the gravitationally dominant body in the system for the given origin body.
    /// This is the body whose gravitational parameter was used by the Lambert solver.
    /// Uses the same logic as TrajectoryPlanner.FindCentralBody().
    /// </summary>
    private CelestialBody? FindCentralBody(CelestialBody origin)
    {
        if (origin == null)
            return null;

        var bodies = origin.GetTree()?.GetNodesInGroup("CelestialBody");
        if (bodies == null || bodies.Count == 0)
            return null;

        float maxInfluence = 0f;
        CelestialBody? dominantBody = null;
        Vector3 testPosition = origin.GlobalPosition;

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
