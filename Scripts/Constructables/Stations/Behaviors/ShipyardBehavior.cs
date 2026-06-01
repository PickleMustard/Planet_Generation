using System;
using System.Collections.Generic;
using Constructables.Stations;
using Constructables.Tick;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using UtilityLibrary;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// Builds ships for the owning station. The station owns each build's construction state
/// (work progress + the resources it has secured from <see cref="StationSatellite.BulkStorage"/>)
/// and drives ticking directly — the <see cref="ShipBuildQueue"/> is pure enqueue/cancel/pause/slot
/// bookkeeping. Resource checking mirrors <c>ManufacturingBehavior</c>: a build only starts once ALL
/// its required resources are present in the station's storage, at which point they are withdrawn
/// all-at-once. Progress advances by a configurable <c>work_per_tick</c> increment per manufacture
/// tick (work-token based, decoupled from frame time). <see cref="WantsTick"/> lets the station sleep
/// while every build is blocked on resources, waking on storage changes.
/// </summary>
public partial class ShipyardBehavior : RefCounted, IStationBehavior, IStationBehaviorConfigurable
{
    private StationSatellite? _owner;
    private ShipBuildQueue? _shipBuildQueue;

    /// <summary>Per-active/queued-build construction state, owned by the station.</summary>
    private readonly Dictionary<LogisticsUnit, ConstructionState> _buildStates = new();

    private float _progressTimer;
    private const float PROGRESS_SIGNAL_INTERVAL = 0.5f;

    /// <summary>Maximum parallel ship builds. Set from config or defaults to 1.</summary>
    public int MaxParallelShipBuilds { get; set; } = 1;

    /// <summary>Work tokens added to each in-progress build per manufacture tick. Config or defaults to 1.</summary>
    public float WorkPerTick { get; set; } = 1f;

    /// <summary>
    /// Applies inline config from the behaviors: YAML block.
    /// Reads <c>max_parallel_ship_builds</c> and <c>work_per_tick</c>.
    /// </summary>
    public void Configure(Dictionary<string, object> config)
    {
        if (config.TryGetValue("max_parallel_ship_builds", out var val))
            MaxParallelShipBuilds = System.Convert.ToInt32(val);
        if (config.TryGetValue("work_per_tick", out var wpt))
            WorkPerTick = System.Convert.ToSingle(wpt, System.Globalization.CultureInfo.InvariantCulture);
    }

    public StationSatellite? Owner => _owner;

    public int ActiveShipBuildCount => _shipBuildQueue?.ActiveCount ?? 0;
    public int QueuedShipBuildCount => _shipBuildQueue?.QueuedCount ?? 0;
    public int MaxParallelBuilds => _shipBuildQueue?.MaxParallelBuilds ?? 0;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;
        _shipBuildQueue = new ShipBuildQueue(_owner, MaxParallelShipBuilds);
        _shipBuildQueue.ShipCancelled += OnShipCancelled;
        GameLogger.Info(
            $"ShipyardBehavior {_owner.Name}: Initialized with "
            + $"{MaxParallelShipBuilds} parallel build slot(s), work_per_tick={WorkPerTick}"
        );
    }

    public void OnUnregister()
    {
        if (_shipBuildQueue != null)
            _shipBuildQueue.ShipCancelled -= OnShipCancelled;
        _shipBuildQueue = null;
        _buildStates.Clear();
    }

    public void OnDetach()
    {
        if (_shipBuildQueue != null)
            _shipBuildQueue.ShipCancelled -= OnShipCancelled;
        _owner = null;
        _shipBuildQueue = null;
        _buildStates.Clear();
    }

    /// <summary>
    /// Drives every active build: promotes from the queue, secures resources all-at-once from
    /// <see cref="StationSatellite.BulkStorage"/> once all are present, advances work by
    /// <see cref="WorkPerTick"/>, and completes builds when work is done. Runs on the
    /// ManufactureTickEngine background thread; storage reads/writes mirror the manufacturing path.
    /// </summary>
    public void OnManufactureTick(float delta, StationSatellite owner)
    {
        if (_shipBuildQueue == null || _owner == null) return;

        _shipBuildQueue.PromoteFromQueue();

        _progressTimer += delta;
        bool emitProgress = _progressTimer >= PROGRESS_SIGNAL_INTERVAL;
        if (emitProgress)
            _progressTimer = 0f;

        var active = _shipBuildQueue.GetActiveBuilds();
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var ship = active[i];
            if (!_buildStates.TryGetValue(ship, out var state))
                continue;
            if (_shipBuildQueue.IsPaused(ship))
                continue;

            // Not yet started: secure resources all-or-nothing, else mark Blocked for UI clarity.
            if (state.Status != ConstructionStatus.InProgress
                && state.Status != ConstructionStatus.Complete)
            {
                if (state.AvailableResources.Count == 0 && AllResourcesPresent(state))
                {
                    foreach (var kvp in state.RequiredResources)
                        state.AvailableResources[kvp.Key] = _owner.BulkStorage.Withdraw(kvp.Key, kvp.Value);
                    state.TryStart(); // held now satisfies required -> InProgress
                }
                else if (state.Status == ConstructionStatus.Pending)
                {
                    state.TryStart(); // empty held + missing resources -> Blocked
                }
            }

            if (state.Status == ConstructionStatus.InProgress)
                state.UpdateProgress(WorkPerTick); // WorkDone += work_per_tick; Complete at >= WorkRequired

            if (emitProgress)
                SignalBus.Instance?.SafeEmitShipConstructionProgress(
                    ship.Name.ToString(), state.GetProgress(), DisplayStatus(ship, state));

            if (state.Status == ConstructionStatus.Complete)
            {
                _shipBuildQueue.RemoveActive(ship);
                _buildStates.Remove(ship);
                OnShipCompleted(ship);
            }
        }

        if (emitProgress)
        {
            foreach (var ship in _shipBuildQueue.GetQueuedShips())
            {
                _buildStates.TryGetValue(ship, out var state);
                SignalBus.Instance?.SafeEmitShipConstructionProgress(
                    ship.Name.ToString(), state?.GetProgress() ?? 0f, DisplayStatus(ship, state));
            }
        }
    }

    /// <summary>
    /// Wants ticks only while work can actually advance: a queued ship can be promoted into a free
    /// slot, an active build is in progress, or an active build is blocked but its resources are now
    /// all present in storage. Otherwise the station sleeps and is re-woken by a storage change.
    /// </summary>
    public bool WantsTick
    {
        get
        {
            if (_shipBuildQueue == null || _owner == null) return false;

            if (_shipBuildQueue.QueuedCount > 0
                && _shipBuildQueue.ActiveCount < _shipBuildQueue.MaxParallelBuilds)
                return true;

            foreach (var ship in _shipBuildQueue.GetActiveBuilds())
            {
                if (_shipBuildQueue.IsPaused(ship)) continue;
                if (!_buildStates.TryGetValue(ship, out var state)) continue;
                if (state.Status == ConstructionStatus.InProgress) return true;
                if (state.Status != ConstructionStatus.Complete && AllResourcesPresent(state)) return true;
            }
            return false;
        }
    }

    public int Priority => 200;

    // --- Public API (mirrors ConstructionYardStation) ---

    /// <summary>
    /// Creates a new <see cref="LogisticsUnit"/>, initializes it on the target body,
    /// configures it with the given <paramref name="definition"/>, and enqueues it into
    /// the build queue. Station owns the full lifecycle from creation onward.
    /// </summary>
    /// <param name="targetBody">Orbital body the ship will orbit.</param>
    /// <param name="bandIndex">Orbit band index on the target body.</param>
    /// <param name="definition">Ship definition template.</param>
    /// <param name="name">Optional ship name; generated if null.</param>
    /// <returns>The newly created and enqueued <see cref="LogisticsUnit"/>.</returns>
    public LogisticsUnit CreateAndEnqueueShip(
        IOrbitalBody targetBody,
        int bandIndex,
        ShipDefinition definition,
        string? name = null)
    {
        if (_owner == null)
            throw new InvalidOperationException("ShipyardBehavior.CreateAndEnqueueShip: no owner attached");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (targetBody == null)
            throw new ArgumentNullException(nameof(targetBody));

        name ??= UtilityLibrary.NameGeneration.NameGenerator.GenerateShipName();

        var unit = new LogisticsUnit { Name = name };

        // Add to body's satellites container
        targetBody.SatellitesContainer.AddChild(unit);

        // Initialize orbit and cargo
        unit.Initialize((Node3D)targetBody, bandIndex);
        unit.InitializeCargo();

        // Apply ship definition
        unit.SetShipDefinition(definition);
        unit.SetFuelCapacity(definition.FuelCapacity);
        unit.SetDryMass(definition.DryMass);

        // Make visible but inactive during construction
        unit.Visible = true;
        unit.IsActive = false;
        unit.ProcessMode = Node.ProcessModeEnum.Disabled;

        // Track constructing station and create the station-owned build state
        unit.ConstructingStation = _owner;
        _buildStates[unit] = new ConstructionState(definition.WorkRequired, definition.RequiredResources);

        // Enqueue into the build queue
        _shipBuildQueue!.Enqueue(unit);

        // Increment band satellite count
        targetBody.IncrementBandCount(bandIndex);

        // Wake the station tick engine
        ManufactureTickEngine.Instance?.Register(_owner);

        // Emit construction-started signal
        SignalBus.Instance?.SafeEmitShipConstructionStarted(unit);

        GameLogger.Info(
            $"ShipyardBehavior {_owner.Name}: Created and enqueued ship '{name}' "
            + $"(definition='{definition.Name}', band={bandIndex})"
        );

        return unit;
    }

    /// <summary>
    /// Adds a pre-created ship to this station's build queue. The ship must already have its
    /// <see cref="LogisticsUnit.ShipDef"/> set; its construction state is created from that definition.
    /// </summary>
    public void EnqueueShipConstruction(LogisticsUnit ship)
    {
        if (_shipBuildQueue == null)
        {
            GameLogger.Warning(
                $"ShipyardBehavior {_owner?.Name}: Cannot enqueue ship — no build queue"
            );
            return;
        }

        var def = ship.ShipDef;
        if (def == null)
        {
            GameLogger.Warning(
                $"ShipyardBehavior {_owner?.Name}: Cannot enqueue ship '{ship.Name}' — no ShipDefinition set"
            );
            return;
        }

        ship.ConstructingStation = _owner;
        _buildStates[ship] = new ConstructionState(def.WorkRequired, def.RequiredResources);
        _shipBuildQueue.Enqueue(ship);

        // Wake the owning station if sleeping — new order arrived.
        ManufactureTickEngine.Instance?.Register(_owner);
    }

    /// <summary>
    /// Cancels construction of a ship, removing it from the queue or active builds.
    /// Resources already secured for the build are refunded in <see cref="OnShipCancelled"/>.
    /// </summary>
    public void CancelShipConstruction(LogisticsUnit ship)
        => _shipBuildQueue?.Cancel(ship);

    /// <summary>
    /// Toggles the manual-pause flag on an in-queue or active ship.
    /// Paused active ships release their slot and return to the front of the queue.
    /// </summary>
    public void SetShipPaused(LogisticsUnit ship, bool paused)
        => _shipBuildQueue?.SetManualPause(ship, paused);

    /// <summary>
    /// Moves <paramref name="ship"/> to appear immediately before <paramref name="before"/>
    /// in the queue. Pass null to move to the end. No-op for active builds.
    /// </summary>
    public void ReorderQueue(LogisticsUnit ship, LogisticsUnit? before)
        => _shipBuildQueue?.ReorderQueue(ship, before);

    /// <summary>Returns the list of ships waiting in the build queue.</summary>
    public IReadOnlyList<LogisticsUnit> GetShipBuildQueue()
        => _shipBuildQueue?.GetQueuedShips()
            ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();

    /// <summary>Returns the list of ships currently being actively built.</summary>
    public IReadOnlyList<LogisticsUnit> GetActiveBuilds()
        => _shipBuildQueue?.GetActiveBuilds()
            ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();

    // --- Build-state queries for UI (return copies; the tick thread mutates the source) ---

    /// <summary>Whether the user has manually paused this ship's build.</summary>
    public bool IsShipPaused(LogisticsUnit ship) => _shipBuildQueue?.IsPaused(ship) ?? false;

    /// <summary>Normalized [0,1] construction progress for a ship, or 0 if not building.</summary>
    public float GetBuildProgress(LogisticsUnit ship)
        => _buildStates.TryGetValue(ship, out var s) ? s.GetProgress() : 0f;

    /// <summary>Display status: "Paused" when paused, otherwise the construction status.</summary>
    public string GetBuildStatus(LogisticsUnit ship)
        => DisplayStatus(ship, _buildStates.TryGetValue(ship, out var s) ? s : null);

    /// <summary>The resources required to build a ship (copy).</summary>
    public IReadOnlyDictionary<string, int> GetRequiredResources(LogisticsUnit ship)
        => _buildStates.TryGetValue(ship, out var s)
            ? new Dictionary<string, int>(s.RequiredResources)
            : new Dictionary<string, int>();

    /// <summary>The resources already secured (withdrawn and held) for a ship's build (copy).</summary>
    public IReadOnlyDictionary<string, int> GetSecuredResources(LogisticsUnit ship)
        => _buildStates.TryGetValue(ship, out var s)
            ? new Dictionary<string, int>(s.AvailableResources)
            : new Dictionary<string, int>();

    // --- Ship lifecycle callbacks ---

    /// <summary>
    /// Called when a ship finishes construction (detected in <see cref="OnManufactureTick"/>).
    /// Runs on the ManufactureTickEngine background thread — all SceneTree mutations are
    /// marshaled to the main thread via <c>CallDeferred</c>.
    /// </summary>
    private void OnShipCompleted(LogisticsUnit ship)
    {
        if (ship == null) return;

        Callable.From(() =>
        {
            ship.IsActive = true;
            ship.Visible = true;
            ship.ProcessMode = Node.ProcessModeEnum.Inherit;

            SetChildrenVisible(ship, true);

            SignalBus.Instance?.SafeEmitShipConstructed(ship);

            GameLogger.Info(
                $"ShipyardBehavior {_owner?.Name}: Ship '{ship.Name}' construction completed and activated"
            );
        }).CallDeferred();
    }

    /// <summary>
    /// Called by <see cref="ShipBuildQueue"/> when a ship is cancelled (user-initiated, main thread).
    /// Refunds secured resources, then frees the ship node. The single refund site.
    /// </summary>
    private void OnShipCancelled(LogisticsUnit ship)
    {
        if (ship == null) return;

        RefundSecuredResources(ship);
        _buildStates.Remove(ship);

        Callable.From(() =>
        {
            if (ship.GetParent() is Node parent)
                parent.RemoveChild(ship);

            ship.QueueFree();

            SignalBus.Instance?.SafeEmitShipConstructionCancelled(ship);

            GameLogger.Info(
                $"ShipyardBehavior {_owner?.Name}: Ship '{ship.Name}' construction cancelled"
            );
        }).CallDeferred();
    }

    // --- Private helpers ---

    /// <summary>True when the station's BulkStorage holds every resource the build requires.</summary>
    private bool AllResourcesPresent(ConstructionState state)
    {
        if (_owner == null) return false;
        foreach (var kvp in state.RequiredResources)
        {
            if (_owner.BulkStorage.GetQuantity(kvp.Key) < kvp.Value)
                return false;
        }
        return true;
    }

    /// <summary>"Paused" when the build is manually paused, otherwise the construction status string.</summary>
    private string DisplayStatus(LogisticsUnit ship, ConstructionState? state)
    {
        if (_shipBuildQueue?.IsPaused(ship) == true) return "Paused";
        return state?.Status.ToString() ?? "None";
    }

    /// <summary>
    /// Refunds the resources actually secured (withdrawn) for <paramref name="ship"/> back to the
    /// owning station's <see cref="StationSatellite.BulkStorage"/>. A build cancelled before securing
    /// has nothing held, so nothing is refunded (no minting).
    /// </summary>
    private void RefundSecuredResources(LogisticsUnit ship)
    {
        if (_owner == null) return;
        if (!_buildStates.TryGetValue(ship, out var state)) return;

        foreach (var kvp in state.AvailableResources)
        {
            if (kvp.Value > 0)
            {
                _owner.BulkStorage.Deposit(kvp.Key, kvp.Value);
                GameLogger.Debug(
                    $"ShipyardBehavior {_owner.Name}: Refunded {kvp.Value} {kvp.Key} from cancelled ship '{ship.Name}'"
                );
            }
        }
        state.AvailableResources.Clear();
    }

    /// <summary>
    /// Recursively sets visibility on all Node3D children.
    /// Mirrors <c>ConstructionManager.SetChildrenVisible</c>.
    /// </summary>
    private static void SetChildrenVisible(Node node, bool visible)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D child3D)
                child3D.Visible = visible;

            SetChildrenVisible(child, visible);
        }
    }
}
