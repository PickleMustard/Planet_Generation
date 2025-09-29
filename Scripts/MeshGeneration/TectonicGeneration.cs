using Godot;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using UtilityLibrary;
using MeshGeneration;

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
        float generalCompressionScale)
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
        IReadOnlyDictionary<Edge, HashSet<VoronoiCell>> edgeMap,
        HashSet<Point> points,
        Dictionary<int, Continent> continents,
        GenericPercent percent)
    {
        GD.PrintRaw($"Calculating Boundary Stress\n{continents.Count}\n");
        // Calculate stress between neighboring continents
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            int continentIndex = continentPair.Key;
            Continent continent = continentPair.Value;
            Vector3 v1 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            Vector3 v2 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            Vector3 UnitNorm = v1.Cross(v2);
            if (UnitNorm.Dot(continent.averagedCenter) < 0f)
            {
                UnitNorm = -UnitNorm;
            }
            Vector3 uAxis = v1;
            Vector3 vAxis = UnitNorm.Cross(uAxis);
            uAxis = uAxis.Normalized();
            vAxis = vAxis.Normalized();
            foreach (VoronoiCell borderCell in continent.boundaryCells)
            {
                foreach (Edge e in borderCell.Edges)
                {
                    List<VoronoiCell> neighbors = new List<VoronoiCell>(edgeMap[e]);
                    VoronoiCell[] original = new VoronoiCell[] { borderCell };
                    List<VoronoiCell> neighbors2 = new List<VoronoiCell>(neighbors.Except(original));
                    VoronoiCell neighborCell = null;
                    if (neighbors2.Count > 0)
                    {
                        neighborCell = neighbors2.First();
                    }
                    else
                    {
                        continue;
                    }
                    if (neighborCell != null && neighborCell.ContinentIndex != borderCell.ContinentIndex)
                    {
                        Vector3 projectedBorderCellMovement = uAxis * (borderCell.MovementDirection.X * continent.velocity) + vAxis * (borderCell.MovementDirection.Y * continent.velocity);
                        Vector3 projectedNeighborCellMovement = uAxis * (neighborCell.MovementDirection.X * continents[neighborCell.ContinentIndex].velocity) + vAxis * (neighborCell.MovementDirection.Y * continents[neighborCell.ContinentIndex].velocity);

                        Vector3 EdgeVector = (((Point)e.P).Position - ((Point)e.Q).Position).Normalized();
                        Vector3 EdgeNormal = EdgeVector.Cross(((Point)e.Q).Position.Normalized());

                        float bcVelNormal = projectedBorderCellMovement.Dot(EdgeNormal);
                        float ncVelNormal = projectedNeighborCellMovement.Dot(EdgeNormal);

                        float bcVelTangent = projectedBorderCellMovement.Dot(EdgeVector);
                        float ncVelTangent = projectedNeighborCellMovement.Dot(EdgeVector);

                        EdgeStress calculatedStress = new EdgeStress
                        {
                            CompressionStress = (bcVelNormal - ncVelNormal) * StressScale,
                            ShearStress = (bcVelTangent - ncVelTangent) * ShearScale,
                            StressDirection = EdgeNormal
                        };
                        e.Stress = calculatedStress;
                        e.Type = ClassifyBoundaryType(calculatedStress);
                        PriorityQueue<Edge, float> toVisit = new PriorityQueue<Edge, float>();
                        HashSet<Edge> visited = new HashSet<Edge>();
                        visited.Add(e);
                        toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges(e.Q).ToArray(), 0.0f);
                        toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges((Point)e.P).ToArray(), 0.0f);
                        while (toVisit.Count > 0)
                        {
                            Edge current;
                            float distance;
                            bool success = toVisit.TryDequeue(out current, out distance);
                            if (!success) break;
                            if (visited.Contains(current) || distance > MaxPropagationDistance) continue;
                            visited.Add(current);
                            float magnitude = CalculateStressAtDistance(e.Stress, distance, current, e);
                            current.StressMagnitude += magnitude;
                            toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges(current.Q).ToArray(), (current.Midpoint - e.Midpoint).Length());
                            toVisit.EnqueueRange(StrDb.GetIncidentHalfEdges(current.P).ToArray(), (current.Midpoint - e.Midpoint).Length());
                        }

                    }
                }
            }
            continents[continentIndex] = continent;
            percent.PercentCurrent++;
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
            Logger.Info($"# of Edges: {edges.Length}");
            float alteredHeight = 0.0f;
            foreach (Edge e in edges)
            {
                //GD.PrintRaw($"Edge: {e} with stress: {e.TotalStress} from {e.CalculatedStress} and {e.PropogatedStress}, Edge Type: {e.Type}\n");
                switch (e.Type)
                {
                    case EdgeType.inactive:
                        alteredHeight += e.StressMagnitude * GeneralHeightScale;
                        break;
                    case EdgeType.transform:
                        alteredHeight += e.Stress.ShearStress * GeneralShearScale;
                        break;
                    case EdgeType.divergent:
                        alteredHeight -= e.Stress.CompressionStress * GeneralCompressionScale;
                        break;
                    case EdgeType.convergent:
                        alteredHeight += e.Stress.CompressionStress * GeneralCompressionScale;
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
        if (compressionFactor > 0.56f)
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
            else return EdgeType.transform;
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
    private float CalculateStressAtDistance(EdgeStress edgeStress, float distance, Edge current, Edge origin)
    {
        float decayFactor = MathF.Exp(-distance / PropagationFalloff);
        float totalStress = MathF.Abs(edgeStress.CompressionStress) + MathF.Abs(edgeStress.ShearStress) * .5f;
        Vector3 toEdge = (current.Midpoint - origin.Midpoint).Normalized();
        float directionalFactor = MathF.Abs(toEdge.Dot(edgeStress.StressDirection));
        return totalStress * decayFactor * directionalFactor;
    }
}
