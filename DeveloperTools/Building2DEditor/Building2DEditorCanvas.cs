#if DEBUG
using System;
using Godot;
using Structures.Enums;
using Structures.Resources;

namespace DeveloperTools.Building2DEditor;

/// <summary>
/// Drawable canvas that renders the current shape's polygon, draggable vertex
/// handles, and slot circles. Coordinates are unit-scaled (radius=1) in the
/// entry; the canvas applies a pixel scale around its center.
/// </summary>
public partial class Building2DEditorCanvas : Control
{
    public event Action? ShapeMutated;

    private Building2DEditorModel.ShapeEditEntry? _entry;
    private float _displayRadius = 140f;
    private int _draggingVertex = -1;
    private int _hoverVertex = -1;

    private const float HandleRadius = 7f;
    private const float SlotRadius = 5f;

    public void SetEntry(Building2DEditorModel.ShapeEditEntry? entry)
    {
        _entry = entry;
        _draggingVertex = -1;
        _hoverVertex = -1;
        QueueRedraw();
    }

    public override Vector2 _GetMinimumSize() => new(360, 360);

    public override void _Draw()
    {
        var size = Size;
        Vector2 center = size * 0.5f;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.1f, 0.1f, 0.12f));

        DrawLine(new Vector2(center.X, 0), new Vector2(center.X, size.Y), new Color(0.25f, 0.25f, 0.30f));
        DrawLine(new Vector2(0, center.Y), new Vector2(size.X, center.Y), new Color(0.25f, 0.25f, 0.30f));

        if (_entry == null || _entry.Vertices.Count < 3) return;

        var verts = new Vector2[_entry.Vertices.Count];
        for (int i = 0; i < verts.Length; i++)
            verts[i] = center + _entry.Vertices[i] * _displayRadius;

        bool drawable = Building2DEditorGeometry.IsDrawablePolygon(_entry.Vertices);
        if (drawable)
            DrawColoredPolygon(verts, new Color(0.30f, 0.45f, 0.60f, 0.7f));

        var loop = new Vector2[verts.Length + 1];
        Array.Copy(verts, loop, verts.Length);
        loop[verts.Length] = verts[0];
        Color outlineColor = drawable ? new Color(0, 0, 0, 0.85f) : new Color(0.95f, 0.25f, 0.25f, 0.95f);
        DrawPolyline(loop, outlineColor, 1.5f);

        for (int i = 0; i < _entry.Sides.Count && i < verts.Length; i++)
        {
            var side = _entry.Sides[i];
            int slotCount = side.Slots.Count;
            if (slotCount == 0) continue;
            Vector2 a = verts[i];
            Vector2 b = verts[(i + 1) % verts.Length];
            for (int s = 0; s < slotCount; s++)
            {
                float t = (s + 1f) / (slotCount + 1f);
                Vector2 anchor = a.Lerp(b, t);
                var spec = side.Slots[s];
                Color slotColor = spec.Kind switch
                {
                    ResourceNodeKind.Import => new Color(0.30f, 0.55f, 0.95f),
                    ResourceNodeKind.Export => new Color(0.95f, 0.55f, 0.20f),
                    _ => new Color(0.75f, 0.75f, 0.75f),
                };
                DrawCircle(anchor, SlotRadius, slotColor);
                DrawArc(anchor, SlotRadius, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.85f), 1f);
                if (spec.StateOfMatter == StateOfMatter.Fluid)
                    DrawCircle(anchor, SlotRadius * 0.45f, new Color(1f, 1f, 1f, 0.9f));
            }
        }

        for (int i = 0; i < verts.Length; i++)
        {
            Color handleFill = i == _draggingVertex
                ? new Color(1f, 0.8f, 0.3f)
                : (i == _hoverVertex ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.85f, 0.4f, 0.6f));
            DrawCircle(verts[i], HandleRadius, handleFill);
            DrawArc(verts[i], HandleRadius, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.9f), 1.5f);
            var font = ThemeDB.FallbackFont;
            if (font != null)
            {
                DrawString(font, verts[i] + new Vector2(HandleRadius + 2, -HandleRadius), i.ToString(),
                    HorizontalAlignment.Left, -1, 10, new Color(1, 1, 1, 0.9f));
            }
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_entry == null) return;
        Vector2 center = Size * 0.5f;

        if (@event is InputEventMouseButton mb)
        {
            Vector2 local = mb.Position;
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    int idx = HitTestVertex(local, center);
                    if (idx >= 0)
                    {
                        _draggingVertex = idx;
                        QueueRedraw();
                        AcceptEvent();
                    }
                }
                else
                {
                    if (_draggingVertex >= 0)
                    {
                        _draggingVertex = -1;
                        QueueRedraw();
                        AcceptEvent();
                    }
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                int idx = HitTestVertex(local, center);
                if (idx >= 0 && _entry.Vertices.Count > 3)
                {
                    _entry.Vertices.RemoveAt(idx);
                    _entry.Sides.RemoveAt(idx);
                    _entry.EnsureSideCount();
                    _entry.IsDirty = true;
                    ShapeMutated?.Invoke();
                    QueueRedraw();
                    AcceptEvent();
                }
                else
                {
                    Vector2 unit = (local - center) / _displayRadius;
                    unit = new Vector2(
                        Mathf.Round(unit.X * 100f) / 100f,
                        Mathf.Round(unit.Y * 100f) / 100f);
                    int insertAt = FindClosestEdge(local, center);
                    int n = _entry.Vertices.Count;
                    Vector2 edgeA = _entry.Vertices[insertAt];
                    Vector2 edgeB = _entry.Vertices[(insertAt + 1) % n];
                    float eps2 = Building2DEditorGeometry.DegenerateEpsilon * Building2DEditorGeometry.DegenerateEpsilon;
                    if (unit.DistanceSquaredTo(edgeA) >= eps2 && unit.DistanceSquaredTo(edgeB) >= eps2)
                    {
                        _entry.Vertices.Insert(insertAt + 1, unit);
                        _entry.Sides.Insert(insertAt + 1, new Building2DEditorModel.SideEdit());
                        _entry.EnsureSideCount();
                        _entry.IsDirty = true;
                        ShapeMutated?.Invoke();
                        QueueRedraw();
                    }
                    AcceptEvent();
                }
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            Vector2 local = mm.Position;
            if (_draggingVertex >= 0)
            {
                Vector2 unit = (local - center) / _displayRadius;
                unit = new Vector2(
                    Mathf.Round(unit.X * 100f) / 100f,
                    Mathf.Round(unit.Y * 100f) / 100f);
                if (!CollidesWithNeighbor(_draggingVertex, unit))
                {
                    _entry.Vertices[_draggingVertex] = unit;
                    _entry.IsDirty = true;
                    ShapeMutated?.Invoke();
                    QueueRedraw();
                }
                AcceptEvent();
            }
            else
            {
                int newHover = HitTestVertex(local, center);
                if (newHover != _hoverVertex)
                {
                    _hoverVertex = newHover;
                    QueueRedraw();
                }
            }
        }
    }

    private int HitTestVertex(Vector2 local, Vector2 center)
    {
        if (_entry == null) return -1;
        float bestDist = HandleRadius + 4f;
        int best = -1;
        for (int i = 0; i < _entry.Vertices.Count; i++)
        {
            Vector2 v = center + _entry.Vertices[i] * _displayRadius;
            float d = local.DistanceTo(v);
            if (d <= bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    private bool CollidesWithNeighbor(int vertexIndex, Vector2 unit)
    {
        if (_entry == null) return false;
        int n = _entry.Vertices.Count;
        if (n < 2) return false;
        float eps2 = Building2DEditorGeometry.DegenerateEpsilon * Building2DEditorGeometry.DegenerateEpsilon;
        Vector2 prev = _entry.Vertices[(vertexIndex - 1 + n) % n];
        Vector2 next = _entry.Vertices[(vertexIndex + 1) % n];
        return unit.DistanceSquaredTo(prev) < eps2 || unit.DistanceSquaredTo(next) < eps2;
    }

    private int FindClosestEdge(Vector2 local, Vector2 center)
    {
        if (_entry == null || _entry.Vertices.Count < 2) return 0;
        float bestDist = float.MaxValue;
        int best = 0;
        for (int i = 0; i < _entry.Vertices.Count; i++)
        {
            Vector2 a = center + _entry.Vertices[i] * _displayRadius;
            Vector2 b = center + _entry.Vertices[(i + 1) % _entry.Vertices.Count] * _displayRadius;
            Vector2 mid = (a + b) * 0.5f;
            float d = local.DistanceTo(mid);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }
}
#endif
