#if DEBUG
using System.Collections.Generic;
using Godot;
using UI.PlanetBoard;
using UtilityLibrary.GameMath.Orbital;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Design-mode 2D board for the System Template Editor. A thin <see cref="Control"/> that builds a
/// SubViewportContainer → SubViewport → <see cref="BoardCameraController"/> + inner renderer in code
/// and reuses only the rendering math from the runtime SystemBoard (which is too coupled to
/// <c>IOrbitalBody</c>/<c>SystemData</c> to subclass). Positions are derived from template
/// <see cref="BodyNode"/> orbital parameters — not physics — so editing is instant.
/// </summary>
public partial class SystemTemplateBoard : Control
{
    private SubViewport _viewport = null!;
    private BoardCameraController _camera = null!;
    private BoardRenderer _renderer = null!;
    private SystemTemplateModel? _model;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var container = new SubViewportContainer
        {
            Stretch = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(container);

        _viewport = new SubViewport
        {
            Size = new Vector2I(800, 600),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
        };
        container.AddChild(_viewport);

        _camera = new BoardCameraController();
        _viewport.AddChild(_camera);

        _renderer = new BoardRenderer();
        _viewport.AddChild(_renderer);
        _renderer.Camera = _camera;

        Resized += OnResized;
        OnResized();
    }

    public void SetModel(SystemTemplateModel? model)
    {
        if (_model != null)
        {
            _model.Changed -= OnModelChanged;
            _model.SelectionChanged -= OnSelectionChanged;
        }
        _model = model;
        if (_model != null)
        {
            _model.Changed += OnModelChanged;
            _model.SelectionChanged += OnSelectionChanged;
        }
        _renderer.SetModel(model);
        ResetCamera();
    }

    private void OnModelChanged()
    {
        // Recompute geometry only. The camera is NEVER auto-adjusted on an edit — doing so would
        // yank the developer's zoom/pan back to a full fit on every keystroke or drag tick. The view
        // is framed once on load (SetModel → ResetCamera); after that it's the developer's to control.
        _renderer.RecomputeLayout();
    }

    private void OnSelectionChanged(BodyNode? _) => _renderer.QueueRedraw();

    public override void _GuiInput(InputEvent @event)
    {
        if (!Visible)
            return;

        // Camera consumes wheel-zoom and middle-drag pan first.
        if (_camera.HandleInputEvent(@event))
        {
            AcceptEvent();
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed)
                {
                    _renderer.BeginInteraction(_camera.ScreenToBoard(mb.Position));
                }
                else
                {
                    _renderer.EndInteraction();
                    // Recompute the (now unfrozen) scale/bbox, but don't touch the camera — keep the
                    // developer's current zoom/pan.
                    _renderer.RecomputeLayout();
                }
                AcceptEvent();
                break;

            case InputEventMouseMotion mm when _renderer.IsDragging:
                _renderer.UpdateDrag(_camera.ScreenToBoard(mm.Position));
                AcceptEvent();
                break;
        }
    }

    private void OnResized()
    {
        _viewport.Size = new Vector2I(Mathf.Max(1, (int)Size.X), Mathf.Max(1, (int)Size.Y));
        _camera.UpdateViewportSize(Size);
    }

    public void ResetCamera()
    {
        _camera.FitToContent(_renderer.ContentBoundingBox);
    }

    /// <summary>Node2D that draws orbits + bodies in board-space; the camera maps board→screen.</summary>
    public partial class BoardRenderer : Node2D
    {
        private const float TargetBoardExtent = 4000f;
        private const int OrbitSamples = 96;
        private const float BodyRadius = 22f;

        private static readonly Color DominantColor = new(1.0f, 0.85f, 0.3f);
        private static readonly Color PlanetaryColor = new(0.4f, 0.8f, 1.0f);
        private static readonly Color BeltColor = new(0.6f, 0.6f, 0.65f);
        private static readonly Color SatelliteColor = new(0.5f, 1.0f, 0.6f);

        public BoardCameraController? Camera { get; set; }

        private SystemTemplateModel? _model;
        private readonly Dictionary<BodyNode, Vector2> _positions = new();
        private float _scale = 1f;
        private Vector2 _origin;
        private Rect2 _bbox = new(Vector2.Zero, new Vector2(1, 1));

        // Drag state.
        private enum DragKind { None, Body, Apogee, Perigee }
        private DragKind _drag = DragKind.None;
        private BodyNode? _dragNode;

        public Rect2 ContentBoundingBox => _bbox;
        public bool IsDragging => _drag != DragKind.None;

        public void SetModel(SystemTemplateModel? model)
        {
            _model = model;
            RecomputeLayout();
        }

        private const float EdgeMargin = 1.2f; // board frames all orbits plus 20% on the edges.

        public void RecomputeLayout()
        {
            _positions.Clear();
            if (_model == null || _model.Roots.Count == 0)
            {
                _bbox = new Rect2(Vector2.Zero, new Vector2(1, 1));
                QueueRedraw();
                return;
            }

            // Pre-scale world-XZ positions (template units).
            var world = new Dictionary<BodyNode, Vector2>();
            Vector2 centroid = Vector2.Zero;
            foreach (var root in _model.Roots)
            {
                Vector2 p = new(root.Position.X, root.Position.Z);
                world[root] = p;
                centroid += p;
            }
            centroid /= _model.Roots.Count;

            foreach (var root in _model.Roots)
                ComputeWorld(root, world);

            // Scale, origin and bounding box stay frozen during a drag so the cursor→template-unit
            // mapping doesn't shift mid-drag (which made bodies fly off). Only body positions update.
            if (!IsDragging)
            {
                _origin = centroid;

                // Extent must cover whole ORBITS, not just body centers, so nothing clips the edge.
                float extent = 1f;
                foreach (var kvp in world)
                {
                    extent = Mathf.Max(extent, (kvp.Value - _origin).Length());
                    if (kvp.Key.IsDominant || kvp.Key.Parent == null)
                        continue;
                    Vector2 parentWorld = world[kvp.Key.Parent];
                    for (int i = 0; i < OrbitSamples; i++)
                    {
                        float t = i / (float)OrbitSamples * Mathf.Tau;
                        Vector2 pt = parentWorld + LocalOrbitOffset(kvp.Key, t);
                        extent = Mathf.Max(extent, (pt - _origin).Length());
                    }
                }
                _scale = TargetBoardExtent / extent;

                // Board-space bbox over the same orbit samples, expanded by EdgeMargin around its centre.
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                void Include(Vector2 worldPt)
                {
                    Vector2 b = (worldPt - _origin) * _scale;
                    minX = Mathf.Min(minX, b.X); minY = Mathf.Min(minY, b.Y);
                    maxX = Mathf.Max(maxX, b.X); maxY = Mathf.Max(maxY, b.Y);
                }
                foreach (var kvp in world)
                {
                    Include(kvp.Value);
                    if (kvp.Key.IsDominant || kvp.Key.Parent == null)
                        continue;
                    Vector2 parentWorld = world[kvp.Key.Parent];
                    for (int i = 0; i < OrbitSamples; i++)
                    {
                        float t = i / (float)OrbitSamples * Mathf.Tau;
                        Include(parentWorld + LocalOrbitOffset(kvp.Key, t));
                    }
                }
                var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                var half = new Vector2((maxX - minX) * 0.5f, (maxY - minY) * 0.5f) * EdgeMargin;
                _bbox = new Rect2(center - half, half * 2f);
            }

            foreach (var kvp in world)
                _positions[kvp.Key] = (kvp.Value - _origin) * _scale;
            QueueRedraw();
        }

        private void ComputeWorld(BodyNode node, Dictionary<BodyNode, Vector2> world)
        {
            foreach (var child in node.Children)
            {
                Vector2 local = LocalOrbitOffset(child, Mathf.DegToRad(child.StartingAngle));
                world[child] = world[node] + local;
                ComputeWorld(child, world);
            }
        }

        /// <summary>Orbital offset (template XZ units) of a non-dominant node from its parent focus.</summary>
        private static Vector2 LocalOrbitOffset(BodyNode node, float angleRad)
        {
            float apogee = node.Category == BodyCategory.Belt ? node.RingApogee : node.Apogee;
            float perigee = node.Category == BodyCategory.Belt ? node.RingPerigee : node.Perigee;
            float ecc = OrbitalMath.CalculateEccentricity(apogee, perigee);
            Vector3 pos = OrbitalMath.CalculateOrbitalPosition(
                Vector3.Right, Vector3.Back, apogee, perigee, angleRad, ecc);
            return new Vector2(pos.X, pos.Z);
        }

        // ─── Interaction (M6) ──────────────────────────────────────────────

        private const float HandleHitRadius = 60f;

        /// <summary>Hit-test on left press: a body under the cursor wins (so a body sitting on its own
        /// apo/peri handle stays draggable); otherwise an apo/peri handle of the already-selected body
        /// (grabbed along the dashed axis, clear of the body); otherwise clear selection.</summary>
        public void BeginInteraction(Vector2 boardPt)
        {
            if (_model == null)
                return;

            // 1) Body under the cursor takes priority.
            BodyNode? hit = BodyAt(boardPt);
            if (hit != null)
            {
                _model.Select(hit);
                if (!hit.IsDominant)
                {
                    _drag = DragKind.Body;
                    _dragNode = hit;
                }
                QueueRedraw();
                return;
            }

            // 2) No body here — try an apogee/perigee handle of the selected body.
            BodyNode? selected = _model.Selected;
            if (selected != null && !selected.IsDominant && selected.Parent != null
                && _positions.TryGetValue(selected.Parent, out Vector2 focus))
            {
                Vector2 peri = focus + LocalOrbitOffset(selected, 0f) * _scale;
                Vector2 apo = focus + LocalOrbitOffset(selected, Mathf.Pi) * _scale;
                if (boardPt.DistanceTo(peri) <= HandleHitRadius)
                {
                    _drag = DragKind.Perigee;
                    _dragNode = selected;
                    return;
                }
                if (boardPt.DistanceTo(apo) <= HandleHitRadius)
                {
                    _drag = DragKind.Apogee;
                    _dragNode = selected;
                    return;
                }
            }

            // 3) Empty space.
            _model.Select(null);
            QueueRedraw();
        }

        public void UpdateDrag(Vector2 boardPt)
        {
            if (_model == null || _dragNode == null || _dragNode.Parent == null)
                return;
            if (!_positions.TryGetValue(_dragNode.Parent, out Vector2 focus))
                return;

            Vector2 rel = boardPt - focus;
            float radiusUnits = rel.Length() / _scale; // board → template units

            // Belts carry their orbit radius in Ring{Apogee,Perigee}, everything else in {Apogee,Perigee}.
            float a = GetApo(_dragNode), p = GetPeri(_dragNode);

            switch (_drag)
            {
                case DragKind.Body:
                {
                    // Angle from the parent focus, and a radius that scales apogee+perigee together
                    // (eccentricity fixed) so the body sits under the cursor at that true anomaly.
                    float angle = Mathf.PosMod(rel.Angle(), Mathf.Tau);
                    float ecc = OrbitalMath.CalculateEccentricity(a, p);
                    float denom = 1f - ecc * ecc;
                    if (denom > 1e-4f)
                    {
                        float semiMajor = radiusUnits * (1f + ecc * Mathf.Cos(angle)) / denom;
                        if (semiMajor > 1f)
                        {
                            SetApo(_dragNode, semiMajor * (1f + ecc));
                            SetPeri(_dragNode, semiMajor * (1f - ecc));
                        }
                    }
                    _dragNode.StartingAngle = Mathf.RadToDeg(angle);
                    break;
                }
                case DragKind.Apogee:
                    // Edit one endpoint independently → changes eccentricity. Keep apogee ≥ perigee.
                    SetApo(_dragNode, Mathf.Max(radiusUnits, p));
                    break;
                case DragKind.Perigee:
                    SetPeri(_dragNode, Mathf.Min(radiusUnits, a));
                    break;
            }

            _model.MarkChanged();
        }

        private static float GetApo(BodyNode n) => n.Category == BodyCategory.Belt ? n.RingApogee : n.Apogee;
        private static float GetPeri(BodyNode n) => n.Category == BodyCategory.Belt ? n.RingPerigee : n.Perigee;
        private static void SetApo(BodyNode n, float v)
        {
            if (n.Category == BodyCategory.Belt) n.RingApogee = v; else n.Apogee = v;
        }
        private static void SetPeri(BodyNode n, float v)
        {
            if (n.Category == BodyCategory.Belt) n.RingPerigee = v; else n.Perigee = v;
        }

        public void EndInteraction()
        {
            _drag = DragKind.None;
            _dragNode = null;
        }

        private BodyNode? BodyAt(Vector2 boardPt)
        {
            BodyNode? best = null;
            float bestDist = BodyRadius * 1.5f;
            foreach (var kvp in _positions)
            {
                float d = boardPt.DistanceTo(kvp.Value);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = kvp.Key;
                }
            }
            return best;
        }

        public override void _Draw()
        {
            if (_model == null)
                return;

            BodyNode? selected = _model.Selected;

            // Orbits first (under bodies).
            foreach (var node in _model.AllNodes())
            {
                if (node.IsDominant || node.Parent == null)
                    continue;
                if (!_positions.TryGetValue(node.Parent, out Vector2 parentBoard))
                    continue;
                bool muted = selected != null && !ReferenceEquals(selected, node);
                DrawOrbit(node, parentBoard, ColorFor(node, muted, 0.6f));
            }

            // Bodies on top.
            foreach (var node in _model.AllNodes())
            {
                if (!_positions.TryGetValue(node, out Vector2 board))
                    continue;
                bool muted = selected != null && !ReferenceEquals(selected, node);
                DrawCircle(board, BodyRadius, ColorFor(node, muted, 1f));
                DrawArc(board, BodyRadius, 0, Mathf.Tau, 24,
                    new Color(0, 0, 0, muted ? 0.3f : 0.8f), 2f);
            }

            // Dotted semi-major axis for the selected non-dominant body.
            if (selected != null && !selected.IsDominant && selected.Parent != null
                && _positions.TryGetValue(selected.Parent, out Vector2 focus))
            {
                DrawSemiMajorAxis(selected, focus);
            }
        }

        private void DrawOrbit(BodyNode node, Vector2 parentBoard, Color color)
        {
            var pts = new Vector2[OrbitSamples + 1];
            for (int i = 0; i <= OrbitSamples; i++)
            {
                float t = i / (float)OrbitSamples * Mathf.Tau;
                Vector2 local = LocalOrbitOffset(node, t) * _scale;
                pts[i] = parentBoard + local;
            }
            DrawPolyline(pts, color, 2f, antialiased: true);
        }

        private void DrawSemiMajorAxis(BodyNode node, Vector2 focus)
        {
            // Perigee at angle 0, apogee at angle π (relative to the parent focus).
            Vector2 peri = focus + LocalOrbitOffset(node, 0f) * _scale;
            Vector2 apo = focus + LocalOrbitOffset(node, Mathf.Pi) * _scale;
            DrawDashedLine(peri, apo, new Color(1f, 1f, 1f, 0.7f), 1.5f);
        }

        private void DrawDashedLine(Vector2 a, Vector2 b, Color color, float width)
        {
            const float dash = 30f, gap = 20f;
            Vector2 dir = b - a;
            float len = dir.Length();
            if (len < 1e-3f)
                return;
            dir /= len;
            float pos = 0f;
            while (pos < len)
            {
                float end = Mathf.Min(pos + dash, len);
                DrawLine(a + dir * pos, a + dir * end, color, width);
                pos = end + gap;
            }
        }

        private static Color ColorFor(BodyNode node, bool muted, float alpha)
        {
            Color c = node.Category switch
            {
                BodyCategory.Dominant => DominantColor,
                BodyCategory.Belt => BeltColor,
                BodyCategory.Satellite => SatelliteColor,
                _ => PlanetaryColor,
            };
            return new Color(c.R, c.G, c.B, muted ? alpha * 0.22f : alpha);
        }
    }
}
#endif
