using Godot;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// A <see cref="Camera2D"/> script that handles middle-mouse drag for panning and
/// scroll-wheel for cursor-anchored zoom. When <see cref="FitToContent"/> is called,
/// the camera frames the given bounding box and computes <see cref="MinZoom"/> so
/// the content always fits within the viewport.
///
/// Two coordinate spaces:
///   - board-space: logical, units = pixels-at-zoom-1.0
///   - screen-space: pixels within the SubViewport
///
/// Forward: <c>screen = (board - Position) * Zoom.X + ViewportSize / 2</c>.
/// Inverse: <c>board  = (screen - ViewportSize / 2) / Zoom.X + Position</c>.
/// </summary>
public partial class BoardCameraController : Camera2D
{
    /// <summary>
    /// Emitted whenever the camera's view (position or zoom) changes,
    /// or its viewport size is updated. Subscribers can use this to
    /// trigger redraws of board-space content tied to the visible area.
    /// </summary>
    [Signal]
    public delegate void ViewChangedEventHandler();

    // ── Export properties ──────────────────────────────────────────────

    [Export]
    public float MaxZoom { get; set; } = 4.0f;

    [Export]
    public float MinZoomExport { get; set; } = 0.1f;

    [Export]
    public float ZoomStep { get; set; } = 1.1f;

    [Export]
    public float FitMargin { get; set; } = 0.95f;

    // ── Public properties ─────────────────────────────────────────────

    /// <summary>
    /// Minimum zoom level. Computed from content bbox and viewport when
    /// <see cref="FitToContent"/> is called; clamped to [<see cref="MinZoomExport"/>,
    /// <see cref="MaxZoom"/>].
    /// </summary>
    public float MinZoom
    {
        get => _minZoom;
        private set => _minZoom = Mathf.Clamp(value, MinZoomExport, MaxZoom);
    }

    // ── Private state ──────────────────────────────────────────────────

    private bool _panning;
    private Vector2 _panStartScreen;
    private Vector2 _panStartOffset;
    private Vector2 _viewportSize = new(800, 600);
    private Rect2 _contentBBox = new(Vector2.Zero, Vector2.Zero);
    private float _minZoom = 0.1f;

    // ── Lifecycle ──────────────────────────────────────────────────────

    public override void _Ready()
    {
        AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
        ProcessCallback = Camera2D.Camera2DProcessCallback.Idle;
        Zoom = Vector2.One;
        MakeCurrent();
    }

    // ── Input handling ──────────────────────────────────────────────────

    /// <summary>
    /// Entry point for input events forwarded from <see cref="PlanetBoardView._GuiInput"/>.
    /// Handles scroll-wheel zoom and middle-mouse panning. Returns <c>true</c>
    /// when the event was consumed (scroll or middle-mouse) so the caller can
    /// stop propagating it to <see cref="BoardWorld"/>.
    /// </summary>
    public bool HandleInputEvent(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb:
                return HandleMouseButton(mb);
            case InputEventMouseMotion mm:
                return HandleMouseMotion(mm);
        }
        return false;
    }

    private bool HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp)
        {
            ZoomAtScreen(mb.Position, ZoomStep);
            return true;
        }

        if (mb.ButtonIndex == MouseButton.WheelDown)
        {
            ZoomAtScreen(mb.Position, 1f / ZoomStep);
            return true;
        }

        if (mb.ButtonIndex == MouseButton.Middle)
        {
            if (mb.Pressed)
            {
                _panning = true;
                _panStartScreen = mb.Position;
                _panStartOffset = Position;
            }
            else
            {
                _panning = false;
            }

            return true;
        }

        return false;
    }

    private bool HandleMouseMotion(InputEventMouseMotion mm)
    {
        if (!_panning)
            return false;

        Position = _panStartOffset - (mm.Position - _panStartScreen) / Zoom.X;
        ClampPosition();
        EmitViewChanged();
        return true;
    }

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Cursor-anchored zoom: zooms by <paramref name="factor"/> while keeping the
    /// board point under <paramref name="screenPos"/> stationary.
    /// </summary>
    public void ZoomAtScreen(Vector2 screenPos, float factor)
    {
        Vector2 boardBefore = ScreenToBoard(screenPos);
        float newZoom = Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom);
        Zoom = new Vector2(newZoom, newZoom);
        Vector2 boardAfter = ScreenToBoard(screenPos);
        Position += boardBefore - boardAfter;
        ClampPosition();
        EmitViewChanged();
    }

    /// <summary>
    /// Sets zoom and position to frame the given content bounding box within the
    /// viewport, and recomputes <see cref="MinZoom"/>.
    /// </summary>
    public void FitToContent(Rect2 contentBBox)
    {
        _contentBBox = contentBBox;
        // A zero-size bbox means "no content" — fall back to a 1×1 rect
        // centered at the given position so the camera has a valid target.
        if (_contentBBox.Size.X <= 0f || _contentBBox.Size.Y <= 0f)
            _contentBBox = new Rect2(_contentBBox.Position, new Vector2(1, 1));

        RecomputeMinZoom();
        Zoom = new Vector2(MinZoom, MinZoom);
        Position = _contentBBox.GetCenter();
        EmitViewChanged();
    }

    /// <summary>
    /// Updates the content bounding box and recomputes <see cref="MinZoom"/>.
    /// If the current zoom level is too high to show the full content
    /// (i.e. zoom &gt; new MinZoom), zooms out to MinZoom and re-centers
    /// the camera.  Otherwise, keeps the user's zoom and pan intact.
    /// Call this when the layout circle grows and the camera needs to
    /// expand its viewable area without jarring resets.
    /// </summary>
    public void EnsureContentVisible(Rect2 contentBBox)
    {
        _contentBBox = contentBBox;
        if (_contentBBox.Size.X <= 0f || _contentBBox.Size.Y <= 0f)
            _contentBBox = new Rect2(_contentBBox.Position, new Vector2(1, 1));

        float oldMinZoom = MinZoom;
        RecomputeMinZoom();

        // If the content grew beyond what the current zoom can show,
        // zoom out to the new minimum and re-center.
        if (Zoom.X > MinZoom + 1e-4f)
        {
            Zoom = new Vector2(MinZoom, MinZoom);
            Position = _contentBBox.GetCenter();
        }
        else
        {
            // Content shrank or stayed the same — just ensure current
            // zoom is still in range and clamp position.
            float clamped = Mathf.Clamp(Zoom.X, MinZoom, MaxZoom);
            if (!Mathf.IsEqualApprox(clamped, Zoom.X))
                Zoom = new Vector2(clamped, clamped);
            ClampPosition();
        }
        EmitViewChanged();
    }

    /// <summary>
    /// Updates the cached viewport size and recomputes <see cref="MinZoom"/>.
    /// Call when the SubViewport is resized.
    /// </summary>
    public void UpdateViewportSize(Vector2 size)
    {
        _viewportSize = new Vector2(Mathf.Max(1f, size.X), Mathf.Max(1f, size.Y));
        RecomputeMinZoom();
        EmitViewChanged();
    }

    // ── Coordinate helpers ─────────────────────────────────────────────

    /// <summary>Converts a board-space position to screen-space.</summary>
    public Vector2 BoardToScreen(Vector2 boardPos) =>
        (boardPos - Position) * Zoom.X + _viewportSize * 0.5f;

    /// <summary>Converts a screen-space position to board-space.</summary>
    public Vector2 ScreenToBoard(Vector2 screenPos) =>
        (screenPos - _viewportSize * 0.5f) / Zoom.X + Position;

    /// <summary>
    /// Returns the camera's currently visible region in board-space.
    /// </summary>
    public Rect2 GetVisibleBoardRect()
    {
        Vector2 size = _viewportSize / Mathf.Max(1e-4f, Zoom.X);
        return new Rect2(Position - size * 0.5f, size);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private void RecomputeMinZoom()
    {
        // Before FitToContent is called the bbox may still be zero-size;
        // in that case use the export default so the camera isn't locked
        // to an extreme zoom range.
        if (_contentBBox.Size.X <= 0f || _contentBBox.Size.Y <= 0f)
        {
            MinZoom = MinZoomExport;
            return;
        }

        float fx = _viewportSize.X / Mathf.Max(1f, _contentBBox.Size.X);
        float fy = _viewportSize.Y / Mathf.Max(1f, _contentBBox.Size.Y);
        MinZoom = Mathf.Min(fx, fy) * FitMargin;
        if (MinZoom > MaxZoom)
            MinZoom = MaxZoom;

        // Clamp current zoom to the new min/max range.
        float clamped = Mathf.Clamp(Zoom.X, MinZoom, MaxZoom);
        if (!Mathf.IsEqualApprox(clamped, Zoom.X))
            Zoom = new Vector2(clamped, clamped);
    }

    private void EmitViewChanged() => EmitSignal(SignalName.ViewChanged);

    private void ClampPosition()
    {
        if (Zoom.X <= MinZoom + 1e-4f)
        {
            // At min zoom, lock center to bbox center.
            Position = _contentBBox.GetCenter();
            return;
        }

        // Allow panning within half a viewport of the bbox center so the
        // camera never strays into empty space.
        Vector2 halfView = _viewportSize / (2f * Zoom.X);
        Vector2 min = _contentBBox.Position - halfView * 0.5f;
        Vector2 max = _contentBBox.End + halfView * 0.5f;
        Position = new Vector2(
            Mathf.Clamp(Position.X, min.X, max.X),
            Mathf.Clamp(Position.Y, min.Y, max.Y)
        );
    }
}
