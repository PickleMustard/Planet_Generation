using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using UI.PlanetBoard.Modes;
using UI.Wireframe;

namespace UI.PlanetBoard;

/// <summary>
/// Thin <see cref="Control"/> wrapper that references a
/// SubViewportContainer → SubViewport → Camera2D + BoardWorld hierarchy
/// defined in <c>PlanetBoardView.tscn</c>. All building rendering and input
/// dispatch is delegated to <see cref="BoardWorld"/>; camera control is
/// delegated to <see cref="BoardCameraController"/>.
///
/// The base class remains <c>Control</c> (not SubViewportContainer) so
/// the existing <c>PlanetBoardWindow</c> export reference and the
/// <c>.tscn</c> node type stay valid.
/// </summary>
public partial class PlanetBoardView : Control
{
    // ── Draw constants ─────────────────────────────────────────────────

    private const float BorderInset = 2f;
    private const float BorderWidth = 1f;
    private static readonly Color BorderColor = new(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.6f);

    // ── Scene tree references (set via Export from .tscn) ─────────────

    [Export]
    private SubViewport? _viewport;

    [Export]
    private BoardCameraController? _cameraController;

    [Export]
    private BoardWorld? _world;

    // ── Lifecycle ──────────────────────────────────────────────────────

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;

        // Wire the camera controller to BoardWorld so hit-testing and
        // coordinate conversion work (critical for all item interaction).
        if (_world != null && _cameraController != null)
            _world.CameraController = _cameraController;

        Resized += OnResized;
        OnResized(); // initial sizing
    }

    // ── Input forwarding ──────────────────────────────────────────────

    /// <summary>
    /// Receives GUI input events from the main viewport because this
    /// Control has <c>MouseFilter.Stop</c>.  Events are forwarded to the
    /// <see cref="BoardCameraController"/> first (scroll zoom, middle-mouse
    /// pan); if the camera does not consume the event it is forwarded to
    /// <see cref="BoardWorld"/> for building/port interaction.
    /// Input is only processed when this view is visible, keeping camera
    /// and board behaviour contained to a known GUI state.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (!Visible)
            return;

        // Camera gets first crack (scroll zoom, middle-mouse pan).
        if (_cameraController != null && _cameraController.HandleInputEvent(@event))
        {
            AcceptEvent();
            return;
        }

        // BoardWorld handles everything else (left/right click, hover, drag).
        if (_world != null && _world.HandleInputEvent(@event))
        {
            AcceptEvent();
        }
    }

    // ── Resize handling ────────────────────────────────────────────────

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
        var rect = new Rect2(new Vector2(BorderInset, BorderInset), new Vector2(w, h));
        DrawRect(rect, BorderColor, filled: false, width: BorderWidth);
    }

    // ── Public API (preserved for PlanetBoardWindow compatibility) ─────

    /// <summary>The orbital body currently displayed, or null.</summary>
    public IOrbitalBody? Body => _world?.Body;

    /// <summary>The currently active <see cref="IPlanetBoardMode"/>, or null.</summary>
    public IPlanetBoardMode? ActiveMode => _world?.ActiveMode;

    /// <summary>The <see cref="BoardWorld"/> managing building nodes and input.</summary>
    public BoardWorld World => _world!;

    /// <summary>The <see cref="BoardCameraController"/> attached to the camera.</summary>
    public BoardCameraController CameraController => _cameraController!;

    /// <summary>Compatibility alias — returns <see cref="CameraController"/>.</summary>
    public BoardCameraController Camera => CameraController;

    /// <summary>Sets the orbital body and refreshes the board from its buildings.</summary>
    public void SetBody(IOrbitalBody? body) => _world?.SetBody(body);

    /// <summary>Switches to a new <see cref="IPlanetBoardMode"/>.</summary>
    public void SetMode(IPlanetBoardMode? mode) => _world?.SetMode(mode);

    /// <summary>Re-creates all building nodes from the current body.</summary>
    public void RefreshFromBody() => _world?.RefreshFromBody();

    /// <summary>Rebuilds the link renderer from current building port state.</summary>
    public void RefreshLinks() => _world?.RefreshLinks();

    /// <summary>
    /// Convenience for the test scene: re-fetch content bbox from the
    /// layout engine and frame the camera on it.
    /// </summary>
    public void ResetCamera()
    {
        if (_world == null || _cameraController == null)
            return;

        var bbox = _world.ContentBoundingBox;
        _cameraController.FitToContent(bbox);
    }
}
