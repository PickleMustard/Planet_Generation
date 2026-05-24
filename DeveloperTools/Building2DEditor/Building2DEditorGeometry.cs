#if DEBUG
using System.Collections.Generic;
using Godot;

namespace DeveloperTools.Building2DEditor;

internal static class Building2DEditorGeometry
{
    public const float DegenerateEpsilon = 1e-4f;

    public static bool IsDrawablePolygon(IReadOnlyList<Vector2> verts)
    {
        if (verts.Count < 3) return false;
        for (int i = 0; i < verts.Count; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % verts.Count];
            if (a.DistanceSquaredTo(b) < DegenerateEpsilon * DegenerateEpsilon) return false;
        }
        return Mathf.Abs(SignedArea(verts)) > DegenerateEpsilon;
    }

    public static float SignedArea(IReadOnlyList<Vector2> verts)
    {
        float sum = 0f;
        for (int i = 0; i < verts.Count; i++)
        {
            var a = verts[i];
            var b = verts[(i + 1) % verts.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum * 0.5f;
    }
}
#endif
