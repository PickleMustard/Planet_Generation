using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

public class BuildingShape2D
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Vector2[] Vertices { get; set; } = System.Array.Empty<Vector2>();
    public List<SideSpec> Sides { get; set; } = new();

    public int VertexCount => Vertices.Length;
    public int SideCount => Vertices.Length;

    public class SideSpec
    {
        public List<SlotSpec> Slots { get; set; } = new();
    }

    public class SlotSpec
    {
        public ResourceNodeKind Kind { get; set; }
        public StateOfMatter StateOfMatter { get; set; }
    }

    public (Vector2 a, Vector2 b) GetEdge(int sideIndex, Vector2 center, float radius)
    {
        int n = Vertices.Length;
        Vector2 a = center + Vertices[sideIndex] * radius;
        Vector2 b = center + Vertices[(sideIndex + 1) % n] * radius;
        return (a, b);
    }

    public Vector2[] GetScaledVertices(Vector2 center, float radius)
    {
        int n = Vertices.Length;
        var verts = new Vector2[n];
        for (int i = 0; i < n; i++)
            verts[i] = center + Vertices[i] * radius;
        return verts;
    }

    /// <summary>Anchor position of slot <paramref name="slotIndex"/> on side
    /// <paramref name="sideIndex"/>. Slots evenly distributed: t = (i+1)/(N+1).</summary>
    public Vector2 GetSlotAnchor(int sideIndex, int slotIndex, Vector2 center, float radius)
    {
        var (a, b) = GetEdge(sideIndex, center, radius);
        int slotCount = sideIndex >= 0 && sideIndex < Sides.Count ? Sides[sideIndex].Slots.Count : 1;
        if (slotCount <= 0) slotCount = 1;
        float t = (slotIndex + 1f) / (slotCount + 1f);
        return a.Lerp(b, t);
    }

    /// <summary>Outward unit normal at the given slot's anchor.</summary>
    public Vector2 GetSlotNormal(int sideIndex, int slotIndex, Vector2 center, float radius)
    {
        Vector2 anchor = GetSlotAnchor(sideIndex, slotIndex, center, radius);
        Vector2 outward = anchor - center;
        if (outward.LengthSquared() < 1e-6f)
        {
            var (a, b) = GetEdge(sideIndex, center, radius);
            Vector2 edge = b - a;
            outward = new Vector2(edge.Y, -edge.X);
        }
        return outward.Normalized();
    }
}
