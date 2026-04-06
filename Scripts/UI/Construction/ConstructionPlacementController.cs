using System.Collections.Generic;
using Constructables;
using Godot;
using Logistics.Resources;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace UI.Construction;

public enum PlacementState
{
    Idle,
    SelectingOrbitalBody,
    SelectingBand,
    SelectingRadius,
    SelectingStation,
    SelectingCell,
}

public partial class ConstructionPlacementController : Node
{
    [Signal]
    public delegate void PlacementFinishedEventHandler();

    public PlacementState CurrentState { get; private set; } = PlacementState.Idle;

    private string? _selectedItemType;
    private StationDefinition? _selectedStationDef;
    private ShipDefinition? _selectedShipDef;
    private BuildingDefinition? _selectedBuildingDef;
    private IOrbitalBody? _targetBody;
    private int _targetBandIndex;
    private float _targetRadius;
    private StationSatellite? _parentStation;
    private BuildingPlacementMode? _buildingPlacementMode;

    [Export]
    private Label _statusLabel = null!;

    [Export]
    private OrbitalBodySelectionPopup _bodyPopup = null!;

    [Export]
    private BandSelectionPopup _bandPopup = null!;

    [Export]
    private RadiusInputPopup _radiusPopup = null!;

    [Export]
    private StationSelectionPopup _stationPopup = null!;

    public override void _Ready()
    {
        // Connect signals
        _bodyPopup.OrbitalBodySelected += OnOrbitalBodySelected;
        _bodyPopup.PopupCancelled += OnPopupCancelled;
        _bandPopup.BandSelected += OnBandSelected;
        _bandPopup.PopupCancelled += OnPopupCancelled;
        _radiusPopup.RadiusSelected += OnRadiusSelected;
        _radiusPopup.PopupCancelled += OnPopupCancelled;
        _stationPopup.StationSelected += OnStationSelected;
        _stationPopup.PopupCancelled += OnPopupCancelled;
    }

    public void BeginPlacement(string itemType, string definitionName)
    {
        _selectedItemType = itemType;

        switch (itemType)
        {
            case "Station":
                StationDatabase.Instance.TryGetStation(definitionName, out _selectedStationDef);
                if (_selectedStationDef == null)
                {
                    GameLogger.Warning($"Station definition '{definitionName}' not found");
                    ResetToIdle();
                    return;
                }
                SetState(PlacementState.SelectingOrbitalBody);
                ShowStatus("Select an orbital body (ESC to cancel)");
                _bodyPopup.Populate();
                _bodyPopup.Visible = true;
                break;

            case "Ship":
                ShipDatabase.Instance.TryGetShip(definitionName, out _selectedShipDef);
                if (_selectedShipDef == null)
                {
                    GameLogger.Warning($"Ship definition '{definitionName}' not found");
                    ResetToIdle();
                    return;
                }
                SetState(PlacementState.SelectingStation);
                ShowStatus("Select a shipyard station (ESC to cancel)");
                ShowStationPopup();
                break;

            case "Building":
                BuildingDatabase.Instance.TryGetBuilding(definitionName, out _selectedBuildingDef);
                if (_selectedBuildingDef == null)
                {
                    GameLogger.Warning($"Building definition '{definitionName}' not found");
                    ResetToIdle();
                    return;
                }
                SetState(PlacementState.SelectingCell);
                ShowStatus("Click to place building (ESC to cancel)");
                StartBuildingPlacement();
                break;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (CurrentState == PlacementState.Idle)
            return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            ResetToIdle();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnOrbitalBodySelected(int bodyIndex)
    {
        _bodyPopup.Visible = false;
        var body = _bodyPopup.GetSelectedBody(bodyIndex);
        if (body == null)
        {
            ResetToIdle();
            return;
        }

        _targetBody = body;

        if (body.UsesBandPlacement)
        {
            if (body.GetBandCount() == 0)
            {
                ShowStatus("This body has no orbit bands available");
                GetTree().CreateTimer(1.5).Timeout += ResetToIdle;
                return;
            }
            SetState(PlacementState.SelectingBand);
            ShowStatus("Select an orbit band");
            _bandPopup.Populate(body);
            _bandPopup.Visible = true;
        }
        else
        {
            SetState(PlacementState.SelectingRadius);
            ShowStatus("Set orbital radius");
            _radiusPopup.Populate(body);
            _radiusPopup.Visible = true;
        }
    }

    private void OnBandSelected(int bandIndex)
    {
        _bandPopup.Visible = false;
        _targetBandIndex = bandIndex;
        DispatchStationConstruction();
    }

    private void OnRadiusSelected(float radius)
    {
        _radiusPopup.Visible = false;
        _targetRadius = radius;
        DispatchStationConstructionAtRadius();
    }

    private void DispatchStationConstruction()
    {
        if (_targetBody == null || _selectedStationDef == null)
        {
            ResetToIdle();
            return;
        }

        try
        {
            ConstructionManager.Instance.CreateStation(
                _targetBody,
                _targetBandIndex,
                null,
                _selectedStationDef
            );
            ShowStatus($"Construction started: {_selectedStationDef.Name}");
            GetTree().CreateTimer(1.5).Timeout += ResetToIdle;
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"Failed to start station construction: {e.Message}");
            ShowStatus($"Error: {e.Message}");
            GetTree().CreateTimer(2.0).Timeout += ResetToIdle;
        }
    }

    private void DispatchStationConstructionAtRadius()
    {
        if (_targetBody == null || _selectedStationDef == null)
        {
            ResetToIdle();
            return;
        }

        try
        {
            ConstructionManager.Instance.CreateStationAtRadius(
                _targetBody,
                _targetRadius,
                null,
                _selectedStationDef
            );
            ShowStatus($"Construction started: {_selectedStationDef.Name}");
            GetTree().CreateTimer(1.5).Timeout += ResetToIdle;
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"Failed to start station construction: {e.Message}");
            ShowStatus($"Error: {e.Message}");
            GetTree().CreateTimer(2.0).Timeout += ResetToIdle;
        }
    }

    private void ShowStationPopup()
    {
        var shipyards = FindShipyardStations();
        _stationPopup.Populate(shipyards);
        _stationPopup.Visible = true;
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
                    && station.CanBuildShips
                    && !station.IsUnderConstruction
                )
                {
                    result.Add(station);
                }
            }
        }
        return result;
    }

    private void OnStationSelected(int stationIndex)
    {
        _stationPopup.Visible = false;
        var station = _stationPopup.GetSelectedStation(stationIndex);
        if (station == null)
        {
            ResetToIdle();
            return;
        }

        _parentStation = station;

        var parentNode = station.GetParent()?.GetParent();
        if (parentNode is not IOrbitalBody parentBody)
        {
            ShowStatus("Could not determine parent body for station");
            GetTree().CreateTimer(1.5).Timeout += ResetToIdle;
            return;
        }

        _targetBody = parentBody;
        _targetBandIndex = station.BandIndex;
        DispatchShipConstruction();
    }

    private void DispatchShipConstruction()
    {
        if (_targetBody == null || _selectedShipDef == null)
        {
            ResetToIdle();
            return;
        }

        try
        {
            ConstructionManager.Instance.CreateLogisticsUnit(
                _targetBody,
                _targetBandIndex,
                null,
                _selectedShipDef,
                _parentStation
            );
            ShowStatus($"Construction started: {_selectedShipDef.Name}");
            GetTree().CreateTimer(1.5).Timeout += ResetToIdle;
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"Failed to start ship construction: {e.Message}");
            ShowStatus($"Error: {e.Message}");
            GetTree().CreateTimer(2.0).Timeout += ResetToIdle;
        }
    }

    private void StartBuildingPlacement()
    {
        _buildingPlacementMode = new BuildingPlacementMode();
        AddChild(_buildingPlacementMode);
        _buildingPlacementMode.Initialize(_selectedBuildingDef!);

        _buildingPlacementMode.PlacementConfirmed += OnBuildingPlacementConfirmed;
        _buildingPlacementMode.PlacementCancelled += OnPopupCancelled;
    }

    private void OnBuildingPlacementConfirmed(
        VoronoiCell primaryCell,
        Node3D body,
        Godot.Collections.Array<VoronoiCell> additionalCells
    )
    {
        DispatchBuildingConstruction(primaryCell, body, additionalCells);
    }

    private void DispatchBuildingConstruction(
        VoronoiCell primaryCell,
        Node3D body,
        Godot.Collections.Array<VoronoiCell>? additionalCells
    )
    {
        if (_selectedBuildingDef == null)
        {
            ResetToIdle();
            return;
        }

        try
        {
            List<VoronoiCell>? extraCells = null;
            if (additionalCells != null && additionalCells.Count > 0)
            {
                extraCells = new List<VoronoiCell>();
                foreach (var cell in additionalCells)
                    extraCells.Add(cell);
            }

            ConstructionManager.Instance.CreateBuilding(
                primaryCell,
                body,
                _selectedBuildingDef,
                extraCells
            );
            ShowStatus(
                $"Construction started: {_selectedBuildingDef.DisplayName ?? _selectedBuildingDef.IdName}"
            );
            GetTree().CreateTimer(1.5).Timeout += ResetToIdle;
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"Failed to start building construction: {e.Message}");
            ShowStatus($"Error: {e.Message}");
            GetTree().CreateTimer(2.0).Timeout += ResetToIdle;
        }
    }

    private void OnPopupCancelled()
    {
        _bodyPopup.Visible = false;
        _bandPopup.Visible = false;
        _radiusPopup.Visible = false;
        _stationPopup.Visible = false;
        ResetToIdle();
    }

    private void SetState(PlacementState state)
    {
        CurrentState = state;
    }

    private void ShowStatus(string text)
    {
        _statusLabel.Text = text;
        _statusLabel.Visible = true;
    }

    private void ResetToIdle()
    {
        GameLogger.Info("Resetting to Idle");
        CurrentState = PlacementState.Idle;
        _selectedItemType = null;
        _selectedStationDef = null;
        _selectedShipDef = null;
        _selectedBuildingDef = null;
        _targetBody = null;
        _parentStation = null;
        _statusLabel.Visible = false;
        _bodyPopup.Visible = false;
        _bandPopup.Visible = false;
        _radiusPopup.Visible = false;
        _stationPopup.Visible = false;

        if (_buildingPlacementMode != null)
        {
            _buildingPlacementMode.Cleanup();
            _buildingPlacementMode.QueueFree();
            _buildingPlacementMode = null;
        }

        EmitSignal(SignalName.PlacementFinished);
    }
}
