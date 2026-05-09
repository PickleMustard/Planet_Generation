using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Resources;

namespace UI.PlanetBoard;

/// <summary>
/// Computes board-space positions for a planet's buildings. Projects each
/// <see cref="Building.PrimaryCell"/>'s 3D center onto a 2D plane via an
/// equirectangular projection, then runs a few collision-relaxation passes so
/// shapes don't overlap.
/// </summary>
public static class BoardLayoutEngine
{
    private const float ProjectionScale = 600f;
    private const float Padding = 16f;
    private const int RelaxationPasses = 20;

    public sealed class Layout
    {
        public Dictionary<Building, Vector2> Positions { get; } = new();
        public Rect2 BoundingBox { get; set; } = new Rect2(Vector2.Zero, Vector2.One);
        public float ShapeRadius { get; set; } = 64f;
    }

    public static Layout Compute(IReadOnlyList<Building> buildings, float defaultRadius = 64f)
    {
        var layout = new Layout { ShapeRadius = defaultRadius };
        if (buildings == null || buildings.Count == 0)
        {
            layout.BoundingBox = new Rect2(Vector2.Zero, new Vector2(1, 1));
            return layout;
        }

        // First pass: equirectangular projection. Buildings without a placed
        // PrimaryCell go in a fallback row below the main bbox.
        var positions = new Dictionary<Building, Vector2>();
        var unplaced = new List<Building>();
        float maxRadius = 0f;
        foreach (var b in buildings)
        {
            float r = ResolveRadius(b, defaultRadius);
            if (r > maxRadius)
                maxRadius = r;
            if (b.PrimaryCell == null)
            {
                unplaced.Add(b);
                continue;
            }
            positions[b] = Project(b.PrimaryCell.Center);
        }

        if (positions.Count > 0)
        {
            for (int pass = 0; pass < RelaxationPasses; pass++)
            {
                if (!RelaxOnce(positions, maxRadius))
                    break;
            }
        }

        if (unplaced.Count > 0)
        {
            float startX = positions.Count > 0 ? GetMin(positions).X : 0f;
            float yRow = positions.Count > 0 ? GetMax(positions).Y + (maxRadius * 2f + Padding) : 0f;
            float step = maxRadius * 2f + Padding;
            for (int i = 0; i < unplaced.Count; i++)
                positions[unplaced[i]] = new Vector2(startX + i * step, yRow);
        }

        layout.ShapeRadius = maxRadius > 0 ? maxRadius : defaultRadius;
        foreach (var kv in positions)
            layout.Positions[kv.Key] = kv.Value;
        layout.BoundingBox = ComputeBBox(positions, layout.ShapeRadius);
        return layout;
    }

    private static Vector2 Project(Vector3 center)
    {
        Vector3 c = center.LengthSquared() > 1e-8f ? center.Normalized() : Vector3.Forward;
        float u = Mathf.Atan2(c.Z, c.X);
        float v = Mathf.Asin(Mathf.Clamp(c.Y, -1f, 1f));
        return new Vector2(u, v) * ProjectionScale;
    }

    private static float ResolveRadius(Building b, float fallback)
    {
        var def = b.Definition;
        if (def != null && def.Visual != null && def.Visual.ShapeSize > 0f)
            return def.Visual.ShapeSize;
        return fallback;
    }

    private static bool RelaxOnce(Dictionary<Building, Vector2> positions, float radius)
    {
        bool moved = false;
        float minDist = radius * 2f + Padding;
        var keys = new Building[positions.Count];
        positions.Keys.CopyTo(keys, 0);
        for (int i = 0; i < keys.Length; i++)
        {
            for (int j = i + 1; j < keys.Length; j++)
            {
                Vector2 a = positions[keys[i]];
                Vector2 b = positions[keys[j]];
                Vector2 delta = b - a;
                float d = delta.Length();
                if (d >= minDist)
                    continue;
                Vector2 push = d > 1e-4f ? delta / d : new Vector2(1, 0);
                float overlap = (minDist - d) * 0.5f + 0.5f;
                positions[keys[i]] = a - push * overlap;
                positions[keys[j]] = b + push * overlap;
                moved = true;
            }
        }
        return moved;
    }

    private static Rect2 ComputeBBox(Dictionary<Building, Vector2> positions, float radius)
    {
        if (positions.Count == 0)
            return new Rect2(Vector2.Zero, new Vector2(1, 1));
        Vector2 min = GetMin(positions);
        Vector2 max = GetMax(positions);
        Vector2 pad = new(radius + Padding, radius + Padding);
        return new Rect2(min - pad, (max - min) + pad * 2f);
    }

    private static Vector2 GetMin(Dictionary<Building, Vector2> positions)
    {
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        foreach (var v in positions.Values)
        {
            if (v.X < min.X) min.X = v.X;
            if (v.Y < min.Y) min.Y = v.Y;
        }
        return min;
    }

    private static Vector2 GetMax(Dictionary<Building, Vector2> positions)
    {
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var v in positions.Values)
        {
            if (v.X > max.X) max.X = v.X;
            if (v.Y > max.Y) max.Y = v.Y;
        }
        return max;
    }
}
