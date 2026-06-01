using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.GameState;
using Structures.MeshGeneration;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Mappers;

/// <summary>
/// Serializes a body's post-generation geometry from the surviving authoritative graph
/// (<see cref="UnifiedCelestialMesh.Continents"/>) and rebuilds it on load by reconstructing the
/// Point/Cell/Continent object graph by index, then re-deriving the renderable mesh and runtime
/// selection structures via <see cref="UnifiedCelestialMesh.RebuildSurfaceMeshFromCells"/>.
/// </summary>
public static class GeometryMapper
{
    public static GeometryDto ToDto(UnifiedCelestialMesh mesh)
    {
        var dto = new GeometryDto
        {
            Size = mesh.size,
            MaxHeight = mesh.maxHeight,
            GenerationType = mesh.GenerationType.ToString(),
            ProjectToSphere = mesh.ProjectToSphere,
            UseCellBiomeForColoring = mesh.UseCellBiomeForColoring,
            Classification = mesh.Classification?.TypeName ?? "",
        };

        var continents = mesh.Continents;
        if (continents == null || continents.Count == 0)
            return dto;

        var cellsByIndex = new Dictionary<int, VoronoiCell>();
        var pointsByIndex = new Dictionary<int, Point>();

        foreach (var continent in continents.Values)
        {
            if (continent.cells == null)
                continue;
            foreach (var cell in continent.cells)
            {
                cellsByIndex.TryAdd(cell.Index, cell);
                foreach (var p in cell.Points)
                    pointsByIndex.TryAdd(p.Index, p);
            }
        }

        foreach (var p in pointsByIndex.Values)
        {
            dto.Points.Add(new PointDto
            {
                Index = p.Index,
                Position = Vec3Dto.From(p.Position),
                Height = p.Height,
                Velocity = Vec3Dto.From(p.Velocity),
                Stress = p.Stress,
                Biome = p.Biome,
                IsOnContinentBorder = p.isOnContinentBorder,
                Radius = p.Radius,
                ContinentIndices = p.ContinentIndecies != null
                    ? p.ContinentIndecies.ToList()
                    : new List<int>(),
            });
        }

        foreach (var cell in cellsByIndex.Values)
        {
            dto.Cells.Add(new CellDto
            {
                Index = cell.Index,
                ContinentIndex = cell.ContinentIndex,
                IsBorderTile = cell.IsBorderTile,
                Interiorness = cell.Interiorness,
                BoundingContinentIndex = cell.BoundingContinentIndex ?? Array.Empty<int>(),
                Center = Vec3Dto.From(cell.Center),
                Height = cell.Height,
                NormalizedHeight = cell.NormalizedHeight,
                Stress = cell.Stress,
                Biome = cell.Biome,
                MovementDirection = Vec2Dto.From(cell.MovementDirection),
                HasGeothermalVent = cell.HasGeothermalVent,
                Resources = new Dictionary<string, float>(cell.Resources),
                PointIndices = cell.Points.Select(p => p.Index).ToArray(),
            });
        }

        foreach (var continent in continents.Values)
        {
            dto.Continents.Add(new ContinentDto
            {
                StartingIndex = continent.StartingIndex,
                CellIndices = continent.cells?.Select(c => c.Index).ToArray() ?? Array.Empty<int>(),
                BoundaryCellIndices = continent.boundaryCells?.Select(c => c.Index).ToArray()
                    ?? Array.Empty<int>(),
                CrustType = continent.elevation.ToString(),
                AverageHeight = continent.averageHeight,
                AverageMoisture = continent.averageMoisture,
                Velocity = continent.velocity,
                Rotation = continent.rotation,
                MovementDirection = Vec2Dto.From(continent.movementDirection),
                AveragedCenter = Vec3Dto.From(continent.averagedCenter),
                UAxis = Vec3Dto.From(continent.uAxis),
                VAxis = Vec3Dto.From(continent.vAxis),
                NeighborContinents = continent.neighborContinents?.ToList() ?? new List<int>(),
                StressAccumulation = continent.stressAccumulation,
                NeighborStress = continent.neighborStress?
                    .Select(kv => new KvIntFloatDto { Key = kv.Key, Value = kv.Value }).ToList()
                    ?? new List<KvIntFloatDto>(),
                BoundaryTypes = continent.boundaryTypes?
                    .Select(kv => new KvIntStringDto { Key = kv.Key, Value = kv.Value.ToString() }).ToList()
                    ?? new List<KvIntStringDto>(),
                ContinentalResources = new Dictionary<string, float>(continent.ContinentalResources),
                ResourceAbundance = new Dictionary<string, float>(continent.ResourceAbundance),
            });
        }

        return dto;
    }

    /// <summary>
    /// Rebuilds the geometry graph from the DTO, attaches a fresh StructureDatabase + Octree to the
    /// mesh, and re-derives the renderable mesh + selection structures. Returns the created StrDb and
    /// Octree so the owning body can point its own references at the same instances.
    /// </summary>
    public static (StructureDatabase StrDb, Octree<Point> Oct) Restore(
        UnifiedCelestialMesh mesh,
        GeometryDto dto)
    {
        // --- Points (index-preserving ctor — never DetermineIndex, to keep the graph consistent) ---
        var pointsByIndex = new Dictionary<int, Point>(dto.Points.Count);
        foreach (var pd in dto.Points)
        {
            var p = new Point(pd.Position.ToVector3(), pd.Index)
            {
                Height = pd.Height,
                Velocity = pd.Velocity.ToVector3(),
                Stress = pd.Stress,
                Biome = pd.Biome,
                isOnContinentBorder = pd.IsOnContinentBorder,
                Radius = pd.Radius,
                ContinentIndecies = new HashSet<int>(pd.ContinentIndices),
            };
            pointsByIndex[pd.Index] = p;
        }

        // --- Cells ---
        var cellsByIndex = new Dictionary<int, VoronoiCell>(dto.Cells.Count);
        foreach (var cd in dto.Cells)
        {
            var pts = cd.PointIndices.Select(i => pointsByIndex[i]).ToArray();
            var cell = new VoronoiCell(cd.Index, pts, Array.Empty<Triangle>(), Array.Empty<Edge>())
            {
                ContinentIndex = cd.ContinentIndex,
                IsBorderTile = cd.IsBorderTile,
                Interiorness = cd.Interiorness,
                BoundingContinentIndex = cd.BoundingContinentIndex,
                Center = cd.Center.ToVector3(),
                Height = cd.Height,
                NormalizedHeight = cd.NormalizedHeight,
                Stress = cd.Stress,
                Biome = cd.Biome,
                MovementDirection = cd.MovementDirection.ToVector2(),
                HasGeothermalVent = cd.HasGeothermalVent,
                Resources = new Dictionary<string, float>(cd.Resources),
            };
            cellsByIndex[cd.Index] = cell;
        }

        // --- Continents ---
        var continents = new Dictionary<int, Continent>(dto.Continents.Count);
        foreach (var cd in dto.Continents)
        {
            var cells = cd.CellIndices.Select(i => cellsByIndex[i]).ToList();
            var boundaryCells = new HashSet<VoronoiCell>(
                cd.BoundaryCellIndices.Select(i => cellsByIndex[i]));
            var points = new HashSet<Point>();
            foreach (var c in cells)
                foreach (var p in c.Points)
                    points.Add(p);

            Enum.TryParse<Continent.CRUST_TYPE>(cd.CrustType, out var crust);
            var neighborStress = cd.NeighborStress.ToDictionary(kv => kv.Key, kv => kv.Value);
            var boundaryTypes = cd.BoundaryTypes.ToDictionary(
                kv => kv.Key,
                kv => Enum.TryParse<Continent.BOUNDARY_TYPE>(kv.Value, out var bt)
                    ? bt
                    : Continent.BOUNDARY_TYPE.Divergent);

            var continent = new Continent(
                cd.StartingIndex, cells, boundaryCells, points, new List<Point>(),
                cd.AveragedCenter.ToVector3(), cd.UAxis.ToVector3(), cd.VAxis.ToVector3(),
                cd.MovementDirection.ToVector2(), cd.Velocity, cd.Rotation, crust,
                cd.AverageHeight, cd.AverageMoisture,
                new HashSet<int>(cd.NeighborContinents), cd.StressAccumulation,
                neighborStress, boundaryTypes)
            {
                ContinentalResources = new Dictionary<string, float>(cd.ContinentalResources),
                ResourceAbundance = new Dictionary<string, float>(cd.ResourceAbundance),
            };
            continents[cd.StartingIndex] = continent;
        }

        // --- Fresh StrDb + Octree sized to enclose all restored points ---
        var strDb = new StructureDatabase(0);
        var oct = new Octree<Point>(ComputeBoundary(pointsByIndex.Values, dto.Size));

        // --- Wire mesh config, then re-derive renderable mesh + selection maps ---
        // A fresh MeshInstance3D has no Mesh; the generation path assigns an empty ArrayMesh via
        // set_mesh before building. Do the same here so RebuildSurfaceMeshFromCells can commit to it.
        mesh.Mesh = new ArrayMesh();
        mesh.StrDb = strDb;
        mesh.size = dto.Size;
        mesh.maxHeight = dto.MaxHeight;
        mesh.ProjectToSphere = dto.ProjectToSphere;
        mesh.UseCellBiomeForColoring = dto.UseCellBiomeForColoring;
        if (Enum.TryParse<UnifiedCelestialMesh.BodyGenerationType>(dto.GenerationType, out var gt))
            mesh.GenerationType = gt;
        mesh.Continents = continents;

        var allCells = cellsByIndex.Values.ToList();
        mesh.RebuildSurfaceMeshFromCells(allCells, oct);
        mesh.CreateCollisionsFromOctree(oct);
        strDb.CellCount = allCells.Count;

        return (strDb, oct);
    }

    private static Aabb ComputeBoundary(IEnumerable<Point> points, float fallbackSize)
    {
        bool any = false;
        Vector3 min = Vector3.Zero;
        Vector3 max = Vector3.Zero;
        foreach (var p in points)
        {
            var v = p.Position;
            if (!any)
            {
                min = max = v;
                any = true;
                continue;
            }
            min = new Vector3(Mathf.Min(min.X, v.X), Mathf.Min(min.Y, v.Y), Mathf.Min(min.Z, v.Z));
            max = new Vector3(Mathf.Max(max.X, v.X), Mathf.Max(max.Y, v.Y), Mathf.Max(max.Z, v.Z));
        }

        if (!any)
        {
            float s = Mathf.Max(fallbackSize, 1f) * 1.2f;
            return new Aabb(Vector3.One * -s, Vector3.One * (2f * s));
        }

        // 20% margin so boundary-coincident points insert cleanly.
        Vector3 size = (max - min);
        Vector3 margin = size * 0.1f + Vector3.One; // +1 guards against a degenerate zero extent
        Vector3 begin = min - margin;
        Vector3 extent = (size + margin * 2f);
        return new Aabb(begin, extent).Abs();
    }
}
