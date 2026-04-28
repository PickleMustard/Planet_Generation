using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.NameGeneration;

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
    public delegate void BuildingConstructionInitializedEventHandler(Dictionary details);

    [Signal]
    public delegate void BuildingConstructionCompletedEventHandler(Dictionary details);

    [Signal]
    public delegate void BuildingConstructionCancelledEventHandler(Dictionary details);

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

    [Export]
    public Array<BuildingConstruction> _buildingsUnderConstruction;

    private ConstructionManager()
    {
        _stationsUnderConstruction = new Array<StationSatellite>();
        _shipsUnderConstruction = new Array<LogisticsUnit>();
        _buildingsUnderConstruction = new Array<BuildingConstruction>();
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
        BuildingConstructionInitialized += OnBuildingConstructionInitialized;
        BuildingConstructionCompleted += OnBuildingConstructionCompleted;
        BuildingConstructionCancelled += OnBuildingConstructionCancelled;

        GD.Print($"[ConstructionManager] Initialized");
    }

    /// <summary>
    /// Called by constructable entities when construction completes.
    /// Handles finalization, registry removal, and signal emission.
    /// </summary>
    public void NotifyConstructionComplete(IConstructable entity)
    {
        if (entity is StationSatellite station)
        {
            _stationsUnderConstruction.Remove(station);
            FinalizeStation(station, new Dictionary());
            EmitSignal(
                SignalName.StationConstructionCompleted,
                new Dictionary { { "station", station }, { "name", station.Name.ToString() } }
            );
        }
        else if (entity is LogisticsUnit ship)
        {
            _shipsUnderConstruction.Remove(ship);
            FinalizeShip(ship, new Dictionary());
            EmitSignal(
                SignalName.ShipConstructionCompleted,
                new Dictionary { { "ship", ship }, { "name", ship.Name.ToString() } }
            );
        }
        else if (entity is BuildingConstruction building)
        {
            _buildingsUnderConstruction.Remove(building);
            FinalizeBuilding(building, new Dictionary());
            EmitSignal(
                SignalName.BuildingConstructionCompleted,
                new Dictionary { { "building", building }, { "name", building.Name.ToString() } }
            );
        }
    }

    /// <summary>
    /// Called by constructable entities when construction is cancelled.
    /// Handles cleanup, registry removal, and signal emission.
    /// </summary>
    public void NotifyConstructionCancelled(IConstructable entity)
    {
        if (entity is StationSatellite station)
        {
            _stationsUnderConstruction.Remove(station);
            CancelStation(station, new Dictionary());
            EmitSignal(
                SignalName.StationConstructionCancelled,
                new Dictionary { { "station", station }, { "name", station.Name.ToString() } }
            );
        }
        else if (entity is LogisticsUnit ship)
        {
            _shipsUnderConstruction.Remove(ship);
            CancelShip(ship, new Dictionary());
            EmitSignal(
                SignalName.ShipConstructionCancelled,
                new Dictionary { { "ship", ship }, { "name", ship.Name.ToString() } }
            );
        }
        else if (entity is BuildingConstruction building)
        {
            _buildingsUnderConstruction.Remove(building);
            CancelBuilding(building, new Dictionary());
            EmitSignal(
                SignalName.BuildingConstructionCancelled,
                new Dictionary { { "building", building }, { "name", building.Name.ToString() } }
            );
        }
    }

    /// <summary>
    /// Called by constructable entities to emit periodic progress updates.
    /// </summary>
    public void NotifyProgressUpdate(string entityName, float progress, string status)
    {
        EmitSignal(SignalName.ConstructionProgressUpdated, entityName, progress, status);
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

    public void EmitBuildingConstruct(BuildingConstruction inConstruction, Dictionary details)
    {
        _buildingsUnderConstruction!.Add(inConstruction);
        EmitSignal(SignalName.BuildingConstructionInitialized, details);
    }

    public void EmitBuildingCancel(BuildingConstruction cancelled, Dictionary details)
    {
        _buildingsUnderConstruction!.Remove(cancelled);
        EmitSignal(SignalName.BuildingConstructionCancelled, details);
    }

    public void EmitBuildingComplete(BuildingConstruction completed, Dictionary details)
    {
        _buildingsUnderConstruction!.Remove(completed);
        EmitSignal(SignalName.BuildingConstructionCompleted, details);
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

    private void OnBuildingConstructionInitialized(Dictionary details)
    {
        var building = details["building"].As<BuildingConstruction>();
        GameLogger.Info(
            $"[ConstructionManager] Building construction initialized: {building.Name}"
        );
    }

    private void OnBuildingConstructionCompleted(Dictionary details)
    {
        var building = details["building"].As<BuildingConstruction>();
        FinalizeBuilding(building, details);
    }

    private void OnBuildingConstructionCancelled(Dictionary details)
    {
        var building = details["building"].As<BuildingConstruction>();
        CancelBuilding(building, details);
    }

    private void FinalizeBuilding(BuildingConstruction completed, Dictionary details)
    {
        completed.Visible = true;
        completed.ProcessMode = ProcessModeEnum.Inherit;
        SetChildrenVisible(completed, true);

        // Register building reference on all occupied cells
        foreach (var cell in completed.OccupiedCells)
            cell.Building = completed;

        // Register with continent economy for production
        RegisterBuildingWithEconomy(completed);

        GameLogger.Info($"[ConstructionManager] Building construction completed: {completed.Name}");
    }

    private void RegisterBuildingWithEconomy(BuildingConstruction building)
    {
        var parentBody = building.GetParent() as CelestialBody;
        if (parentBody?.Mesh?.Continents == null || building.PrimaryCell == null)
            return;

        int continentIdx = building.PrimaryCell.ContinentIndex;
        if (continentIdx < 0)
            return;

        if (!parentBody.Mesh.Continents.TryGetValue(continentIdx, out var continent))
            return;

        continent.InitializeEconomy();

        // Register continent economy with per-body managers
        parentBody.EconomyMgr?.RegisterEconomy(continent.Economy!);
        parentBody.TransferMgr?.RegisterContinentEndpoint(continentIdx, continent.Economy!);

        string recipeId = building.Definition?.Production?.DefaultRecipe ?? "";
        if (!string.IsNullOrEmpty(recipeId))
        {
            continent.Economy!.RegisterBuilding(building, recipeId);
            building.ActiveRecipeId = recipeId;
        }

        // Notify per-body TransferManager if this is a transfer station
        if (building.Definition?.TransferStation != null)
        {
            parentBody.TransferMgr?.OnTransferStationBuilt(continentIdx, building);
        }

        SignalBus.Instance?.EmitBuildingConstructed(continentIdx);
    }

    private void UnregisterBuildingFromEconomy(BuildingConstruction building)
    {
        // Free up building limit slot
        if (building.Definition?.BuildingLimit > 0)
            BuildingDatabase.Instance?.DecrementGlobalPlacement(building.Definition.IdName!);

        // Clear building reference from all occupied cells
        foreach (var cell in building.OccupiedCells)
            cell.Building = null;

        var parentBody = building.GetParent() as CelestialBody;
        if (parentBody?.Mesh?.Continents == null || building.PrimaryCell == null)
            return;

        int continentIdx = building.PrimaryCell.ContinentIndex;
        if (continentIdx < 0)
            return;

        if (
            parentBody.Mesh.Continents.TryGetValue(continentIdx, out var continent)
            && continent.Economy != null
        )
        {
            continent.Economy.UnregisterBuilding(building);
        }

        // Notify per-body TransferManager if this is a transfer station
        if (building.Definition?.TransferStation != null)
        {
            parentBody?.TransferMgr?.OnTransferStationDestroyed(continentIdx, building);
        }

        SignalBus.Instance?.EmitBuildingRemoved(continentIdx);
    }

    private void CancelBuilding(BuildingConstruction cancelled, Dictionary details)
    {
        GameLogger.Info($"[ConstructionManager] Building construction cancelled: {cancelled.Name}");

        // Free up building limit slot
        if (cancelled.Definition?.BuildingLimit > 0)
            BuildingDatabase.Instance?.DecrementGlobalPlacement(cancelled.Definition.IdName!);

        // Unregister from economy if it was already registered
        UnregisterBuildingFromEconomy(cancelled);

        cancelled.CancelConstruction();

        if (cancelled.GetParent() is Node parent)
            parent.RemoveChild(cancelled);

        cancelled.QueueFree();
    }

    /// <summary>
    /// Creates a building on the surface of a celestial body at the specified Voronoi cell.
    /// When an architectStation is provided, the building is registered with the station's work budget.
    /// </summary>
    public BuildingConstruction CreateBuilding(
        VoronoiCell primaryCell,
        Node3D parentBody,
        BuildingDefinition definition,
        List<VoronoiCell>? additionalCells = null,
        OrbitalArchitectStation? architectStation = null
    )
    {
        if (primaryCell == null)
            throw new ArgumentNullException(nameof(primaryCell));
        if (parentBody == null)
            throw new ArgumentNullException(nameof(parentBody));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        var building = new BuildingConstruction();

        parentBody.AddChild(building);

        // Get body radius for scaling (works with both CelestialBody and SatelliteBody)
        float bodyRadius = 1.0f;
        if (parentBody is IOrbitalBody orbitalBody)
        {
            bodyRadius = orbitalBody.Radius;
        }
        else if (parentBody is CelestialBody parentCelestialBody)
        {
            bodyRadius = parentCelestialBody.Radius;
        }

        building.SetBuildingDefinition(definition, bodyRadius);
        building.SetPlacement(primaryCell, additionalCells, parentBody);
        building.StartConstruction(new Dictionary());
        building.Visible = true;

        // Emit signal to notify other systems
        EmitBuildingConstruct(
            building,
            new Dictionary { { "building", building }, { "name", building.Name.ToString() } }
        );

        // Track against building limit at construction start
        if (definition.BuildingLimit > 0)
            BuildingDatabase.Instance?.MarkGloballyPlaced(definition.IdName!);

        // Register with the body's centralized BuildingConstructionManager
        if (
            parentBody is CelestialBody celestialBody
            && celestialBody.BuildingConstructionMgr != null
        )
        {
            celestialBody.BuildingConstructionMgr.RegisterBuilding(building);
            GameLogger.Debug(
                $"Started construction of building '{definition.DisplayName ?? definition.IdName}' on cell {primaryCell.Index} ({definition.WorkRequired} work)"
            );
        }
        else
        {
            GameLogger.Warning(
                $"No BuildingConstructionManager on body '{parentBody.Name}' — building '{definition.DisplayName ?? definition.IdName}' will not tick"
            );
        }

        return building;
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
        else if (target is BuildingConstruction building)
        {
            building.DeliverResources(resourceId, amount);
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
        name ??= stationDefinition?.Name ?? NameGenerator.GenerateStationName();

        // Create the appropriate station subclass based on definition
        var station = CreateStationInstance(name, stationDefinition);

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
            Dictionary stationDetails = new Dictionary();
            if (targetBody.GetType() == typeof(CelestialBody))
            {
                stationDetails.Add("parent_body", (CelestialBody)targetBody);
                stationDetails.Add("parent_type", "CelestialBody");
                stationDetails.Add("band_index", bandIndex);
            }
            else if (targetBody.GetType() == typeof(SatelliteBody))
            {
                stationDetails.Add("parent_body", (SatelliteBody)targetBody);
                stationDetails.Add("parent_type", "SatelliteBody");
                stationDetails.Add("band_index", bandIndex);
            }
            station.StartConstruction(stationDetails);

            // Make visible but translucent during construction
            station.Visible = true;
            stationDetails.Add("station", station);
            stationDetails.Add("name", station.Name.ToString());

            // Emit signal to notify other systems
            EmitStationConstruct(station, stationDetails);

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
    /// Creates a station satellite at a specific orbital radius (continuous placement).
    /// Used for bodies that don't use discrete orbit bands (stars, black holes, neutron stars).
    /// </summary>
    public StationSatellite CreateStationAtRadius(
        IOrbitalBody targetBody,
        float radius,
        string? name = null,
        StationDefinition? stationDefinition = null
    )
    {
        if (targetBody == null)
        {
            throw new ArgumentNullException(nameof(targetBody), "Target body cannot be null");
        }

        if (radius <= targetBody.Radius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                $"Radius {radius} must be greater than body radius {targetBody.Radius}"
            );
        }

        name ??= stationDefinition?.Name ?? NameGenerator.GenerateStationName();

        var station = CreateStationInstance(name, stationDefinition);

        targetBody.SatellitesContainer.AddChild(station);

        station.InitializeOrbitAtRadius(targetBody, radius);

        if (stationDefinition != null)
        {
            station.SetStationDefinition(stationDefinition);
            Dictionary locationDetails = new Dictionary();
            if (targetBody.GetType() == typeof(CelestialBody))
            {
                locationDetails.Add("parent_body", (CelestialBody)targetBody);
                locationDetails.Add("parent_type", "CelestialBody");
            }
            else if (targetBody.GetType() == typeof(SatelliteBody))
            {
                locationDetails.Add("parent_body", (SatelliteBody)targetBody);
                locationDetails.Add("parent_type", "SatelliteBody");
            }
            station.StartConstruction(locationDetails);
            station.Visible = true;

            // Emit signal to notify other systems
            EmitStationConstruct(
                station,
                new Dictionary { { "station", station }, { "name", station.Name.ToString() } }
            );

            GameLogger.Debug(
                $"Started construction of station '{name}' at radius {radius} ({stationDefinition.ConstructionTime}s)"
            );
        }
        else
        {
            GameLogger.Debug($"Created station '{name}' at radius {radius} around {targetBody}");
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
        if (
            shipDefinition != null
            && parentStation != null
            && parentStation is not ConstructionYardStation
        )
        {
            throw new InvalidOperationException(
                $"Station '{parentStation.Name}' is not a construction yard (type: {parentStation.StationType})"
            );
        }

        // Generate name if not provided
        name ??= shipDefinition?.Name ?? NameGenerator.GenerateShipName();

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

            // Make visible but inactive during construction
            unit.Visible = true;

            // Emit signal to notify other systems
            EmitShipConstruct(
                unit,
                new Dictionary { { "ship", unit }, { "name", unit.Name.ToString() } }
            );

            // If parent station is a construction yard, enqueue via the yard's build queue
            if (parentStation is ConstructionYardStation yard)
            {
                yard.EnqueueShipConstruction(unit);
                GameLogger.Debug(
                    $"Enqueued ship '{name}' at construction yard '{yard.Name}' ({shipDefinition.ConstructionTime}s)"
                );
            }
            else
            {
                // Fallback: start construction directly (no parent yard)
                unit.StartConstruction(new Dictionary());
                GameLogger.Debug(
                    $"Started construction of ship '{name}' in band {bandIndex} ({shipDefinition.ConstructionTime}s)"
                );
            }
        }
        else
        {
            GameLogger.Debug(
                $"Created logistics unit '{name}' in band {bandIndex} around {targetBody}"
            );
        }

        return unit;
    }

    /// <summary>
    /// Creates the appropriate station subclass based on the station definition's capabilities.
    /// </summary>
    private static StationSatellite CreateStationInstance(
        string name,
        StationDefinition? definition
    )
    {
        if (definition?.CanBuildShips == true)
            return new ConstructionYardStation { Name = name };

        if (definition?.CanBuildBuildings == true)
            return new OrbitalArchitectStation { Name = name };

        return new StationSatellite { Name = name };
    }

    /// <summary>
    /// Creates the company headquarters building - can only be called once per game.
    /// </summary>
    public HeadquartersBuilding? CreateHeadquarters(
        VoronoiCell primaryCell,
        Node3D parentBody,
        List<VoronoiCell>? additionalCells = null
    )
    {
        if (BuildingDatabase.Instance?.IsGloballyPlaced("company_headquarters") == true)
        {
            GameLogger.Warning("[ConstructionManager] Headquarters already exists");
            return null;
        }

        if (!BuildingDatabase.Instance.TryGetBuilding("company_headquarters", out var definition))
        {
            GameLogger.Error("[ConstructionManager] Headquarters definition not found");
            return null;
        }

        var building = new HeadquartersBuilding();
        parentBody.AddChild(building);

        // Get body radius for scaling
        float bodyRadius = 1.0f;
        if (parentBody is IOrbitalBody orbitalBody)
        {
            bodyRadius = orbitalBody.Radius;
        }
        else if (parentBody is CelestialBody celestialBodyCheck)
        {
            bodyRadius = celestialBodyCheck.Radius;
        }

        building.SetBuildingDefinition(definition, bodyRadius);
        building.SetPlacement(primaryCell, additionalCells, parentBody);
        building.Visible = true;

        // Register with economy
        RegisterHeadquartersWithEconomy(building);

        EmitBuildingConstruct(
            building,
            new Godot.Collections.Dictionary
            {
                { "building", building },
                { "name", building.Name.ToString() },
                { "is_headquarters", true },
            }
        );

        _pendingHeadquarters = building;
        return building;
    }

    private void RegisterHeadquartersWithEconomy(HeadquartersBuilding building)
    {
        var parentBody = building.GetParent() as CelestialBody;
        if (parentBody?.Mesh?.Continents == null || building.PrimaryCell == null)
            return;

        int continentIdx = building.PrimaryCell.ContinentIndex;
        if (
            continentIdx < 0
            || !parentBody.Mesh.Continents.TryGetValue(continentIdx, out var continent)
        )
            return;

        continent.InitializeEconomy();
        parentBody.EconomyMgr?.RegisterEconomy(continent.Economy!);
        parentBody.TransferMgr?.RegisterContinentEndpoint(continentIdx, continent.Economy!);
    }

    /// <summary>
    /// Finalizes a headquarters after the player confirms the naming dialogue:
    /// registers the building with its continent economy, binds the transfer
    /// station (if any), and deposits starting stockpiles via InitializeHeadquarters.
    /// </summary>
    public void FinalizeHeadquarters(HeadquartersBuilding building)
    {
        var parentBody = building.GetParent() as CelestialBody;
        if (parentBody?.Mesh?.Continents == null || building.PrimaryCell == null)
            return;

        int continentIdx = building.PrimaryCell.ContinentIndex;
        if (
            continentIdx < 0
            || !parentBody.Mesh.Continents.TryGetValue(continentIdx, out var continent)
            || continent.Economy == null
        )
            return;

        string recipeId = building.Definition?.Production?.DefaultRecipe ?? "";
        GameLogger.Info(
            $"[ConstructionManager] FinalizeHeadquarters recipeId: {recipeId}, Definition {building.Definition}, Production {building.Definition?.Production}"
        );
        if (!string.IsNullOrEmpty(recipeId))
        {
            continent.Economy.RegisterBuilding(building, recipeId);
            building.ActiveRecipeId = recipeId;
        }

        if (building.Definition?.TransferStation != null)
        {
            parentBody.TransferMgr?.OnTransferStationBuilt(continentIdx, building);
        }

        building.InitializeHeadquarters(parentBody, continentIdx);
        SignalBus.Instance?.EmitBuildingConstructed(continentIdx);

        if (_pendingHeadquarters == building)
            _pendingHeadquarters = null;
    }

    /// <summary>
    /// Undoes a headquarters placement prior to finalization: frees the node
    /// and releases the global placement slot so the player can re-place.
    /// </summary>
    public void CancelHeadquarters(HeadquartersBuilding building)
    {
        BuildingDatabase.Instance?.DecrementGlobalPlacement("company_headquarters");

        if (_pendingHeadquarters == building)
            _pendingHeadquarters = null;

        if (GodotObject.IsInstanceValid(building))
            building.QueueFree();
    }

    private HeadquartersBuilding? _pendingHeadquarters;

    /// <summary>
    /// The most recently placed headquarters awaiting naming confirmation.
    /// Null once the placement is finalized or cancelled.
    /// </summary>
    public HeadquartersBuilding? PendingHeadquarters => _pendingHeadquarters;
}
