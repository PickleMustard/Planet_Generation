using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using Structures.Enums;
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
    public Vector3 Center { get; set; }
    public float Stress { get; set; } = 0.0f;
    public int Increment { get; set; } = 1;

    /// <summary>
    /// The dominant biome type for this cell, calculated from its points.
    /// When points have multiple biomes assigned, this represents the most common biome.
    /// </summary>
    public Biome.BiomeType Biome { get; set; }

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
    public BuildingConstruction? Building { get; set; }

    /// <summary>
    /// Priority order for biome tie-breaking. Higher values indicate higher priority.
    /// Used when multiple biomes have the same count in a cell.
    /// </summary>
    private static readonly Dictionary<Biome.BiomeType, int> BiomePriority = new Dictionary<Biome.BiomeType, int>
    {
        { Structures.Enums.Biome.BiomeType.Mountain, 100 },
        { Structures.Enums.Biome.BiomeType.Forest, 90 },
        { Structures.Enums.Biome.BiomeType.Rainforest, 85 },
        { Structures.Enums.Biome.BiomeType.Taiga, 80 },
        { Structures.Enums.Biome.BiomeType.Grassland, 70 },
        { Structures.Enums.Biome.BiomeType.Coastal, 60 },
        { Structures.Enums.Biome.BiomeType.Tundra, 50 },
        { Structures.Enums.Biome.BiomeType.Desert, 40 },
        { Structures.Enums.Biome.BiomeType.SandDesert, 35 },
        { Structures.Enums.Biome.BiomeType.StoneDesert, 30 },
        { Structures.Enums.Biome.BiomeType.Icecap, 20 },
        { Structures.Enums.Biome.BiomeType.Ocean, 10 },
        { Structures.Enums.Biome.BiomeType.Swamp, 75 },
        { Structures.Enums.Biome.BiomeType.Glacier, 25 },
        { Structures.Enums.Biome.BiomeType.FrozenPlain, 45 },
        { Structures.Enums.Biome.BiomeType.RustedPlain, 55 },
        { Structures.Enums.Biome.BiomeType.RustedMountain, 95 },
        { Structures.Enums.Biome.BiomeType.RustedDesert, 37 },
        { Structures.Enums.Biome.BiomeType.ScouredPlain, 65 },
        { Structures.Enums.Biome.BiomeType.VolcanicPeak, 98 },
        { Structures.Enums.Biome.BiomeType.ObsidianField, 42 },
        { Structures.Enums.Biome.BiomeType.AshPlain, 38 },
        { Structures.Enums.Biome.BiomeType.VolcanicPlain, 32 },
        { Structures.Enums.Biome.BiomeType.LavaOcean, 8 },
        { Structures.Enums.Biome.BiomeType.ShallowOcean, 15 },
        { Structures.Enums.Biome.BiomeType.DeepOcean, 5 },
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
        Biome = Structures.Enums.Biome.BiomeType.Grassland;

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
    /// Calculates and sets the dominant biome for this cell based on the biomes of its points.
    /// Uses majority voting with priority-based tie-breaking.
    /// </summary>
    public void CalculateCellBiome()
    {
        if (Points == null || Points.Length == 0)
        {
            Biome = Structures.Enums.Biome.BiomeType.Grassland;
            return;
        }

        var biomeCounts = new Dictionary<Structures.Enums.Biome.BiomeType, int>();

        foreach (Point p in Points)
        {
            var pointBiome = p.Biome;
            if (!biomeCounts.ContainsKey(pointBiome))
                biomeCounts[pointBiome] = 0;
            biomeCounts[pointBiome]++;
        }

        if (biomeCounts.Count == 0)
        {
            Biome = Structures.Enums.Biome.BiomeType.Grassland;
            return;
        }

        // Find the maximum count
        int maxCount = biomeCounts.Values.Max();

        // Get all biomes with the maximum count
        var topBiomes = biomeCounts.Where(kvp => kvp.Value == maxCount).Select(kvp => kvp.Key).ToList();

        if (topBiomes.Count == 1)
        {
            // Single majority biome
            Biome = topBiomes[0];
        }
        else
        {
            // Tie - use priority order
            Biome = topBiomes.OrderByDescending(b => BiomePriority.GetValueOrDefault(b, 0)).First();
        }
    }

    public override string ToString()
    {
        string output = "";
        output += $"VoronoiCell: ({Index}";
        //foreach(Point p in Points) {
        //  output += $"{p}, ";
        //}
        //foreach(Triangle t in Triangles) {
        //  output += $"{t}, ";
        //}
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
            // Get the three vertices of this triangle
            if (triangle.Points == null || triangle.Points.Count < 3)
                continue;

            Vector3 p0 = triangle.Points[0].Position;
            Vector3 p1 = triangle.Points[1].Position;
            Vector3 p2 = triangle.Points[2].Position;

            // Calculate triangle surface normal
            Vector3 edge1 = p1 - p0;
            Vector3 edge2 = p2 - p0;
            Vector3 normal = edge1.Cross(edge2).Normalized();

            // Calculate radial vector (from assumed planet center at origin to triangle centroid)
            Vector3 centroid = (p0 + p1 + p2) / 3f;
            Vector3 radial = centroid.Normalized();

            // Calculate angle between normal and radial vector
            // Flat terrain = normal parallel to radial (0° slope)
            // Vertical cliff = normal perpendicular to radial (90° slope)
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
