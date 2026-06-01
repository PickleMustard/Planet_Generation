using System.Collections.Generic;
using Constructables;
using Godot;
using PlayerInteraction.CellSelection;
using ProceduralGeneration.PlanetGeneration;
using UI.Components;
using UtilityLibrary;

namespace UI;

/// <summary>
/// Diegetic GUI window for inspecting orbital bodies (CelestialBody / SatelliteBody).
/// UI panels are arranged around the screen edges while the body remains visible and
/// interactable in the center. Opened by left-clicking a body; closed via Escape or
/// the close button.
/// </summary>
public partial class OrbitalBodyWindow : Control
{
    public static OrbitalBodyWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private Button? _backButton;

    [Export]
    private OrbitalBodyHeaderPanel? _headerPanel;

    [Export]
    private OrbitalBodyTabbedPanel? _tabbedPanel;

    [Export]
    private OrbitalBodyDetailsPanel? _detailsPanel;

    [Export]
    private CompassRose? _compassRose;

    [Export]
    private ReticleOverlay? _reticleOverlay;

    // Negative-space layout: the planet should sit centered inside the
    // unobstructed region (viewport minus tab strip on the right and details
    // pane along the bottom). These pixel constants are mirrored in the .tscn
    // anchors of TabbedPanel and DetailsPanel.
    private const float TAB_PANEL_WIDTH = 540f + 32f;   // panel + right gutter
    private const float DETAILS_PANEL_HEIGHT = 290f;

    private IOrbitalBody? _currentBody;
    private Camera3D? _playerCamera;
    private OrbitalBodyOrbitCamera? _orbitCamera;
    private BillboardLabelManager? _billboardManager;

    private HashSet<int>? _currentBodyContinentIndices;
    private double _lastEconomyRefreshTime;
    private const double ECONOMY_DEBOUNCE_SECONDS = 0.5;

    /// <summary>
    /// Set when the window opens so the mouse-release from the opening click
    /// doesn't trigger cell selection.
    /// </summary>
    private bool _ignoreNextRelease;

    /// <summary>
    /// Whether this window is currently open.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>The body currently being inspected, or null.</summary>
    public IOrbitalBody? CurrentBody => _currentBody;

    /// <summary>
    /// When true, the window ignores all input (drag / cell-select). Set by the
    /// placement overlay while placing on the inspected body so the placement
    /// click isn't also consumed as a camera drag or cell selection. The window
    /// stays visible throughout.
    /// </summary>
    public bool InteractionSuspended { get; set; }

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void StationInspectRequestedEventHandler(int stationIndex);

    [Signal]
    public delegate void LogisticsUnitInspectRequestedEventHandler(int unitIndex);

    [Signal]
    public delegate void BuildingInspectRequestedEventHandler(Building building);

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        _orbitCamera = GetNode<OrbitalBodyOrbitCamera>("OrbitalBodyOrbitCamera");
        //_billboardManager = new BillboardLabelManager();
        //AddChild(_billboardManager);

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;

        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;
    }

    public override void _Process(double delta)
    {
        if (!IsOpen)
            return;

        if (_compassRose != null && _orbitCamera != null)
            _compassRose.RotationRadians = _orbitCamera.GetPlanetNorthScreenAngle();
    }

    /// <summary>
    /// Returns the pixel offset to apply to the orbit camera so the body
    /// projects to the negative-space center (the unobstructed area of the
    /// window) rather than the geometric viewport center.
    /// </summary>
    private Vector2 ComputeCameraOffset()
    {
        // Shift the body left by half the tab strip's width and up by half
        // the bottom pane's height.
        return new Vector2(TAB_PANEL_WIDTH * 0.5f, -DETAILS_PANEL_HEIGHT * 0.5f);
    }

    private void OnCloseButtonPressed()
    {
        EmitSignal(SignalName.WindowCloseRequested);
    }

    private void OnBackPressed()
    {
        EmitSignal(SignalName.BackRequested);
    }

    public override void _ExitTree()
    {
        UnsubscribeSignals();

        if (Instance == this)
            Instance = null;
    }

    // ───────── Open / Close ─────────

    public void ShowWindow(IOrbitalBody body, Camera3D playerCamera)
    {
        _currentBody = body;
        _playerCamera = playerCamera;
        Visible = true;
        IsOpen = true;
        _ignoreNextRelease = true;

        // Switch to visible cursor for UI interaction + drag
        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        // Begin camera orbit
        if (_orbitCamera != null)
        {
            _orbitCamera.ScreenOffset = ComputeCameraOffset();
            _orbitCamera.BeginOrbit(playerCamera, body);
        }

        // Populate panels
        _headerPanel?.Populate(body);
        _tabbedPanel?.Initialize(body);
        if (_tabbedPanel != null && _detailsPanel != null)
            _detailsPanel.Initialize(body, _tabbedPanel);

        // Billboard labels
        _billboardManager?.Initialize(body, playerCamera);

        var bodyInfo = OrbitalBodyConverter.ToDictionary(body);

        // Listen for body removal
        if (body is Node bodyNode)
            bodyNode.TreeExiting += OnBodyExiting;

        SignalBus.Instance?.EmitOrbitalBodyWindowOpened(bodyInfo);

        SubscribeSignals();

        GameLogger.Info($"[OrbitalBodyWindow] Opened for '{body.BodyName}'");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        UnsubscribeSignals();

        IsOpen = false;

        // End camera orbit (smooth transition back)
        _orbitCamera?.EndOrbit();

        // Clean up billboard labels
        _billboardManager?.Cleanup();

        // Clear panels
        _headerPanel?.Clear();
        if (_tabbedPanel != null && _detailsPanel != null)
            _detailsPanel.Disconnect(_tabbedPanel);
        _tabbedPanel?.Clear();
        _detailsPanel?.Clear();

        // Disconnect body signal
        if (_currentBody is Node bodyNode && IsInstanceValid(bodyNode))
            bodyNode.TreeExiting -= OnBodyExiting;

        // Restore captured mouse
        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _currentBody = null;
        _playerCamera = null;
        Visible = false;

        SignalBus.Instance?.EmitOrbitalBodyWindowClosed();

        GameLogger.Info("[OrbitalBodyWindow] Closed");
    }

    // ───────── Input ─────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen)
            return;

        // Placement overlay owns input while placing on this body — ignore
        // drag/cell-select so the placement click isn't double-handled.
        if (InteractionSuspended)
            return;

        // Escape handling moved to GUIManager - emit signal instead
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            EmitSignal(SignalName.WindowCloseRequested);
            GetViewport().SetInputAsHandled();
            return;
        }

        // Handle left-click: drag rotates camera, click selects cells
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
            {
                // Start tracking for drag detection
                _orbitCamera?.HandleDragInput(@event);
                GetViewport().SetInputAsHandled();
                return;
            }
            else
            {
                // The opening click's release arrives here before the orbit
                // camera has tracked any press — skip it to avoid firing a
                // stray cell-selection.
                if (_ignoreNextRelease)
                {
                    _ignoreNextRelease = false;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Release: if it was a drag, orbit camera already handled it.
                // If it was a click (no drag), perform cell selection.
                bool wasDrag = _orbitCamera?.IsDragging == true;
                _orbitCamera?.HandleDragInput(@event);

                if (!wasDrag)
                    PerformCellSelection(mouseBtn.Position);

                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // Forward mouse motion to orbit camera for drag rotation
        if (@event is InputEventMouseMotion)
        {
            _orbitCamera?.HandleDragInput(@event);
        }
    }

    // ───────── Cell Selection ─────────

    /// <summary>
    /// Performs a raycast from the camera at the given screen position and selects
    /// the hit Voronoi cell, mirroring the logic in PlayerController.OnCastRay.
    /// </summary>
    private void PerformCellSelection(Vector2 screenPos)
    {
        if (_playerCamera == null || _currentBody == null)
            return;

        var origin = _playerCamera.ProjectRayOrigin(screenPos);
        var direction = origin + _playerCamera.ProjectRayNormal(screenPos) * 1000f;

        var spaceState = _playerCamera.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(origin, direction);
        query.CollideWithAreas = true;
        var result = spaceState.IntersectRay(query);

        if (result.Count == 0)
            return;

        var collider = (Node3D)result["collider"];

        // Check if we hit a billboard label click area
        if (
            _billboardManager != null
            && _billboardManager.TryGetClickedItem(collider, out var labelType, out var labelIndex)
        )
        {
            // Forward to tabbed panel to show details for the clicked entity
            _tabbedPanel?.EmitSignal(
                OrbitalBodyTabbedPanel.SignalName.ItemSelected,
                labelType,
                labelIndex
            );
            return;
        }

        var selectableBody = FindSelectableBody(collider);
        if (selectableBody == null)
            return;

        var faceIndex = (int)result["face_index"];
        var selectionResult = selectableBody.GetFaceFromIndex(faceIndex);

        if (selectionResult?.Cell != null)
        {
            CellSelectionManager.Instance?.SelectCell(
                selectionResult.Cell,
                (Node3D)selectableBody
            );
        }
        else
        {
            // Fallback to octree method
            var position = (Vector3)result["position"];
            var fallback = selectableBody.FindNearestCell(position);
            if (fallback?.Cell != null)
            {
                CellSelectionManager.Instance?.SelectCell(
                    fallback.Cell,
                    (Node3D)selectableBody
                );
            }
        }
    }

    private static ISelectableBody? FindSelectableBody(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is ISelectableBody body)
                return body;
            current = current.GetParentOrNull<Node>();
        }
        return null;
    }

    // ───────── Public API ─────────

    /// <summary>
    /// Called by OrbitalBodyDetailsPanel when the user wants to inspect a specific station.
    /// Emits StationInspectRequested so the HSM can handle the cross-window transition.
    /// </summary>
    public void RequestStationInspect(int stationIndex)
    {
        EmitSignal(SignalName.StationInspectRequested, stationIndex);
    }

    /// <summary>
    /// Called by OrbitalBodyDetailsPanel when the user wants to inspect a logistics unit.
    /// Emits LogisticsUnitInspectRequested so the HSM can handle the cross-window transition.
    /// </summary>
    public void RequestLogisticsUnitInspect(int unitIndex)
    {
        EmitSignal(SignalName.LogisticsUnitInspectRequested, unitIndex);
    }

    /// <summary>
    /// Called by OrbitalBodyTabbedPanel when the user clicks a building row in the
    /// Buildings tab. Emits BuildingInspectRequested so the HSM transitions to BuildingHSM.
    /// </summary>
    public void RequestBuildingInspect(Building building)
    {
        EmitSignal(SignalName.BuildingInspectRequested, building);
    }

    // ───────── Callbacks ─────────

    private void OnBodyExiting()
    {
        GameLogger.Warning(
            "[OrbitalBodyWindow] Inspected body is leaving the tree — closing window"
        );
        HideWindow();
    }

    // ───────── Signal Wiring ─────────

    private void SubscribeSignals()
    {
        if (_currentBody?.Mesh?.Continents != null)
            _currentBodyContinentIndices = new HashSet<int>(_currentBody.Mesh.Continents.Keys);
        else
            _currentBodyContinentIndices = null;

        if (SignalBus.Instance == null)
            return;

        SignalBus.Instance.ContinentEconomyTicked += OnContinentEconomyTicked;
        SignalBus.Instance.ContinentPowerStateChanged += OnContinentPowerStateChanged;
        SignalBus.Instance.ContinentResourceShortage += OnContinentResourceShortage;
        SignalBus.Instance.TransferDispatched += OnTransferDispatched;
        SignalBus.Instance.TransferArrived += OnTransferArrived;
        SignalBus.Instance.TransferScheduleStateChanged += OnTransferScheduleStateChanged;
        SignalBus.Instance.StationUpgraded += OnStationUpgraded;
        SignalBus.Instance.BuildingConstructed += OnBuildingCountChanged;
        SignalBus.Instance.BuildingRemoved += OnBuildingCountChanged;
    }

    private void UnsubscribeSignals()
    {
        _currentBodyContinentIndices = null;

        if (SignalBus.Instance == null)
            return;

        SignalBus.Instance.ContinentEconomyTicked -= OnContinentEconomyTicked;
        SignalBus.Instance.ContinentPowerStateChanged -= OnContinentPowerStateChanged;
        SignalBus.Instance.ContinentResourceShortage -= OnContinentResourceShortage;
        SignalBus.Instance.TransferDispatched -= OnTransferDispatched;
        SignalBus.Instance.TransferArrived -= OnTransferArrived;
        SignalBus.Instance.TransferScheduleStateChanged -= OnTransferScheduleStateChanged;
        SignalBus.Instance.StationUpgraded -= OnStationUpgraded;
        SignalBus.Instance.BuildingConstructed -= OnBuildingCountChanged;
        SignalBus.Instance.BuildingRemoved -= OnBuildingCountChanged;
    }

    private bool IsContinentRelevant(int continentIndex)
    {
        return IsOpen
            && _currentBody != null
            && _currentBodyContinentIndices != null
            && _currentBodyContinentIndices.Contains(continentIndex);
    }

    private void RefreshActiveTab()
    {
        _tabbedPanel?.RefreshCurrentTab();
        _detailsPanel?.RefreshCurrentDetails();
    }

    // ───────── Signal Handlers ─────────

    private void OnContinentEconomyTicked(int continentIndex)
    {
        if (!IsContinentRelevant(continentIndex))
            return;

        double now = Time.GetUnixTimeFromSystem();
        if (now - _lastEconomyRefreshTime < ECONOMY_DEBOUNCE_SECONDS)
            return;
        _lastEconomyRefreshTime = now;

        _headerPanel?.Populate(_currentBody!);

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        if (tab == 0 || tab == 1 || tab == 3)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnContinentPowerStateChanged(int continentIndex, bool isDeficit)
    {
        if (!IsContinentRelevant(continentIndex))
            return;

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        // Power deficit affects Overview (Economy summary), Buildings (powered state),
        // Grids (brownout flag), and Stations (active state).
        if (tab == 0 || tab == 1 || tab == 2 || tab == 3)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnContinentResourceShortage(int continentIndex, string resourceId, bool isShortage)
    {
        if (!IsContinentRelevant(continentIndex))
            return;

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnTransferDispatched(string orderId, int originContinentIndex)
    {
        if (!IsContinentRelevant(originContinentIndex))
            return;

        _headerPanel?.Populate(_currentBody!);

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        if (tab == 4)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnTransferArrived(string orderId, bool fullyAccepted)
    {
        if (!IsOpen || _currentBody == null)
            return;

        _headerPanel?.Populate(_currentBody);

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        if (tab == 4)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnTransferScheduleStateChanged(string scheduleId, int newState)
    {
        if (!IsOpen || _currentBody == null)
            return;

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        if (tab == 4)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnBuildingCountChanged(int continentIndex)
    {
        if (!IsContinentRelevant(continentIndex))
            return;

        _headerPanel?.Populate(_currentBody!);

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        if (tab == 0 || tab == 1 || tab == 3)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnStationUpgraded(StationSatellite station)
    {
        if (!IsOpen || _currentBody == null)
            return;

        if (station.ParentBody != _currentBody)
            return;

        _headerPanel?.Populate(_currentBody);

        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        // Overview surfaces station counts; Stations tab (3) lists stations directly.
        if (tab == 0 || tab == 3)
            _tabbedPanel?.RefreshCurrentTab();

        _detailsPanel?.RefreshCurrentDetails();
    }
}
