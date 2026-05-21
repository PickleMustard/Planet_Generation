using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// A <see cref="Node2D"/> that draws a single building's polygon, icon, label,
/// and port dots in local coordinates. Supports free drag so buildings can be
/// repositioned by the user within the board's circle boundary.
/// </summary>
public partial class BuildingNode2D : Node2D
{
    private const float PortStubBoard = 8f;
    private const float PortVisualRadius = 6f;
    public const string DefaultShapeId = "hexagon";

    // ── Cached visual data (set by Setup) ──────────────────────────────

    private string _shapeId = DefaultShapeId;
    private BuildingShape2D? _shape;
    private float _radius = 64f;
    private Color _fillColor = new(0.30f, 0.45f, 0.60f);
    private Texture2D? _icon;
    private string _displayName = "";
    private bool _isDragged;
    private PortData? _hoveredPort;
    private PortData? _dragSourcePort;

    // ── Public properties ──────────────────────────────────────────────

    /// <summary>The data-model reference.</summary>
    public Building Building { get; private set; } = null!;

    /// <summary>Cached shape id.</summary>
    public string ShapeId
    {
        get => _shapeId;
        private set => _shapeId = value;
    }

    /// <summary>Cached resolved shape (may be null if id unknown).</summary>
    public BuildingShape2D? Shape => _shape;

    /// <summary>Cached circumradius in pixels.</summary>
    public float Radius
    {
        get => _radius;
        private set => _radius = value;
    }

    /// <summary>Cached fill color.</summary>
    public Color FillColor
    {
        get => _fillColor;
        private set => _fillColor = value;
    }

    /// <summary>Cached icon texture (may be null).</summary>
    public Texture2D? Icon
    {
        get => _icon;
        private set => _icon = value;
    }

    /// <summary>Cached display name.</summary>
    public string DisplayName
    {
        get => _displayName;
        private set => _displayName = value;
    }

    /// <summary>Port data for each <see cref="ResourceNode"/> on this building.</summary>
    public IReadOnlyList<PortData> Ports { get; private set; } = new List<PortData>();

    /// <summary>Whether the building is currently being dragged.</summary>
    public bool IsDragged
    {
        get => _isDragged;
        set
        {
            _isDragged = value;
            QueueRedraw();
        }
    }

    /// <summary>The port currently being hovered (for highlight rendering).</summary>
    public PortData? HoveredPort
    {
        get => _hoveredPort;
        set
        {
            if (ReferenceEquals(_hoveredPort, value))
                return;
            _hoveredPort = value;
            QueueRedraw();
        }
    }

    /// <summary>The port being used as a drag source (for link creation highlight).</summary>
    public PortData? DragSourcePort
    {
        get => _dragSourcePort;
        set
        {
            if (ReferenceEquals(_dragSourcePort, value))
                return;
            _dragSourcePort = value;
            QueueRedraw();
        }
    }

    // ── Setup ──────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes this node from the given building and its board-space
    /// position. Must be called once after creation, before the node is added to
    /// the scene tree.
    /// </summary>
    public void Setup(Building building, Vector2 boardPosition)
    {
        Building = building;
        Position = boardPosition;

        var def = building.Definition;
        var visual = def?.Visual;

        ShapeId = string.IsNullOrEmpty(visual?.ShapeId) ? DefaultShapeId : visual!.ShapeId;
        _shape = BuildingShape2DDatabase.Instance.Get(ShapeId);
        if (_shape == null)
        {
            GameLogger.Warning(
                $"BuildingNode2D: shape '{ShapeId}' not found in BuildingShape2DDatabase " +
                $"for building '{building.Name}'; rendering will be empty"
            );
        }
        Radius = visual?.ShapeSize > 0 ? visual.ShapeSize : 64f;
        FillColor = visual?.ShapeColor ?? new Color(0.30f, 0.45f, 0.60f);
        Icon = def?.Icon?.GetTexture(IconSize.Medium);
        DisplayName = building.Name;

        // Build port data from building nodes
        var ports = new List<PortData>();
        if (_shape != null)
        {
            foreach (var node in building.Nodes)
            {
                if (node.SideIndex < 0 || node.SideIndex >= _shape.SideCount)
                {
                    GameLogger.Warning(
                        $"BuildingNode2D: node SideIndex {node.SideIndex} out of range " +
                        $"for shape '{ShapeId}' ({_shape.SideCount} sides); skipping"
                    );
                    continue;
                }
                var port = new PortData
                {
                    Node = node,
                    LocalAnchor = _shape.GetSlotAnchor(node.SideIndex, node.SlotIndex, Vector2.Zero, Radius),
                    OutwardNormal = _shape.GetSlotNormal(node.SideIndex, node.SlotIndex, Vector2.Zero, Radius),
                    Owner = this,
                };
                ports.Add(port);
            }
        }
        Ports = ports;
    }

    // ── Drag support ───────────────────────────────────────────────────

    /// <summary>Marks the building as being dragged and triggers a redraw.</summary>
    public void StartDrag()
    {
        IsDragged = true;
    }

    /// <summary>Moves the building to the given board-space position.</summary>
    public void DragTo(Vector2 boardPosition)
    {
        Position = boardPosition;
    }

    /// <summary>Marks the drag as finished and triggers a redraw.</summary>
    public void EndDrag()
    {
        IsDragged = false;
    }

    // ── Drawing ────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_shape == null)
            return;

        // All coordinates in local space — center is Vector2.Zero.
        Vector2 center = Vector2.Zero;

        // ── Polygon fill ──
        var verts = _shape.GetScaledVertices(center, Radius);
        DrawColoredPolygon(verts, FillColor);

        // ── Outline ──
        DrawPolyline(LoopClosed(verts), new Color(0, 0, 0, 0.85f), Mathf.Max(1.5f, 1.0f));

        // ── Icon ──
        if (Icon != null)
        {
            float iconSize = Mathf.Min(Radius * 0.9f, 64f);
            var iconRect = new Rect2(
                center - new Vector2(iconSize / 2f, iconSize / 2f + 8f),
                new Vector2(iconSize, iconSize)
            );
            DrawTextureRect(Icon, iconRect, false);
        }

        // ── Label ──
        var font = ThemeDB.FallbackFont;
        if (font != null)
        {
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(12f), 8, 20);
            string text = DisplayName ?? "";
            Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Center, -1, fontSize);
            DrawString(
                font,
                center + new Vector2(-textSize.X / 2f, Radius * 0.4f),
                text,
                HorizontalAlignment.Center,
                -1,
                fontSize,
                new Color(1, 1, 1, 0.9f)
            );
        }

        // ── Port dots ──
        foreach (var p in Ports)
            DrawPort(p);
    }

    private void DrawPort(PortData p)
    {
        Vector2 pos = p.LocalAnchor + p.OutwardNormal * PortStubBoard;

        // Fill color by kind
        Color c = p.Node.Kind switch
        {
            ResourceNodeKind.Import => new Color(0.30f, 0.55f, 0.95f),
            ResourceNodeKind.Export => new Color(0.95f, 0.55f, 0.20f),
            _ => new Color(0.75f, 0.75f, 0.75f),
        };
        DrawCircle(pos, PortVisualRadius, c);

        // Black outline
        DrawArc(pos, PortVisualRadius, 0, Mathf.Tau, 24, new Color(0, 0, 0, 0.85f), 1.2f);

        // Linked ring (green)
        if (p.Node.Link != null)
            DrawArc(pos, PortVisualRadius + 3f, 0, Mathf.Tau, 24, new Color(0.30f, 0.85f, 0.40f), 1.5f);

        // Hovered / drag-source highlight ring (white)
        if (ReferenceEquals(p, HoveredPort) || ReferenceEquals(p, DragSourcePort))
            DrawArc(pos, PortVisualRadius + 6f, 0, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.9f), 1.5f);
    }

    // ── Hit testing ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the closest <see cref="PortData"/> within <paramref name="hitRadius"/>
    /// of <paramref name="localMousePos"/>, or null if none is close enough.
    /// </summary>
    public PortData? HitTestPort(Vector2 localMousePos, float hitRadius = 12f)
    {
        PortData? best = null;
        float bestDist = hitRadius;

        foreach (var p in Ports)
        {
            Vector2 portDrawn = p.LocalAnchor + p.OutwardNormal * PortStubBoard;
            float d = localMousePos.DistanceTo(portDrawn);
            if (d <= bestDist)
            {
                bestDist = d;
                best = p;
            }
        }

        return best;
    }

    /// <summary>
    /// Point-in-polygon test against the shape vertices using the ray-casting
    /// algorithm. Returns true for points inside the polygon, false otherwise.
    /// </summary>
    public bool HitTestShape(Vector2 localMousePos)
    {
        if (_shape == null)
            return false;
        var verts = _shape.GetScaledVertices(Vector2.Zero, Radius);
        return PointInPolygon(localMousePos, verts);
    }

    // ── Nested types ───────────────────────────────────────────────────

    /// <summary>
    /// Holds the per-port data needed for rendering and hit-testing a single
    /// <see cref="ResourceNode"/> on a <see cref="BuildingNode2D"/>.
    /// </summary>
    public sealed class PortData
    {
        /// <summary>The data-model resource node.</summary>
        public ResourceNode Node = null!;

        /// <summary>Position of this slot on its polygon edge, in local coordinates.</summary>
        public Vector2 LocalAnchor;

        /// <summary>Unit normal pointing outward from the polygon center through the port.</summary>
        public Vector2 OutwardNormal;

        /// <summary>The <see cref="BuildingNode2D"/> that owns this port.</summary>
        public BuildingNode2D Owner = null!;
    }

    // ── Private helpers ────────────────────────────────────────────────

    /// <summary>
    /// Ray-casting point-in-polygon test. Casts a horizontal ray from the test
    /// point and counts edge crossings — odd count means the point is inside.
    /// </summary>
    private static bool PointInPolygon(Vector2 point, Vector2[] verts)
    {
        bool inside = false;
        int n = verts.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 vi = verts[i];
            Vector2 vj = verts[j];
            // Check if the edge (vj → vi) crosses the horizontal ray from point to the right.
            if ((vi.Y > point.Y) != (vj.Y > point.Y)
                && point.X < (vj.X - vi.X) * (point.Y - vi.Y) / (vj.Y - vi.Y) + vi.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Returns a copy of the vertex array with the first vertex appended to close the loop.</summary>
    private static Vector2[] LoopClosed(Vector2[] verts)
    {
        var loop = new Vector2[verts.Length + 1];
        for (int i = 0; i < verts.Length; i++)
            loop[i] = verts[i];
        loop[^1] = verts[0];
        return loop;
    }
}
