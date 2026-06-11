using System.Collections.Generic;
using Godot;

namespace ProceduralGeneration;

/// <summary>
/// One labelled row inside the planet-cartouche stats block.
/// </summary>
public readonly struct CartoucheStat
{
    public readonly string Key;
    public readonly string Value;

    public CartoucheStat(string key, string value)
    {
        Key = key;
        Value = value;
    }
}

/// <summary>
/// Pulls display data for the planetary-overview cartouche from an IOrbitalBody.
/// Walks the scene tree up to the parent Barycenter for system/sector identifiers.
/// </summary>
public static class PlanetCartoucheData
{
    private const string Placeholder = "—";

    /// <summary>
    /// "KP-04 · Sector V" pulled from the parent Barycenter, or empty.
    /// </summary>
    public static string GetDesignation(IOrbitalBody body)
    {
        var bary = FindParentBarycenter(body);
        return bary?.SectorId ?? "";
    }

    /// <summary>
    /// "RockyPlanet · Temperate" or "GasGiant · StandardJupiter".
    /// </summary>
    public static string GetClassification(IOrbitalBody body)
    {
        string type = body.Classification.TypeName;
        string? sub = body.Classification.SubtypeAsObject?.ToString();
        return string.IsNullOrEmpty(sub) ? type : $"{type} · {sub}";
    }

    /// <summary>
    /// Returns the 8 stats grid rows in display order. Missing data points
    /// render as an em-dash so the grid stays balanced.
    /// </summary>
    public static IReadOnlyList<CartoucheStat> GetStatRows(IOrbitalBody body)
    {
        var rows = new List<CartoucheStat>(8);

        rows.Add(new CartoucheStat("MASS", $"{body.Mass:G3} Mₑ"));
        rows.Add(new CartoucheStat("RADIUS", $"{body.Radius:F0} km"));
        rows.Add(new CartoucheStat("GRAVITY", ComputeGravity(body)));
        rows.Add(new CartoucheStat("DAY", Placeholder));
        rows.Add(new CartoucheStat("ATMOS", ComputeAtmosphere(body)));
        rows.Add(new CartoucheStat("SURFACE", ComputeSurface(body)));
        rows.Add(new CartoucheStat("CLIMATE", ComputeClimate(body)));
        rows.Add(new CartoucheStat("SETTLED", Placeholder));

        return rows;
    }

    private static string ComputeAtmosphere(IOrbitalBody body)
        => body.Atmosphere <= 0f ? "None" : $"{body.Atmosphere:0.##} atm";

    private static string ComputeGravity(IOrbitalBody body)
    {
        if (body.Radius <= 0f)
            return Placeholder;
        // Newtonian surface gravity in g-equivalents, with mass already
        // expressed in Earth masses and radius assumed in kilometres.
        const float earthRadiusKm = 6371f;
        float radiiRatio = body.Radius / earthRadiusKm;
        if (radiiRatio <= 0f)
            return Placeholder;
        float g = body.Mass / (radiiRatio * radiiRatio);
        return $"{g:F2} g";
    }

    private static string ComputeSurface(IOrbitalBody body)
    {
        var mesh = body.Mesh;
        if (mesh?.Continents == null || mesh.Continents.Count == 0)
            return Placeholder;
        int cells = 0;
        foreach (var continent in mesh.Continents.Values)
            cells += continent.cells.Count;
        return $"{mesh.Continents.Count}c · {cells} cells";
    }

    private static string ComputeClimate(IOrbitalBody body)
    {
        var sub = body.Classification.SubtypeAsObject?.ToString();
        return string.IsNullOrEmpty(sub) ? Placeholder : sub!;
    }

    private static Barycenter? FindParentBarycenter(IOrbitalBody body)
    {
        if (body is not Node3D node)
            return null;
        Node? current = node;
        while (current != null)
        {
            if (current is Barycenter b)
                return b;
            current = current.GetParentOrNull<Node>();
        }
        return null;
    }
}
