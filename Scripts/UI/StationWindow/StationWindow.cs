using Constructables;
using Godot;
using Godot.Collections;
using PlayerInteraction.Camera;
using UI.Components;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Diegetic full-screen GUI window for inspecting orbital stations
/// (<see cref="StationSatellite"/>). The main player camera is repurposed to
/// frame the station in the left third of the viewport with the station's
/// parent body behind it; the right two thirds host the cartouche (upper
/// left) and a behavior-driven tabbed panel.
/// <para>
/// The embedded <see cref="FilterableSidebar"/> is driven by
/// <see cref="SignalBus.StationSidebarRequested"/> so that any child panel
/// can request sidebar content without holding a direct reference.
/// </para>
/// </summary>
public partial class StationWindow : Control
{
    public static StationWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private Button? _backButton;

    [Export]
    private StationCartouchePanel? _cartouchePanel;

    [Export]
    private StationTabbedPanel? _tabbedPanel;

    [Export]
    private FilterableSidebar? _sidebar;

    private StationSatellite? _currentStation;
    private Camera3D? _playerCamera;
    private PlayerCameraController? _controller;

    // Right two-thirds is the interactive panel region; left third holds the
    // station view. Mirrors the negative-space pattern used by
    // OrbitalBodyWindow.ComputeCameraOffset.
    private const float TAB_PANEL_FRACTION = 2f / 3f;

    public bool IsOpen { get; private set; }

    public StationSatellite? CurrentStation => _currentStation;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void BuildingInspectRequestedEventHandler(Building building);

    public void RequestBuildingInspect(Building building)
        => EmitSignal(SignalName.BuildingInspectRequested, building);

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;
        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;

        // Hide sidebar initially
        if (_sidebar != null)
            _sidebar.Visible = false;

        // Subscribe to SignalBus for sidebar events
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.StationSidebarRequested += OnSidebarRequested;
            SignalBus.Instance.StationSidebarDismissed += OnSidebarDismissed;
        }
    }

    private void OnCloseButtonPressed()
        => EmitSignal(SignalName.WindowCloseRequested);

    private void OnBackPressed()
        => EmitSignal(SignalName.BackRequested);

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.StationSidebarRequested -= OnSidebarRequested;
            SignalBus.Instance.StationSidebarDismissed -= OnSidebarDismissed;
        }

        if (Instance == this)
            Instance = null;
    }

    public void ShowWindow(StationSatellite station, Camera3D playerCamera)
    {
        if (IsOpen)
            HideWindow();

        _currentStation = station;
        _playerCamera = playerCamera;
        _controller = playerCamera as PlayerCameraController;
        IsOpen = true;
        Visible = true;

        _controller?.EnterFocus(
            station.GetOrCreateCameraAnchor(),
            station,
            new StationFramingStrategy(station),
            ComputeCameraOffset());

        _cartouchePanel?.Populate(station);
        _tabbedPanel?.Initialize(station);

        if (station is Node stationNode)
            stationNode.TreeExiting += OnStationExiting;

        GameLogger.Info($"[StationWindow] Opened for '{station.Name}'");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        // Hide the built-in sidebar
        if (_sidebar != null)
        {
            _sidebar.ClearItems();
            _sidebar.Visible = false;
        }

        _controller?.ExitFocus();
        _controller = null;
        _cartouchePanel?.Clear();
        _tabbedPanel?.Clear();

        if (_currentStation is Node stationNode && IsInstanceValid(stationNode))
            stationNode.TreeExiting -= OnStationExiting;

        _currentStation = null;
        _playerCamera = null;
        Visible = false;

        GameLogger.Info("[StationWindow] Closed");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen)
            return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            EmitSignal(SignalName.WindowCloseRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    private Vector2 ComputeCameraOffset()
    {
        // Positive X shifts the station LEFT on screen — center it in the
        // left third by sliding by half the panel-region width.
        var vp = GetViewport().GetVisibleRect().Size;
        return new Vector2(vp.X * TAB_PANEL_FRACTION * 0.5f, 0f);
    }

    private void OnStationExiting()
    {
        GameLogger.Warning("[StationWindow] Inspected station is leaving the tree — closing window");
        HideWindow();
    }

    // --- StationSidebar signal handlers ---

    /// <summary>
    /// Handles <see cref="SignalBus.StationSidebarRequested"/>.
    /// Populates the embedded sidebar with the supplied items and makes it visible.
    /// Each item dictionary should contain at minimum "id" and "name" keys.
    /// </summary>
    private void OnSidebarRequested(string label, Godot.Collections.Array<Dictionary> items)
    {
        if (_sidebar == null)
        {
            GameLogger.Warning("[StationWindow] Sidebar node not configured — cannot show");
            return;
        }

        _sidebar.SetTitle(label);
        _sidebar.SetSearchPlaceholder("Search…");
        _sidebar.ClearItems();

        foreach (var itemDict in items)
        {
            string id = itemDict.TryGetValue("id", out var idVar) ? idVar.AsString() : "";
            string name = itemDict.TryGetValue("name", out var nameVar) ? nameVar.AsString() : id;
            string mass = itemDict.TryGetValue("mass", out var massVar) ? massVar.AsString() : "";
            string time = itemDict.TryGetValue("time", out var timeVar) ? timeVar.AsString() : "";
            string resources = itemDict.TryGetValue("resources", out var resVar) ? resVar.AsString() : "";

            // Whole row is clickable — no per-item button. Capturing `id` per
            // iteration is correct (foreach declares a fresh variable each pass).
            var row = new SidebarListItem(id);

            var nameLabel = new Label
            {
                Text = name,
                CustomMinimumSize = new Vector2(120, 0),
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 13);
            row.AddCell(nameLabel);

            if (!string.IsNullOrEmpty(mass))
            {
                var massLabel = new Label { Text = mass };
                massLabel.AddThemeFontSizeOverride("font_size", 12);
                massLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.8f));
                row.AddCell(massLabel);
            }

            if (!string.IsNullOrEmpty(time))
            {
                var timeLabel = new Label { Text = time };
                timeLabel.AddThemeFontSizeOverride("font_size", 12);
                timeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.8f));
                row.AddCell(timeLabel);
            }

            if (!string.IsNullOrEmpty(resources))
            {
                var resLabel = new Label { Text = resources };
                resLabel.AddThemeFontSizeOverride("font_size", 11);
                resLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.6f));
                resLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddCell(resLabel);
            }

            row.Activated += selectedId =>
            {
                SignalBus.Instance?.EmitStationSidebarItemSelected(selectedId);
            };

            _sidebar.AddItem(row, filterText: name);
        }

        _sidebar.Visible = true;
        GameLogger.Info($"[StationWindow] Sidebar opened: {label} ({items.Count} items)");
    }

    /// <summary>
    /// Handles <see cref="SignalBus.StationSidebarDismissed"/>.
    /// Hides the sidebar and clears its contents.
    /// </summary>
    private void OnSidebarDismissed()
    {
        if (_sidebar == null)
            return;

        _sidebar.ClearItems();
        _sidebar.Visible = false;
        GameLogger.Info("[StationWindow] Sidebar dismissed");
    }
}
