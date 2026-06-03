using System;
using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using UI;
using UI.OrbitalScheduling;
using UI.PlanetBoard;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.SystemBoard;

/// <summary>
/// Root <see cref="Node2D"/> for the System Overview board. Unlike
/// <see cref="UI.PlanetBoard.BoardWorld"/> (one child node per item, single body),
/// this draws the whole star system in a single <see cref="_Draw"/> from flat
/// placement lists — barycenter at origin, every body with its billboard texture +
/// name + orbit around its parent, recursive satellites, station octagons, and
/// logistics-unit triangles (apex along travel for in-transit units, with the
/// current leg's route arc). Items move every frame, so positions are recomputed
/// live; heavy enumeration only happens on <see cref="RebuildLayout"/>.
/// </summary>
public partial class SystemBoardWorld : Node2D
{
    // ── Scaling constants ───────────────────────────────────────────────
    private const float TargetBoardExtent = 4000f;
    private const float BodyIconBase = 12f;
    private const float BodyIconK = 6f;
    private const float BodyIconMin = 10f;
    private const float BodyIconMax = 60f;
    private const float SatelliteClearance = 1.8f; // orbit radius >= parentIcon * this
    private const float StationRadius = 18f;
    private const float UnitRadius = 13f;
    private const int BeltIconCap = 64;      // max belt-member textures drawn per belt
    private const float BeltIconRadius = 6f; // belt debris icon half-size
    private const float BoardSizeFactor = 1.2f; // board ~1.2x the largest orbit, W and H

    // ── Draw constants ──────────────────────────────────────────────────
    private static readonly Color OrbitColor = new(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.45f);
    private static readonly Color FaintOrbitColor = new(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0.25f);
    private static readonly Color StationFill = new(0.78f, 0.56f, 0.18f);
    private static readonly Color StationFillBuilding = new(0.78f, 0.56f, 0.18f, 0.25f);
    private static readonly Color UnitFill = new(0.36f, 0.55f, 0.78f);
    private const int OrbitSegments = 96;

    // ── Camera ──────────────────────────────────────────────────────────
    private BoardCameraController? _cameraController;

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

    // ── State ───────────────────────────────────────────────────────────
    private Node? _systemContainer;
    private SystemData? _systemData;
    private Barycenter? _barycenter;
    private bool _pickMode;

    private Vector2 _worldOrigin;
    private float _scale = 1f;

    private readonly List<BodyPlacement> _bodies = new();
    private readonly List<StationPlacement> _stations = new();
    private readonly List<BeltPlacement> _belts = new();
    private readonly Dictionary<IOrbitalBody, float> _iconRadii = new();

    private double _redrawAccum;
    private const double RedrawInterval = 0.1; // ~10 Hz

    /// <summary>Id of the leg origin station — drawn faintly, not selectable.</summary>
    public string OriginStationId { get; set; } = "";

    /// <summary>Most recently picked destination station id, or empty.</summary>
    public string SelectedStationId { get; private set; } = "";

    /// <summary>Raised when a station is picked in pick mode (station, body it orbits).</summary>
    public event Action<StationSatellite, IOrbitalBody>? EndpointPicked;

    /// <summary>Raised on hover with a label for the hovered item, or null.</summary>
    public event Action<string?>? HoverChanged;

    // ── Nested placement records ────────────────────────────────────────
    private sealed class BodyPlacement
    {
        public IOrbitalBody Body = null!;
        public IOrbitalBody? Parent;   // null/Barycenter => top-level
        public int Depth;
    }

    private sealed class StationPlacement
    {
        public StationSatellite Station = null!;
        public IOrbitalBody Parent = null!;
    }

    /// <summary>
    /// One belt (asteroid or comet) around a parent body. Belt members are
    /// individual analytical CelestialBodies parented directly under the host;
    /// the board collapses them into a single orbit ring plus a sampled set of
    /// member textures rather than thousands of per-member rings.
    /// </summary>
    private sealed class BeltPlacement
    {
        public IOrbitalBody Parent = null!;
        public OrbitalBodyType Kind;
        public readonly List<CelestialBody> Members = new();
    }

    // ── Public API ──────────────────────────────────────────────────────

    public void SetPickMode(bool enabled) => _pickMode = enabled;

    public void SetFromUnit(LogisticsUnit unit)
    {
        _systemContainer = OrbitalScheduleUiHelpers.FindSystemContainer(unit);
        RebuildLayout();
    }

    public void SetSystem(Node systemContainer)
    {
        _systemContainer = systemContainer;
        RebuildLayout();
    }

    /// <summary>Re-enumerates bodies, stations and the barycenter from the container.</summary>
    public void RebuildLayout()
    {
        _bodies.Clear();
        _stations.Clear();
        _belts.Clear();
        _iconRadii.Clear();

        // Resolve via the container node (always in-tree), since this world node
        // may not yet be in the scene tree when RebuildLayout runs.
        Node resolveFrom = _systemContainer ?? this;
        _systemData = SystemData.FindForNode(resolveFrom);
        _barycenter = FindBarycenter(resolveFrom);

        if (_systemContainer == null)
        {
            QueueRedraw();
            return;
        }

        var roots = OrbitalScheduleUiHelpers.GetAllBodies(_systemContainer);
        foreach (var body in roots)
        {
            int depth = OrbitDepth(body);
            _bodies.Add(new BodyPlacement { Body = body, Parent = RealParent(body), Depth = depth });

            var container = SafeSatellites(body);
            if (container != null)
                foreach (var child in container.GetChildren())
                    if (child is StationSatellite st)
                        _stations.Add(new StationPlacement { Station = st, Parent = body });
        }

        CollectBelts(roots);
        RecomputeScale(roots);
        QueueRedraw();
    }

    /// <summary>
    /// Groups belt members — analytical satellites parented directly under a body
    /// (asteroids / comets) — into one <see cref="BeltPlacement"/> per
    /// (parent, asteroid|comet). These are excluded from <see cref="_bodies"/> so
    /// each belt draws a single ring instead of thousands.
    /// </summary>
    private void CollectBelts(List<IOrbitalBody> roots)
    {
        var groups = new Dictionary<(IOrbitalBody, OrbitalBodyType), BeltPlacement>();
        foreach (var body in roots)
        {
            if (body is not Node node)
                continue;
            foreach (var child in node.GetChildren())
            {
                if (child is not CelestialBody cb)
                    continue;
                if (cb.Classification is not BodyClassification.Satellite || !cb.ForceAnalyticalOrbit)
                    continue;

                var key = (body, cb.Classification.Type);
                if (!groups.TryGetValue(key, out var belt))
                {
                    belt = new BeltPlacement { Parent = body, Kind = cb.Classification.Type };
                    groups[key] = belt;
                    _belts.Add(belt);
                }
                belt.Members.Add(cb);
            }
        }
    }

    /// <summary>Bounding box of all body board positions, for camera framing.</summary>
    public Rect2 ContentBoundingBox
    {
        get
        {
            if (_bodies.Count == 0)
                return new Rect2(new Vector2(-TargetBoardExtent, -TargetBoardExtent),
                                 new Vector2(TargetBoardExtent * 2f, TargetBoardExtent * 2f));

            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

            void Enclose(Vector2 c, float rad)
            {
                min.X = Mathf.Min(min.X, c.X - rad); min.Y = Mathf.Min(min.Y, c.Y - rad);
                max.X = Mathf.Max(max.X, c.X + rad); max.Y = Mathf.Max(max.Y, c.Y + rad);
            }

            foreach (var bp in _bodies)
            {
                Vector2 p = BoardPosFor(bp.Body);
                Enclose(p, IconRadiusFor(bp.Body));
                // Enclose the whole orbit ring, not just the body's current point,
                // so the far side of every path stays on-screen when zoomed out.
                if (bp.Parent != null)
                {
                    Vector2 parentPos = BoardPosFor(bp.Parent);
                    Enclose(parentPos, (p - parentPos).Length());
                }
            }

            foreach (var belt in _belts)
            {
                if (belt.Members.Count == 0)
                    continue;
                Vector2 parentPos = BoardPosFor(belt.Parent);
                float maxR = 0f;
                foreach (var m in belt.Members)
                    maxR = Mathf.Max(maxR, (BoardPos(m.BodyPosition) - parentPos).Length());
                Enclose(parentPos, maxR);
            }

            // Grow about the center so the board is ~1.2x the largest orbit (W and H).
            Vector2 center = (min + max) * 0.5f;
            Vector2 half = (max - min) * 0.5f * BoardSizeFactor;
            Vector2 pad = new(32f, 32f);
            return new Rect2(center - half - pad, half * 2f + pad * 2f);
        }
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        SetProcess(true);
        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.StationConstructionStarted += OnStationsChanged;
            bus.StationConstructed += OnStationsChanged;
            bus.StationRemoved += OnStationsChanged;
        }
    }

    public override void _ExitTree()
    {
        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.StationConstructionStarted -= OnStationsChanged;
            bus.StationConstructed -= OnStationsChanged;
            bus.StationRemoved -= OnStationsChanged;
        }
        if (_cameraController != null)
        {
            _cameraController.ViewChanged -= OnCameraViewChanged;
            _cameraController = null;
        }
    }

    private void OnStationsChanged(StationSatellite _) => RebuildLayout();

    public override void _Process(double delta)
    {
        // Bodies and units move continuously; throttle redraws to ~10 Hz.
        _redrawAccum += delta;
        if (_redrawAccum >= RedrawInterval)
        {
            _redrawAccum = 0;
            QueueRedraw();
        }
    }

    // ── Drawing ─────────────────────────────────────────────────────────

    public override void _Draw()
    {
        DrawBodyOrbits();
        DrawBelts();
        DrawUnits();          // route arcs + parked rings drawn within, behind triangles
        DrawBodies();
        DrawStations();
        DrawBarycenter();
        DrawPickOverlay();
    }

    private void DrawBodyOrbits()
    {
        foreach (var bp in _bodies)
        {
            if (bp.Parent == null)
                continue;
            Vector2 parentPos = BoardPosFor(bp.Parent);
            float r = (BoardPosFor(bp.Body) - parentPos).Length();
            if (r > 0.5f)
                DrawArc(parentPos, r, 0f, Mathf.Tau, OrbitSegments, OrbitColor, 1.5f, antialiased: true);
        }
    }

    private void DrawBelts()
    {
        foreach (var belt in _belts)
        {
            int count = belt.Members.Count;
            if (count == 0)
                continue;

            Vector2 parentPos = BoardPosFor(belt.Parent);

            // Single orbit ring at the mean member radius.
            float sum = 0f;
            foreach (var m in belt.Members)
                sum += (BoardPos(m.BodyPosition) - parentPos).Length();
            float r = sum / count;
            if (r > 0.5f)
                DrawArc(parentPos, r, 0f, Mathf.Tau, OrbitSegments, OrbitColor, 1.5f, antialiased: true);

            // Capped, even-sampled member textures placed at their true positions.
            int shown = Mathf.Min(count, BeltIconCap);
            int stride = Mathf.Max(1, count / shown);
            for (int i = 0; i < count; i += stride)
            {
                var m = belt.Members[i];
                Vector2 pos = BoardPos(m.BodyPosition);
                Texture2D? tex = SafeTexture(m);
                if (tex != null)
                    DrawTextureRect(tex,
                        new Rect2(pos - new Vector2(BeltIconRadius, BeltIconRadius),
                            new Vector2(BeltIconRadius * 2f, BeltIconRadius * 2f)),
                        tile: false);
                else
                    DrawCircle(pos, BeltIconRadius * 0.5f, FaintOrbitColor);
            }
        }
    }

    private void DrawBodies()
    {
        var font = ThemeDB.FallbackFont;
        foreach (var bp in _bodies)
        {
            Vector2 pos = BoardPosFor(bp.Body);
            float r = IconRadiusFor(bp.Body);

            Texture2D? tex = SafeTexture(bp.Body);
            if (tex != null)
                DrawTextureRect(tex, new Rect2(pos - new Vector2(r, r), new Vector2(r * 2f, r * 2f)), tile: false);
            else
                DrawCircle(pos, r, FaintOrbitColor);

            if (font != null)
            {
                string name = bp.Body.BodyName ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    var size = font.GetStringSize(name, HorizontalAlignment.Center, -1, 12);
                    DrawString(font, new Vector2(pos.X - size.X / 2f, pos.Y + r + 4f + size.Y),
                        name, HorizontalAlignment.Center, -1, 12, WireColors.Ink);
                }
            }
        }
    }

    private void DrawStations()
    {
        var font = ThemeDB.FallbackFont;
        foreach (var sp in _stations)
        {
            Vector2 pos = StationBoardPos(sp);
            Vector2 parentPos = BoardPosFor(sp.Parent);

            // Faint orbit ring around the host body.
            float orbitR = (pos - parentPos).Length();
            if (orbitR > 0.5f)
                DrawArc(parentPos, orbitR, 0f, Mathf.Tau, OrbitSegments, FaintOrbitColor, 1f, antialiased: true);

            bool building = sp.Station.IsUnderConstruction;
            Vector2[] oct = SystemBoardShapes.RegularPolygon(pos, StationRadius, 8);
            DrawColoredPolygon(oct, building ? StationFillBuilding : StationFill);
            DrawClosedOutline(oct, WireColors.Ink, 1.5f);

            Texture2D? icon = sp.Station.Definition?.Icon?.Texture;
            if (icon != null)
            {
                float ir = StationRadius * 0.9f;
                DrawTextureRect(icon, new Rect2(pos - new Vector2(ir, ir) * 0.5f, new Vector2(ir, ir)),
                    tile: false, modulate: sp.Station.Definition!.Icon.Tint);
            }

            if (font != null)
            {
                string label = string.IsNullOrEmpty(sp.Station.Name) ? "Station" : sp.Station.Name;
                if (building)
                    label += $" ({Mathf.RoundToInt(Mathf.Clamp(sp.Station.ConstructionProgress, 0f, 1f) * 100f)}%)";
                var size = font.GetStringSize(label, HorizontalAlignment.Center, -1, 11);
                DrawString(font, new Vector2(pos.X - size.X / 2f, pos.Y + StationRadius + 4f + size.Y),
                    label, HorizontalAlignment.Center, -1, 11, WireColors.Ink);
            }
        }
    }

    private void DrawUnits()
    {
        if (_systemData == null)
            return;

        foreach (var unit in _systemData.GetAllShips())
        {
            if (unit == null || !IsInstanceValid(unit))
                continue;

            Vector2 pos = BoardPos(unit.GlobalPosition);
            bool inTransit = unit.State == LogisticsUnitState.InTransit;

            if (inTransit)
                DrawLegRoute(unit);
            else
                DrawParkedRing(unit, pos);

            float heading = HeadingFor(unit, pos);
            Vector2[] tri = SystemBoardShapes.Triangle(pos, UnitRadius, heading);
            DrawColoredPolygon(tri, UnitFill);
            DrawClosedOutline(tri, WireColors.Ink, 1.5f);

            Texture2D? icon = unit.ShipDef?.Icon?.Texture;
            if (icon != null)
            {
                float ir = UnitRadius * 0.8f;
                DrawTextureRect(icon, new Rect2(pos - new Vector2(ir, ir) * 0.5f, new Vector2(ir, ir)),
                    tile: false, modulate: unit.ShipDef!.Icon.Tint);
            }
        }
    }

    private void DrawParkedRing(LogisticsUnit unit, Vector2 pos)
    {
        var parent = OrbitalScheduleUiHelpers.GetCurrentBody(unit);
        if (parent == null)
            return;
        Vector2 parentPos = BoardPos(parent.BodyPosition);
        float r = (pos - parentPos).Length();
        if (r > 0.5f)
            DrawArc(parentPos, r, 0f, Mathf.Tau, OrbitSegments, FaintOrbitColor, 1f, antialiased: true);
    }

    private void DrawLegRoute(LogisticsUnit unit)
    {
        var traj = unit.ScheduleExecutor?.ActiveSchedule?.CurrentLeg?.SelectedTrajectory;
        if (traj == null)
            return;
        Vector2 a = BoardPos(traj.PredictedOriginPosition);
        Vector2 b = BoardPos(traj.PredictedDestinationPosition);
        float bow = (b - a).Length() * 0.15f;
        Vector2[] pts = SystemBoardShapes.BulgedBezier(a, b, bow, 32);
        DrawPolyline(pts, WireColors.Orange, 1.5f, antialiased: true);
    }

    private void DrawBarycenter()
    {
        if (_barycenter == null)
            return;
        var central = CentralBody();
        if (central == null)
            return;
        float dist = (BoardPos(central.BodyPosition) - Vector2.Zero).Length();
        if (dist > IconRadiusFor(central))
            DrawCircle(Vector2.Zero, 4f, Colors.White);
    }

    private void DrawPickOverlay()
    {
        if (!_pickMode)
            return;
        foreach (var sp in _stations)
        {
            string id = sp.Station.Id ?? "";
            if (string.IsNullOrEmpty(id))
                continue;
            bool isOrigin = id == OriginStationId;
            bool isSelected = id == SelectedStationId;
            if (!isOrigin && !isSelected)
                continue;
            Vector2 pos = StationBoardPos(sp);
            Color ring = isOrigin ? WireColors.InkFaint : WireColors.Orange;
            DrawArc(pos, StationRadius + 6f, 0f, Mathf.Tau, 48, ring, 2f, antialiased: true);
        }
    }

    private void DrawClosedOutline(Vector2[] verts, Color color, float width)
    {
        var loop = new Vector2[verts.Length + 1];
        Array.Copy(verts, loop, verts.Length);
        loop[verts.Length] = verts[0];
        DrawPolyline(loop, color, width, antialiased: true);
    }

    // ── Input ───────────────────────────────────────────────────────────

    public bool HandleInputEvent(InputEvent @event)
    {
        if (_cameraController == null)
            return false;

        switch (@event)
        {
            case InputEventMouseButton mb when mb.Pressed && mb.ButtonIndex == MouseButton.Left:
                return OnLeftClick(_cameraController.ScreenToBoard(mb.Position));
            case InputEventMouseMotion mm:
                UpdateHover(_cameraController.ScreenToBoard(mm.Position));
                return false; // never consume motion
        }
        return false;
    }

    private bool OnLeftClick(Vector2 boardPt)
    {
        if (!_pickMode)
            return false;

        var hit = HitTestStation(boardPt);
        if (hit == null)
            return false;

        string id = hit.Station.Id ?? "";
        if (string.IsNullOrEmpty(id))
            return false;

        if (id == OriginStationId)
        {
            ToastSystem.Instance?.ShowWarning("Cannot route back to the leg's origin.");
            return true;
        }
        if (hit.Station.IsUnderConstruction)
        {
            ToastSystem.Instance?.ShowWarning("Station is still under construction.");
            return true;
        }

        SelectedStationId = id;
        EndpointPicked?.Invoke(hit.Station, hit.Parent);
        QueueRedraw();
        return true;
    }

    private void UpdateHover(Vector2 boardPt)
    {
        var st = HitTestStation(boardPt);
        if (st != null)
        {
            HoverChanged?.Invoke(string.IsNullOrEmpty(st.Station.Name) ? "Orbital station" : st.Station.Name);
            return;
        }
        foreach (var bp in _bodies)
        {
            if ((BoardPosFor(bp.Body) - boardPt).Length() <= IconRadiusFor(bp.Body))
            {
                HoverChanged?.Invoke(bp.Body.BodyName);
                return;
            }
        }
        HoverChanged?.Invoke(null);
    }

    private StationPlacement? HitTestStation(Vector2 boardPt)
    {
        foreach (var sp in _stations)
        {
            Vector2[] oct = SystemBoardShapes.RegularPolygon(StationBoardPos(sp), StationRadius, 8);
            if (SystemBoardShapes.PointInPolygon(boardPt, oct))
                return sp;
        }
        return null;
    }

    // ── Geometry / scaling ──────────────────────────────────────────────

    private Vector2 BoardPos(Vector3 worldPos) =>
        (SystemBoardShapes.ProjectXZ(worldPos) - _worldOrigin) * _scale;

    /// <summary>
    /// Board position of a body. Top-level bodies map directly; satellites keep
    /// their angular position but are pushed out to at least
    /// <see cref="SatelliteClearance"/>× the parent icon radius so moons stay
    /// visible outside their planet.
    /// </summary>
    private Vector2 BoardPosFor(IOrbitalBody body)
    {
        var parent = RealParent(body);
        if (parent == null)
            return BoardPos(body.BodyPosition);

        Vector2 parentPos = BoardPosFor(parent);
        Vector2 raw = BoardPos(body.BodyPosition);
        Vector2 dir = raw - parentPos;
        float dist = dir.Length();
        float minDist = IconRadiusFor(parent) * SatelliteClearance;
        if (dist >= minDist)
            return raw;
        Vector2 unit = dist > 1e-3f ? dir / dist : Vector2.Right;
        return parentPos + unit * minDist;
    }

    private Vector2 StationBoardPos(StationPlacement sp)
    {
        Vector2 parentPos = BoardPosFor(sp.Parent);
        Vector2 raw = BoardPos(sp.Station.GlobalPosition);
        Vector2 dir = raw - parentPos;
        float dist = dir.Length();
        float minDist = IconRadiusFor(sp.Parent) * 1.6f + StationRadius;
        if (dist >= minDist)
            return raw;
        Vector2 unit = dist > 1e-3f ? dir / dist : Vector2.Up;
        return parentPos + unit * minDist;
    }

    private float IconRadiusFor(IOrbitalBody body)
    {
        if (_iconRadii.TryGetValue(body, out float cached))
            return cached;
        float r = Mathf.Clamp(BodyIconBase + BodyIconK * Mathf.Log(1f + Mathf.Max(0f, body.Radius)),
            BodyIconMin, BodyIconMax);
        _iconRadii[body] = r;
        return r;
    }

    private float HeadingFor(LogisticsUnit unit, Vector2 pos)
    {
        if (unit.State == LogisticsUnitState.InTransit)
        {
            Vector3 vel = unit.GetOrbitalVelocity();
            if (new Vector2(vel.X, vel.Z).LengthSquared() > 1e-6f)
                return SystemBoardShapes.HeadingFromVelocityXZ(vel);
            var dest = unit.ScheduleExecutor?.ActiveSchedule?.CurrentLeg?.SelectedTrajectory;
            if (dest != null)
            {
                Vector2 to = BoardPos(dest.PredictedDestinationPosition) - pos;
                if (to.LengthSquared() > 1e-6f)
                    return to.Angle();
            }
        }
        // Parked: point prograde along the orbit tangent (cosmetic).
        return unit.OrbitalAngle + Mathf.Pi / 2f;
    }

    private void RecomputeScale(List<IOrbitalBody> bodies)
    {
        _worldOrigin = _barycenter != null
            ? SystemBoardShapes.ProjectXZ(_barycenter.BodyPosition)
            : Vector2.Zero;

        float extent = 1f;
        foreach (var b in bodies)
        {
            float d = (SystemBoardShapes.ProjectXZ(b.BodyPosition) - _worldOrigin).Length();
            if (d > extent) extent = d;
        }
        // Belt members live outside `bodies`; fold them in so the outermost belt
        // doesn't fall off the board.
        foreach (var belt in _belts)
            foreach (var m in belt.Members)
            {
                float d = (SystemBoardShapes.ProjectXZ(m.BodyPosition) - _worldOrigin).Length();
                if (d > extent) extent = d;
            }
        _scale = TargetBoardExtent / extent;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static IOrbitalBody? RealParent(IOrbitalBody body)
    {
        var p = body.OrbitalParent;
        return p is null or Barycenter ? null : p;
    }

    private static int OrbitDepth(IOrbitalBody body)
    {
        int d = 0;
        for (var p = RealParent(body); p != null; p = RealParent(p))
            d++;
        return d;
    }

    private IOrbitalBody? CentralBody()
    {
        IOrbitalBody? best = null;
        float bestMass = float.NegativeInfinity;
        foreach (var bp in _bodies)
        {
            if (RealParent(bp.Body) != null)
                continue;
            if (bp.Body.Mass > bestMass)
            {
                bestMass = bp.Body.Mass;
                best = bp.Body;
            }
        }
        return best;
    }

    private static Barycenter? FindBarycenter(Node from)
    {
        var tree = from.GetTree();
        if (tree == null)
            return null;
        foreach (var node in tree.GetNodesInGroup("Barycenter"))
            if (node is Barycenter b)
                return b;
        return null;
    }

    /// <summary>Reads a body's satellite container, tolerating types that throw.</summary>
    private static Node? SafeSatellites(IOrbitalBody body)
    {
        try { return body.SatellitesContainer; }
        catch { return null; }
    }

    private static Texture2D? SafeTexture(IOrbitalBody body)
    {
        try
        {
            var bb = body.BillboardTextures;
            if (bb == null || !bb.AreTexturesReady)
                return null;
            return bb.GetTextureForDistance(5000f);
        }
        catch { return null; }
    }
}
