using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;

namespace UI.PlanetBoard;

/// <summary>
/// Computes board-space positions for a planet's buildings using Vogel's
/// sunflower (phyllotaxis) packing. The first building sits at the origin
/// and subsequent buildings spiral outward at the golden angle, producing
/// uniform density across the disk regardless of count. Incremental adds
/// simply append at the next sunflower index, so existing positions stay
/// stable. The bounding disk radius is a continuous function of count.
/// </summary>
public static class BoardLayoutEngine
{
    /// <summary>Extra gap (board-space pixels) added to shape diameter to derive nearest-neighbor spacing.</summary>
    private const float Padding = 24f;

    /// <summary>Multiplier on shape diameter for the minimum disk radius (single-building case).</summary>
    private const float MinRadiusDiameterMultiplier = 2f;

    /// <summary>Rotation offset applied to every sunflower index so the spiral has a consistent orientation.</summary>
    private const float StartAngle = -Mathf.Pi / 2f;

    /// <summary>Golden angle in radians: π × (3 − √5) ≈ 137.5°.</summary>
    private static readonly float GoldenAngleRad = Mathf.Pi * (3f - Mathf.Sqrt(5f));

    /// <summary>
    /// Result of a full layout computation or incremental placement.
    /// </summary>
    public sealed class Layout
    {
        public Dictionary<Building, Vector2> Positions { get; } = new();
        public Rect2 BoundingBox { get; set; } = new Rect2(Vector2.Zero, Vector2.One);
        public float ShapeRadius { get; set; } = 64f;
        public float CircleRadius { get; set; }
    }

    /// <summary>
    /// Result of an incremental single-building placement via
    /// <see cref="ComputePositionForNew"/>.
    /// </summary>
    public sealed class IncrementalResult
    {
        /// <summary>Position for the newly added building.</summary>
        public Vector2 NewPosition;

        /// <summary>The disk radius after placement.</summary>
        public float NewCircleRadius;

        /// <summary>
        /// Updated positions for existing buildings that moved. Empty for
        /// sunflower placement — indices are stable and old positions are
        /// preserved.
        /// </summary>
        public Dictionary<Building, Vector2> UpdatedPositions = new();
    }

    // ── Full layout computation ────────────────────────────────────────

    /// <summary>
    /// Computes sunflower positions for all buildings. Index 0 sits at the
    /// origin; subsequent indices spiral outward at the golden angle.
    /// </summary>
    public static Layout Compute(IReadOnlyList<Building> buildings, float defaultRadius = 64f)
    {
        var layout = new Layout { ShapeRadius = defaultRadius };
        if (buildings == null || buildings.Count == 0)
        {
            layout.BoundingBox = new Rect2(Vector2.Zero, new Vector2(1, 1));
            return layout;
        }

        float maxRadius = ResolveMaxRadius(buildings, defaultRadius);
        float shapeDiameter = maxRadius * 2f;
        layout.ShapeRadius = maxRadius;

        int count = buildings.Count;
        float c = ComputePackingSpacing(shapeDiameter);
        layout.CircleRadius = ComputeCircleRadius(count, shapeDiameter);

        for (int i = 0; i < count; i++)
            layout.Positions[buildings[i]] = SunflowerPosition(i, c, StartAngle);

        layout.BoundingBox = ComputeBBox(layout.Positions, maxRadius);
        return layout;
    }

    // ── Incremental single-building placement ──────────────────────────

    /// <summary>
    /// Computes a position for a single new building. The new building is
    /// appended at the next sunflower index, so existing positions are not
    /// disturbed. The disk radius is recomputed continuously.
    /// </summary>
    /// <param name="newBuilding">The building to place.</param>
    /// <param name="existingBuildings">Existing buildings in order.</param>
    /// <param name="existingPositions">Current board-space positions of existing buildings (unused; preserved for API compatibility).</param>
    /// <param name="currentCircleRadius">Current disk radius (unused; preserved for API compatibility).</param>
    /// <param name="defaultRadius">Fallback shape radius.</param>
    public static IncrementalResult ComputePositionForNew(
        Building newBuilding,
        IReadOnlyList<Building> existingBuildings,
        Dictionary<Building, Vector2> existingPositions,
        float currentCircleRadius,
        float defaultRadius = 64f)
    {
        var result = new IncrementalResult();

        float newRadius = ResolveRadius(newBuilding, defaultRadius);
        float maxExistingRadius = 0f;
        if (existingBuildings != null)
        {
            foreach (var b in existingBuildings)
            {
                float r = ResolveRadius(b, defaultRadius);
                if (r > maxExistingRadius)
                    maxExistingRadius = r;
            }
        }
        float effectiveMax = Mathf.Max(newRadius, maxExistingRadius);
        float shapeDiameter = effectiveMax * 2f;

        int newIndex = existingBuildings?.Count ?? 0;
        int totalCount = newIndex + 1;
        float c = ComputePackingSpacing(shapeDiameter);

        result.NewPosition = SunflowerPosition(newIndex, c, StartAngle);
        result.NewCircleRadius = ComputeCircleRadius(totalCount, shapeDiameter);
        // UpdatedPositions intentionally left empty: sunflower indices are
        // stable, so previously-placed buildings stay where they were.
        return result;
    }

    // ── Sunflower geometry ────────────────────────────────────────────

    /// <summary>
    /// Nearest-neighbor packing constant for Vogel's spiral. With
    /// <c>r_i = c·√i</c>, adjacent spiral neighbors sit roughly <c>c</c>
    /// apart. Choosing <c>c = shapeDiameter + Padding</c> guarantees a
    /// collision-free minimum spacing.
    /// </summary>
    public static float ComputePackingSpacing(float shapeDiameter) => shapeDiameter + Padding;

    /// <summary>
    /// Vogel sunflower position for the <paramref name="i"/>-th item
    /// (0-indexed). Index 0 returns the origin; subsequent indices spiral
    /// outward at the golden angle.
    /// </summary>
    public static Vector2 SunflowerPosition(int i, float c, float rotationOffset)
    {
        if (i <= 0)
            return Vector2.Zero;
        float r = c * Mathf.Sqrt(i);
        float theta = i * GoldenAngleRad + rotationOffset;
        return new Vector2(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r);
    }

    // ── Disk radius computation ───────────────────────────────────────

    /// <summary>
    /// Continuous, monotonic disk radius required to contain
    /// <paramref name="count"/> sunflower-packed shapes of the given
    /// <paramref name="shapeDiameter"/>.
    /// </summary>
    public static float ComputeCircleRadius(int count, float shapeDiameter)
    {
        if (count <= 0)
            return 0f;

        float minRadius = shapeDiameter * MinRadiusDiameterMultiplier;
        if (count == 1)
            return minRadius;

        float c = ComputePackingSpacing(shapeDiameter);
        float diskRadius = c * Mathf.Sqrt(count - 1);
        // Pad by half a shape edge plus the global padding so the outermost
        // shape fits entirely inside the disk.
        float padded = diskRadius + shapeDiameter * 0.5f + Padding;
        return Mathf.Max(padded, minRadius);
    }

    // ── Station outer-ring placement ───────────────────────────────────

    /// <summary>Extra gap (board-space pixels) between the buildings disk and the station ring.</summary>
    public const float StationRingGap = 48f;

    /// <summary>Default circumradius for a station diamond in board-space pixels.</summary>
    public const float DefaultStationRadius = 36f;

    /// <summary>
    /// Radius at which the innermost station ring's centers sit.
    /// </summary>
    public static float ComputeStationRingRadius(float innerCircleRadius, float stationRadius = DefaultStationRadius)
        => innerCircleRadius + StationRingGap + stationRadius;

    /// <summary>Radial gap (board-space pixels) between successive station rings.</summary>
    public const float RingSpacing = 2f * DefaultStationRadius + StationRingGap;

    /// <summary>
    /// Result of <see cref="ComputeStationRings"/>: each station's board position plus the
    /// distinct ring radii (one per orbit band present), sorted from innermost to outermost.
    /// </summary>
    public sealed class StationRingLayout
    {
        public Dictionary<StationSatellite, Vector2> Positions { get; } = new();
        public List<float> RingRadii { get; } = new();
    }

    /// <summary>
    /// Places stations on concentric rings grouped by orbit band (<see cref="StationSatellite.BandIndex"/>).
    /// Distinct bands are sorted ascending (distance order) and mapped to ring ordinals 0,1,2…;
    /// ring <c>k</c> sits at <c>ComputeStationRingRadius + k · RingSpacing</c>. Stations sharing a
    /// band are spread evenly around their ring at angle <c>StartAngle + i · 2π / N</c>.
    /// </summary>
    public static StationRingLayout ComputeStationRings(
        IReadOnlyList<StationSatellite> stations,
        float innerCircleRadius,
        float stationRadius = DefaultStationRadius)
    {
        var layout = new StationRingLayout();
        if (stations == null || stations.Count == 0)
            return layout;

        // Group stations by band, preserving a stable per-band order.
        var byBand = new Dictionary<int, List<StationSatellite>>();
        foreach (var s in stations)
        {
            if (!byBand.TryGetValue(s.BandIndex, out var list))
            {
                list = new List<StationSatellite>();
                byBand[s.BandIndex] = list;
            }
            list.Add(s);
        }

        float baseRadius = ComputeStationRingRadius(innerCircleRadius, stationRadius);
        var bands = byBand.Keys.ToList();
        bands.Sort();

        for (int k = 0; k < bands.Count; k++)
        {
            float ringR = baseRadius + k * RingSpacing;
            layout.RingRadii.Add(ringR);

            var bandStations = byBand[bands[k]];
            float step = Mathf.Tau / bandStations.Count;
            for (int i = 0; i < bandStations.Count; i++)
            {
                float angle = StartAngle + i * step;
                layout.Positions[bandStations[i]] = AngleToPosition(angle, ringR);
            }
        }

        return layout;
    }

    // ── Geometry helpers ──────────────────────────────────────────────

    /// <summary>
    /// Converts an angle (radians, 0 = right, -π/2 = top) to a position
    /// on the circle of the given radius centered at the origin.
    /// </summary>
    public static Vector2 AngleToPosition(float angle, float radius)
    {
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    /// <summary>
    /// Extracts the angle of a position relative to the origin.
    /// </summary>
    public static float PositionToAngle(Vector2 position)
    {
        if (position.LengthSquared() < 1e-8f)
            return StartAngle;
        return Mathf.Atan2(position.Y, position.X);
    }

    /// <summary>
    /// Positive angular difference from <paramref name="from"/> to
    /// <paramref name="to"/>, in [0, 2π).
    /// </summary>
    public static float AngleDiff(float to, float from)
    {
        float diff = to - from;
        while (diff < 0f) diff += Mathf.Tau;
        while (diff >= Mathf.Tau) diff -= Mathf.Tau;
        return diff;
    }

    // ── Private helpers ───────────────────────────────────────────────

    private static float ResolveRadius(Building b, float fallback)
    {
        var def = b.Definition;
        if (def != null && def.Visual != null && def.Visual.ShapeSize > 0f)
            return def.Visual.ShapeSize;
        return fallback;
    }

    private static float ResolveMaxRadius(IReadOnlyList<Building> buildings, float fallback)
    {
        float max = 0f;
        foreach (var b in buildings)
        {
            float r = ResolveRadius(b, fallback);
            if (r > max)
                max = r;
        }
        return max > 0f ? max : fallback;
    }

    private static Rect2 ComputeBBox(Dictionary<Building, Vector2> positions, float radius)
    {
        if (positions.Count == 0)
            return new Rect2(Vector2.Zero, new Vector2(1, 1));
        Vector2 min = GetMin(positions);
        Vector2 max = GetMax(positions);
        Vector2 pad = new(radius + 16f, radius + 16f);
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
