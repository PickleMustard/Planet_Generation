using System;
using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using Structures.MeshGeneration;

namespace Structures.GameState;

public partial class VoronoiCell : Resource, IVoronoiCell
{
    public Point[] Points { get; set; }
    public Triangle[] Triangles { get; set; }
    public Edge[] Edges { get; set; }
    public Edge[]? OutsideEdges { get; set; } //Edges that lie on the border of a continent
    public Aabb BoundingBox { get; set; }
    public int Index { get; set; }
    public int ContinentIndex { get; set; }
    public bool IsBorderTile { get; set; }
    public int[] BoundingContinentIndex { get; set; }
    public int Interiorness { get; set; } = int.MaxValue;
    public Dictionary<Edge, int> EdgeBoundaryMap { get; set; }
    public Vector2 MovementDirection { get; set; }
    public float Height { get; set; }

    /// <summary>
    /// Per-body normalized height in [0, 1]. 0 = lowest cell on this body, 1 = highest.
    /// Computed once after continent heights are finalized; YAML placement constraints
    /// (min_elevation/max_elevation) compare against this, not the raw world-unit Height.
    /// </summary>
    public float NormalizedHeight { get; set; }
    public Vector3 Center { get; set; }
    public float Stress { get; set; } = 0.0f;
    public int Increment { get; set; } = 1;

    /// <summary>
    /// Default biome ID used for unassigned cells.
    /// </summary>
    public const string DefaultBiomeId = "biome_grassland";

    /// <summary>
    /// The dominant biome ID for this cell, calculated from its points.
    /// When points have multiple biomes assigned, this represents the most common biome
    /// (with priority-based tie-breaking).
    /// </summary>
    public string Biome { get; set; } = DefaultBiomeId;

    /// <summary>
    /// Cached slope value in degrees. Calculated on first access.
    /// </summary>
    private float? _cachedSlope;

    /// <summary>
    /// Resources available in this cell.
    /// Key is the resource ID, value is the abundance (0-1).
    /// </summary>
    public Dictionary<string, float> Resources { get; set; } = new();

    /// <summary>
    /// The building constructed on this cell, if any.
    /// </summary>
    public Building? Building { get; set; }

    /// <summary>
    /// True if a geothermal vent was placed on this cell during procedural
    /// generation. Required for geothermal plant placement. Distribution favors
    /// deep-ocean cells and divergent tectonic boundaries; penalized at
    /// convergent boundaries and on mountain / plain terrain.
    /// </summary>
    public bool HasGeothermalVent { get; set; } = false;

    /// <summary>
    /// Priority order for biome tie-breaking. Higher values indicate higher priority.
    /// Used when multiple biomes have the same count in a cell.
    /// </summary>
    private static readonly Dictionary<string, int> BiomePriority = new(StringComparer.Ordinal)
    {
        { "biome_mountain", 100 },
        { "biome_volcanic_peak", 98 },
        { "biome_rusted_mountain", 95 },
        { "biome_forest", 90 },
        { "biome_rainforest", 85 },
        { "biome_taiga", 80 },
        { "biome_swamp", 75 },
        { "biome_grassland", 70 },
        { "biome_scoured_plain", 65 },
        { "biome_coastal", 60 },
        { "biome_rusted_plain", 55 },
        { "biome_tundra", 50 },
        { "biome_frozen_plain", 45 },
        { "biome_obsidian_field", 42 },
        { "biome_desert", 40 },
        { "biome_ash_plain", 38 },
        { "biome_rusted_desert", 37 },
        { "biome_sand_desert", 35 },
        { "biome_volcanic_plain", 32 },
        { "biome_stone_desert", 30 },
        { "biome_glacier", 25 },
        { "biome_icecap", 20 },
        { "biome_shallow_ocean", 15 },
        { "biome_ocean", 10 },
        { "biome_lava_ocean", 8 },
        { "biome_deep_ocean", 5 },
    };

    public VoronoiCell(int triangleIndex, Point[] points, Triangle[] triangles, Edge[] edges)
    {
        Triangles = triangles;
        Points = points;
        Edges = edges;
        Index = triangleIndex;
        ContinentIndex = -1;
        BoundingContinentIndex = new int[] { };
        IsBorderTile = false;
        EdgeBoundaryMap = new Dictionary<Edge, int>();
        Biome = DefaultBiomeId;

        Vector3 center = new Vector3(0, 0, 0);
        foreach (Point p in Points)
        {
            center += p.Position;
        }
        center /= Points.Length;
        Center = center;
    }

    public void GenerateBoundingBox()
    {
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        Vector3 center = new Vector3(0, 0, 0);
        float height = 0f;
        foreach (Point p in Points)
        {
            center += p.Position;
            height += (p.Height / 10f);
            minX = Mathf.Min(minX, p.Position.X);
            minY = Mathf.Min(minY, p.Position.Y);
            minZ = Mathf.Min(minZ, p.Position.Z);
            maxX = Mathf.Max(maxX, p.Position.X);
            maxY = Mathf.Max(maxY, p.Position.Y);
            maxZ = Mathf.Max(maxZ, p.Position.Z);
        }
        center /= Points.Length;
        height /= Points.Length;
        Center = center;
        Height = height;
        Vector3 min = new Vector3(minX, minY, minZ);
        Vector3 max = new Vector3(maxX, maxY, maxZ);
        Vector3 extents = (max - min) * 1.1f;
        Vector3 expandedMin = min - (extents - (max - min)) / 2f;
        BoundingBox = new Aabb(expandedMin, extents).Abs();
    }

    /// <summary>
    /// Calculates and sets the dominant biome ID for this cell based on the biomes of its points.
    /// Uses majority voting with priority-based tie-breaking.
    /// </summary>
    public void CalculateCellBiome()
    {
        if (Points == null || Points.Length == 0)
        {
            Biome = DefaultBiomeId;
            return;
        }

        var biomeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (Point p in Points)
        {
            var pointBiome = p.Biome ?? DefaultBiomeId;
            if (!biomeCounts.ContainsKey(pointBiome))
                biomeCounts[pointBiome] = 0;
            biomeCounts[pointBiome]++;
        }

        if (biomeCounts.Count == 0)
        {
            Biome = DefaultBiomeId;
            return;
        }

        int maxCount = biomeCounts.Values.Max();
        var topBiomes = biomeCounts.Where(kvp => kvp.Value == maxCount).Select(kvp => kvp.Key).ToList();

        Biome = topBiomes.Count == 1
            ? topBiomes[0]
            : topBiomes.OrderByDescending(b => BiomePriority.GetValueOrDefault(b, 0)).First();
    }

    public override string ToString()
    {
        string output = "";
        output += $"VoronoiCell: ({Index}";
        output += ")";
        output += $"{BoundingBox}, ";
        output += $", {Points.Length}# Points, {Edges.Length}# Edges, {Triangles.Length}# Triangles.";
        output += $"Part of: {ContinentIndex}, Height: {Height}, Biome: {Biome}";

        return output;
    }

    /// <summary>
    /// Calculates the average slope of this cell in degrees.
    /// Slope is determined by the angle between each triangle's surface normal
    /// and the vector from the cell center to the planet center (assumed at origin).
    /// </summary>
    /// <returns>Slope angle in degrees (0-90)</returns>
    public float GetSlope()
    {
        if (_cachedSlope.HasValue)
            return _cachedSlope.Value;

        if (Triangles == null || Triangles.Length == 0 || Points == null || Points.Length < 3)
        {
            _cachedSlope = 0f;
            return 0f;
        }

        float totalSlope = 0f;
        int validTriangles = 0;

        foreach (var triangle in Triangles)
        {
            if (triangle.Points == null || triangle.Points.Count < 3)
                continue;

            Vector3 p0 = triangle.Points[0].Position;
            Vector3 p1 = triangle.Points[1].Position;
            Vector3 p2 = triangle.Points[2].Position;

            Vector3 edge1 = p1 - p0;
            Vector3 edge2 = p2 - p0;
            Vector3 normal = edge1.Cross(edge2).Normalized();

            Vector3 centroid = (p0 + p1 + p2) / 3f;
            Vector3 radial = centroid.Normalized();

            float dot = Mathf.Abs(normal.Dot(radial));
            float angleRad = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
            float angleDeg = Mathf.RadToDeg(angleRad);

            totalSlope += angleDeg;
            validTriangles++;
        }

        _cachedSlope = validTriangles > 0 ? totalSlope / validTriangles : 0f;
        return _cachedSlope.Value;
    }

    /// <summary>
    /// Invalidates the cached slope value. Call this if the cell's geometry changes.
    /// </summary>
    public void InvalidateSlopeCache()
    {
        _cachedSlope = null;
    }
}
