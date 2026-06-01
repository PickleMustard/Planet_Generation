using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.GameState;
using Structures.MeshGeneration;

namespace ProceduralGeneration.MeshGeneration;

/// <summary>
/// Handles tectonic plate simulation and stress calculation for planetary terrain generation.
/// This class simulates the interaction between continental plates, calculates boundary stresses,
/// and applies the resulting deformations to the terrain mesh.
/// </summary>
public class TectonicGeneration
{
    /// <summary>
    /// Database containing all structural data for the mesh including vertices, edges, and cells.
    /// </summary>
    private readonly StructureDatabase StrDb;

    /// <summary>
    /// Random number generator for procedural generation and random sampling.
    /// </summary>
    private readonly RandomNumberGenerator rand;

    /// <summary>
    /// Scaling factor for compression stress calculations.
    /// </summary>
    private readonly float StressScale;

    /// <summary>
    /// Scaling factor for shear stress calculations.
    /// </summary>
    private readonly float ShearScale;

    /// <summary>
    /// Maximum distance that stress can propagate from its source edge.
    /// </summary>
    private readonly float MaxPropagationDistance;

    /// <summary>
    /// Rate at which stress magnitude decreases with distance from source.
    /// </summary>
    private readonly float PropagationFalloff;

    /// <summary>
    /// Threshold below which stress is considered inactive and doesn't affect terrain.
    /// </summary>
    private readonly float InactiveStressThreshold;

    /// <summary>
    /// General scaling factor for height modifications due to inactive stress.
    /// </summary>
    private readonly float GeneralHeightScale;

    /// <summary>
    /// Scaling factor for height modifications due to shear stress.
    /// </summary>
    private readonly float GeneralShearScale;

    /// <summary>
    /// Scaling factor for height modifications due to compression stress.
    /// </summary>
    private readonly float GeneralCompressionScale;

    /// <summary>
    /// Number of Laplacian smoothing iterations applied to per-edge boundary stress
    /// after raw computation. 0 disables smoothing.
    /// </summary>
    private readonly int BoundaryStressSmoothingIterations;

    /// <summary>
    /// Blend weight in [0,1] for each Laplacian smoothing iteration.
    /// Each edge's stress is lerped toward the mean of its boundary-adjacent neighbors.
    /// </summary>
    private readonly float BoundaryStressSmoothingWeight;

    /// <summary>
    /// When true, sample per-plate velocity at the edge midpoint instead of using
    /// the bulk cell MovementDirection. Eliminates the discontinuity caused by ω × r
    /// being evaluated at two different cell centers across a boundary.
    /// </summary>
    private readonly bool SampleVelocityAtEdgeMidpoint;

    /// <summary>
    /// Initializes a new instance of the TectonicGeneration class with specified parameters.
    /// </summary>
    /// <param name="strDb">Structure database containing mesh data.</param>
    /// <param name="rng">Random number generator for procedural generation.</param>
    /// <param name="stressScale">Scaling factor for compression stress calculations.</param>
    /// <param name="shearScale">Scaling factor for shear stress calculations.</param>
    /// <param name="maxPropagationDistance">Maximum distance stress can propagate from source.</param>
    /// <param name="propagationFalloff">Rate at which stress decreases with distance.</param>
    /// <param name="inactiveStressThreshold">Threshold below which stress is considered inactive.</param>
    /// <param name="generalHeightScale">Scaling factor for height modifications from inactive stress.</param>
    /// <param name="generalShearScale">Scaling factor for height modifications from shear stress.</param>
    /// <param name="generalCompressionScale">Scaling factor for height modifications from compression stress.</param>
    /// <param name="boundaryStressSmoothingIterations">Iterations of Laplacian smoothing over boundary edge stress. 0 disables.</param>
    /// <param name="boundaryStressSmoothingWeight">Per-iteration blend weight in [0,1] for Laplacian smoothing.</param>
    /// <param name="sampleVelocityAtEdgeMidpoint">If true, sample plate velocity at the edge midpoint rather than per-cell.</param>
    public TectonicGeneration(
        StructureDatabase strDb,
        RandomNumberGenerator rng,
        float stressScale,
        float shearScale,
        float maxPropagationDistance,
        float propagationFalloff,
        float inactiveStressThreshold,
        float generalHeightScale,
        float generalShearScale,
        float generalCompressionScale,
        int boundaryStressSmoothingIterations = 0,
        float boundaryStressSmoothingWeight = 0.5f,
        bool sampleVelocityAtEdgeMidpoint = false
    )
    {
        StrDb = strDb;
        rand = rng;
        StressScale = stressScale;
        ShearScale = shearScale;
        MaxPropagationDistance = maxPropagationDistance;
        PropagationFalloff = propagationFalloff;
        InactiveStressThreshold = inactiveStressThreshold;
        GeneralHeightScale = generalHeightScale;
        GeneralShearScale = generalShearScale;
        GeneralCompressionScale = generalCompressionScale;
        BoundaryStressSmoothingIterations = Mathf.Max(0, boundaryStressSmoothingIterations);
        BoundaryStressSmoothingWeight = Mathf.Clamp(boundaryStressSmoothingWeight, 0f, 1f);
        SampleVelocityAtEdgeMidpoint = sampleVelocityAtEdgeMidpoint;
    }

    /// <summary>
    /// Calculates stress at boundaries between continental plates and propagates stress throughout the mesh.
    /// This method analyzes the interaction between neighboring continents, calculates compression and shear
    /// stresses at their boundaries, and propagates these stresses through the mesh structure.
    /// </summary>
    /// <param name="edgeMap">Dictionary mapping edges to their adjacent Voronoi cells.</param>
    /// <param name="points">Collection of all points in the mesh.</param>
    /// <param name="continents">Dictionary of continents with their movement properties.</param>
    /// <param name="percent">Progress tracking object for reporting completion status.</param>
    /// <remarks>
    /// The method performs the following steps:
    /// 1. For each continent, calculates local coordinate system based on random point pairs
    /// 2. For each boundary cell, analyzes edges that border different continents
    /// 3. Calculates compression and shear stress based on relative plate movement
    /// 4. Classifies boundary type (convergent, divergent, transform, or inactive)
    /// 5. Propagates stress from boundary edges to surrounding mesh using priority queue
    /// </remarks>
    public void CalculateBoundaryStress(
        IReadOnlyDictionary<EdgeKey, HashSet<VoronoiCell>> edgeMap,
        HashSet<Point> points,
        Dictionary<int, Continent> continents
    )
    {
        GD.Print($"Calculating Boundary Stress\n{continents.Count}\n");

        // Ensure every continent has a stable tangent basis cached on the continent itself.
        // Re-use existing uAxis/vAxis if previously populated; otherwise derive one and store it.
        foreach (var kv in continents)
        {
            EnsureContinentTangentBasis(kv.Value);
        }

        // Calculate stress between neighboring continents
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            int continentIndex = continentPair.Key;
            Continent continent = continentPair.Value;
            Vector3 uAxis = continent.uAxis;
            Vector3 vAxis = continent.vAxis;
            GD.Print($"Boundary Cells: {continent.boundaryCells.Count}");
            foreach (Edge e in continent.boundaryEdges)
            {
                List<VoronoiCell> neighbors = new List<VoronoiCell>(edgeMap[e.key]);
                VoronoiCell? neighborCell = null;
                VoronoiCell? borderCell = null;
                if (neighbors.Count < 2)
                    continue;
                if (neighbors[0].ContinentIndex == continent.StartingIndex)
                {
                    borderCell = neighbors[0];
                    neighborCell = neighbors[1];
                }
                else
                {
                    neighborCell = neighbors[0];
                    borderCell = neighbors[1];
                }
                if (neighborCell != null && neighborCell.ContinentIndex != continent.StartingIndex)
                {
                    Continent neighborContinent = continents[neighborCell.ContinentIndex];
                    Vector3 projectedBorderCellMovement;
                    Vector3 projectedNeighborCellMovement;

                    if (SampleVelocityAtEdgeMidpoint)
                    {
                        // Phase A: sample each plate's velocity field at the shared edge midpoint.
                        // Both sides evaluate at the same world-space point, so per-vertex
                        // discontinuities collapse to the genuine cross-plate ω/v difference.
                        Vector3 edgeMid = e.Midpoint;
                        Vector2 bcVel2D = PlateVelocityAtPoint(continent, edgeMid);
                        Vector2 ncVel2D = PlateVelocityAtPoint(neighborContinent, edgeMid);
                        projectedBorderCellMovement = uAxis * bcVel2D.X + vAxis * bcVel2D.Y;
                        projectedNeighborCellMovement =
                            neighborContinent.uAxis * ncVel2D.X
                            + neighborContinent.vAxis * ncVel2D.Y;
                    }
                    else
                    {
                        projectedBorderCellMovement =
                            uAxis * (borderCell.MovementDirection.X * continent.velocity)
                            + vAxis * (borderCell.MovementDirection.Y * continent.velocity);
                        projectedNeighborCellMovement =
                            uAxis
                                * (
                                    neighborCell.MovementDirection.X
                                    * neighborContinent.velocity
                                )
                            + vAxis
                                * (
                                    neighborCell.MovementDirection.Y
                                    * neighborContinent.velocity
                                );
                    }

                    Vector3 EdgeVector = (
                        ((Point)e.P).Position - ((Point)e.Q).Position
                    ).Normalized();
                    Vector3 EdgeNormal = EdgeVector.Cross(((Point)e.Q).Position.Normalized());

                    float bcVelNormal = projectedBorderCellMovement.Dot(EdgeNormal);
                    float ncVelNormal = projectedNeighborCellMovement.Dot(-EdgeNormal);

                    float bcVelTangent = projectedBorderCellMovement.Dot(-EdgeVector);
                    float ncVelTangent = projectedNeighborCellMovement.Dot(EdgeVector);

                    float compressionStrength = (bcVelNormal - ncVelNormal) * StressScale;
                    float shearStrength = (bcVelTangent - ncVelTangent) * ShearScale;
                    if (float.IsNaN(compressionStrength) || double.IsNaN(compressionStrength))
                        compressionStrength = 0.0f;
                    if (float.IsNaN(shearStrength) || double.IsNaN(shearStrength))
                        shearStrength = 0.0f;

                    EdgeStress calculatedStress = new EdgeStress
                    {
                        CompressionStress = compressionStrength,
                        ShearStress = shearStrength,
                        StressDirection = EdgeNormal,
                    };
                    if (
                        float.IsNaN(calculatedStress.CompressionStress)
                        || double.IsNaN(calculatedStress.CompressionStress)
                        || float.IsNaN(calculatedStress.ShearStress)
                        || double.IsNaN(calculatedStress.ShearStress)
                    )
                        GD.Print(
                            $"Stress: {calculatedStress.CompressionStress} + {calculatedStress.ShearStress}"
                        );
                    e.Stress = calculatedStress;
                    e.Type = ClassifyBoundaryType(calculatedStress);
                    //var totalStress = calculatedStress.CompressionStress + calculatedStress.ShearStress;
                    //borderCell.Stress += totalStress;
                    float totalStress =
                        MathF.Abs(calculatedStress.CompressionStress) * .8f
                        + MathF.Abs(calculatedStress.ShearStress) * .3f;
                    borderCell.Stress += totalStress;
                    //switch (e.Type)
                    //{
                    //    case EdgeType.inactive:
                    //        break;
                    //    case EdgeType.transform:
                    //        borderCell.Stress += calculatedStress.ShearStress;
                    //        //neighborCell.Stress += calculatedStress.ShearStress;
                    //        break;
                    //    case EdgeType.divergent:
                    //        borderCell.Stress -= calculatedStress.CompressionStress;
                    //        //neighborCell.Stress -= calculatedStress.CompressionStress;
                    //        break;
                    //    case EdgeType.convergent:
                    //        borderCell.Stress += calculatedStress.CompressionStress;
                    //        //neighborCell.Stress += calculatedStress.CompressionStress;
                    //        break;
                    //}

                    //PriorityQueue<Edge, float> toVisit = new PriorityQueue<Edge, float>();
                    //HashSet<Edge> visitedEdge = new HashSet<Edge>();
                    //visitedEdge.Add(e);
                    //toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges(e.Q).ToArray(), 0.0f);
                    //toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges((Point)e.P).ToArray(), 0.0f);
                    //while (toVisit.Count > 0)
                    //{
                    //    Edge current;
                    //    float distance;
                    //    bool success = toVisit.TryDequeue(out current, out distance);
                    //    if (!success) break;
                    //    if (visitedEdge.Contains(current) || distance > MaxPropagationDistance) continue;
                    //    visitedEdge.Add(current);
                    //    float magnitude = CalculateStressAtDistance(e.Stress, distance, current, e);
                    //    current.StressMagnitude += magnitude;
                    //    var edges = StrDb.GetIncidentHalfEdges(current.Q);
                    //    foreach (var edge in edges)
                    //    {
                    //        var length = (current.Midpoint - edge.Midpoint).Length();
                    //        if (!visitedEdge.Contains(edge) && length < MaxPropagationDistance)
                    //        {
                    //            toVisit.Enqueue(edge, length);
                    //        }
                    //        //toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges(current.Q).ToArray(), (current.Midpoint - e.Midpoint).Length());
                    //        //toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges(current.P).ToArray(), (current.Midpoint - e.Midpoint).Length());
                    //    }
                    //    edges = StrDb.GetIncidentHalfEdges(current.P);
                    //    foreach (var edge in edges)
                    //    {
                    //        var length = (current.Midpoint - edge.Midpoint).Length();
                    //        if (!visitedEdge.Contains(edge) && length < MaxPropagationDistance)
                    //        {
                    //            toVisit.Enqueue(edge, length);
                    //        }
                    //    }

                    //}
                }
            }

            // Phase B: Laplacian smoothing on boundary edge stress, then re-derive
            // boundary type + per-cell stress contribution from smoothed values.
            if (BoundaryStressSmoothingIterations > 0)
            {
                var adjacency = BuildBoundaryEdgeAdjacency(continent);
                SmoothBoundaryEdgeStress(
                    continent,
                    adjacency,
                    BoundaryStressSmoothingIterations,
                    BoundaryStressSmoothingWeight
                );
                ReclassifyBoundaryEdges(continent, edgeMap);
            }

            Queue<VoronoiCell> queue = new Queue<VoronoiCell>(continent.boundaryCells);
            HashSet<VoronoiCell> visited = new HashSet<VoronoiCell>();
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                visited.Add(cell);
                foreach (var edge in cell.Edges)
                {
                    edge.StressMagnitude += cell.Stress;
                }
                var neighbors = UnifiedCelestialMesh.GetCellNeighbors(cell, StrDb);
                foreach (var neighbor in neighbors)
                {
                    neighbor.Stress =
                        neighbor.Stress
                        + (cell.Stress * Mathf.Pow(PropagationFalloff, (float)cell.Interiorness))
                            / (neighbor.Increment);
                    neighbor.Increment++;
                    if (!visited.Contains(neighbor) && neighbor.ContinentIndex == continentIndex)
                        queue.Enqueue(neighbor);
                }
            }
            continents[continentIndex] = continent;
        }
    }

    /// <summary>
    /// Applies calculated tectonic stresses to terrain vertices, modifying their heights.
    /// This method processes all vertices in the mesh and adjusts their heights based on the
    /// stress values of their incident edges, creating realistic terrain features like
    /// mountains, valleys, and trenches.
    /// </summary>
    /// <param name="continents">Dictionary of continents (not directly used but kept for interface consistency).</param>
    /// <param name="cells">List of Voronoi cells (not directly used but kept for interface consistency).</param>
    /// <remarks>
    /// The method applies different height modifications based on edge types:
    /// - Inactive edges: General height scaling based on stress magnitude
    /// - Transform edges: Height modification based on shear stress (creates strike-slip features)
    /// - Divergent edges: Height reduction based on compression stress (creates rifts/trenches)
    /// - Convergent edges: Height increase based on compression stress (creates mountains)
    /// </remarks>
    public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        foreach (Point p in StrDb.VoronoiCellVertices)
        {
            Edge[] edges = StrDb.GetIncidentHalfEdges(p);
            // GameLogger.Info($"# of Edges: {edges.Length}");  // Moved to Debug to reduce console noise
            float alteredHeight = 0.0f;
            foreach (Edge e in edges)
            {
                switch (e.Type)
                {
                    case EdgeType.inactive:
                        alteredHeight += e.StressMagnitude * GeneralHeightScale;
                        break;
                    case EdgeType.transform:
                        alteredHeight += e.Stress!.ShearStress * GeneralShearScale;
                        break;
                    case EdgeType.divergent:
                        alteredHeight -= e.Stress!.CompressionStress * GeneralCompressionScale;
                        break;
                    case EdgeType.convergent:
                        alteredHeight += e.Stress!.CompressionStress * GeneralCompressionScale;
                        break;
                }
            }
            p.Height += alteredHeight;
        }
    }

    /// <summary>
    /// Classifies the type of tectonic boundary based on calculated stress values.
    /// This method analyzes compression and shear stress components to determine
    /// whether a boundary is convergent, divergent, transform, or inactive.
    /// </summary>
    /// <param name="es">EdgeStress object containing compression and shear stress values.</param>
    /// <returns>EdgeType enum value indicating the classification of the boundary.</returns>
    /// <remarks>
    /// Classification logic:
    /// - If total stress is below threshold: inactive
    /// - If compression factor > 56%: convergent (positive compression) or divergent (negative compression)
    /// - If shear factor > 70%: transform boundary
    /// - Otherwise: classify based on dominant stress type
    /// </remarks>
    private EdgeType ClassifyBoundaryType(EdgeStress es)
    {
        float normalizedCompression = Mathf.Abs(es.CompressionStress);
        float normalizedShear = Mathf.Abs(es.ShearStress);
        float totalStress = normalizedCompression + normalizedShear;

        if (totalStress < InactiveStressThreshold)
        {
            return EdgeType.inactive;
        }

        float compressionFactor = normalizedCompression / (totalStress + .0001f);
        float shearFactor = normalizedShear / (totalStress + .0001f);
        if (compressionFactor > 0.7f)
        {
            if (es.CompressionStress >= 0.0f)
            {
                return EdgeType.convergent;
            }
            else
            {
                return EdgeType.divergent;
            }
        }
        else if (shearFactor > 0.7f)
        {
            return EdgeType.transform;
        }
        else
        {
            if (normalizedCompression > normalizedShear)
                return es.CompressionStress >= 0.0f ? EdgeType.convergent : EdgeType.divergent;
            else
                return EdgeType.transform;
        }
    }

    /// <summary>
    /// Calculates the stress magnitude at a given distance from the source edge.
    /// This method models how tectonic stress propagates through the crust with
    /// exponential decay and directional attenuation.
    /// </summary>
    /// <param name="edgeStress">The original stress values at the source edge.</param>
    /// <param name="distance">Distance from the source edge to the current edge.</param>
    /// <param name="current">The edge receiving the propagated stress.</param>
    /// <param name="origin">The source edge from which stress originates.</param>
    /// <returns>Calculated stress magnitude at the current edge location.</returns>
    /// <remarks>
    /// The calculation considers:
    /// - Exponential decay based on distance and propagation falloff rate
    /// - Combined compression and shear stress (with shear weighted at 50%)
    /// - Directional factor based on alignment with stress direction
    /// </remarks>
    private float CalculateStressAtDistance(
        EdgeStress edgeStress,
        float distance,
        Edge current,
        Edge origin
    )
    {
        float decayFactor = MathF.Exp(-distance / PropagationFalloff);
        float totalStress =
            MathF.Abs(edgeStress.CompressionStress) + MathF.Abs(edgeStress.ShearStress) * .5f;
        Vector3 toEdge = (current.Midpoint - origin.Midpoint).Normalized();
        float directionalFactor = MathF.Abs(toEdge.Dot(edgeStress.StressDirection));
        return totalStress * decayFactor * directionalFactor;
    }

    /// <summary>
    /// Ensures the continent has a deterministic tangent basis (uAxis, vAxis) stored on it.
    /// Derived from two well-separated chord vectors through random continent points,
    /// flipped so the basis normal points outward through averagedCenter. Only computed
    /// if the continent's basis is currently zero (preserves any externally cached basis).
    /// </summary>
    private void EnsureContinentTangentBasis(Continent continent)
    {
        if (continent.uAxis.LengthSquared() > 0f && continent.vAxis.LengthSquared() > 0f)
            return;
        if (continent.points == null || continent.points.Count < 2)
            return;

        Vector3 v1 = (
            continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                .ToVector3().Normalized()
            - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                .ToVector3().Normalized()
        );
        Vector3 v2 = (
            continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                .ToVector3().Normalized()
            - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1))
                .ToVector3().Normalized()
        );
        Vector3 unitNorm = v1.Cross(v2);
        if (unitNorm.Dot(continent.averagedCenter) < 0f)
            unitNorm = -unitNorm;
        Vector3 uAxis = v1.Normalized();
        Vector3 vAxis = unitNorm.Cross(uAxis).Normalized();
        continent.uAxis = uAxis;
        continent.vAxis = vAxis;
    }

    /// <summary>
    /// Samples the plate's 2D velocity field at a world-space point using the plate's
    /// own tangent basis. v_total(x) = linearVelocity + ω × r where r is the projected
    /// offset from the plate's averagedCenter onto its (uAxis, vAxis) plane.
    /// </summary>
    private static Vector2 PlateVelocityAtPoint(Continent c, Vector3 worldPoint)
    {
        Vector3 offset3D = worldPoint - c.averagedCenter;
        Vector2 offset2D = new Vector2(c.uAxis.Dot(offset3D), c.vAxis.Dot(offset3D));
        Vector2 linear = new Vector2(
            c.movementDirection.X * c.velocity,
            c.movementDirection.Y * c.velocity
        );
        Vector2 rot = new Vector2(-c.rotation * offset2D.Y, c.rotation * offset2D.X);
        return linear + rot;
    }

    /// <summary>
    /// Builds vertex-adjacency map over a continent's boundary edges:
    /// edge E maps to the set of other boundary edges (in this same continent) that
    /// share at least one endpoint with E. Triple junctions naturally connect to
    /// all incident boundary edges on this continent only.
    /// </summary>
    private static Dictionary<Edge, List<Edge>> BuildBoundaryEdgeAdjacency(Continent continent)
    {
        var byVertex = new Dictionary<Point, List<Edge>>();
        foreach (var e in continent.boundaryEdges)
        {
            if (!byVertex.TryGetValue(e.P, out var listP))
            {
                listP = new List<Edge>();
                byVertex[e.P] = listP;
            }
            listP.Add(e);
            if (!byVertex.TryGetValue(e.Q, out var listQ))
            {
                listQ = new List<Edge>();
                byVertex[e.Q] = listQ;
            }
            listQ.Add(e);
        }

        var adjacency = new Dictionary<Edge, List<Edge>>(continent.boundaryEdges.Count);
        foreach (var e in continent.boundaryEdges)
        {
            var adj = new List<Edge>();
            foreach (var other in byVertex[e.P])
                if (!ReferenceEquals(other, e))
                    adj.Add(other);
            foreach (var other in byVertex[e.Q])
                if (!ReferenceEquals(other, e))
                    adj.Add(other);
            adjacency[e] = adj;
        }
        return adjacency;
    }

    /// <summary>
    /// Double-buffered Laplacian relaxation of boundary edge stress. Each iteration
    /// blends every edge's CompressionStress, ShearStress, and StressDirection toward
    /// the unweighted mean of its boundary-adjacent neighbors using blendWeight.
    /// Edges with no neighbors are left unchanged.
    /// </summary>
    private static void SmoothBoundaryEdgeStress(
        Continent continent,
        Dictionary<Edge, List<Edge>> adjacency,
        int iterations,
        float blendWeight
    )
    {
        var edges = continent.boundaryEdges.Where(e => e.Stress != null).ToList();
        if (edges.Count == 0)
            return;

        var compression = new Dictionary<Edge, float>(edges.Count);
        var shear = new Dictionary<Edge, float>(edges.Count);
        var direction = new Dictionary<Edge, Vector3>(edges.Count);
        foreach (var e in edges)
        {
            compression[e] = e.Stress!.CompressionStress;
            shear[e] = e.Stress.ShearStress;
            direction[e] = e.Stress.StressDirection;
        }

        var nextCompression = new Dictionary<Edge, float>(edges.Count);
        var nextShear = new Dictionary<Edge, float>(edges.Count);
        var nextDirection = new Dictionary<Edge, Vector3>(edges.Count);

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var e in edges)
            {
                if (!adjacency.TryGetValue(e, out var nbrs) || nbrs.Count == 0)
                {
                    nextCompression[e] = compression[e];
                    nextShear[e] = shear[e];
                    nextDirection[e] = direction[e];
                    continue;
                }

                float sumC = 0f, sumS = 0f;
                Vector3 sumDir = Vector3.Zero;
                int count = 0;
                foreach (var n in nbrs)
                {
                    if (!compression.ContainsKey(n))
                        continue;
                    sumC += compression[n];
                    sumS += shear[n];
                    sumDir += direction[n];
                    count++;
                }
                if (count == 0)
                {
                    nextCompression[e] = compression[e];
                    nextShear[e] = shear[e];
                    nextDirection[e] = direction[e];
                    continue;
                }

                float avgC = sumC / count;
                float avgS = sumS / count;
                Vector3 avgDir = sumDir / count;

                nextCompression[e] = Mathf.Lerp(compression[e], avgC, blendWeight);
                nextShear[e] = Mathf.Lerp(shear[e], avgS, blendWeight);
                Vector3 blendedDir = direction[e].Lerp(avgDir, blendWeight);
                nextDirection[e] = blendedDir.LengthSquared() > 0f
                    ? blendedDir.Normalized()
                    : direction[e];
            }

            (compression, nextCompression) = (nextCompression, compression);
            (shear, nextShear) = (nextShear, shear);
            (direction, nextDirection) = (nextDirection, direction);
        }

        foreach (var e in edges)
        {
            e.Stress!.CompressionStress = compression[e];
            e.Stress.ShearStress = shear[e];
            e.Stress.StressDirection = direction[e];
        }
    }

    /// <summary>
    /// After smoothing, re-derive each boundary edge's EdgeType from its smoothed stress
    /// and recompute per-border-cell Stress accumulation from the smoothed edge values.
    /// Cell stress is zeroed for this continent's boundary cells first so that the
    /// downstream interior BFS propagation sees only smoothed contributions.
    /// </summary>
    private void ReclassifyBoundaryEdges(
        Continent continent,
        IReadOnlyDictionary<EdgeKey, HashSet<VoronoiCell>> edgeMap
    )
    {
        foreach (var cell in continent.boundaryCells)
        {
            cell.Stress = 0f;
        }

        foreach (var e in continent.boundaryEdges)
        {
            if (e.Stress == null)
                continue;
            e.Type = ClassifyBoundaryType(e.Stress);

            if (!edgeMap.TryGetValue(e.key, out var neighbors) || neighbors.Count < 2)
                continue;
            VoronoiCell? borderCell = null;
            foreach (var c in neighbors)
            {
                if (c.ContinentIndex == continent.StartingIndex)
                {
                    borderCell = c;
                    break;
                }
            }
            if (borderCell == null)
                continue;

            float totalStress =
                MathF.Abs(e.Stress.CompressionStress) * 0.8f
                + MathF.Abs(e.Stress.ShearStress) * 0.3f;
            borderCell.Stress += totalStress;
        }
    }
}
