using Godot;
using Structures.MeshGeneration;
using Structures.GameState;
using System;
using System.Collections.Generic;
using System.Linq;
using UtilityLibrary;

namespace ProceduralGeneration.MeshGeneration;

public static class TectonicGenerationHelpers
{
    public static void CalculateBoundaryStress(
        StructureDatabase strDb,
        IReadOnlyDictionary<EdgeKey, HashSet<VoronoiCell>> edgeMap,
        HashSet<Point> points,
        Dictionary<int, Continent> continents,
        GenericPercent percent,
        RandomNumberGenerator rand,
        float stressScale,
        float shearScale,
        float maxPropagationDistance,
        float propagationFalloff)
    {
        GD.Print($"Calculating Boundary Stress\n{continents.Count}\n");
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
            GD.Print($"Boundary Cells: {continent.boundaryCells.Count}");
            foreach (Edge e in continent.boundaryEdges)
            {
                List<VoronoiCell> neighbors = new List<VoronoiCell>(edgeMap[e.key]);
                VoronoiCell neighborCell = null;
                VoronoiCell borderCell = null;
                if (neighbors.Count < 2) continue;
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
                    Vector3 projectedBorderCellMovement = uAxis * (borderCell.MovementDirection.X * continent.velocity) + vAxis * (borderCell.MovementDirection.Y * continent.velocity);
                    Vector3 projectedNeighborCellMovement = uAxis * (neighborCell.MovementDirection.X * continents[neighborCell.ContinentIndex].velocity) + vAxis * (neighborCell.MovementDirection.Y * continents[neighborCell.ContinentIndex].velocity);

                    Vector3 EdgeVector = (((Point)e.P).Position - ((Point)e.Q).Position).Normalized();
                    Vector3 EdgeNormal = EdgeVector.Cross(((Point)e.Q).Position.Normalized());

                    float bcVelNormal = projectedBorderCellMovement.Dot(EdgeNormal);
                    float ncVelNormal = projectedNeighborCellMovement.Dot(-EdgeNormal);

                    float bcVelTangent = projectedBorderCellMovement.Dot(-EdgeVector);
                    float ncVelTangent = projectedNeighborCellMovement.Dot(EdgeVector);

                    float compressionStrength = (bcVelNormal - ncVelNormal) * stressScale;
                    float shearStrength = (bcVelTangent - ncVelTangent) * shearScale;
                    if (float.IsNaN(compressionStrength) || double.IsNaN(compressionStrength))
                        compressionStrength = 0.0f;
                    if (float.IsNaN(shearStrength) || double.IsNaN(shearStrength))
                        shearStrength = 0.0f;

                    EdgeStress calculatedStress = new EdgeStress
                    {
                        CompressionStress = compressionStrength,
                        ShearStress = shearStrength,
                        StressDirection = EdgeNormal
                    };
                    if (float.IsNaN(calculatedStress.CompressionStress) || double.IsNaN(calculatedStress.CompressionStress) || float.IsNaN(calculatedStress.ShearStress) || double.IsNaN(calculatedStress.ShearStress))
                        GD.Print($"Stress: {calculatedStress.CompressionStress} + {calculatedStress.ShearStress}");
                    e.Stress = calculatedStress;
                    e.Type = ClassifyBoundaryType(calculatedStress, 0.1f);
                    float totalStress = MathF.Abs(calculatedStress.CompressionStress) * .8f + MathF.Abs(calculatedStress.ShearStress) * .3f;
                    borderCell.Stress += totalStress;
                }
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
                var neighbors = UnifiedCelestialMesh.GetCellNeighbors(cell, strDb);
                foreach (var neighbor in neighbors)
                {
                    neighbor.Stress = neighbor.Stress + (cell.Stress * Mathf.Pow(propagationFalloff, (float)cell.Interiorness)) / (neighbor.Increment);
                    neighbor.Increment++;
                    if (!visited.Contains(neighbor) && neighbor.ContinentIndex == continentIndex) queue.Enqueue(neighbor);
                }
            }
            continents[continentIndex] = continent;
            percent.PercentCurrent++;
        }
    }

    public static void ApplyStressToTerrain(
        StructureDatabase strDb,
        Dictionary<int, Continent> continents,
        List<VoronoiCell> cells,
        float generalHeightScale,
        float generalShearScale,
        float generalCompressionScale)
    {
        foreach (Point p in strDb.VoronoiCellVertices)
        {
            Edge[] edges = strDb.GetIncidentHalfEdges(p);
            GameLogger.Info($"# of Edges: {edges.Length}");
            float alteredHeight = 0.0f;
            foreach (Edge e in edges)
            {
                switch (e.Type)
                {
                    case EdgeType.inactive:
                        alteredHeight += e.StressMagnitude * generalHeightScale;
                        break;
                    case EdgeType.transform:
                        alteredHeight += e.Stress.ShearStress * generalShearScale;
                        break;
                    case EdgeType.divergent:
                        alteredHeight -= e.Stress.CompressionStress * generalCompressionScale;
                        break;
                    case EdgeType.convergent:
                        alteredHeight += e.Stress.CompressionStress * generalCompressionScale;
                        break;
                }
            }
            p.Height += alteredHeight;
        }
    }

    private static EdgeType ClassifyBoundaryType(EdgeStress es, float inactiveStressThreshold)
    {
        float normalizedCompression = Mathf.Abs(es.CompressionStress);
        float normalizedShear = Mathf.Abs(es.ShearStress);
        float totalStress = normalizedCompression + normalizedShear;

        if (totalStress < inactiveStressThreshold)
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
            else return EdgeType.transform;
        }
    }
}
