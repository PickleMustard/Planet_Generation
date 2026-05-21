using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using ProceduralGeneration;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UI.PlanetBoard.Modes;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// Root <see cref="Node2D"/> that sits inside the SubViewport and serves as
/// the parent of all <see cref="BuildingNode2D"/> instances and the
/// <see cref="BoardLinkRenderer"/>. Listens to <see cref="SignalBus"/> signals
/// to create/destroy building nodes, coordinates input dispatch to the active
/// <see cref="IPlanetBoardMode"/>, and handles building drag and port
/// interaction. Supports incremental add/remove so existing building
/// positions are preserved when new buildings arrive.
/// </summary>
public partial class BoardWorld : Node2D
{
    // ── Child node references (set via Export from .tscn) ───────────────

    [Export]
    private BoardLinkRenderer _linkRenderer = null!;

    // ── Camera reference (set by PlanetBoardView after scene setup) ─────

    private BoardCameraController? _cameraController;

    /// <summary>
    /// The <see cref="BoardCameraController"/> that controls the viewport camera.
    /// Set by <see cref="PlanetBoardView"/> after the scene hierarchy is built.
    /// Subscribes to <see cref="BoardCameraController.ViewChanged"/> so the
    /// dot-grid background redraws when the camera pans or zooms.
    /// </summary>
    public BoardCameraController? CameraController
    {
        get => _cameraController;
        set
        {
            if (ReferenceEquals(_cameraController, value))
                return;
            if (_cameraController != null)
                _cameraController.ViewChanged -= OnCameraViewChanged;
            _cameraController = value;
            if (_cameraController != null)
                _cameraController.ViewChanged += OnCameraViewChanged;
            QueueRedraw();
        }
    }

    private void OnCameraViewChanged() => QueueRedraw();

    // ── Private state ───────────────────────────────────────────────────

    private IOrbitalBody? _body;
    private IPlanetBoardMode? _mode;

    private readonly Dictionary<Building, BuildingNode2D> _buildingNodes = new();
    private readonly Dictionary<StationSatellite, StationNode2D> _stationNodes = new();
    private float _circleRadius;
    private float _shapeRadius = 64f;
    private float _stationRingRadius;

    // ── Draw constants ──────────────────────────────────────────────────

    private static readonly Color CircleOutlineColor = new(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.55f);
    private const float CircleOutlineWidth = 1.5f;
    private const int CircleOutlineSegments = 96;

    private const float DotGridSpacing = 128f;
    private const float DotGridDotRadius = 1.5f;
    private static readonly Color DotGridColor = new(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.15f);

    // Building drag state
    private BuildingNode2D? _draggedBuilding;
    private Vector2 _dragOffset;
    private Vector2 _buildingPressScreenPos;
    private const float ClickMaxScreenDistance = 4f;

    // ── Selection events ────────────────────────────────────────────────

    /// <summary>Raised when a building is clicked without being dragged.</summary>
    public event System.Action<Building>? BuildingClicked;

    /// <summary>Raised when a link edge is clicked.</summary>
    public event System.Action<ResourceLink>? LinkClicked;

    /// <summary>Raised when a bundle dot on a link is clicked.</summary>
    public event System.Action<ResourceLink, ResourcePackage>? BundleClicked;

    /// <summary>Raised when an empty area of the board is clicked.</summary>
    public event System.Action? SelectionCleared;

    // Port drag state (link creation)
    private BuildingNode2D.PortData? _dragSourcePort;
    private Vector2 _dragCursorWorld;
    private bool _dragValid;

    // Hover state
    private BuildingNode2D.PortData? _hoveredPort;
    private ResourceLink? _hoveredLink;

    // ── Public accessors (for mode use) ──────────────────────────────────

    /// <summary>All currently managed <see cref="BuildingNode2D"/> nodes.</summary>
    public IReadOnlyList<BuildingNode2D> BuildingNodes => _buildingNodes.Values.ToList();

    /// <summary>All currently managed <see cref="StationNode2D"/> nodes (outer ring).</summary>
    public IReadOnlyList<StationNode2D> StationNodes => _stationNodes.Values.ToList();

    /// <summary>The port currently being dragged from (link creation source).</summary>
    public BuildingNode2D.PortData? DragSourcePort => _dragSourcePort;

    /// <summary>Current cursor position in board-space during a port drag.</summary>
    public Vector2 DragCursorWorld => _dragCursorWorld;

    /// <summary>Whether the current drag would produce a valid link.</summary>
    public bool DragValid => _dragValid;

    /// <summary>The currently active <see cref="IPlanetBoardMode"/>.</summary>
    public IPlanetBoardMode? ActiveMode => _mode;

    /// <summary>The orbital body this board is displaying.</summary>
    public IOrbitalBody? Body => _body;

    /// <summary>Current circle radius used for building placement.</summary>
    public float CircleRadius => _circleRadius;

    /// <summary>
    /// Bounding box encompassing all current building positions, suitable
    /// for camera framing.
    /// </summary>
    public Rect2 ContentBoundingBox
    {
        get
        {
            if (_buildingNodes.Count == 0 && _stationNodes.Count == 0)
                return new Rect2(Vector2.Zero, new Vector2(1, 1));

            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
            float maxR = 0f;
            foreach (var bn in _buildingNodes.Values)
            {
                if (bn.Position.X < min.X) min.X = bn.Position.X;
                if (bn.Position.Y < min.Y) min.Y = bn.Position.Y;
                if (bn.Position.X > max.X) max.X = bn.Position.X;
                if (bn.Position.Y > max.Y) max.Y = bn.Position.Y;
                if (bn.Radius > maxR) maxR = bn.Radius;
            }
            foreach (var sn in _stationNodes.Values)
            {
                if (sn.Position.X < min.X) min.X = sn.Position.X;
                if (sn.Position.Y < min.Y) min.Y = sn.Position.Y;
                if (sn.Position.X > max.X) max.X = sn.Position.X;
                if (sn.Position.Y > max.Y) max.Y = sn.Position.Y;
                if (sn.Radius > maxR) maxR = sn.Radius;
            }
            Vector2 pad = new(maxR + 16f, maxR + 16f);
            return new Rect2(min - pad, (max - min) + pad * 2f);
        }
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.BuildingConstructed += OnBuildingsChanged;
            bus.BuildingRemoved += OnBuildingsChanged;
            bus.ResourceLinkChanged += OnLinksChanged;
            bus.StationConstructed += OnStationConstructed;
            bus.StationRemoved += OnStationRemoved;
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
            bus.StationConstructed -= OnStationConstructed;
            bus.StationRemoved -= OnStationRemoved;
        }
        if (_cameraController != null)
        {
            _cameraController.ViewChanged -= OnCameraViewChanged;
            _cameraController = null;
        }
    }

    // ── Drawing ──────────────────────────────────────────────────────────

    public override void _Draw()
    {
        DrawDotGrid();
        DrawCircleOutline();
        DrawStationRingOutline();
    }

    private void DrawCircleOutline()
    {
        if (_circleRadius <= 0f)
            return;
        DrawArc(Vector2.Zero, _circleRadius, 0f, Mathf.Tau,
                CircleOutlineSegments, CircleOutlineColor, CircleOutlineWidth, antialiased: true);
    }

    private void DrawStationRingOutline()
    {
        if (_stationRingRadius <= 0f || _stationNodes.Count == 0)
            return;
        DrawArc(Vector2.Zero, _stationRingRadius, 0f, Mathf.Tau,
                CircleOutlineSegments, CircleOutlineColor, CircleOutlineWidth, antialiased: true);
    }

    private void DrawDotGrid()
    {
        if (_cameraController == null)
            return;

        Rect2 view = _cameraController.GetVisibleBoardRect();
        float minX = view.Position.X - DotGridSpacing;
        float minY = view.Position.Y - DotGridSpacing;
        float maxX = view.Position.X + view.Size.X + DotGridSpacing;
        float maxY = view.Position.Y + view.Size.Y + DotGridSpacing;

        float startX = Mathf.Floor(minX / DotGridSpacing) * DotGridSpacing;
        float startY = Mathf.Floor(minY / DotGridSpacing) * DotGridSpacing;

        for (float y = startY; y <= maxY; y += DotGridSpacing)
            for (float x = startX; x <= maxX; x += DotGridSpacing)
                DrawCircle(new Vector2(x, y), DotGridDotRadius, DotGridColor);
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Sets the orbital body and refreshes the board from its buildings.</summary>
    public void SetBody(IOrbitalBody? body)
    {
        _body = body;
        RefreshFromBody();
        if (_mode != null && _body != null)
            _mode.OnEnter(this, _body);
    }

    /// <summary>Switches to a new <see cref="IPlanetBoardMode"/>, calling OnExit/OnEnter.</summary>
    public void SetMode(IPlanetBoardMode? mode)
    {
        _mode?.OnExit();
        _mode = mode;
        if (_mode != null && _body != null)
            _mode.OnEnter(this, _body);
    }

    /// <summary>
    /// Clears all <see cref="BuildingNode2D"/> children and re-creates them
    /// from the current body's active buildings using
    /// <see cref="BoardLayoutEngine.Compute"/>. This is the full-rebuild
    /// path used when the body changes. For incremental updates (single
    /// building added/removed), <see cref="AddBuildingNode"/> and
    /// <see cref="RemoveBuildingNode"/> are preferred.
    /// </summary>
    public void RefreshFromBody()
    {
        // Clear existing building nodes
        foreach (var bn in _buildingNodes.Values)
            bn.QueueFree();
        _buildingNodes.Clear();
        foreach (var sn in _stationNodes.Values)
            sn.QueueFree();
        _stationNodes.Clear();
        _hoveredPort = null;
        _hoveredLink = null;
        _draggedBuilding = null;
        _dragSourcePort = null;
        _circleRadius = 0f;
        _shapeRadius = 64f;
        _stationRingRadius = 0f;

        var emptyBBox = new Rect2(Vector2.Zero, Vector2.Zero);

        if (_body == null)
        {
            _linkRenderer.RebuildLinks(GetAllBuildingNodes());
            CameraController?.FitToContent(emptyBBox);
            QueueRedraw();
            return;
        }

        var manager = _body.BuildingConstructionMgr;
        if (manager == null)
        {
            _linkRenderer.RebuildLinks(GetAllBuildingNodes());
            CameraController?.FitToContent(emptyBBox);
            QueueRedraw();
            return;
        }

        var matched = _body.GetAllBuildings();
        var layout = BoardLayoutEngine.Compute(matched);
        _circleRadius = layout.CircleRadius;
        _shapeRadius = layout.ShapeRadius;

        foreach (var b in matched)
        {
            if (!layout.Positions.TryGetValue(b, out var pos))
                continue;

            var node = new BuildingNode2D();
            node.Setup(b, pos);
            AddChild(node);
            _buildingNodes[b] = node;
        }

        BuildStationRing();

        _linkRenderer.RebuildLinks(GetAllBuildingNodes());

        // Fit the camera to the union of buildings and stations so the
        // outer station ring is reachable from the initial framed view.
        CameraController?.FitToContent(ContentBoundingBox);
        QueueRedraw();
    }

    /// <summary>
    /// Incrementally adds a single building to the board. Computes its
    /// position via <see cref="BoardLayoutEngine.ComputePositionForNew"/>,
    /// placing it in the largest angular gap. If the circle must grow,
    /// existing buildings are pushed outward equally.
    /// </summary>
    public void AddBuildingNode(Building building)
    {
        if (building == null || _buildingNodes.ContainsKey(building))
            return;

        float defaultRadius = 64f;
        var existingBuildings = _buildingNodes.Keys.ToList();
        var existingPositions = new Dictionary<Building, Vector2>();
        foreach (var kv in _buildingNodes)
            existingPositions[kv.Key] = kv.Value.Position;

        var result = BoardLayoutEngine.ComputePositionForNew(
            building, existingBuildings, existingPositions, _circleRadius, defaultRadius);

        // Sunflower indices are stable; UpdatedPositions stays empty in
        // practice, but apply anything the engine produced.
        foreach (var kv in result.UpdatedPositions)
        {
            if (_buildingNodes.TryGetValue(kv.Key, out var node))
                node.Position = kv.Value;
        }

        _circleRadius = result.NewCircleRadius;

        var newNode = new BuildingNode2D();
        newNode.Setup(building, result.NewPosition);
        AddChild(newNode);
        _buildingNodes[building] = newNode;
        if (newNode.Radius > _shapeRadius)
            _shapeRadius = newNode.Radius;

        _linkRenderer.RebuildLinks(GetAllBuildingNodes());
        CameraController?.EnsureContentVisible(ContentBoundingBox);
        QueueRedraw();
    }

    /// <summary>
    /// Incrementally removes a single building from the board.
    /// </summary>
    public void RemoveBuildingNode(Building building)
    {
        if (building == null || !_buildingNodes.TryGetValue(building, out var node))
            return;

        _buildingNodes.Remove(building);
        node.QueueFree();

        // Shrink the disk to match the new count so the boundary doesn't
        // linger past where buildings live.
        _circleRadius = BoardLayoutEngine.ComputeCircleRadius(_buildingNodes.Count, _shapeRadius * 2f);

        _linkRenderer.RebuildLinks(GetAllBuildingNodes());
        if (_buildingNodes.Count > 0)
            CameraController?.EnsureContentVisible(ContentBoundingBox);
        QueueRedraw();
    }

    /// <summary>Delegates screen-to-board conversion to the camera controller.</summary>
    public Vector2 ScreenToWorld(Vector2 screenPos) =>
        CameraController != null ? CameraController.ScreenToBoard(screenPos) : Vector2.Zero;

    // ── Station ring management ─────────────────────────────────────────

    /// <summary>
    /// Rebuilds the outer station ring from the current body's
    /// <see cref="IOrbitalBody.SatellitesContainer"/>. Filters to
    /// <see cref="StationSatellite"/> children that are not under construction.
    /// </summary>
    private void BuildStationRing()
    {
        foreach (var sn in _stationNodes.Values)
            sn.QueueFree();
        _stationNodes.Clear();
        _stationRingRadius = 0f;

        if (_body == null) return;

        var stations = new List<StationSatellite>();
        var container = _body.SatellitesContainer;
        if (container != null)
        {
            foreach (var child in container.GetChildren())
            {
                if (child is StationSatellite st && !st.IsUnderConstruction)
                    stations.Add(st);
            }
        }

        if (stations.Count == 0) return;

        var ring = BoardLayoutEngine.ComputeStationRing(stations, _circleRadius);
        _stationRingRadius = BoardLayoutEngine.ComputeStationRingRadius(_circleRadius);

        foreach (var s in stations)
        {
            if (!ring.TryGetValue(s, out var pos)) continue;
            var node = new StationNode2D();
            node.Setup(s, pos);
            AddChild(node);
            _stationNodes[s] = node;
        }
    }

    private void OnStationConstructed(StationSatellite station)
    {
        if (_body == null || station == null) return;
        if (_stationNodes.ContainsKey(station)) return;
        if (station.GetParent() != _body.SatellitesContainer) return;
        BuildStationRing();
        CameraController?.EnsureContentVisible(ContentBoundingBox);
        QueueRedraw();
    }

    private void OnStationRemoved(StationSatellite station)
    {
        if (station == null) return;
        if (!_stationNodes.TryGetValue(station, out var node)) return;
        _stationNodes.Remove(station);
        node.QueueFree();

        // Recompute remaining station positions on the same ring.
        var remaining = new List<StationSatellite>(_stationNodes.Keys);
        if (remaining.Count == 0)
        {
            _stationRingRadius = 0f;
        }
        else
        {
            var ring = BoardLayoutEngine.ComputeStationRing(remaining, _circleRadius);
            _stationRingRadius = BoardLayoutEngine.ComputeStationRingRadius(_circleRadius);
            foreach (var s in remaining)
            {
                if (ring.TryGetValue(s, out var pos) && _stationNodes.TryGetValue(s, out var n))
                    n.Position = pos;
            }
        }
        CameraController?.EnsureContentVisible(ContentBoundingBox);
        QueueRedraw();
    }

    // ── Signal handlers ──────────────────────────────────────────────────

    private void OnBuildingsChanged(int _)
    {
        if (_body == null)
            return;

        var currentBuildings = new HashSet<Building>(_body.GetAllBuildings());
        var displayedBuildings = new HashSet<Building>(_buildingNodes.Keys);

        // Add new buildings that aren't displayed yet.
        foreach (var b in currentBuildings)
        {
            if (!displayedBuildings.Contains(b))
                AddBuildingNode(b);
        }

        // Remove buildings that are no longer active.
        foreach (var b in displayedBuildings)
        {
            if (!currentBuildings.Contains(b))
                RemoveBuildingNode(b);
        }
    }

    /// <summary>Rebuilds the link renderer from current building port state.</summary>
    public void RefreshLinks() =>
        _linkRenderer.RebuildLinks(GetAllBuildingNodes());

    private void OnLinksChanged() => RefreshLinks();

    /// <summary>Returns all <see cref="BuildingNode2D"/> instances as a list.</summary>
    private IReadOnlyList<BuildingNode2D> GetAllBuildingNodes() =>
        _buildingNodes.Values.ToList();

    // ── Input handling ──────────────────────────────────────────────────

    /// <summary>
    /// Entry point for input events forwarded from
    /// <see cref="PlanetBoardView._GuiInput"/>.  Returns <c>true</c> when
    /// the event was consumed by building/port interaction so the caller can
    /// stop further propagation.
    /// </summary>
    public bool HandleInputEvent(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb:
                HandleMouseButton(mb);
                return true;  // BoardWorld handles all mouse-button input
            case InputEventMouseMotion mm:
                HandleMouseMotion(mm);
                return true;  // BoardWorld handles all mouse-motion for hover
        }
        return false;
    }

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        Vector2 worldPos = CameraController != null
            ? CameraController.ScreenToBoard(mb.Position)
            : Vector2.Zero;

        // ── Left press ──────────────────────────────────────────────
        if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            // 1) Hit test ports
            var hitPort = HitTestPortAll(worldPos);
            if (hitPort != null)
            {
                if (_mode != null && _mode.OnPortDragStart(hitPort.Node))
                {
                    _dragSourcePort = hitPort;
                    _dragCursorWorld = worldPos;
                    _dragValid = false;
                    hitPort.Owner.DragSourcePort = hitPort;
                    return;
                }

                _mode?.OnPortClick(hitPort.Node, mb.ButtonIndex);
                return;
            }

            // 2) Hit test building shapes
            var hitBuilding = HitTestShapeAll(worldPos);
            if (hitBuilding != null)
            {
                _draggedBuilding = hitBuilding;
                _dragOffset = worldPos - hitBuilding.Position;
                _buildingPressScreenPos = mb.Position;
                hitBuilding.StartDrag();
                return;
            }

            // 3) Hit test bundle dots (must precede link-edge hit so dots win the line)
            var hitBundle = _linkRenderer.HitTestBundle(worldPos);
            if (hitBundle.HasValue)
            {
                BundleClicked?.Invoke(hitBundle.Value.Link, hitBundle.Value.Package);
                return;
            }

            // 4) Hit test link edges
            var hitEdge = _linkRenderer.HitTestEdge(worldPos);
            if (hitEdge.HasValue)
            {
                _mode?.OnEdgeClick(hitEdge.Value.Link, mb.ButtonIndex);
                LinkClicked?.Invoke(hitEdge.Value.Link);
                return;
            }

            // 4) Hit test stations on outer ring
            var hitStation = HitTestStationAll(worldPos);
            if (hitStation != null)
            {
                _mode?.OnStationClick(hitStation, mb.ButtonIndex);
                return;
            }

            // 5) Empty click
            _mode?.OnEmptyClick(worldPos, mb.ButtonIndex);
            SelectionCleared?.Invoke();
            return;
        }

        // ── Left release ────────────────────────────────────────────
        if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
        {
            // Port drag end
            if (_dragSourcePort != null)
            {
                var dropPort = HitTestPortAll(worldPos);
                _mode?.OnPortDragEnd(dropPort?.Node);
                ClearPortDragState();
                return;
            }

            // Building drag end — distinguish click vs drag by screen-space travel.
            if (_draggedBuilding != null)
            {
                var clickedBuilding = _draggedBuilding;
                bool isClick = (mb.Position - _buildingPressScreenPos).Length() < ClickMaxScreenDistance;
                _draggedBuilding.EndDrag();
                _linkRenderer.RebuildLinks(GetAllBuildingNodes());
                _draggedBuilding = null;
                if (isClick)
                    BuildingClicked?.Invoke(clickedBuilding.Building);
                return;
            }
        }

        // ── Right press ─────────────────────────────────────────────
        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            // Cancel port drag
            if (_dragSourcePort != null)
            {
                _mode?.OnPortDragEnd(null);
                ClearPortDragState();
                return;
            }

            // Hit test edges
            var edge = _linkRenderer.HitTestEdge(worldPos);
            if (edge.HasValue && _mode != null && _mode.OnEdgeClick(edge.Value.Link, mb.ButtonIndex))
            {
                return;
            }

            // Hit test ports
            var port = HitTestPortAll(worldPos);
            if (port != null && _mode != null && _mode.OnPortClick(port.Node, mb.ButtonIndex))
            {
                return;
            }

            // Hit test stations
            var stationRight = HitTestStationAll(worldPos);
            if (stationRight != null && _mode != null && _mode.OnStationClick(stationRight, mb.ButtonIndex))
            {
                return;
            }

            _mode?.OnEmptyClick(worldPos, mb.ButtonIndex);
        }
    }

    private StationNode2D? HitTestStationAll(Vector2 worldPos)
    {
        foreach (var sn in _stationNodes.Values)
        {
            if (sn.HitTestShape(worldPos - sn.Position))
                return sn;
        }
        return null;
    }

    private void HandleMouseMotion(InputEventMouseMotion mm)
    {
        Vector2 worldPos = CameraController != null
            ? CameraController.ScreenToBoard(mm.Position)
            : Vector2.Zero;

        // Building drag — constrain to circle interior
        if (_draggedBuilding != null)
        {
            Vector2 targetPos = worldPos - _dragOffset;
            targetPos = ClampToCircle(targetPos, _draggedBuilding.Radius);
            _draggedBuilding.DragTo(targetPos);
            return;
        }

        // Port drag
        if (_dragSourcePort != null)
        {
            _dragCursorWorld = worldPos;
            var dropPort = HitTestPortAll(worldPos);
            _dragValid = dropPort != null
                && dropPort != _dragSourcePort
                && ResourceLink.CanConnect(_dragSourcePort.Node, dropPort.Node);
            _mode?.OnPortDragUpdate(worldPos, dropPort?.Node);

            // Compute preview source world position
            Vector2 sourceWorld = _dragSourcePort.Owner.Position
                + _dragSourcePort.LocalAnchor
                + _dragSourcePort.OutwardNormal * 8f;
            _linkRenderer.SetDragPreview(sourceWorld, worldPos, _dragValid);
            return;
        }

        // Hover detection
        var hoveredPort = HitTestPortAll(worldPos);
        if (hoveredPort != null)
        {
            UpdateHoveredPort(hoveredPort);
            _hoveredLink = null;
            _linkRenderer.SetHoveredLink(null);
        }
        else
        {
            ClearHoveredPort();

            var hoveredEdge = _linkRenderer.HitTestEdge(worldPos);
            var newHoveredLink = hoveredEdge?.Link;
            if (!ReferenceEquals(_hoveredLink, newHoveredLink))
            {
                _hoveredLink = newHoveredLink;
                _linkRenderer.SetHoveredLink(_hoveredLink);
            }
        }
    }

    // ── Circle constraint ───────────────────────────────────────────────

    /// <summary>
    /// Clamps a position so the building stays inside the circle boundary.
    /// The building's center must be at most <c>circleRadius - buildingRadius</c>
    /// from the origin so the shape doesn't cross the boundary.
    /// </summary>
    private Vector2 ClampToCircle(Vector2 position, float buildingRadius)
    {
        float maxDist = Mathf.Max(_circleRadius - buildingRadius, buildingRadius);
        float dist = position.Length();
        if (dist <= maxDist)
            return position;
        if (dist < 1e-8f)
            return new Vector2(0f, -maxDist); // default to top
        return position.Normalized() * maxDist;
    }

    // ── Hit testing helpers ──────────────────────────────────────────────

    /// <summary>
    /// Hit-tests ports across all buildings, returning the closest one within
    /// default hit radius, or null.
    /// </summary>
    private BuildingNode2D.PortData? HitTestPortAll(Vector2 worldPos, float hitRadius = 12f)
    {
        BuildingNode2D.PortData? best = null;
        float bestDist = hitRadius;

        foreach (var bn in _buildingNodes.Values)
        {
            Vector2 localPos = worldPos - bn.Position;
            foreach (var port in bn.Ports)
            {
                Vector2 portDrawn = port.LocalAnchor + port.OutwardNormal * 8f;
                float d = localPos.DistanceTo(portDrawn);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = port;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Hit-tests building shapes across all buildings, returning the first one
    /// that contains the world-space point (in local coords), or null.
    /// </summary>
    private BuildingNode2D? HitTestShapeAll(Vector2 worldPos)
    {
        foreach (var bn in _buildingNodes.Values)
        {
            Vector2 localPos = worldPos - bn.Position;
            if (bn.HitTestShape(localPos))
                return bn;
        }

        return null;
    }

    // ── Hover state management ──────────────────────────────────────────

    private void UpdateHoveredPort(BuildingNode2D.PortData? port)
    {
        if (ReferenceEquals(_hoveredPort, port))
            return;

        // Clear previous
        if (_hoveredPort != null && _hoveredPort.Owner != null)
            _hoveredPort.Owner.HoveredPort = null;

        _hoveredPort = port;

        // Set new
        if (_hoveredPort != null && _hoveredPort.Owner != null)
            _hoveredPort.Owner.HoveredPort = _hoveredPort;
    }

    private void ClearHoveredPort()
    {
        if (_hoveredPort != null && _hoveredPort.Owner != null)
            _hoveredPort.Owner.HoveredPort = null;
        _hoveredPort = null;
    }

    // ── Port drag state management ──────────────────────────────────────

    private void ClearPortDragState()
    {
        if (_dragSourcePort != null && _dragSourcePort.Owner != null)
            _dragSourcePort.Owner.DragSourcePort = null;
        _dragSourcePort = null;
        _dragValid = false;
        _linkRenderer.SetDragPreview(null, null, false);
    }
}
