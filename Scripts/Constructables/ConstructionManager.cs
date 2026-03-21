using System;
using Constructables.ArtificialSatellites;
using Godot;
using Godot.Collections;
using Structures.GameState;
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

    private static ConstructionManager _instance;
    public static ConstructionManager Instance => _instance;

    private Array<StationSatellite> _stationsUnderConstruction;
    private Array<LogisticsUnit> _shipsUnderConstruction;

    //Ensure singleton instance & signals are connected to correct methods
    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;

        _stationsUnderConstruction = new Array<StationSatellite>();
        _shipsUnderConstruction = new Array<LogisticsUnit>();

        StationConstructionInitialized += OnStationConstructionInitialized;
        StationConstructionCompleted += OnStationConstructionCompleted;
        StationConstructionCancelled += OnStationConstructionCancelled;
        ShipConstructionInitialized += OnShipConstructionInitialized;
        ShipConstructionCompleted += OnShipConstructionCompleted;
        ShipConstructionCancelled += OnShipConstructionCancelled;

        GD.Print($"[ConstructionManager] Initialized");
    }

    public void EmitStationConstruct(StationSatellite inConstruction, Dictionary details)
    {
        _stationsUnderConstruction.Add(inConstruction);
        EmitSignal(SignalName.StationConstructionInitialized, details);
    }

    public void EmitShipConstruct(LogisticsUnit inConstruction, Dictionary details)
    {
        _shipsUnderConstruction.Add(inConstruction);
        EmitSignal(SignalName.ShipConstructionInitialized, details);
    }

    public void EmitStationCancel(StationSatellite cancelled, Dictionary details)
    {
        _stationsUnderConstruction.Remove(cancelled);
        EmitSignal(SignalName.StationConstructionCancelled, details);
    }

    public void EmitShipCancel(LogisticsUnit cancelled, Dictionary details)
    {
        _shipsUnderConstruction.Remove(cancelled);
        EmitSignal(SignalName.ShipConstructionCancelled, details);
    }

    public void EmitStationComplete(StationSatellite completed, Dictionary details)
    {
        _stationsUnderConstruction.Remove(completed);
        EmitSignal(SignalName.StationConstructionCompleted, details);
    }

    public void EmitShipComplete(LogisticsUnit completed, Dictionary details)
    {
        _shipsUnderConstruction.Remove(completed);
        EmitSignal(SignalName.ShipConstructionCompleted, details);
    }

    //Given a filter, return a list of all stations under construction
    public Array<StationSatellite> GetStationsUnderConstruction(
        String orbitalFilter = "",
        String typeFilter = ""
    )
    {
        if (string.IsNullOrEmpty(orbitalFilter) && string.IsNullOrEmpty(typeFilter))
            return _stationsUnderConstruction;

        var filtered = new Array<StationSatellite>();
        foreach (var station in _stationsUnderConstruction)
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
            return _shipsUnderConstruction;

        var filtered = new Array<LogisticsUnit>();
        foreach (var ship in _shipsUnderConstruction)
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

        cancelled.OnDestroy();

        if (cancelled.GetParent() is Node parent)
            parent.RemoveChild(cancelled);

        cancelled.QueueFree();
    }

    //Will be given a LogisticsUnit object and a dictionary containing configuration details
    //Should remove object and children from SceneTree and ensure complete cleanup
    private void CancelShip(LogisticsUnit cancelled, Dictionary details)
    {
        GameLogger.Info($"[ConstructionManager] Ship construction cancelled: {cancelled.Name}");

        cancelled.OnDestroy();

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
}
