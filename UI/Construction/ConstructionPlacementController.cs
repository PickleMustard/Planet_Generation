using System.Collections.Generic;
using Constructables;
using Constructables.ArtificialSatellites;
using Godot;
using UtilityLibrary;

namespace UI.Construction;

public enum PlacementState
{
    Idle,
    SelectingOrbitalBody,
    SelectingBand,
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
    private IOrbitalBody? _targetBody;
    private int _targetBandIndex;
    private StationSatellite? _parentStation;

    private Label _statusLabel = null!;
    private BandSelectionPopup _bandPopup = null!;
    private StationSelectionPopup _stationPopup = null!;

    public override void _Ready()
    {
        BuildStatusLabel();
        BuildBandPopup();
        BuildStationPopup();
    }

    private void BuildStatusLabel()
    {
        _statusLabel = new Label
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.0f,
            AnchorBottom = 0.0f,
            OffsetLeft = -200,
            OffsetRight = 200,
            OffsetTop = 20,
            OffsetBottom = 50,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 18);
        GetParent().CallDeferred("add_child", _statusLabel);
    }

    private void BuildBandPopup()
    {
        _bandPopup = new BandSelectionPopup { Visible = false };
        _bandPopup.BandSelected += OnBandSelected;
        _bandPopup.PopupCancelled += OnPopupCancelled;
        GetParent().CallDeferred("add_child", _bandPopup);
    }

    private void BuildStationPopup()
    {
        _stationPopup = new StationSelectionPopup { Visible = false };
        _stationPopup.StationSelected += OnStationSelected;
        _stationPopup.PopupCancelled += OnPopupCancelled;
        GetParent().CallDeferred("add_child", _stationPopup);
    }

    public void BeginPlacement(string itemType, string definitionName)
    {
        _selectedItemType = itemType;

        switch (itemType)
        {
            case "Station":
                _selectedStationDef = LogisticsConfigLoader.GetStationDefinition(definitionName);
                if (_selectedStationDef == null)
                {
                    GameLogger.Warning($"Station definition '{definitionName}' not found");
                    ResetToIdle();
                    return;
                }
                SetState(PlacementState.SelectingOrbitalBody);
                ShowStatus("Click an orbital body to place station (ESC to cancel)");
                break;

            case "Ship":
                _selectedShipDef = LogisticsConfigLoader.GetShipDefinition(definitionName);
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
                SetState(PlacementState.SelectingCell);
                ShowStatus("Buildings are not yet implemented");
                // Auto-cancel after a brief display
                GetTree().CreateTimer(2.0).Timeout += ResetToIdle;
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (CurrentState == PlacementState.Idle) return;

        // ESC cancels placement from any state
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            ResetToIdle();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Handle body selection via left-click raycast
        if (CurrentState == PlacementState.SelectingOrbitalBody
            && @event is InputEventMouseButton mouseEvent
            && mouseEvent.ButtonIndex == MouseButton.Left
            && mouseEvent.Pressed)
        {
            TrySelectOrbitalBody();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TrySelectOrbitalBody()
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var mousePos = GetViewport().GetMousePosition();
        var origin = camera.ProjectRayOrigin(mousePos);
        var direction = origin + camera.ProjectRayNormal(mousePos) * 10000f;

        var spaceState = camera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(origin, direction);
        query.CollideWithAreas = true;
        var result = spaceState.IntersectRay(query);

        if (result.Count == 0) return;

        var collider = (Node3D)result["collider"];

        // Walk up the tree to find a CelestialBody in the "CelestialBody" group
        Node? current = collider;
        IOrbitalBody? orbitalBody = null;
        while (current != null)
        {
            if (current is IOrbitalBody body && current.IsInGroup("CelestialBody"))
            {
                orbitalBody = body;
                break;
            }
            current = current.GetParent();
        }

        if (orbitalBody == null) return;

        if (orbitalBody.GetBandCount() == 0)
        {
            ShowStatus("This body has no orbit bands available");
            GetTree().CreateTimer(1.5).Timeout += () =>
                ShowStatus("Click an orbital body to place station (ESC to cancel)");
            return;
        }

        _targetBody = orbitalBody;
        SetState(PlacementState.SelectingBand);
        ShowStatus("Select an orbit band");
        _bandPopup.Populate(orbitalBody);
        _bandPopup.Visible = true;
    }

    private void OnBandSelected(int bandIndex)
    {
        _bandPopup.Visible = false;
        _targetBandIndex = bandIndex;
        DispatchStationConstruction();
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
                _targetBody, _targetBandIndex, null, _selectedStationDef);
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
            if (node is not IOrbitalBody body) continue;
            foreach (var child in body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite station
                    && station.CanBuildShips
                    && !station.IsUnderConstruction)
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

        // Derive the parent orbital body from the station's position in the scene tree
        // Station is under SatellitesContainer, which is a child of the CelestialBody
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
                _targetBody, _targetBandIndex, null, _selectedShipDef, _parentStation);
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

    private void OnPopupCancelled()
    {
        _bandPopup.Visible = false;
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
        CurrentState = PlacementState.Idle;
        _selectedItemType = null;
        _selectedStationDef = null;
        _selectedShipDef = null;
        _targetBody = null;
        _parentStation = null;
        _statusLabel.Visible = false;
        _bandPopup.Visible = false;
        _stationPopup.Visible = false;
        EmitSignal(SignalName.PlacementFinished);
    }
}
