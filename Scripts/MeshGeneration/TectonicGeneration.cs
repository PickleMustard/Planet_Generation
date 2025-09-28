using Godot;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using UtilityLibrary;
using MeshGeneration;

public class TectonicGeneration
{
    private readonly StructureDatabase StrDb;
    private readonly RandomNumberGenerator rand;
    private readonly float StressScale;
    private readonly float ShearScale;
    private readonly float MaxPropagationDistance;
    private readonly float PropagationFalloff;
    private readonly float InactiveStressThreshold;
    private readonly float GeneralHeightScale;
    private readonly float GeneralShearScale;
    private readonly float GeneralCompressionScale;

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

    public void CalculateBoundaryStress(
        Dictionary<Edge, HashSet<VoronoiCell>> edgeMap,
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
                        toVisit.EnqueueRange(StrDb.GetEdgesFromPoint(e.Q).ToArray(), 0.0f);
                        toVisit.EnqueueRange(StrDb.GetEdgesFromPoint((Point)e.P).ToArray(), 0.0f);
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
                            toVisit.EnqueueRange(StrDb.GetEdgesFromPoint(current.Q).ToArray(), (current.Midpoint - e.Midpoint).Length());
                            toVisit.EnqueueRange(StrDb.GetEdgesFromPoint(current.P).ToArray(), (current.Midpoint - e.Midpoint).Length());
                        }

                    }
                }
            }
            continents[continentIndex] = continent;
            percent.PercentCurrent++;
        }
    }

    public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        foreach (Point p in StrDb.VoronoiCellVertices)
        {
            Edge[] edges = StrDb.GetEdgesFromPoint(p);
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

    private float CalculateStressAtDistance(EdgeStress edgeStress, float distance, Edge current, Edge origin)
    {
        float decayFactor = MathF.Exp(-distance / PropagationFalloff);
        float totalStress = MathF.Abs(edgeStress.CompressionStress) + MathF.Abs(edgeStress.ShearStress) * .5f;
        Vector3 toEdge = (current.Midpoint - origin.Midpoint).Normalized();
        float directionalFactor = MathF.Abs(toEdge.Dot(edgeStress.StressDirection));
        return totalStress * decayFactor * directionalFactor;
    }
}
