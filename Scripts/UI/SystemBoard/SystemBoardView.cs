using System;
using Constructables;
using Godot;
using ProceduralGeneration;
using Structures.Logistics;
using UI.PlanetBoard;
using UI.Wireframe;

namespace UI.SystemBoard;

/// <summary>
/// Thin <see cref="Control"/> wrapper over a
/// SubViewportContainer → SubViewport → Camera2D + <see cref="SystemBoardWorld"/>
/// hierarchy defined in <c>SystemBoardView.tscn</c>. Reuses
/// <see cref="BoardCameraController"/> unchanged for zoom/pan and forwards GUI
/// input to camera-first, then world. Parallel to <see cref="PlanetBoardView"/>
/// but renders the whole system instead of a single body.
/// </summary>
public partial class SystemBoardView : Control
{
    private const float BorderInset = 2f;
    private const float BorderWidth = 1f;
    private static readonly Color BorderColor = new(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.6f);

    [Export] private SubViewport? _viewport;
    [Export] private BoardCameraController? _cameraController;
    [Export] private SystemBoardWorld? _world;

    /// <summary>Re-raised from the world when a station is picked in pick mode.</summary>
    public event Action<StationSatellite, IOrbitalBody>? EndpointPicked;

    public SystemBoardWorld World => _world!;
    public BoardCameraController Camera => _cameraController!;

    public string OriginStationId
    {
        get => _world?.OriginStationId ?? "";
        set { if (_world != null) _world.OriginStationId = value; }
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;

        if (_world != null && _cameraController != null)
            _world.CameraController = _cameraController;

        if (_world != null)
        {
            _world.EndpointPicked += OnEndpointPicked;
            _world.HoverChanged += OnHoverChanged;
        }

        Resized += OnResized;
        OnResized();

        // Data may have been set before this view entered the tree (the camera
        // wasn't wired yet); frame whatever the world currently holds.
        ResetCamera();
    }

    public override void _ExitTree()
    {
        if (_world != null)
        {
            _world.EndpointPicked -= OnEndpointPicked;
            _world.HoverChanged -= OnHoverChanged;
        }
    }

    private void OnEndpointPicked(StationSatellite station, IOrbitalBody body)
        => EndpointPicked?.Invoke(station, body);

    private void OnHoverChanged(string? label) => TooltipText = label ?? "";

    public override void _GuiInput(InputEvent @event)
    {
        if (!Visible)
            return;

        if (_cameraController != null && _cameraController.HandleInputEvent(@event))
        {
            AcceptEvent();
            return;
        }

        if (_world != null && _world.HandleInputEvent(@event))
            AcceptEvent();
    }

    private void OnResized()
    {
        if (_viewport != null)
            _viewport.Size = new Vector2I((int)Size.X, (int)Size.Y);
        if (_cameraController != null)
            _cameraController.UpdateViewportSize(Size);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = Mathf.Max(0f, Size.X - BorderInset * 2f);
        float h = Mathf.Max(0f, Size.Y - BorderInset * 2f);
        if (w <= 0f || h <= 0f)
            return;
        DrawRect(new Rect2(new Vector2(BorderInset, BorderInset), new Vector2(w, h)),
            BorderColor, filled: false, width: BorderWidth);
    }

    // ── Public API ──────────────────────────────────────────────────────

    public void SetPickMode(bool enabled) => _world?.SetPickMode(enabled);

    public void SetFromUnit(LogisticsUnit unit)
    {
        _world?.SetFromUnit(unit);
        ResetCamera();
    }

    public void SetSystem(Node systemContainer)
    {
        _world?.SetSystem(systemContainer);
        ResetCamera();
    }

    public void ResetCamera()
    {
        if (_world == null || _cameraController == null)
            return;
        _cameraController.FitToContent(_world.ContentBoundingBox);
    }
}
