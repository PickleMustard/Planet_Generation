using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Transfers;
using UtilityLibrary;

namespace UI.TransferPlanning;

/// <summary>
/// Top-level modal window for planning resource transfers between continents/stations.
/// Orchestrates endpoint selection, cargo manifest, route preview, and dispatch.
/// </summary>
public partial class TransferPlanningWindow : Control
{
    public static TransferPlanningWindow? Instance { get; private set; }

    [Export]
    private Control? _blockInput;

    [Export]
    private PanelContainer? _panelContainer;

    [Export]
    private TextureButton? _closeButton;

    [Export]
    private EndpointSelectionPanel? _endpointSelectionPanel;

    [Export]
    private TransferRoutePanel? _transferRoutePanel;

    [Export]
    private CargoManifestPanel? _cargoManifestPanel;

    [Export]
    private TransferModeSelector? _transferModeSelector;

    [Export]
    private TransferStatusBar? _transferStatusBar;

    // Origin info labels (right column)
    [Export]
    private Label? _originNameLabel;

    [Export]
    private Label? _stationStatusLabel;

    [Export]
    private Label? _capacityLabel;

    [Export]
    private Label? _activeTransfersLabel;

    private Node3D? _currentBody;
    private Continent? _currentContinent;
    private int _originContinentIndex = -1;
    private BodyTransferManager? _transferMgr;
    private TransferDestination? _selectedDestination;
    private float _totalCapacity;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.ContinentTransferCapacityChanged -= OnCapacityChanged;
        }
    }

    public override void _Ready()
    {
        Hide();

        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;

        if (_blockInput != null)
            _blockInput.GuiInput += OnBackdropInput;

        if (_endpointSelectionPanel != null)
            _endpointSelectionPanel.DestinationSelected += OnDestinationSelected;

        if (_cargoManifestPanel != null)
            _cargoManifestPanel.ManifestChanged += OnManifestChanged;

        if (_transferModeSelector != null)
            _transferModeSelector.ModeChanged += OnModeChanged;

        if (_transferStatusBar != null)
            _transferStatusBar.DispatchPressed += OnDispatchPressed;

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.ContinentTransferCapacityChanged += OnCapacityChanged;
        }

        GameLogger.Info("TransferPlanningWindow initialized");
    }

    public override void _Input(InputEvent @event)
    {
        // Escape handling moved to GUIManager - emit signal instead
        if (!Visible)
            return;
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            EmitSignal(SignalName.WindowCloseRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// Opens the transfer planning window for a given continent.
    /// </summary>
    public void ShowWindow(int continentIndex, Node3D body, Continent continent)
    {
        _originContinentIndex = continentIndex;
        _currentBody = body;
        _currentContinent = continent;
        _selectedDestination = null;

        // Resolve transfer manager
        _transferMgr = body switch
        {
            CelestialBody cb => cb.TransferMgr,
            SatelliteBody sb => sb.TransferMgr,
            _ => null,
        };

        if (_transferMgr == null || !_transferMgr.HasTransferStation(continentIndex))
        {
            ToastSystem.Instance?.ShowError("Origin has no transfer station");
            return;
        }

        // Read capacity and transfer info
        _totalCapacity = _transferMgr.GetTotalCapacity(continentIndex);
        int maxConcurrent = _transferMgr.GetMaxConcurrentTransfers(continentIndex);
        int activeCount = _transferMgr.GetActiveTransferCountForContinent(continentIndex);

        // Populate origin info (right column)
        if (_originNameLabel != null)
            _originNameLabel.Text = $"Continent {continentIndex}";
        if (_stationStatusLabel != null)
            _stationStatusLabel.Text = "Transfer Station Active";
        if (_capacityLabel != null)
            _capacityLabel.Text = $"Capacity: {_totalCapacity:F0}";
        if (_activeTransfersLabel != null)
            _activeTransfersLabel.Text = $"Transfers: {activeCount} / {maxConcurrent}";

        // Populate sub-panels
        _endpointSelectionPanel?.Populate(body, continentIndex);

        if (continent.Economy != null)
            _cargoManifestPanel?.Initialize(continent.Economy, _totalCapacity);

        if (_transferRoutePanel != null && body is ISelectableBody selectable)
            _transferRoutePanel.SetOrigin(selectable, continent);

        _transferModeSelector?.Reset();
        _transferStatusBar?.SetButtonText("Dispatch Transfer");

        Show();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        Validate();

        GameLogger.Info($"TransferPlanningWindow shown for continent {continentIndex}");
    }

    /// <summary>
    /// Hides the window and restores game state.
    /// </summary>
    public void HideWindow()
    {
        Hide();

        _transferRoutePanel?.ClearPreview();
        _cargoManifestPanel?.Clear();
        _endpointSelectionPanel?.Clear();

        _currentBody = null;
        _currentContinent = null;
        _originContinentIndex = -1;
        _transferMgr = null;
        _selectedDestination = null;

        // Note: Return to ContinentInfoWindow is now handled by HSM transition

        GameLogger.Info("TransferPlanningWindow hidden");
    }

    private void OnClosePressed()
    {
        HideWindow();
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (
            @event is InputEventMouseButton mouseEvent
            && mouseEvent.ButtonIndex == MouseButton.Left
            && mouseEvent.Pressed
        )
        {
            HideWindow();
        }
    }

    private void OnDestinationSelected(TransferDestination destination)
    {
        _selectedDestination = destination;

        if (
            _transferMgr != null
            && _transferRoutePanel != null
            && _currentBody is ISelectableBody selectable
        )
        {
            float travelTime = _transferMgr.ComputeTravelTime(_originContinentIndex, destination);
            float speed = _transferMgr.GetVehicleSpeed(_originContinentIndex);
            _transferRoutePanel.SetDestination(selectable, destination, travelTime, speed);
        }

        Validate();
    }

    private void OnManifestChanged()
    {
        Validate();
    }

    private void OnModeChanged(bool isScheduleMode)
    {
        if (_transferStatusBar != null)
            _transferStatusBar.SetButtonText(
                isScheduleMode ? "Create Schedule" : "Dispatch Transfer"
            );
        Validate();
    }

    private void OnCapacityChanged(int continentIndex, float newCapacity)
    {
        if (continentIndex != _originContinentIndex || !Visible)
            return;

        _totalCapacity = newCapacity;
        if (_capacityLabel != null)
            _capacityLabel.Text = $"Capacity: {_totalCapacity:F0}";

        _cargoManifestPanel?.UpdateCapacity(_totalCapacity);
        Validate();
    }

    private void Validate()
    {
        if (_transferStatusBar == null)
            return;

        if (_transferMgr == null || !_transferMgr.HasTransferStation(_originContinentIndex))
        {
            _transferStatusBar.SetError("Origin has no transfer station");
            return;
        }

        if (_selectedDestination == null)
        {
            _transferStatusBar.SetError("Select a destination");
            return;
        }

        float usedCapacity = _cargoManifestPanel?.GetUsedCapacity() ?? 0f;
        int resourceCount = _cargoManifestPanel?.GetResourceCount() ?? 0;

        if (resourceCount == 0 || usedCapacity <= 0f)
        {
            _transferStatusBar.SetError("Add resources to transfer");
            return;
        }

        if (usedCapacity > _totalCapacity)
        {
            _transferStatusBar.SetError("Cargo exceeds capacity");
            return;
        }

        bool isScheduleMode = _transferModeSelector?.IsScheduleMode ?? false;

        if (!isScheduleMode)
        {
            int activeCount = _transferMgr.GetActiveTransferCountForContinent(
                _originContinentIndex
            );
            int maxConcurrent = _transferMgr.GetMaxConcurrentTransfers(_originContinentIndex);
            if (activeCount >= maxConcurrent)
            {
                _transferStatusBar.SetError("Max concurrent transfers reached");
                return;
            }
        }

        _transferStatusBar.SetValid();
    }

    private void OnDispatchPressed()
    {
        if (_transferMgr == null || _selectedDestination == null || _cargoManifestPanel == null)
            return;

        bool isScheduleMode = _transferModeSelector?.IsScheduleMode ?? false;

        if (isScheduleMode)
        {
            DispatchSchedule();
        }
        else
        {
            DispatchOneTime();
        }
    }

    private void DispatchOneTime()
    {
        var resources = _cargoManifestPanel!.GetManifestResources();
        string? orderId = _transferMgr!.DispatchOneTimeTransfer(
            _originContinentIndex,
            _selectedDestination!,
            resources
        );

        if (orderId != null)
        {
            ToastSystem.Instance?.ShowInfo("Transfer dispatched!");
            HideWindow();
        }
        else
        {
            ToastSystem.Instance?.ShowError("Failed to dispatch transfer");
        }
    }

    private void DispatchSchedule()
    {
        var proportions = _cargoManifestPanel!.GetManifestResources();
        DepartureConditionMode departureMode =
            _transferModeSelector?.GetDepartureMode() ?? DepartureConditionMode.AnyResource;
        DepartureThreshold threshold =
            _transferModeSelector?.GetThreshold() ?? DepartureThreshold.Half;

        string? scheduleId = _transferMgr!.CreateSchedule(
            _originContinentIndex,
            _selectedDestination!,
            proportions,
            departureMode,
            threshold
        );

        if (scheduleId != null)
        {
            _transferMgr.StartSchedule(scheduleId);
            ToastSystem.Instance?.ShowInfo("Schedule created!");
            HideWindow();
        }
        else
        {
            ToastSystem.Instance?.ShowError("Failed to create schedule");
        }
    }
}
