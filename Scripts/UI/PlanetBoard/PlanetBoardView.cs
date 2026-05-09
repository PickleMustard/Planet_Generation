using System.Collections.Generic;
using Constructables;
using Constructables.Buildings;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using UI.PlanetBoard.Modes;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// Custom 2D Control that renders all buildings of a planet as polygons with
/// edge-anchored ports, draws active <see cref="ResourceLink"/>s between them,
/// and dispatches input to the active <see cref="IPlanetBoardMode"/>.
///
/// Coordinate handling lives in <see cref="BoardCamera"/>; geometry per-shape
/// in <see cref="BuildingShapeGeometry"/>; layout in <see cref="BoardLayoutEngine"/>.
/// </summary>
public partial class PlanetBoardView : Control
{
    public sealed class PortView
    {
        public ResourceNode Node = null!;
        public Vector2 BoardAnchor;
        public Vector2 OutwardNormal;
    }

    public sealed class BuildingNodeView
    {
        public Building Building = null!;
        public Vector2 BoardPos;
        public string Shape = "hexagon";
        public float Radius = 64f;
        public Color FillColor;
        public Texture2D? Icon;
        public string DisplayName = "";
        public List<PortView> Ports = new();
    }

    private const float PortHitRadiusScreen = 12f;
    private const float EdgeHitRadiusScreen = 6f;
    private const float PortVisualRadius = 6f;
    private const float PortStubBoard = 8f;
    private const float ZoomStep = 1.1f;

    public BoardCamera Camera { get; } = new();

    private CelestialBody? _body;
    private IPlanetBoardMode? _mode;
    private readonly List<BuildingNodeView> _buildings = new();
    private readonly List<(ResourceLink Link, PortView Source, PortView Target)> _links = new();

    private PortView? _hoveredPort;
    private (ResourceLink Link, PortView Source, PortView Target)? _hoveredEdge;
    private bool _panning;
    private Vector2 _lastMousePos;

    private PortView? _dragSource;
    private Vector2 _dragCursorScreen;
    private bool _dragValid;

    public CelestialBody? Body => _body;
    public IPlanetBoardMode? ActiveMode => _mode;
    public IReadOnlyList<BuildingNodeView> BuildingViews => _buildings;
    public IReadOnlyList<(ResourceLink Link, PortView Source, PortView Target)> LinkViews => _links;
    public PortView? DragSource => _dragSource;
    public Vector2 DragCursorScreen => _dragCursorScreen;
    public bool DragValid => _dragValid;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += () =>
        {
            Camera.SetViewportSize(Size);
            QueueRedraw();
        };
        Camera.SetViewportSize(Size);

        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.BuildingConstructed += OnBuildingsChanged;
            bus.BuildingRemoved += OnBuildingsChanged;
            bus.ResourceLinkChanged += OnLinksChanged;
        }
    }

    public override void _ExitTree()
    {
        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.BuildingConstructed -= OnBuildingsChanged;
            bus.BuildingRemoved -= OnBuildingsChanged;
            bus.ResourceLinkChanged -= OnLinksChanged;
        }
    }

    private void OnBuildingsChanged(int _) => RefreshFromBody();
    private void OnLinksChanged() => RefreshLinks();

    public void SetBody(CelestialBody? body)
    {
        _body = body;
        RefreshFromBody();
    }

    public void SetMode(IPlanetBoardMode? mode)
    {
        _mode?.OnExit();
        _mode = mode;
        if (_mode != null && _body != null)
            _mode.OnEnter(this, _body);
        QueueRedraw();
    }

    public void RefreshFromBody()
    {
        _buildings.Clear();
        _links.Clear();
        _hoveredPort = null;
        _hoveredEdge = null;

        if (_body == null)
        {
            QueueRedraw();
            return;
        }

        var manager = _body.BuildingConstructionMgr;
        if (manager == null)
        {
            QueueRedraw();
            return;
        }

        var matched = new List<Building>(manager.GetActiveBuildings());
        var layout = BoardLayoutEngine.Compute(matched);
        foreach (var b in matched)
        {
            if (!layout.Positions.TryGetValue(b, out var pos))
                continue;
            _buildings.Add(BuildView(b, pos));
        }

        Camera.SetContentBBox(layout.BoundingBox);
        Camera.FitToContent();

        RebuildLinkViews();
        QueueRedraw();
    }

    public void RefreshLinks()
    {
        RebuildLinkViews();
        QueueRedraw();
    }

    private void RebuildLinkViews()
    {
        _links.Clear();
        var seen = new HashSet<ResourceLink>();
        foreach (var bv in _buildings)
        {
            foreach (var port in bv.Ports)
            {
                var link = port.Node.Link;
                if (link == null || !seen.Add(link))
                    continue;
                var source = FindPort(link.Source);
                var target = FindPort(link.Target);
                if (source == null || target == null)
                    continue;
                _links.Add((link, source, target));
            }
        }
    }

    private PortView? FindPort(ResourceNode? node)
    {
        if (node == null)
            return null;
        foreach (var bv in _buildings)
            foreach (var p in bv.Ports)
                if (p.Node == node)
                    return p;
        return null;
    }

    private static BuildingNodeView BuildView(Building b, Vector2 boardPos)
    {
        var def = b.Definition;
        var visual = def?.Visual;
        string shape = BuildingShapeGeometry.NormalizeShape(visual?.Shape);
        float radius = visual?.ShapeSize > 0 ? visual.ShapeSize : 64f;
        Color fill = visual?.ShapeColor ?? new Color(0.30f, 0.45f, 0.60f);
        var view = new BuildingNodeView
        {
            Building = b,
            BoardPos = boardPos,
            Shape = shape,
            Radius = radius,
            FillColor = fill,
            Icon = def?.Icon?.GetTexture(IconSize.Medium),
            DisplayName = b.Name,
        };

        foreach (var node in b.Nodes)
        {
            var anchor = BuildingShapeGeometry.GetPortAnchor(shape, node.Side, boardPos, radius);
            var normal = BuildingShapeGeometry.GetPortNormal(shape, node.Side, boardPos, radius);
            view.Ports.Add(new PortView
            {
                Node = node,
                BoardAnchor = anchor,
                OutwardNormal = normal,
            });
        }
        return view;
    }

    public PortView? HitTestPort(Vector2 screenPos)
    {
        float bestDist = PortHitRadiusScreen;
        PortView? best = null;
        foreach (var bv in _buildings)
        {
            foreach (var p in bv.Ports)
            {
                Vector2 portScreen = Camera.BoardToScreen(p.BoardAnchor + p.OutwardNormal * PortStubBoard);
                float d = portScreen.DistanceTo(screenPos);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
        }
        return best;
    }

    public (ResourceLink Link, PortView Source, PortView Target)? HitTestEdge(Vector2 screenPos)
    {
        float bestDist = EdgeHitRadiusScreen;
        (ResourceLink, PortView, PortView)? best = null;
        foreach (var (link, src, tgt) in _links)
        {
            Vector2 a = Camera.BoardToScreen(src.BoardAnchor);
            Vector2 b = Camera.BoardToScreen(tgt.BoardAnchor);
            float d = DistanceToSegment(screenPos, a, b);
            if (d <= bestDist)
            {
                bestDist = d;
                best = (link, src, tgt);
            }
        }
        return best;
    }

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

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb:
                HandleMouseButton(mb);
                break;
            case InputEventMouseMotion mm:
                HandleMouseMotion(mm);
                break;
        }
    }

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
        {
            Camera.ZoomAt(mb.Position, ZoomStep);
            QueueRedraw();
            AcceptEvent();
            return;
        }
        if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
        {
            Camera.ZoomAt(mb.Position, 1f / ZoomStep);
            QueueRedraw();
            AcceptEvent();
            return;
        }
        if (mb.ButtonIndex == MouseButton.Middle)
        {
            _panning = mb.Pressed;
            _lastMousePos = mb.Position;
            AcceptEvent();
            return;
        }

        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                var hit = HitTestPort(mb.Position);
                if (hit != null)
                {
                    if (_mode != null && _mode.OnPortDragStart(hit.Node))
                    {
                        _dragSource = hit;
                        _dragCursorScreen = mb.Position;
                        _dragValid = false;
                        QueueRedraw();
                        AcceptEvent();
                        return;
                    }
                    _mode?.OnPortClick(hit.Node, mb.ButtonIndex);
                    AcceptEvent();
                    return;
                }
                var edge = HitTestEdge(mb.Position);
                if (edge.HasValue)
                {
                    _mode?.OnEdgeClick(edge.Value.Link, mb.ButtonIndex);
                    AcceptEvent();
                    return;
                }
                _mode?.OnEmptyClick(Camera.ScreenToBoard(mb.Position), mb.ButtonIndex);
            }
            else
            {
                if (_dragSource != null)
                {
                    var drop = HitTestPort(mb.Position);
                    _mode?.OnPortDragEnd(drop?.Node);
                    _dragSource = null;
                    _dragValid = false;
                    QueueRedraw();
                }
            }
            AcceptEvent();
            return;
        }

        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            if (_dragSource != null)
            {
                _mode?.OnPortDragEnd(null);
                _dragSource = null;
                _dragValid = false;
                QueueRedraw();
                AcceptEvent();
                return;
            }
            var edge = HitTestEdge(mb.Position);
            if (edge.HasValue && _mode != null && _mode.OnEdgeClick(edge.Value.Link, mb.ButtonIndex))
            {
                AcceptEvent();
                return;
            }
            var port = HitTestPort(mb.Position);
            if (port != null && _mode != null && _mode.OnPortClick(port.Node, mb.ButtonIndex))
            {
                AcceptEvent();
                return;
            }
            _mode?.OnEmptyClick(Camera.ScreenToBoard(mb.Position), mb.ButtonIndex);
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mm)
    {
        if (_panning)
        {
            Camera.ApplyPanFromScreenDelta(mm.Relative);
            _lastMousePos = mm.Position;
            QueueRedraw();
            AcceptEvent();
            return;
        }

        var port = HitTestPort(mm.Position);
        var edge = port == null ? HitTestEdge(mm.Position) : null;
        bool changed = !ReferenceEquals(port, _hoveredPort)
            || (_hoveredEdge?.Link != edge?.Link);
        _hoveredPort = port;
        _hoveredEdge = edge;

        if (_dragSource != null)
        {
            _dragCursorScreen = mm.Position;
            _dragValid = port != null && port != _dragSource && ResourceLink.CanConnect(_dragSource.Node, port.Node);
            _mode?.OnPortDragUpdate(Camera.ScreenToBoard(mm.Position), port?.Node);
            QueueRedraw();
            return;
        }

        if (changed)
            QueueRedraw();
    }

    public override void _Draw()
    {
        DrawBackground();
        DrawLinks();
        DrawBuildings();
        DrawDragPreview();
        if (_mode != null)
            _mode.DrawOverlay(this, Camera);
    }

    private void DrawBackground()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.08f, 0.10f, 0.12f, 1f));
        DrawGrid();
    }

    private void DrawGrid()
    {
        // Light grid — purely cosmetic, gives a sense of motion when panning.
        var color = new Color(1f, 1f, 1f, 0.05f);
        float step = 100f * Camera.Zoom;
        if (step < 16f)
            return;
        Vector2 origin = Camera.BoardToScreen(Vector2.Zero);
        for (float x = origin.X % step; x < Size.X; x += step)
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), color);
        for (float y = origin.Y % step; y < Size.Y; y += step)
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), color);
    }

    private void DrawLinks()
    {
        float w = Mathf.Max(2f, 1.5f * Camera.Zoom);
        foreach (var (link, src, tgt) in _links)
        {
            Vector2 a = Camera.BoardToScreen(src.BoardAnchor);
            Vector2 b = Camera.BoardToScreen(tgt.BoardAnchor);
            Color c = _hoveredEdge?.Link == link
                ? new Color(0.95f, 0.85f, 0.20f)
                : new Color(0.30f, 0.85f, 0.40f);
            DrawLine(a, b, c, w, antialiased: true);
        }
    }

    private void DrawBuildings()
    {
        foreach (var bv in _buildings)
        {
            DrawBuilding(bv);
        }
    }

    private void DrawBuilding(BuildingNodeView bv)
    {
        var verts = BuildingShapeGeometry.GetVertices(bv.Shape, bv.BoardPos, bv.Radius);
        var screenVerts = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            screenVerts[i] = Camera.BoardToScreen(verts[i]);
        DrawColoredPolygon(screenVerts, bv.FillColor);
        DrawPolyline(LoopClosed(screenVerts), new Color(0, 0, 0, 0.85f), Mathf.Max(1.5f, Camera.Zoom));

        // Icon + label inside
        Vector2 centerScreen = Camera.BoardToScreen(bv.BoardPos);
        if (bv.Icon != null)
        {
            float iconSize = Mathf.Min(bv.Radius * Camera.Zoom * 0.9f, 64f);
            var iconRect = new Rect2(centerScreen - new Vector2(iconSize / 2f, iconSize / 2f + 8f), new Vector2(iconSize, iconSize));
            DrawTextureRect(bv.Icon, iconRect, false);
        }
        var font = ThemeDB.FallbackFont;
        if (font != null)
        {
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(12f * Camera.Zoom), 8, 20);
            string text = bv.DisplayName ?? "";
            Vector2 textSize = font.GetStringSize(text, HorizontalAlignment.Center, -1, fontSize);
            DrawString(font, centerScreen + new Vector2(-textSize.X / 2f, bv.Radius * Camera.Zoom * 0.4f),
                text, HorizontalAlignment.Center, -1, fontSize, new Color(1, 1, 1, 0.9f));
        }

        foreach (var p in bv.Ports)
            DrawPort(p);
    }

    private void DrawPort(PortView p)
    {
        Vector2 screen = Camera.BoardToScreen(p.BoardAnchor + p.OutwardNormal * PortStubBoard);
        Color c = p.Node.Kind switch
        {
            ResourceNodeKind.Import => new Color(0.30f, 0.55f, 0.95f),
            ResourceNodeKind.Export => new Color(0.95f, 0.55f, 0.20f),
            _ => new Color(0.75f, 0.75f, 0.75f),
        };
        DrawCircle(screen, PortVisualRadius, c);
        DrawArc(screen, PortVisualRadius, 0, Mathf.Tau, 24, new Color(0, 0, 0, 0.85f), 1.2f);

        if (p.Node.Link != null)
            DrawArc(screen, PortVisualRadius + 3f, 0, Mathf.Tau, 24, new Color(0.30f, 0.85f, 0.40f), 1.5f);

        if (ReferenceEquals(p, _hoveredPort) || ReferenceEquals(p, _dragSource))
            DrawArc(screen, PortVisualRadius + 6f, 0, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.9f), 1.5f);
    }

    private void DrawDragPreview()
    {
        if (_dragSource == null)
            return;
        Vector2 a = Camera.BoardToScreen(_dragSource.BoardAnchor + _dragSource.OutwardNormal * PortStubBoard);
        Vector2 b = _dragCursorScreen;
        Color c = _dragValid ? new Color(0.30f, 0.85f, 0.40f) : new Color(0.85f, 0.30f, 0.30f);
        DrawDashedLine(a, b, c, 2f, 8f);
    }

    private static Vector2[] LoopClosed(Vector2[] verts)
    {
        var loop = new Vector2[verts.Length + 1];
        for (int i = 0; i < verts.Length; i++)
            loop[i] = verts[i];
        loop[^1] = verts[0];
        return loop;
    }
}
