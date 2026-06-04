using System.Collections.Generic;
using Godot;

namespace UtilityLibrary.SaveLoad.Dto;

/// <summary>
/// Plain POCO data-transfer objects for the YAML save format. These intentionally hold no Godot
/// node references and no behaviour — only serializable scalars and nested DTOs. Godot vectors are
/// represented as <see cref="Vec3Dto"/>/<see cref="Vec2Dto"/> and ulong seeds as strings so the
/// default YamlDotNet POCO (de)serializer needs no custom converters.
/// </summary>
public sealed class Vec3Dto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vec3Dto() { }

    public Vec3Dto(Vector3 v)
    {
        X = v.X;
        Y = v.Y;
        Z = v.Z;
    }

    public Vector3 ToVector3() => new Vector3(X, Y, Z);

    public static Vec3Dto From(Vector3 v) => new Vec3Dto(v);
}

public sealed class Vec2Dto
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vec2Dto() { }

    public Vec2Dto(Vector2 v)
    {
        X = v.X;
        Y = v.Y;
    }

    public Vector2 ToVector2() => new Vector2(X, Y);

    public static Vec2Dto From(Vector2 v) => new Vec2Dto(v);
}

/// <summary>Key/value pair used in place of non-string-keyed dictionaries for stable YAML emission.</summary>
public sealed class KvIntIntDto
{
    public int Key { get; set; }
    public int Value { get; set; }
}

public sealed class KvIntFloatDto
{
    public int Key { get; set; }
    public float Value { get; set; }
}

public sealed class KvIntStringDto
{
    public int Key { get; set; }
    public string Value { get; set; } = "";
}

public sealed class SaveFileDto
{
    public MetaDto Meta { get; set; } = new();
    public SessionDto Session { get; set; } = new();
    public CompanyDto Company { get; set; } = new();
    public EconomyDto Economy { get; set; } = new();

    /// <summary>Game-instance clock (save_version ≥ 6). Defaults to tick 0 for older saves.</summary>
    public TimeDto Time { get; set; } = new();

    public List<BodyDto> Bodies { get; set; } = new();

    /// <summary>Logistics units (save_version ≥ 2). Null/omitted in v1 saves.</summary>
    public List<LogisticsUnitDto>? LogisticsUnits { get; set; }

    /// <summary>Placed buildings (save_version ≥ 3). Null/omitted in earlier saves.</summary>
    public List<BuildingDto>? Buildings { get; set; }

    /// <summary>Orbital stations (save_version ≥ 3). Null/omitted in earlier saves.</summary>
    public List<StationDto>? Stations { get; set; }
}

public sealed class MetaDto
{
    public int SaveVersion { get; set; }
    public string GameVersion { get; set; } = "";
    public string CreatedUtc { get; set; } = "";
    public string TemplateName { get; set; } = "";
}

public sealed class SessionDto
{
    public string SystemName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public bool IsGameStarted { get; set; }
    public BarycenterDto Barycenter { get; set; } = new();
}

public sealed class BarycenterDto
{
    public string SystemName { get; set; } = "";
    public string SectorId { get; set; } = "";
    public Vec3Dto Position { get; set; } = new();
    public Vec3Dto Velocity { get; set; } = new();
    public float Mass { get; set; }
}

public sealed class CompanyDto
{
    public double Budget { get; set; }
    public double Debt { get; set; }
    public float Antagonism { get; set; }
    public double Research { get; set; }

    /// <summary>Budget at the start of the current quarter (save_version ≥ 7). Defaults to 0 on older saves.</summary>
    public double QuarterOpeningBudget { get; set; }

    /// <summary>Net Budget change over the last closed quarter (save_version ≥ 7). Defaults to 0 on older saves.</summary>
    public double LastQuarterRevenue { get; set; }
}

public sealed class TimeDto
{
    /// <summary>Total manufacture ticks elapsed since the game instance began. Quarter/year derive from this.</summary>
    public long TotalTicks { get; set; }
}

public sealed class EconomyDto
{
    public float PriceUpdateInterval { get; set; }
    public List<MarketEntryDto> Market { get; set; } = new();
}

public sealed class MarketEntryDto
{
    public string Id { get; set; } = "";
    public double BasePrice { get; set; }
    public double CurrentPrice { get; set; }
    // Supply/demand pressures are transient (reset each price tick) and intentionally not saved.
}

public sealed class BodyDto
{
    /// <summary>"Celestial" or "Satellite".</summary>
    public string Kind { get; set; } = "Celestial";

    /// <summary>Node name — the cross-reference key used to resolve OrbitalParent on load.</summary>
    public string Name { get; set; } = "";

    /// <summary>Body-type name (round-tripped via BodyClassification.FromType / FromSatelliteType).</summary>
    public string Classification { get; set; } = "";

    public float Mass { get; set; }
    public float Radius { get; set; }

    /// <summary>ulong BodySeed serialized as string to avoid YAML integer-width ambiguity.</summary>
    public string BodySeed { get; set; } = "0";

    public float Atmosphere { get; set; }

    /// <summary>Local position under system_container (Celestial) or under the parent body (Satellite).</summary>
    public Vec3Dto Position { get; set; } = new();
    public Vec3Dto Velocity { get; set; } = new();
    public Vec3Dto TotalForce { get; set; } = new();
    public Vec3Dto SavedForce { get; set; } = new();

    public string? OrbitalParentName { get; set; }

    // Satellite analytic-orbit state (defensive; re-derived from position on first frame).
    public float OrbitalAngle { get; set; }
    public float OrbitalRadius { get; set; }
    public float OrbitalSpeed { get; set; }
    public bool OrbitalInitialized { get; set; }

    public List<KvIntIntDto> BandSatelliteCounts { get; set; } = new();

    /// <summary>Body-level resource deposits (satellites only); null/empty for planets.</summary>
    public List<ResourceDepositDto>? SatelliteResources { get; set; }

    public GeometryDto Geometry { get; set; } = new();
}

public sealed class ResourceDepositDto
{
    public string ResourceId { get; set; } = "";
    public float Abundance { get; set; }
    public float Accessibility { get; set; }
}

public sealed class GeometryDto
{
    public float Size { get; set; }
    public float MaxHeight { get; set; }
    public string GenerationType { get; set; } = "";
    public bool ProjectToSphere { get; set; }
    public bool UseCellBiomeForColoring { get; set; }
    public string Classification { get; set; } = "";
    public List<PointDto> Points { get; set; } = new();
    public List<CellDto> Cells { get; set; } = new();
    public List<ContinentDto> Continents { get; set; } = new();
}

public sealed class PointDto
{
    public int Index { get; set; }
    public Vec3Dto Position { get; set; } = new();
    public float Height { get; set; }
    public Vec3Dto Velocity { get; set; } = new();
    public float Stress { get; set; }
    public string Biome { get; set; } = "";
    public bool IsOnContinentBorder { get; set; }
    public float Radius { get; set; }
    public List<int> ContinentIndices { get; set; } = new();
}

public sealed class CellDto
{
    public int Index { get; set; }
    public int ContinentIndex { get; set; }
    public bool IsBorderTile { get; set; }
    public int Interiorness { get; set; }
    public int[] BoundingContinentIndex { get; set; } = System.Array.Empty<int>();
    public Vec3Dto Center { get; set; } = new();
    public float Height { get; set; }
    public float NormalizedHeight { get; set; }
    public float Stress { get; set; }
    public string Biome { get; set; } = "";
    public Vec2Dto MovementDirection { get; set; } = new();
    public bool HasGeothermalVent { get; set; }
    public Dictionary<string, float> Resources { get; set; } = new();

    /// <summary>Ordered point indices — MUST match VoronoiCell.Points[] order (triangle triples).</summary>
    public int[] PointIndices { get; set; } = System.Array.Empty<int>();
}

public sealed class ContinentDto
{
    public int StartingIndex { get; set; }
    public int[] CellIndices { get; set; } = System.Array.Empty<int>();
    public int[] BoundaryCellIndices { get; set; } = System.Array.Empty<int>();
    public string CrustType { get; set; } = "Continental";
    public float AverageHeight { get; set; }
    public float AverageMoisture { get; set; }
    public float Velocity { get; set; }
    public float Rotation { get; set; }
    public Vec2Dto MovementDirection { get; set; } = new();
    public Vec3Dto AveragedCenter { get; set; } = new();
    public Vec3Dto UAxis { get; set; } = new();
    public Vec3Dto VAxis { get; set; } = new();
    public List<int> NeighborContinents { get; set; } = new();
    public float StressAccumulation { get; set; }
    public List<KvIntFloatDto> NeighborStress { get; set; } = new();
    public List<KvIntStringDto> BoundaryTypes { get; set; } = new();
    public Dictionary<string, float> ContinentalResources { get; set; } = new();
    public Dictionary<string, float> ResourceAbundance { get; set; } = new();
}
