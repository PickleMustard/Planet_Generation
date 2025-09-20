using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using static MeshGeneration.StructureDatabase;

namespace MeshGeneration
{
    public static class EdgeStressCalculator
    {
        /// <summary>
        /// Calculates stress on boundary edges between continents based on relative movement
        /// </summary>
        /// <param name="edge">The edge to calculate stress for</param>
        /// <param name="cell1">First Voronoi cell sharing this edge</param>
        /// <param name="cell2">Second Voronoi cell sharing this edge</param>
        /// <param name="continents">Dictionary of all continents</param>
        /// <returns>Calculated stress value</returns>
        public static float CalculateBoundaryStress(Edge edge, VoronoiCell cell1, VoronoiCell cell2, Dictionary<int, Continent> continents)
        {
            // Check if both cells belong to different continents
            if (cell1.ContinentIndex == cell2.ContinentIndex)
                return 0f;

            // Get the continents
            Continent continent1 = continents[cell1.ContinentIndex];
            Continent continent2 = continents[cell2.ContinentIndex];

            // Calculate relative movement vector
            Vector2 relativeMovement = (cell1.MovementDirection * continent1.velocity) -
                                      (cell2.MovementDirection * continent2.velocity);

            // Calculate distance between cell centers
            float distance = (cell1.Center - cell2.Center).Length();

            // Normalize distance to a reasonable scale
            float normalizedDistance = distance / 2.0f; // Assuming sphere radius is 1

            // Calculate stress based on relative velocity and distance
            float stress = relativeMovement.LengthSquared() / (normalizedDistance + 0.1f);

            // Determine boundary type based on relative movement
            Continent.BOUNDARY_TYPE boundaryType = DetermineBoundaryType(continent1, continent2);

            // Apply boundary type modifiers
            switch (boundaryType)
            {
                case Continent.BOUNDARY_TYPE.Convergent:
                    stress *= 1.5f; // Higher stress for convergent boundaries
                    break;
                case Continent.BOUNDARY_TYPE.Divergent:
                    stress *= 1.2f; // Moderate stress for divergent boundaries
                    break;
                case Continent.BOUNDARY_TYPE.Transform:
                    stress *= 0.8f; // Lower stress for transform boundaries
                    break;
            }

            return stress;
        }

        /// <summary>
        /// Propagates stress from a source edge to connected edges with exponential decay
        /// </summary>
        /// <param name="sourceEdge">The edge to propagate stress from</param>
        /// <param name="voronoiCells">List of all Voronoi cells</param>
        /// <param name="decayFactor">Factor for exponential decay (0.0 to 1.0)</param>
        /// <returns>Dictionary mapping connected edges to their propagated stress values</returns>
        public static Dictionary<Edge, float> PropagateStress(Edge sourceEdge, List<VoronoiCell> voronoiCells, float decayFactor = 0.7f)
        {
            Dictionary<Edge, float> propagatedStress = new Dictionary<Edge, float>();

            // Get cells that share this edge
            HashSet<VoronoiCell> edgeCells = EdgeMap.ContainsKey(sourceEdge) ? EdgeMap[sourceEdge] : new HashSet<VoronoiCell>();

            if (edgeCells.Count == 0)
                return propagatedStress;

            // For each cell sharing the edge, propagate to connected edges
            foreach (VoronoiCell cell in edgeCells)
            {
                // Propagate to all edges of this cell
                foreach (Edge connectedEdge in cell.Edges)
                {
                    if (connectedEdge == sourceEdge)
                        continue;

                    // Calculate distance between edge midpoints
                    Vector3 sourceMidpoint = (sourceEdge.P.ToVector3() + sourceEdge.Q.ToVector3()) / 2.0f;
                    Vector3 connectedMidpoint = (connectedEdge.P.ToVector3() + connectedEdge.Q.ToVector3()) / 2.0f;
                    float distance = (sourceMidpoint - connectedMidpoint).Length();

                    // Apply exponential decay
                    float propagatedValue = sourceEdge.CalculatedStress * Mathf.Pow(decayFactor, distance);

                    // Add to dictionary, keeping the maximum stress if edge already has a value
                    if (propagatedStress.ContainsKey(connectedEdge))
                    {
                        propagatedStress[connectedEdge] = Mathf.Max(propagatedStress[connectedEdge], propagatedValue);
                    }
                    else
                    {
                        propagatedStress[connectedEdge] = propagatedValue;
                    }
                }
            }

            return propagatedStress;
        }

        /// <summary>
        /// Calculates stress on interior edges using a spring model based on deformation
        /// </summary>
        /// <param name="edge">The edge to calculate stress for</param>
        /// <param name="cell">The Voronoi cell containing this edge</param>
        /// <param name="restLength">The rest length of the edge (optional, calculated if not provided)</param>
        /// <returns>Calculated stress value based on spring model</returns>
        public static float CalculateSpringStress(Edge edge, VoronoiCell cell, float restLength = -1f)
        {
            // Calculate current edge length
            float currentLength = (edge.P.ToVector3() - edge.Q.ToVector3()).Length();

            // If rest length not provided, estimate it based on average edge length in the cell
            if (restLength <= 0f)
            {
                float totalLength = 0f;
                foreach (Edge e in cell.Edges)
                {
                    totalLength += (e.P.ToVector3() - e.Q.ToVector3()).Length();
                }
                restLength = totalLength / cell.Edges.Length;
            }

            // Spring model: F = -k * (x - x0)
            // Stress is proportional to the deformation
            float deformation = Mathf.Abs(currentLength - restLength);
            float springConstant = 0.5f; // Adjustable parameter

            return springConstant * deformation;
        }

        /// <summary>
        /// Determines the boundary type between two continents based on their movement
        /// </summary>
        /// <param name="continent1">First continent</param>
        /// <param name="continent2">Second continent</param>
        /// <returns>Boundary type</returns>
        private static Continent.BOUNDARY_TYPE DetermineBoundaryType(Continent continent1, Continent continent2)
        {
            // Calculate relative movement vector
            Vector2 relativeMovement = continent2.movementDirection - continent1.movementDirection;
            float dotProduct = continent1.movementDirection.Dot(continent2.movementDirection);

            if (dotProduct > 0.5f) // Moving in similar directions
            {
                return Continent.BOUNDARY_TYPE.Transform;
            }
            else if (dotProduct < -0.5f) // Moving towards each other
            {
                return Continent.BOUNDARY_TYPE.Divergent;
            }
            else // Moving apart or perpendicular
            {
                return Continent.BOUNDARY_TYPE.Convergent;
            }
        }
    }
}
