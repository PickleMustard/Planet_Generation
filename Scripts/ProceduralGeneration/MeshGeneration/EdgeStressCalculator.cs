using System.Collections.Generic;
using Godot;
using Structures.MeshGeneration;
using Structures.GameState;

namespace ProceduralGeneration.MeshGeneration
{
    /// <summary>
    /// Provides static methods for calculating and propagating stress on mesh edges
    /// in a planetary generation system. This class handles boundary stress between
    /// continents, stress propagation through connected edges, and spring-based
    /// stress calculations for interior edges.
    /// </summary>
    public static class EdgeStressCalculator
    {
        /// <summary>
        /// Calculates stress on boundary edges between continents based on relative movement.
        /// This method determines the stress level at the boundary between two different
        /// continents by analyzing their relative velocities and movement directions.
        /// </summary>
        /// <param name="edge">The edge to calculate stress for, representing the boundary between cells</param>
        /// <param name="cell1">First Voronoi cell sharing this edge, belonging to one continent</param>
        /// <param name="cell2">Second Voronoi cell sharing this edge, belonging to another continent</param>
        /// <param name="continents">Dictionary mapping continent indices to Continent objects containing movement data</param>
        /// <returns>A float value representing the calculated stress magnitude. Returns 0 if cells belong to the same continent.</returns>
        /// <remarks>
        /// The stress calculation considers:
        /// - Relative movement vectors between continents
        /// - Distance between cell centers
        /// - Boundary type (convergent, divergent, or transform) with appropriate modifiers
        ///
        /// Boundary types and their stress modifiers:
        /// - Convergent: 1.5x multiplier (higher stress)
        /// - Divergent: 1.2x multiplier (moderate stress)
        /// - Transform: 0.8x multiplier (lower stress)
        /// </remarks>
        public static float CalculateBoundaryStress(Edge edge, VoronoiCell cell1, VoronoiCell cell2, Dictionary<int, Continent> continents)
        {
            UtilityLibrary.Logger.EnterFunction("CalculateBoundaryStress", $"edgeIndex={edge.Index}, cell1={cell1.Index}, cell2={cell2.Index}");
            // Check if both cells belong to different continents
            if (cell1.ContinentIndex == cell2.ContinentIndex)
            {
                UtilityLibrary.Logger.ExitFunction("CalculateBoundaryStress", "returned 0 (same continent)");
                return 0f;
            }

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

            UtilityLibrary.Logger.ExitFunction("CalculateBoundaryStress", $"returned stress={stress:F4}, boundaryType={boundaryType}");
            return stress;
        }

        /// <summary>
        /// Propagates stress from a source edge to connected edges with exponential decay.
        /// This method simulates how stress spreads through a mesh by distributing stress
        /// from a high-stress edge to its connected neighbors, with intensity decreasing
        /// based on distance.
        /// </summary>
        /// <param name="sourceEdge">The edge to propagate stress from, containing the initial stress magnitude</param>
        /// <param name="voronoiCells">List of all Voronoi cells in the mesh (currently unused but kept for API compatibility)</param>
        /// <param name="db">StructureDatabase containing edge-to-cell mappings for connectivity information</param>
        /// <param name="decayFactor">Factor for exponential decay (0.0 to 1.0). Lower values cause faster decay.</param>
        /// <returns>Dictionary mapping connected edges to their propagated stress values. Returns empty dictionary if source edge has no associated cells.</returns>
        /// <remarks>
        /// The propagation algorithm:
        /// 1. Finds all Voronoi cells that share the source edge
        /// 2. For each cell, examines all connected edges
        /// 3. Calculates distance between edge midpoints
        /// 4. Applies exponential decay: stress * (decayFactor ^ distance)
        /// 5. Keeps maximum stress value if multiple paths reach the same edge
        ///
        /// This creates a realistic stress distribution pattern where nearby edges
        /// receive more stress than distant ones.
        /// </remarks>
        public static Dictionary<Edge, float> PropagateStress(Edge sourceEdge, List<VoronoiCell> voronoiCells, StructureDatabase db, float decayFactor = 0.7f)
        {
            UtilityLibrary.Logger.EnterFunction("PropagateStress", $"edgeIndex={sourceEdge.Index}, decayFactor={decayFactor}");
            Dictionary<Edge, float> propagatedStress = new Dictionary<Edge, float>();

            // Get cells that share this edge
            HashSet<VoronoiCell> edgeCells = db.EdgeMap.ContainsKey(sourceEdge) ? db.EdgeMap[sourceEdge] : new HashSet<VoronoiCell>();
            UtilityLibrary.Logger.Info($"edgeCells.Count={edgeCells.Count}");

            if (edgeCells.Count == 0)
            {
                UtilityLibrary.Logger.ExitFunction("PropagateStress", "returned 0 edges (no cells)");
                return propagatedStress;
            }

            int updates = 0;
            // For each cell sharing the edge, propagate to connected edges
            foreach (VoronoiCell cell in edgeCells)
            {
                // Propagate to all edges of this cell
                foreach (Edge connectedEdge in cell.Edges)
                {
                    if (connectedEdge == sourceEdge)
                        continue;

                    // Calculate distance between edge midpoints
                    Vector3 sourceMidpoint = (((Point)sourceEdge.P).ToVector3() + ((Point)sourceEdge.Q).ToVector3()) / 2.0f;
                    Vector3 connectedMidpoint = (((Point)connectedEdge.P).ToVector3() + ((Point)connectedEdge.Q).ToVector3()) / 2.0f;
                    float distance = (sourceMidpoint - connectedMidpoint).Length();

                    // Apply exponential decay
                    float propagatedValue = sourceEdge.StressMagnitude * Mathf.Pow(decayFactor, distance);

                    // Add to dictionary, keeping the maximum stress if edge already has a value
                    if (propagatedStress.ContainsKey(connectedEdge))
                    {
                        propagatedStress[connectedEdge] = Mathf.Max(propagatedStress[connectedEdge], propagatedValue);
                    }
                    else
                    {
                        propagatedStress[connectedEdge] = propagatedValue;
                    }
                    updates++;
                }
            }

            UtilityLibrary.Logger.ExitFunction("PropagateStress", $"returned edges={propagatedStress.Count}, updates={updates}");
            return propagatedStress;
        }

        /// <summary>
        /// Calculates stress on interior edges using a spring model based on deformation.
        /// This method simulates elastic stress within a continent by treating edges as
        /// springs that resist deformation from their natural rest length.
        /// </summary>
        /// <param name="edge">The edge to calculate stress for, representing a spring element</param>
        /// <param name="cell">The Voronoi cell containing this edge, used for calculating average edge lengths</param>
        /// <param name="restLength">The rest length of the edge (optional). If negative or zero, calculates average edge length from the cell.</param>
        /// <returns>A float value representing the spring-based stress. Higher values indicate greater deformation.</returns>
        /// <remarks>
        /// The spring model uses Hooke's law: F = -k * (x - x0)
        /// Where:
        /// - k is the spring constant (currently 0.5)
        /// - x is the current edge length
        /// - x0 is the rest length
        ///
        /// If no rest length is provided, the method estimates it by calculating
        /// the average length of all edges in the containing Voronoi cell.
        /// This provides a reasonable baseline for natural edge lengths within
        /// the local mesh structure.
        /// </remarks>
        public static float CalculateSpringStress(Edge edge, VoronoiCell cell, float restLength = -1f)
        {
            UtilityLibrary.Logger.EnterFunction("CalculateSpringStress", $"edgeIndex={edge.Index}, cellIndex={cell.Index}, restLength={restLength}");
            // Calculate current edge length
            float currentLength = (((Point)edge.P).ToVector3() - ((Point)edge.Q).ToVector3()).Length();

            // If rest length not provided, estimate it based on average edge length in the cell
            if (restLength <= 0f)
            {
                float totalLength = 0f;
                foreach (Edge e in cell.Edges)
                {
                    totalLength += (((Point)e.P).ToVector3() - ((Point)e.Q).ToVector3()).Length();
                }
                restLength = totalLength / cell.Edges.Length;
            }

            // Spring model: F = -k * (x - x0)
            // Stress is proportional to the deformation
            float deformation = Mathf.Abs(currentLength - restLength);
            float springConstant = 0.5f; // Adjustable parameter

            float result = springConstant * deformation;
            UtilityLibrary.Logger.ExitFunction("CalculateSpringStress", $"returned stress={result:F4}");
            return result;
        }

        /// <summary>
        /// Determines the boundary type between two continents based on their movement directions.
        /// This helper method classifies tectonic boundaries by analyzing the relative
        /// movement patterns of adjacent continents.
        /// </summary>
        /// <param name="continent1">First continent with movementDirection property</param>
        /// <param name="continent2">Second continent with movementDirection property</param>
        /// <returns>A BOUNDARY_TYPE enum value indicating the type of tectonic boundary</returns>
        /// <remarks>
        /// Boundary classification logic:
        /// - Transform boundary: dot product > 0.5 (continents moving in similar directions)
        /// - Divergent boundary: dot product < -0.5 (continents moving towards each other)
        /// - Convergent boundary: all other cases (continents moving apart or perpendicular)
        ///
        /// This classification helps determine appropriate stress modifiers in the
        /// CalculateBoundaryStress method, simulating real-world tectonic interactions.
        /// </remarks>
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
