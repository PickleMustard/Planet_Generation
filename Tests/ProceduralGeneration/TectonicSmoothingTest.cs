using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.GameState;
using Structures.MeshGeneration;
using static GdUnit4.Assertions;

namespace Tests.ProceduralGeneration;

/// <summary>
/// Coverage for the boundary-stress smoothing pipeline added to
/// <see cref="TectonicGeneration.CalculateBoundaryStress"/>: Phase A (edge-midpoint
/// velocity sampling) and Phase B (Laplacian smoothing on boundary edge stress).
///
/// Builds a synthetic 2-continent strip along a great-circle arc and asserts:
///   1. With smoothing enabled, the per-edge stddev along the boundary chain falls
///      below the unsmoothed baseline.
///   2. With smoothing disabled (iterations=0, sampleVelocity=false), the legacy
///      raw-stress path is deterministic and produces nonzero stress.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TectonicSmoothingTest
{
    private const int ContinentAStart = 100;
    private const int ContinentBStart = 200;
    private const int ChainLength = 8;

    private sealed class TestScene
    {
        public StructureDatabase StrDb { get; init; } = null!;
        public Dictionary<EdgeKey, HashSet<VoronoiCell>> EdgeMap { get; init; } = null!;
        public HashSet<Point> Points { get; init; } = null!;
        public Dictionary<int, Continent> Continents { get; init; } = null!;
        public List<Edge> BoundaryChain { get; init; } = null!;
    }

    /// <summary>
    /// Builds a strip of <see cref="ChainLength"/> boundary edges along the equator,
    /// each shared by one cell from continent A (north) and one from continent B
    /// (south). Wires <see cref="StructureDatabase.CellMap"/> so the interior BFS
    /// can resolve cell neighbors without a full mesh.
    /// </summary>
    private static TestScene BuildScene()
    {
        var strDb = new StructureDatabase(0);
        var pts = new HashSet<Point>();

        // (ChainLength + 1) equator points spaced across a longitude arc.
        var equator = new Point[ChainLength + 1];
        for (int i = 0; i <= ChainLength; i++)
        {
            float t = i / (float)ChainLength;
            float ang = Mathf.Lerp(-0.6f, 0.6f, t); // ~±34° longitude band
            var pos = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            equator[i] = strDb.GetOrCreatePoint(pos);
            pts.Add(equator[i]);
        }

        var aCenter = strDb.GetOrCreatePoint(new Vector3(0f, 1f, 0f));
        var bCenter = strDb.GetOrCreatePoint(new Vector3(0f, -1f, 0f));
        pts.Add(aCenter);
        pts.Add(bCenter);

        var boundaryChain = new List<Edge>();
        var aCells = new List<VoronoiCell>();
        var bCells = new List<VoronoiCell>();
        var edgeMap = new Dictionary<EdgeKey, HashSet<VoronoiCell>>();

        for (int i = 0; i < ChainLength; i++)
        {
            var edge = Edge.MakeEdge(equator[i], equator[i + 1]);
            boundaryChain.Add(edge);

            var aCell = new VoronoiCell(
                i,
                new[] { aCenter, equator[i], equator[i + 1] },
                Array.Empty<Triangle>(),
                new[] { edge }
            )
            {
                ContinentIndex = ContinentAStart,
                IsBorderTile = true,
                Interiorness = 0,
            };
            var bCell = new VoronoiCell(
                ChainLength + i,
                new[] { bCenter, equator[i], equator[i + 1] },
                Array.Empty<Triangle>(),
                new[] { edge }
            )
            {
                ContinentIndex = ContinentBStart,
                IsBorderTile = true,
                Interiorness = 0,
            };
            aCells.Add(aCell);
            bCells.Add(bCell);

            edgeMap[edge.key] = new HashSet<VoronoiCell> { aCell, bCell };

            RegisterCellInMap(strDb, aCell);
            RegisterCellInMap(strDb, bCell);
        }

        var aPoints = new HashSet<Point>(equator) { aCenter };
        var bPoints = new HashSet<Point>(equator) { bCenter };

        // Pre-set tangent basis on both continents so EnsureContinentTangentBasis
        // does not pick a random basis from the point cloud. Aligning the basis with
        // (1,0,0)/(0,0,1) gives the boundary chain a well-defined shear axis.
        Vector3 uAxis = new Vector3(1f, 0f, 0f);
        Vector3 vAxis = new Vector3(0f, 0f, 1f);

        var contA = new Continent(
            ContinentAStart,
            aCells,
            new HashSet<VoronoiCell>(aCells),
            aPoints,
            new List<Point>(),
            aCenter.Position.Normalized(),
            uAxis,
            vAxis,
            new Vector2(1f, 0f),
            1.0f,
            Mathf.DegToRad(45f),
            Continent.CRUST_TYPE.Continental,
            1f,
            1f,
            new HashSet<int> { ContinentBStart },
            0f,
            new Dictionary<int, float>(),
            new Dictionary<int, Continent.BOUNDARY_TYPE>()
        );
        contA.boundaryEdges = new HashSet<Edge>(boundaryChain);

        var contB = new Continent(
            ContinentBStart,
            bCells,
            new HashSet<VoronoiCell>(bCells),
            bPoints,
            new List<Point>(),
            bCenter.Position.Normalized(),
            uAxis,
            vAxis,
            new Vector2(-1f, 0f),
            1.0f,
            Mathf.DegToRad(-30f),
            Continent.CRUST_TYPE.Oceanic,
            -1f,
            1f,
            new HashSet<int> { ContinentAStart },
            0f,
            new Dictionary<int, float>(),
            new Dictionary<int, Continent.BOUNDARY_TYPE>()
        );
        contB.boundaryEdges = new HashSet<Edge>(boundaryChain);

        // Per-cell MovementDirection mirrors the production formula at
        // UnifiedCelestialMesh.cs:2129-2137. Each cell's center is offset along
        // the equator, so ω × r magnitudes differ per cell — exactly the source
        // of edge-to-edge stress jitter the smoothing pass is meant to fix.
        SeedCellMovement(contA, aCells);
        SeedCellMovement(contB, bCells);

        var continents = new Dictionary<int, Continent>
        {
            [ContinentAStart] = contA,
            [ContinentBStart] = contB,
        };
        return new TestScene
        {
            StrDb = strDb,
            EdgeMap = edgeMap,
            Points = pts,
            Continents = continents,
            BoundaryChain = boundaryChain,
        };
    }

    private static void RegisterCellInMap(StructureDatabase strDb, VoronoiCell cell)
    {
        foreach (var p in cell.Points)
        {
            if (!strDb.CellMap.TryGetValue(p, out var set))
            {
                set = new HashSet<VoronoiCell>();
                strDb.CellMap[p] = set;
            }
            set.Add(cell);
        }
    }

    private static void SeedCellMovement(Continent c, List<VoronoiCell> cells)
    {
        // Derive a tangent basis once for projection of cell centers.
        Vector3 uAxis = new Vector3(1f, 0f, 0f);
        Vector3 vAxis = new Vector3(0f, 0f, 1f);
        foreach (var cell in cells)
        {
            Vector3 cellCenter = Vector3.Zero;
            foreach (var p in cell.Points)
                cellCenter += p.Position;
            cellCenter /= cell.Points.Length;
            cellCenter = cellCenter.Normalized();

            Vector3 offset3D = c.averagedCenter - cellCenter;
            Vector2 offset2D = new Vector2(uAxis.Dot(offset3D), vAxis.Dot(offset3D));

            cell.MovementDirection =
                new Vector2(c.movementDirection.X * c.velocity, c.movementDirection.Y * c.velocity)
                + new Vector2(-c.rotation * offset2D.Y, c.rotation * offset2D.X);
        }
    }

    private static TectonicGeneration MakeGen(
        StructureDatabase strDb,
        int iterations,
        bool sampleMidpoint
    ) =>
        new(
            strDb,
            new RandomNumberGenerator { Seed = 42UL },
            stressScale: 1f,
            shearScale: 1f,
            maxPropagationDistance: 1f,
            propagationFalloff: 0.5f,
            inactiveStressThreshold: 0.01f,
            generalHeightScale: 1f,
            generalShearScale: 1f,
            generalCompressionScale: 1f,
            boundaryStressSmoothingIterations: iterations,
            boundaryStressSmoothingWeight: 0.5f,
            sampleVelocityAtEdgeMidpoint: sampleMidpoint
        );

    private static float Stddev(IEnumerable<float> xs)
    {
        var arr = xs.ToArray();
        if (arr.Length < 2)
            return 0f;
        float mean = arr.Average();
        float sumSq = 0f;
        foreach (var x in arr)
            sumSq += (x - mean) * (x - mean);
        return Mathf.Sqrt(sumSq / arr.Length);
    }

    [TestCase]
    public void SmoothingReducesShearStressVariance()
    {
        var raw = BuildScene();
        MakeGen(raw.StrDb, iterations: 0, sampleMidpoint: true)
            .CalculateBoundaryStress(raw.EdgeMap, raw.Points, raw.Continents);
        float stddevRaw = Stddev(
            raw.BoundaryChain.Select(e => MathF.Abs(e.Stress!.ShearStress))
        );

        var smoothed = BuildScene();
        MakeGen(smoothed.StrDb, iterations: 3, sampleMidpoint: true)
            .CalculateBoundaryStress(smoothed.EdgeMap, smoothed.Points, smoothed.Continents);
        float stddevSmoothed = Stddev(
            smoothed.BoundaryChain.Select(e => MathF.Abs(e.Stress!.ShearStress))
        );

        AssertThat(stddevRaw > 0f)
            .OverrideFailureMessage("baseline produced zero variance — scene degenerate")
            .IsTrue();
        AssertThat(stddevSmoothed < stddevRaw * 0.7f)
            .OverrideFailureMessage(
                $"smoothing did not reduce variance enough: raw={stddevRaw}, smoothed={stddevSmoothed}"
            )
            .IsTrue();
    }

    [TestCase]
    public void LegacyPathIsDeterministic()
    {
        var s1 = BuildScene();
        MakeGen(s1.StrDb, iterations: 0, sampleMidpoint: false)
            .CalculateBoundaryStress(s1.EdgeMap, s1.Points, s1.Continents);
        var v1 = s1.BoundaryChain.Select(e => e.Stress!.ShearStress).ToArray();

        var s2 = BuildScene();
        MakeGen(s2.StrDb, iterations: 0, sampleMidpoint: false)
            .CalculateBoundaryStress(s2.EdgeMap, s2.Points, s2.Continents);
        var v2 = s2.BoundaryChain.Select(e => e.Stress!.ShearStress).ToArray();

        AssertThat(v1.Length).IsEqual(ChainLength);
        bool anyNonzero = v1.Any(x => MathF.Abs(x) > 1e-6f);
        AssertThat(anyNonzero)
            .OverrideFailureMessage("legacy path produced all-zero shear stress")
            .IsTrue();
        for (int i = 0; i < v1.Length; i++)
        {
            AssertThat(Mathf.IsEqualApprox(v1[i], v2[i]))
                .OverrideFailureMessage(
                    $"non-deterministic at edge {i}: {v1[i]} vs {v2[i]}"
                )
                .IsTrue();
        }
    }
}
