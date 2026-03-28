using System;
using Constructables.ArtificialSatellites;
using Godot;
using Godot.Collections;
using Structures.Enums;
using UtilityLibrary;

/* Construction Manager is responsible for managing the communication of construction requests and events
 * between the user and various systems
 *
 * Construction for units is distinct for different types
 * Artificial Satellites and Stations can be constructed in any available band around a body
 *  or can be constructed in a defined orbit around a dominant body / barycenter
 *
 * Ships can only be constructed by certain Stations that have the capacity to fabricate them
 *   Spawned ships will inherit the orbit, position, and velocity of the station that spawned them
 *
 * To coordinate between the variuos endpoints and the GUI, the construction manager will respond to requests from the user,
 *  reflect state of construction that is managed by the various systems, own and generate signals for state changes, and parse
 *  data from the GUI and route it to the appropriate systems
 */
namespace Constructables;

public partial class ConstructionManager : Node
{
    [Signal]
    public delegate void StationConstructionInitializedEventHandler(Dictionary details);

    [Signal]
    public delegate void StationConstructionCompletedEventHandler(Dictionary details);

    [Signal]
    public delegate void StationConstructionCancelledEventHandler(Dictionary details);

    [Signal]
    public delegate void ShipConstructionInitializedEventHandler(Dictionary details);

    [Signal]
    public delegate void ShipConstructionCompletedEventHandler(Dictionary details);

    [Signal]
    public delegate void ShipConstructionCancelledEventHandler(Dictionary details);

    [Signal]
    public delegate void ConstructionProgressUpdatedEventHandler(
        string entityName,
        float progress,
        string status
    );

  private static ConstructionManager _instance;
    public static ConstructionManager Instance => _instance;

    [Export]
    public Array<StationSatellite> _stationsUnderConstruction;

    [Export]
    public Array<LogisticsUnit> _shipsUnderConstruction;

    private float _progressSignalTimer;
    private const float PROGRESS_SIGNAL_INTERVAL = 0.5f;

    private ConstructionManager()
    {
        _stationsUnderConstruction = new Array<StationSatellite>();
        _shipsUnderConstruction = new Array<LogisticsUnit>();
        _instance = this;
    }

    //Ensure singleton instance & signals are connected to correct methods
    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;

        StationConstructionInitialized += OnStationConstructionInitialized;
        StationConstructionCompleted += OnStationConstructionCompleted;
        StationConstructionCancelled += OnStationConstructionCancelled;
        ShipConstructionInitialized += OnShipConstructionInitialized;
        ShipConstructionCompleted += OnShipConstructionCompleted;
        ShipConstructionCancelled += OnShipConstructionCancelled;

        GD.Print($"[ConstructionManager] Initialized");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _progressSignalTimer += dt;
        bool emitProgress = _progressSignalTimer >= PROGRESS_SIGNAL_INTERVAL;
        if (emitProgress)
            _progressSignalTimer = 0f;

        // Tick stations under construction (iterate in reverse for safe removal)
        for (int i = _stationsUnderConstruction.Count - 1; i >= 0; i--)
        {
            var station = _stationsUnderConstruction[i];
            if (!IsInstanceValid(station))
            {
                _stationsUnderConstruction.RemoveAt(i);
                continue;
            }

            station.UpdateProgress(dt);

            if (emitProgress)
            {
                EmitSignal(
                    SignalName.ConstructionProgressUpdated,
                    station.Name.ToString(),
                    station.GetProgress(),
                    station.GetStatus()
                );
            }

            if (station.GetStatus() == ConstructionStatus.Complete.ToString())
            {
                _stationsUnderConstruction.RemoveAt(i);
                FinalizeStation(station, new Dictionary());
                EmitSignal(
                    SignalName.StationConstructionCompleted,
                    new Dictionary { { "station", station }, { "name", station.Name.ToString() } }
                );
            }
        }

        // Tick ships under construction
        for (int i = _shipsUnderConstruction.Count - 1; i >= 0; i--)
        {
            var ship = _shipsUnderConstruction[i];
            if (!IsInstanceValid(ship))
            {
                _shipsUnderConstruction.RemoveAt(i);
                continue;
            }

            ship.UpdateProgress(dt);

            if (emitProgress)
            {
                EmitSignal(
                    SignalName.ConstructionProgressUpdated,
                    ship.Name.ToString(),
                    ship.GetProgress(),
                    ship.GetStatus()
                );
            }

            if (ship.GetStatus() == ConstructionStatus.Complete.ToString())
            {
                _shipsUnderConstruction.RemoveAt(i);
                FinalizeShip(ship, new Dictionary());
                EmitSignal(
                    SignalName.ShipConstructionCompleted,
                    new Dictionary { { "ship", ship }, { "name", ship.Name.ToString() } }
                );
            }
        }
    }

    public void EmitStationConstruct(StationSatellite inConstruction, Dictionary details)
    {
        _stationsUnderConstruction!.Add(inConstruction);
        EmitSignal(SignalName.StationConstructionInitialized, details);
    }

    public void EmitShipConstruct(LogisticsUnit inConstruction, Dictionary details)
    {
        _shipsUnderConstruction!.Add(inConstruction);
        EmitSignal(SignalName.ShipConstructionInitialized, details);
    }

    public void EmitStationCancel(StationSatellite cancelled, Dictionary details)
    {
        _stationsUnderConstruction!.Remove(cancelled);
        EmitSignal(SignalName.StationConstructionCancelled, details);
    }

    public void EmitShipCancel(LogisticsUnit cancelled, Dictionary details)
    {
        _shipsUnderConstruction!.Remove(cancelled);
        EmitSignal(SignalName.ShipConstructionCancelled, details);
    }

    public void EmitStationComplete(StationSatellite completed, Dictionary details)
    {
        _stationsUnderConstruction!.Remove(completed);
        EmitSignal(SignalName.StationConstructionCompleted, details);
    }

    public void EmitShipComplete(LogisticsUnit completed, Dictionary details)
    {
        _shipsUnderConstruction!.Remove(completed);
        EmitSignal(SignalName.ShipConstructionCompleted, details);
    }

    //Given a filter, return a list of all stations under construction
    public Array<StationSatellite> GetStationsUnderConstruction(
        String orbitalFilter = "",
        String typeFilter = ""
    )
    {
        if (string.IsNullOrEmpty(orbitalFilter) && string.IsNullOrEmpty(typeFilter))
            return _stationsUnderConstruction!;

        var filtered = new Array<StationSatellite>();
        foreach (var station in _stationsUnderConstruction!)
        {
            if (
                !string.IsNullOrEmpty(orbitalFilter)
                && station.BandIndex.ToString() != orbitalFilter
            )
                continue;

            if (
                !string.IsNullOrEmpty(typeFilter)
                && !station.Name.ToString().Contains(typeFilter, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            filtered.Add(station);
        }

        return filtered;
    }

    //Given a filter, return a list of all ships under construction
    public Array<LogisticsUnit> GetShipsUnderConstruction(
        String stationFilter = "",
        String typeFilter = ""
    )
    {
        if (string.IsNullOrEmpty(stationFilter) && string.IsNullOrEmpty(typeFilter))
            return _shipsUnderConstruction!;

        var filtered = new Array<LogisticsUnit>();
        foreach (var ship in _shipsUnderConstruction!)
        {
            if (!string.IsNullOrEmpty(stationFilter))
            {
                var parent = ship.GetParent();
                if (
                    parent == null
                    || !parent
                        .Name.ToString()
                        .Contains(stationFilter, StringComparison.OrdinalIgnoreCase)
                )
                    continue;
            }

            if (
                !string.IsNullOrEmpty(typeFilter)
                && !ship.Name.ToString().Contains(typeFilter, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            filtered.Add(ship);
        }

        return filtered;
    }

    //Will be given an empty StationSatellite object and a dictionary containing configuration details
    //Should configure variables from position and band index, should place in SceneTree
    //Should disable any interaction variables until finished or cancelled
    private void InitializeStation(StationSatellite inConstruction, Dictionary details)
    {
        var parentBody = details["parent_body"].As<Node3D>();
        int bandIndex = details["band_index"].AsInt32();

        inConstruction.IsActive = false;
        inConstruction.Visible = false;
        inConstruction.ProcessMode = ProcessModeEnum.Disabled;

        if (parentBody.FindChild("SatellitesContainer") is Node3D container)
            container.AddChild(inConstruction);
        else
            parentBody.AddChild(inConstruction);

        inConstruction.Initialize(parentBody, bandIndex);

        GameLogger.Info(
            $"[ConstructionManager] Station construction initialized: {inConstruction.Name} in band {bandIndex}"
        );
    }

    //Will be given an empty LogisticsUnit object and a dictionary containing configuration details
    //Should configure variables from position and band index, should place in SceneTree
    //Should disable any interaction variables until finished or cancelled
    private void InitializeShip(LogisticsUnit inConstruction, Dictionary details)
    {
        var parentStation = details["parent_station"].As<Node3D>();
        int bandIndex = details["band_index"].AsInt32();

        inConstruction.IsActive = false;
        inConstruction.Visible = false;
        inConstruction.ProcessMode = ProcessModeEnum.Disabled;

        var parentBody = parentStation.GetParent()?.GetParent() as Node3D ?? parentStation;
        if (parentBody.FindChild("SatellitesContainer") is Node3D container)
            container.AddChild(inConstruction);
        else
            parentBody.AddChild(inConstruction);

        inConstruction.Initialize(parentBody, bandIndex);

        GameLogger.Info(
            $"[ConstructionManager] Ship construction initialized: {inConstruction.Name} at station {parentStation.Name}"
        );
    }

    //Will be given a StationSatellite object and a dictionary containing configuration details
    //Should enable any interaction variables and ensure the object and mesh children are visible
    private void FinalizeStation(StationSatellite completed, Dictionary details)
    {
        completed.IsActive = true;
        completed.Visible = true;
        completed.ProcessMode = ProcessModeEnum.Inherit;

        SetChildrenVisible(completed, true);

        GameLogger.Info($"[ConstructionManager] Station construction completed: {completed.Name}");
    }

    //Will be given a LogisticsUnit object and a dictionary containing configuration details
    //Should enable any interaction variables and ensure the object and mesh children are visible
    private void FinalizeShip(LogisticsUnit completed, Dictionary details)
    {
        completed.IsActive = true;
        completed.Visible = true;
        completed.ProcessMode = ProcessModeEnum.Inherit;

        SetChildrenVisible(completed, true);

        GameLogger.Info($"[ConstructionManager] Ship construction completed: {completed.Name}");
    }

    //Will be given a StationSatellite object and a dictionary containing configuration details
    //Should remove object and children from SceneTree and ensure complete cleanup
    private void CancelStation(StationSatellite cancelled, Dictionary details)
    {
        GameLogger.Info($"[ConstructionManager] Station construction cancelled: {cancelled.Name}");

        cancelled.CancelConstruction();

        if (cancelled.GetParent() is Node parent)
            parent.RemoveChild(cancelled);

        cancelled.QueueFree();
    }

    //Will be given a LogisticsUnit object and a dictionary containing configuration details
    //Should remove object and children from SceneTree and ensure complete cleanup
    private void CancelShip(LogisticsUnit cancelled, Dictionary details)
    {
        GameLogger.Info($"[ConstructionManager] Ship construction cancelled: {cancelled.Name}");

        cancelled.CancelConstruction();

        if (cancelled.GetParent() is Node parent)
            parent.RemoveChild(cancelled);

        cancelled.QueueFree();
    }

    private void OnStationConstructionInitialized(Dictionary details)
    {
        var station = details["station"].As<StationSatellite>();
        InitializeStation(station, details);
    }

    private void OnStationConstructionCompleted(Dictionary details)
    {
        var station = details["station"].As<StationSatellite>();
        FinalizeStation(station, details);
    }

    private void OnStationConstructionCancelled(Dictionary details)
    {
        var station = details["station"].As<StationSatellite>();
        CancelStation(station, details);
    }

    private void OnShipConstructionInitialized(Dictionary details)
    {
        var ship = details["ship"].As<LogisticsUnit>();
        InitializeShip(ship, details);
    }

    private void OnShipConstructionCompleted(Dictionary details)
    {
        var ship = details["ship"].As<LogisticsUnit>();
        FinalizeShip(ship, details);
    }

    private void OnShipConstructionCancelled(Dictionary details)
    {
        var ship = details["ship"].As<LogisticsUnit>();
        CancelShip(ship, details);
    }

    private static void SetChildrenVisible(Node node, bool visible)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D child3D)
                child3D.Visible = visible;

            SetChildrenVisible(child, visible);
        }
    }

    /// <summary>
    /// Delivers resources to a constructable entity (station or ship) that is under construction.
    /// </summary>
    public void DeliverResourcesToConstruction(IConstructable target, string resourceId, int amount)
    {
        if (target is StationSatellite station)
        {
            station.DeliverResources(resourceId, amount);
        }
        else if (target is LogisticsUnit unit)
        {
            unit.DeliverResources(resourceId, amount);
        }
    }

    /// <summary>
    /// Creates a station satellite in orbit around the specified body.
    /// When a StationDefinition is provided, creates the station in construction mode (inactive, tracked).
    /// When null, creates instantly (preserves current debug/test behavior).
    /// </summary>
    public StationSatellite CreateStation(
        IOrbitalBody targetBody,
        int bandIndex,
        string? name = null,
        StationDefinition? stationDefinition = null
    )
    {
        if (targetBody == null)
        {
            throw new ArgumentNullException(nameof(targetBody), "Target body cannot be null");
        }

        if (bandIndex < 0 || bandIndex >= targetBody.GetBandCount())
        {
            throw new ArgumentOutOfRangeException(
                nameof(bandIndex),
                $"Band index {bandIndex} out of range. Available bands: {targetBody.GetBandCount()}"
            );
        }

        if (!targetBody.CanAddToBand(bandIndex))
        {
            int currentCount = targetBody.GetBandSatelliteCount(bandIndex);
            throw new InvalidOperationException(
                $"Cannot add station to band {bandIndex}: band is at capacity ({currentCount})"
            );
        }

        // Generate name if not provided
        name ??= stationDefinition?.Name ?? $"Station_{Guid.NewGuid().ToString()[..8]}";

        // Create station
        var station = new StationSatellite { Name = name };

        // Add to body's satellites container
        targetBody.SatellitesContainer.AddChild(station);

        // Initialize with orbital parameters - this sets up position/velocity based on band
        station.Initialize((Node3D)targetBody, bandIndex);

        // Increment band count
        targetBody.IncrementBandCount(bandIndex);

        // If a definition is provided, enter construction mode
        if (stationDefinition != null)
        {
            station.SetStationDefinition(stationDefinition);
            station.StartConstruction(new Dictionary());

            // Make visible but translucent during construction
            station.Visible = true;

            _stationsUnderConstruction.Add(station);

            GameLogger.Debug(
                $"Started construction of station '{name}' in band {bandIndex} ({stationDefinition.ConstructionTime}s)"
            );
        }
        else
        {
            GameLogger.Debug($"Created station '{name}' in band {bandIndex} around {targetBody}");
        }

        return station;
    }

    /// <summary>
    /// Creates a logistics unit (ship) in orbit around the specified body.
    /// When a ShipDefinition is provided, creates the ship in construction mode.
    /// When null, creates instantly (preserves current debug/test behavior).
    /// Ship construction requires a parent station with CanBuildShips == true when a definition is provided.
    /// </summary>
    public LogisticsUnit CreateLogisticsUnit(
        IOrbitalBody targetBody,
        int bandIndex,
        string? name = null,
        ShipDefinition? shipDefinition = null,
        StationSatellite? parentStation = null
    )
    {
        if (targetBody == null)
        {
            throw new ArgumentNullException(nameof(targetBody), "Target body cannot be null");
        }

        if (bandIndex < 0 || bandIndex >= targetBody.GetBandCount())
        {
            throw new ArgumentOutOfRangeException(
                nameof(bandIndex),
                $"Band index {bandIndex} out of range. Available bands: {targetBody.GetBandCount()}"
            );
        }

        if (!targetBody.CanAddToBand(bandIndex))
        {
            int currentCount = targetBody.GetBandSatelliteCount(bandIndex);
            throw new InvalidOperationException(
                $"Cannot add ship to band {bandIndex}: band is at capacity ({currentCount})"
            );
        }

        // Validate ship construction at station
        if (shipDefinition != null && parentStation != null && !parentStation.CanBuildShips)
        {
            throw new InvalidOperationException(
                $"Station '{parentStation.Name}' cannot build ships (type: {parentStation.StationType})"
            );
        }

        // Generate name if not provided
        name ??= shipDefinition?.Name ?? $"Ship_{Guid.NewGuid().ToString()[..8]}";

        // Create unit
        var unit = new LogisticsUnit { Name = name };

        // Add to body's satellites container
        targetBody.SatellitesContainer.AddChild(unit);

        // Initialize with orbital parameters - this sets up position/velocity based on band
        unit.Initialize((Node3D)targetBody, bandIndex);
        unit.InitializeCargo();

        // Apply ship definition properties or defaults
        if (shipDefinition != null)
        {
            unit.SetFuelCapacity(shipDefinition.FuelCapacity);
            unit.SetDryMass(shipDefinition.DryMass);
        }
        else
        {
            unit.SetFuelCapacity(1000f);
        }

        // Increment band count
        targetBody.IncrementBandCount(bandIndex);

        // If a definition is provided, enter construction mode
        if (shipDefinition != null)
        {
            unit.SetShipDefinition(shipDefinition);
            unit.StartConstruction(new Dictionary());

            // Make visible but inactive during construction
            unit.Visible = true;

            _shipsUnderConstruction.Add(unit);

            GameLogger.Debug(
                $"Started construction of ship '{name}' in band {bandIndex} ({shipDefinition.ConstructionTime}s)"
            );
        }
        else
        {
            GameLogger.Debug(
                $"Created logistics unit '{name}' in band {bandIndex} around {targetBody}"
            );
        }

        return unit;
    }
}
