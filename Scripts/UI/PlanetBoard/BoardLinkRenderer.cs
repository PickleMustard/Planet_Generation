using System.Collections.Generic;
using Godot;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// A <see cref="Node2D"/> child of <see cref="BoardWorld"/> responsible for
/// drawing all <see cref="ResourceLink"/> lines between building ports, plus
/// the drag-preview dashed line when creating a new link. Since this node
/// sits at <c>Position = Vector2.Zero</c> inside the BoardWorld, world-space
/// coordinates equal local coordinates.
/// </summary>
public partial class BoardLinkRenderer : Node2D
{
    private const float PortStubBoard = 8f;
    private const float BundleRadius = 5f;
    private const float StuckClusterStart = 14f;
    private const float StuckClusterSpacing = 12f;
    private static readonly Color BundleColor = new(0.45f, 0.75f, 1.00f);
    private static readonly Color BundleOutline = new(0.10f, 0.20f, 0.35f);
    private static readonly Color StuckOutline = new(0.95f, 0.30f, 0.25f);

    // ── Link storage ───────────────────────────────────────────────────

    private readonly List<(ResourceLink Link, BuildingNode2D.PortData Source, BuildingNode2D.PortData Target)> _links = new();

    // ── Highlight state ────────────────────────────────────────────────

    private ResourceLink? _hoveredLink;

    // ── Drag preview state ─────────────────────────────────────────────

    private Vector2? _dragPreviewFrom;
    private Vector2? _dragPreviewTo;
    private bool _dragPreviewValid;

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the link list from the current set of buildings. Iterates
    /// every building's ports; for each port with a non-null
    /// <see cref="ResourceNode.Link"/>, finds the matching port on another
    /// building and stores the tuple. Avoids duplicate pairs via a
    /// <see cref="HashSet{T}"/>.
    /// </summary>
    public void RebuildLinks(IReadOnlyList<BuildingNode2D> buildings)
    {
        _links.Clear();
        var seen = new HashSet<ResourceLink>();

        foreach (var bn in buildings)
        {
            foreach (var port in bn.Ports)
            {
                var link = port.Node.Link;
                if (link == null || !seen.Add(link))
                    continue;

                var source = FindPort(buildings, link.Source);
                var target = FindPort(buildings, link.Target);
                if (source == null || target == null)
                    continue;

                _links.Add((link, source, target));
            }
        }

        QueueRedraw();
    }

    /// <summary>Sets the hovered link for yellow highlighting and redraws.</summary>
    public void SetHoveredLink(ResourceLink? link)
    {
        _hoveredLink = link;
        QueueRedraw();
    }

    /// <summary>Sets the drag preview line endpoints and validity, then redraws.</summary>
    public void SetDragPreview(Vector2? fromWorld, Vector2? toWorld, bool valid)
    {
        _dragPreviewFrom = fromWorld;
        _dragPreviewTo = toWorld;
        _dragPreviewValid = valid;
        QueueRedraw();
    }

    /// <summary>
    /// Hit-tests all link edges against <paramref name="worldPos"/> and returns
    /// the closest one within <paramref name="hitRadius"/>, or null.
    /// </summary>
    public (ResourceLink Link, BuildingNode2D.PortData Source, BuildingNode2D.PortData Target)? HitTestEdge(
        Vector2 worldPos, float hitRadius = 6f)
    {
        (ResourceLink, BuildingNode2D.PortData, BuildingNode2D.PortData)? best = null;
        float bestDist = hitRadius;

        foreach (var (link, src, tgt) in _links)
        {
            Vector2 a = PortWorldPosition(src);
            Vector2 b = PortWorldPosition(tgt);
            float d = DistanceToSegment(worldPos, a, b);
            if (d <= bestDist)
            {
                bestDist = d;
                best = (link, src, tgt);
            }
        }

        return best;
    }

    /// <summary>
    /// Hit-tests all bundle dots (in-flight + stuck) against <paramref name="worldPos"/>
    /// and returns the closest within <paramref name="hitRadius"/>, or null.
    /// </summary>
    public (ResourceLink Link, ResourcePackage Package)? HitTestBundle(
        Vector2 worldPos, float hitRadius = 6f)
    {
        (ResourceLink, ResourcePackage)? best = null;
        float bestDist = hitRadius;

        foreach (var (link, src, tgt) in _links)
        {
            Vector2 a = PortWorldPosition(src);
            Vector2 b = PortWorldPosition(tgt);

            foreach (var pkg in link.InFlight)
            {
                Vector2 pos = a.Lerp(b, Mathf.Clamp(pkg.Progress, 0f, 1f));
                float d = worldPos.DistanceTo(pos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = (link, pkg);
                }
            }

            for (int i = 0; i < link.ArrivalBuffer.Count; i++)
            {
                Vector2 pos = StuckClusterPos(a, b, i);
                float d = worldPos.DistanceTo(pos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = (link, link.ArrivalBuffer[i]);
                }
            }
        }

        return best;
    }

    // ── Drawing ─────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        foreach (var (link, _, _) in _links)
        {
            if (link.InFlight.Count > 0 || link.ArrivalBuffer.Count > 0)
            {
                QueueRedraw();
                return;
            }
        }
    }

    public override void _Draw()
    {
        DrawLinks();
        DrawBundles();
        DrawDragPreview();
    }

    private void DrawBundles()
    {
        foreach (var (link, src, tgt) in _links)
        {
            Vector2 a = PortWorldPosition(src);
            Vector2 b = PortWorldPosition(tgt);

            foreach (var pkg in link.InFlight)
            {
                Vector2 pos = a.Lerp(b, Mathf.Clamp(pkg.Progress, 0f, 1f));
                DrawCircle(pos, BundleRadius, BundleColor);
                DrawArc(pos, BundleRadius, 0f, Mathf.Tau, 16,
                    pkg.Stuck ? StuckOutline : BundleOutline, 1.2f, true);
            }

            for (int i = 0; i < link.ArrivalBuffer.Count; i++)
            {
                Vector2 pos = StuckClusterPos(a, b, i);
                DrawCircle(pos, BundleRadius, BundleColor);
                DrawArc(pos, BundleRadius, 0f, Mathf.Tau, 16, StuckOutline, 1.5f, true);
            }
        }
    }

    private void DrawLinks()
    {
        float width = Mathf.Max(2f, 1.5f);

        foreach (var (link, src, tgt) in _links)
        {
            Vector2 a = PortWorldPosition(src);
            Vector2 b = PortWorldPosition(tgt);

            Color c = link == _hoveredLink
                ? new Color(0.95f, 0.85f, 0.20f)
                : new Color(0.30f, 0.85f, 0.40f);

            DrawLine(a, b, c, width, antialiased: true);
        }
    }

    private void DrawDragPreview()
    {
        if (_dragPreviewFrom == null || _dragPreviewTo == null)
            return;

        Color c = _dragPreviewValid
            ? new Color(0.30f, 0.85f, 0.40f)
            : new Color(0.85f, 0.30f, 0.30f);

        DrawDashedLine(_dragPreviewFrom.Value, _dragPreviewTo.Value, c, 2f, 8f);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Computes the world-space position of a port, accounting for the
    /// building node's position and the port's local anchor + outward normal.
    /// </summary>
    private static Vector2 PortWorldPosition(BuildingNode2D.PortData port)
    {
        return port.Owner.Position + port.LocalAnchor + port.OutwardNormal * PortStubBoard;
    }

    /// <summary>
    /// Computes the cluster position for the i-th stuck package near the target port.
    /// Packs 3-per-row, stacking inward along the link tangent away from the target.
    /// </summary>
    private static Vector2 StuckClusterPos(Vector2 a, Vector2 b, int index)
    {
        Vector2 ab = b - a;
        float len = ab.Length();
        if (len < 1e-4f)
            return b;
        Vector2 inward = -ab / len;
        Vector2 perp = new(-inward.Y, inward.X);
        int row = index / 3;
        int col = (index % 3) - 1;
        return b + inward * (StuckClusterStart + row * StuckClusterSpacing)
                 + perp * (col * StuckClusterSpacing);
    }

    /// <summary>
    /// Finds the <see cref="BuildingNode2D.PortData"/> that wraps the given
    /// <see cref="ResourceNode"/> by iterating all buildings' ports.
    /// </summary>
    private static BuildingNode2D.PortData? FindPort(IReadOnlyList<BuildingNode2D> buildings, ResourceNode? node)
    {
        if (node == null)
            return null;

        foreach (var bn in buildings)
            foreach (var p in bn.Ports)
                if (p.Node == node)
                    return p;

        return null;
    }

    /// <summary>
    /// Projects point <paramref name="p"/> onto segment <c>ab</c>,
    /// clamps t ∈ [0,1], and returns the distance from <paramref name="p"/>
    /// to the projected point.
    /// </summary>
    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.LengthSquared();
        if (lengthSq < 1e-4f)
            return p.DistanceTo(a);
        float t = Mathf.Clamp((p - a).Dot(ab) / lengthSq, 0f, 1f);
        Vector2 proj = a + ab * t;
        return p.DistanceTo(proj);
    }
}
