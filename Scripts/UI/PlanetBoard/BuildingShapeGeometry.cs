using System;
using System.Collections.Generic;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// Pure geometry helpers for the 2D PlanetBoard. Maps a YAML shape name to polygon
/// vertices and a <see cref="BuildingSide"/> to the polygon edge that hosts the
/// corresponding port. Vertex 0 sits at angle -π/2 (top), polygons run clockwise,
/// so edge i = segment(v[i], v[(i+1) % N]) and edge 0 is the top edge.
/// </summary>
public static class BuildingShapeGeometry
{
    public const string DefaultShape = "hexagon";

    private static readonly Dictionary<string, int> SideCounts = new()
    {
        { "triangle", 3 },
        { "square", 4 },
        { "rectangle", 4 },
        { "pentagon", 5 },
        { "hexagon", 6 },
    };

    private static readonly Dictionary<(string Shape, BuildingSide Side), int> SideToEdge = new()
    {
        // hexagon: edges run clockwise from top
        { ("hexagon", BuildingSide.Top), 0 },
        { ("hexagon", BuildingSide.East), 1 },
        { ("hexagon", BuildingSide.South), 2 },
        { ("hexagon", BuildingSide.Bottom), 3 },
        { ("hexagon", BuildingSide.West), 4 },
        { ("hexagon", BuildingSide.North), 5 },

        // pentagon
        { ("pentagon", BuildingSide.Top), 0 },
        { ("pentagon", BuildingSide.East), 1 },
        { ("pentagon", BuildingSide.South), 2 },
        { ("pentagon", BuildingSide.West), 3 },
        { ("pentagon", BuildingSide.North), 4 },

        // square
        { ("square", BuildingSide.Top), 0 },
        { ("square", BuildingSide.East), 1 },
        { ("square", BuildingSide.Bottom), 2 },
        { ("square", BuildingSide.West), 3 },

        // rectangle (same edge count + indices as square)
        { ("rectangle", BuildingSide.Top), 0 },
        { ("rectangle", BuildingSide.East), 1 },
        { ("rectangle", BuildingSide.Bottom), 2 },
        { ("rectangle", BuildingSide.West), 3 },

        // triangle
        { ("triangle", BuildingSide.Top), 0 },
        { ("triangle", BuildingSide.East), 1 },
        { ("triangle", BuildingSide.West), 2 },
    };

    private static readonly Dictionary<(string Shape, BuildingSide Side), BuildingSide> Fallbacks =
        new()
        {
            { ("pentagon", BuildingSide.Bottom), BuildingSide.South },
            { ("square", BuildingSide.North), BuildingSide.Top },
            { ("square", BuildingSide.South), BuildingSide.Bottom },
            { ("rectangle", BuildingSide.North), BuildingSide.Top },
            { ("rectangle", BuildingSide.South), BuildingSide.Bottom },
            { ("triangle", BuildingSide.Bottom), BuildingSide.West },
            { ("triangle", BuildingSide.North), BuildingSide.Top },
            { ("triangle", BuildingSide.South), BuildingSide.East },
        };

    public static int GetSideCount(string shape) =>
        SideCounts.TryGetValue(NormalizeShape(shape), out int count) ? count : 6;

    public static string NormalizeShape(string? shape)
    {
        if (string.IsNullOrWhiteSpace(shape))
            return DefaultShape;
        string s = shape.Trim().ToLowerInvariant();
        return SideCounts.ContainsKey(s) ? s : DefaultShape;
    }

    /// <summary>
    /// Returns the polygon vertices in clockwise order. Vertex 0 is the topmost
    /// vertex (angle -π/2 in screen-y-down coordinates). Center is added to each.
    /// Rectangles use a 1.5:1 aspect.
    /// </summary>
    public static Vector2[] GetVertices(string shape, Vector2 center, float radius)
    {
        string norm = NormalizeShape(shape);
        if (norm == "rectangle")
        {
            float w = radius * 1.5f;
            float h = radius;
            return new[]
            {
                center + new Vector2(-w, -h),
                center + new Vector2(w, -h),
                center + new Vector2(w, h),
                center + new Vector2(-w, h),
            };
        }

        int n = SideCounts[norm];
        var verts = new Vector2[n];
        // Square: rotate by π/4 so edges align with up/right/down/left rather than
        // landing on diagonals. All other regular N-gons use the canonical "vertex 0 at top".
        float baseAngle = norm == "square" ? -Mathf.Pi / 2f + Mathf.Pi / 4f : -Mathf.Pi / 2f;
        for (int i = 0; i < n; i++)
        {
            float a = baseAngle + i * Mathf.Tau / n;
            verts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return verts;
    }

    /// <summary>
    /// Returns the index of the polygon edge that hosts the given side's port.
    /// Falls back to the nearest available side if the shape doesn't have a
    /// dedicated edge for that <see cref="BuildingSide"/>; logs a warning the
    /// first time a fallback fires per (shape, side) pair.
    /// </summary>
    public static int GetEdgeIndex(string shape, BuildingSide side)
    {
        string norm = NormalizeShape(shape);
        var key = (norm, side);
        if (SideToEdge.TryGetValue(key, out int edge))
            return edge;

        if (Fallbacks.TryGetValue(key, out BuildingSide fallback))
        {
            WarnFallbackOnce(norm, side, fallback);
            if (SideToEdge.TryGetValue((norm, fallback), out int fallbackEdge))
                return fallbackEdge;
        }

        WarnFallbackOnce(norm, side, BuildingSide.Top);
        return 0;
    }

    /// <summary>Returns the screen-space midpoint of the edge that hosts this side's port.</summary>
    public static Vector2 GetPortAnchor(string shape, BuildingSide side, Vector2 center, float radius)
    {
        var verts = GetVertices(shape, center, radius);
        int edge = GetEdgeIndex(shape, side);
        Vector2 a = verts[edge];
        Vector2 b = verts[(edge + 1) % verts.Length];
        return (a + b) * 0.5f;
    }

    /// <summary>Outward-facing unit normal of the edge that hosts this side's port.</summary>
    public static Vector2 GetPortNormal(string shape, BuildingSide side, Vector2 center, float radius)
    {
        var verts = GetVertices(shape, center, radius);
        int edge = GetEdgeIndex(shape, side);
        Vector2 a = verts[edge];
        Vector2 b = verts[(edge + 1) % verts.Length];
        Vector2 mid = (a + b) * 0.5f;
        return (mid - center).Normalized();
    }

    private static readonly HashSet<(string, BuildingSide)> _warned = new();

    private static void WarnFallbackOnce(string shape, BuildingSide side, BuildingSide fallback)
    {
        var key = (shape, side);
        if (!_warned.Add(key))
            return;
        GameLogger.Warning(
            $"BuildingShapeGeometry: shape '{shape}' has no dedicated edge for BuildingSide.{side}; using {fallback} edge."
        );
    }
}
