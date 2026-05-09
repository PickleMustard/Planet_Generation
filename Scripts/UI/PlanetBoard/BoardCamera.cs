using Godot;

namespace UI.PlanetBoard;

/// <summary>
/// Pure 2D zoom/pan transform for the PlanetBoard. Two coordinate spaces:
///   - board-space: logical, units = pixels-at-zoom-1.0
///   - screen-space: the Control's local pixels
///
/// Forward: <c>screen = (board - PanBoard) * Zoom + ViewportSize / 2</c>.
/// Inverse: <c>board  = (screen - ViewportSize / 2) / Zoom + PanBoard</c>.
///
/// MinZoom is computed so the full <see cref="ContentBBox"/> fits in
/// <see cref="ViewportSize"/> with a small margin; at min zoom, panning is locked
/// to the bbox center. Pan velocity in screen-pixels stays constant across zooms
/// because <see cref="ApplyPanFromScreenDelta"/> divides the screen delta by Zoom.
/// </summary>
public sealed class BoardCamera
{
    private const float FitMargin = 0.95f;

    public float MaxZoom { get; set; } = 4.0f;
    public float MinZoom { get; private set; } = 0.1f;
    public float Zoom { get; private set; } = 1.0f;
    public Vector2 PanBoard { get; private set; } = Vector2.Zero;
    public Vector2 ViewportSize { get; private set; } = new Vector2(800, 600);
    public Rect2 ContentBBox { get; private set; } = new Rect2(Vector2.Zero, new Vector2(1, 1));

    public Vector2 BoardToScreen(Vector2 board) =>
        (board - PanBoard) * Zoom + ViewportSize * 0.5f;

    public Vector2 ScreenToBoard(Vector2 screen) =>
        (screen - ViewportSize * 0.5f) / Zoom + PanBoard;

    public bool IsAtMinZoom => Zoom <= MinZoom + 1e-4f;

    /// <summary>Updates viewport size and recomputes MinZoom + clamps state.</summary>
    public void SetViewportSize(Vector2 size)
    {
        ViewportSize = new Vector2(Mathf.Max(1f, size.X), Mathf.Max(1f, size.Y));
        RecomputeMinZoom();
        Zoom = Mathf.Clamp(Zoom, MinZoom, MaxZoom);
        ClampPan();
    }

    /// <summary>Updates the content bounding box and recomputes MinZoom + clamps state.</summary>
    public void SetContentBBox(Rect2 bbox)
    {
        if (bbox.Size.X <= 0f || bbox.Size.Y <= 0f)
            bbox = new Rect2(bbox.Position, new Vector2(Mathf.Max(1f, bbox.Size.X), Mathf.Max(1f, bbox.Size.Y)));
        ContentBBox = bbox;
        RecomputeMinZoom();
        Zoom = Mathf.Clamp(Zoom, MinZoom, MaxZoom);
        ClampPan();
    }

    /// <summary>Snaps to MinZoom centered on the content bbox.</summary>
    public void FitToContent()
    {
        RecomputeMinZoom();
        Zoom = MinZoom;
        PanBoard = ContentBBox.GetCenter();
    }

    /// <summary>
    /// Cursor-anchored zoom: zooms by <paramref name="factor"/> while keeping the
    /// board point under <paramref name="screenPos"/> stationary.
    /// </summary>
    public void ZoomAt(Vector2 screenPos, float factor)
    {
        Vector2 boardBefore = ScreenToBoard(screenPos);
        Zoom = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Vector2 boardAfter = ScreenToBoard(screenPos);
        PanBoard += boardBefore - boardAfter;
        ClampPan();
    }

    /// <summary>
    /// Applies a screen-space pan delta (e.g. <see cref="InputEventMouseMotion.Relative"/>)
    /// such that screen-pixel velocity stays constant across zooms. Locked at MinZoom.
    /// </summary>
    public void ApplyPanFromScreenDelta(Vector2 screenDelta)
    {
        if (IsAtMinZoom)
            return;
        PanBoard -= screenDelta / Zoom;
        ClampPan();
    }

    private void RecomputeMinZoom()
    {
        if (ContentBBox.Size.X <= 0f || ContentBBox.Size.Y <= 0f)
        {
            MinZoom = 0.1f;
            return;
        }
        float fx = ViewportSize.X / ContentBBox.Size.X;
        float fy = ViewportSize.Y / ContentBBox.Size.Y;
        MinZoom = Mathf.Min(fx, fy) * FitMargin;
        if (MinZoom > MaxZoom)
            MinZoom = MaxZoom;
    }

    private void ClampPan()
    {
        if (IsAtMinZoom)
        {
            PanBoard = ContentBBox.GetCenter();
            return;
        }

        // Half-viewport in board-space — keep the bbox center within reach so
        // scrolling never strands the camera with no content visible.
        Vector2 halfView = ViewportSize / (2f * Zoom);
        Vector2 min = ContentBBox.Position - halfView * 0.5f;
        Vector2 max = ContentBBox.End + halfView * 0.5f;
        PanBoard = new Vector2(
            Mathf.Clamp(PanBoard.X, min.X, max.X),
            Mathf.Clamp(PanBoard.Y, min.Y, max.Y)
        );
    }
}
