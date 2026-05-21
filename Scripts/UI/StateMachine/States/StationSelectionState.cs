using System.Collections.Generic;
using Constructables;
using Constructables.Stations.Behaviors;
using Godot;
using Logistics.Resources;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for selecting a shipyard station when constructing ships.
/// Wraps the existing StationSelectionPopup as a child node.
/// </summary>
public partial class StationSelectionState : LimboState
{
    private Construction.StationSelectionPopup? _popup;
    private ShipDefinition? _shipDef;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "StationSelectionState");

        _popup = GetNodeOrNull<Construction.StationSelectionPopup>("StationSelectionPopup");
        if (_popup == null)
        {
            GameLogger.Error("StationSelectionState: StationSelectionPopup not found as child");
            Dispatch("placement_cancelled");
            return;
        }

        // Look up ship definition
        var defName = Blackboard?.Top().GetVar("ConstructionDefinitionName").AsString() ?? "";
        ShipDatabase.Instance.TryGetShip(defName, out _shipDef);
        if (_shipDef == null)
        {
            GameLogger.Error($"StationSelectionState: Ship definition '{defName}' not found");
            Dispatch("placement_cancelled");
            return;
        }

        var shipyards = FindShipyardStations();
        _popup.Populate(shipyards);
        _popup.Visible = true;

        _popup.StationSelected += OnStationSelected;
        _popup.PopupCancelled += OnPopupCancelled;

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug($"StationSelectionState: Selecting shipyard for '{_shipDef.Name}'");
    }

    public override void _Exit()
    {
        base._Exit();

        if (_popup != null)
        {
            _popup.StationSelected -= OnStationSelected;
            _popup.PopupCancelled -= OnPopupCancelled;
            _popup.Visible = false;
        }

        _popup = null;
        _shipDef = null;
    }

    private void OnStationSelected(int stationIndex)
    {
        if (_popup == null || _shipDef == null)
            return;

        var station = _popup.GetSelectedStation(stationIndex);
        if (station == null)
        {
            Dispatch("placement_cancelled");
            return;
        }

        var parentNode = station.GetParent()?.GetParent();
        if (parentNode is not IOrbitalBody parentBody)
        {
            GameLogger.Warning(
                "StationSelectionState: Could not determine parent body for station"
            );
            ToastSystem.Instance?.Show("Could not determine parent body for station");
            Dispatch("placement_cancelled");
            return;
        }

        try
        {
            ConstructionManager.Instance.CreateLogisticsUnit(
                parentBody,
                station.BandIndex,
                null,
                _shipDef,
                station
            );
            ToastSystem.Instance?.Show($"Construction started: {_shipDef.Name}");
            Dispatch("station_confirmed");
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"StationSelectionState: Failed to create ship — {e.Message}");
            ToastSystem.Instance?.Show($"Error: {e.Message}");
        }
    }

    private void OnPopupCancelled()
    {
        Dispatch("placement_cancelled");
    }

    private List<StationSatellite> FindShipyardStations()
    {
        var result = new List<StationSatellite>();
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        foreach (var node in bodies)
        {
            if (node is not IOrbitalBody body)
                continue;
            foreach (var child in body.SatellitesContainer.GetChildren())
            {
                if (
                    child is StationSatellite station
                    && station.GetBehavior<ShipyardBehavior>() != null
                    && !station.IsUnderConstruction
                )
                {
                    result.Add(station);
                }
            }
        }
        return result;
    }
}
