using Godot;
using MeshGeneration;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//using static Structures.Point;
using static MeshGeneration.StructureDatabase;
using static Structures.Biome;


public partial class GenerateDocArrayMesh : MeshInstance3D
{
    public static GenericPercent percent;
    public DelaunatorSharp.Delaunator dl;
    Vector3 origin = new Vector3(0, 0, 0);
    public int VertexIndex = 0;
    public int testIndex = 0;
    public static float maxHeight = 0;


    //public static Dictionary<(float, float, float), Point> triCircumcenters = new Dictionary<(float, float, float), Point>();

    public static int VoronoiCellCount = 0;
    public static int CurrentlyProcessingVoronoiCount = 0;

    public List<Face> dualFaces = new List<Face>();
    public List<Edge> generatedEdges = new List<Edge>();
    RandomNumberGenerator rand = new RandomNumberGenerator();

    public static GenerateDocArrayMesh instance;

    [ExportCategory("Planet Generation")]
    [ExportGroup("Mesh Generation")]
    [Export]
    public int subdivide = 1;
    [Export]
    public int[] VerticesPerEdge = new int[] { 1, 2, 4 };
    [Export]
    public int size = 5;
    [Export]
    public bool ProjectToSphere = true;
    [Export]
    public ulong Seed = 5001;
    [Export]
    public int NumAbberations = 3;
    [Export]
    public int NumDeformationCycles = 3;

    [ExportGroup("Tectonic Settings")]
    [Export]
    public int NumContinents = 5;
    [Export]
    public float ConvergentBoundaryStrength = 0.5f;
    [Export]
    public float DivergentBoundaryStrength = 0.3f;
    [Export]
    public float TransformBoundaryStrength = 0.1f;

    [ExportGroup("Finalized Object")]
    [Export]
    public bool GenerateRealistic = true;
    [Export]
    public bool ShouldDisplayBiomes = true;

    [ExportCategory("Generation Debug")]
    [ExportGroup("Debug")]
    [Export]
    public bool AllTriangles = false;
    [Export]
    public bool ShouldDrawArrowsInterface = false;

    public static bool ShouldDrawArrows = false;

    public override void _Ready()
    {
        ShouldDrawArrows = ShouldDrawArrowsInterface;
        percent = new GenericPercent();
        rand.Seed = Seed;
        GD.Print($"Rand Seed: {rand.Seed}");
        PolygonRendererSDL.DrawPoint(this, size, new Vector3(0, 0, 0), 0.1f, Colors.White);
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        Task generatePlanet = Task.Factory.StartNew(() => GeneratePlanetAsync());


        GD.Print($"Number of Vertices: {circumcenters.Values.Count}");
    }

    private void GeneratePlanetAsync()
    {
        Task firstPass = Task.Factory.StartNew(() => GenerateFirstPass());
        Task.WaitAll(firstPass);
        Task secondPass = Task.Factory.StartNew(() => GenerateSecondPass());
    }

    private void GenerateFirstPass()
    {
        GD.Print($"Rand Seed: {rand.Seed}");
        GenericPercent emptyPercent = new GenericPercent();
        BaseMeshGeneration baseMesh = new BaseMeshGeneration(rand, subdivide, VerticesPerEdge);
        emptyPercent.PercentTotal = 0;
        var function = FunctionTimer.TimeFunction<int>(
            "Base Mesh Generation",
            () =>
            {
                baseMesh.PopulateArrays();
                baseMesh.GenerateNonDeformedFaces();
                baseMesh.GenerateTriangleList();
                return 0;
            }, emptyPercent);


        var OptimalArea = (4.0f * Mathf.Pi * size * size) / BaseTris.Count;
        float OptimalSideLength = Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;
        function = FunctionTimer.TimeFunction<int>("Deformed Mesh Generation", () =>
        {
            try
            {
                baseMesh.InitiateDeformation(NumDeformationCycles, NumAbberations, OptimalSideLength);
            }
            catch (Exception e)
            {
                GD.PrintRaw($"\nDeform Mesh Error:  {e.Message}\n{e.StackTrace}\n");
            }
            return 0;
        }, emptyPercent);

        GD.Print("Deformed Base Mesh");
        if (AllTriangles)
        {
            foreach (var triangle in BaseTris)
            {
                RenderTriangleAndConnections(triangle.Value);
            }
        }

    }

    private void GenerateSecondPass()
    {
        VoronoiCellGeneration voronoiCellGeneration = new VoronoiCellGeneration();
        GenericPercent emptyPercent = new GenericPercent();
        emptyPercent.PercentTotal = 0;
        percent.PercentTotal = VertexPoints.Count;
        GD.PrintRaw($"Generating Voronoi Cells | {VertexPoints.Count}");
        var function = FunctionTimer.TimeFunction<int>("Voronoi Cell Generation", () =>
        {
            voronoiCellGeneration.GenerateVoronoiCells(percent);
            return 0;
        }, percent);

        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        function = FunctionTimer.TimeFunction<int>("Flood Filling", () =>
        {
            continents = FloodFillContinentGeneration(VoronoiCells);
            return 0;
        }, emptyPercent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>("Calculate Voronoi Cells", () =>
        {
            foreach (VoronoiCell vc in VoronoiCells)
            {
                var cellNeighbors = GetCellNeighbors(vc);
                float averageHeight = vc.Height;
                List<Edge> OutsideEdges = new List<Edge>();
                List<int> BoundingContinentIndex = new List<int>();
                foreach (VoronoiCell neighbor in cellNeighbors)
                {
                    //GD.Print($"VC: {vc.Index}, {vc.ContinentIndex} | Neighbor: {VoronoiCells[neighbor].Index}, {VoronoiCells[neighbor].ContinentIndex}");
                    //GD.Print($"Continent: {continents[vc.ContinentIndex]}, Boundary Cells: {continents[vc.ContinentIndex].boundaryCells}");
                    if (neighbor.ContinentIndex != vc.ContinentIndex)
                    {
                        vc.IsBorderTile = true;
                        continents[vc.ContinentIndex].boundaryCells.Add(vc);
                        continents[vc.ContinentIndex].neighborContinents.Add(neighbor.ContinentIndex);
                        continents[neighbor.ContinentIndex].neighborContinents.Add(vc.ContinentIndex);
                        neighbor.IsBorderTile = true;
                        BoundingContinentIndex.Add(neighbor.ContinentIndex);
                        foreach (Point p in vc.Points)
                        {
                            foreach (Point p2 in neighbor.Points)
                            {
                                if (p.Equals(p2))
                                {
                                    p.continentBorder = true;
                                }
                            }
                        }
                        foreach (Edge e in vc.Edges)
                        {
                            foreach (Edge e2 in neighbor.Edges)
                            {
                                if (e.Equals(e2) || e.ReverseEdge().Equals(e2))
                                {
                                    vc.EdgeBoundaryMap.Add(e, neighbor.ContinentIndex);
                                    OutsideEdges.Add(e);
                                }
                            }
                        }

                    }
                    //averageHeight += VoronoiCells[neighbor].Height;
                }
                vc.BoundingContinentIndex = BoundingContinentIndex.ToArray();
                vc.OutsideEdges = OutsideEdges.ToArray();
                vc.Height = averageHeight / (cellNeighbors.Length + 1);
                foreach (Point p in vc.Points)
                {
                    p.ContinentIndecies.Add(vc.ContinentIndex);
                    //GD.PrintRaw($"Point {p} is in Continent {vc.ContinentIndex}\n");
                }
            }
            return 0;
        }, percent);
        emptyPercent.Reset();
        function = FunctionTimer.TimeFunction<int>("Average out Heights", () =>
        {
            foreach (Point p in VoronoiCellVertices)
            {
                //GD.PrintRaw($"{p}, {CellMap.ContainsKey(p)}\n");
            }
            try
            {
                UpdateVertexHeights(VoronoiCellVertices, continents);
            }
            catch (Exception e)
            {
                GD.PrintRaw($"\nHeight Average Error:  {e.Message}\n{e.StackTrace}\n");
            }

            //foreach (Point p in circumcenters.Values)
            //{
            //    var EdgesFrom = worldHalfEdgeMapFrom[p].Values.ToList();
            //    var EdgesTo = worldHalfEdgeMapTo[p].Values.ToList();
            //    HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
            //    foreach (Edge e in EdgesFrom)
            //    {
            //        foreach (Triangle t in VoronoiEdgeTriMap[e])
            //        {
            //            trianglesWithPoint.Add(t);
            //        }
            //    }
            //    foreach (Edge e in EdgesTo)
            //    {
            //        foreach (Triangle t in VoronoiEdgeTriMap[e])
            //        {
            //            trianglesWithPoint.Add(t);
            //        }
            //    }

            //    float height = p.Height;
            //    int counter = 1;
            //    foreach (Triangle t in trianglesWithPoint)
            //    {
            //        foreach (Point p2 in t.Points)
            //        {
            //            height += p2.Height;
            //            counter++;
            //        }
            //    }
            //    height /= counter;
            //    //p.Height = height;

            //    //var pointEdges = baseEdges.Where(e => e.P == p || e.Q == p);
            //}
            return 0;
        }, emptyPercent);
        percent.PercentTotal = continents.Count;
        function = FunctionTimer.TimeFunction<int>("Calculate Boundary Stress", () =>
        {
            try
            {
                CalculateBoundaryStress(EdgeMap, VoronoiCellVertices, continents);
            }
            catch (Exception boundsError)
            {
                GD.PrintRaw($"\nBoundary Stress Error:  {boundsError.Message}\n{boundsError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>("Apply Stress to Terrain", () =>
        {
            try
            {
                ApplyStressToTerrain(continents, VoronoiCells);
                //UpdateVertexHeights(VoronoiCellVertices, continents);
            }
            catch (Exception stressError)
            {
                GD.PrintRaw($"\nStress Error:  {stressError.Message}\n{stressError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        maxHeight = VoronoiCellVertices.Max(p => p.Height);
        function = FunctionTimer.TimeFunction<int>("Assign Biomes", () =>
        {
            try
            {
                AssignBiomes(continents, VoronoiCells);
            }
            catch (Exception biomeError)
            {
                GD.PrintRaw($"\nBiome Error:  {biomeError.Message}\n{biomeError.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        percent.PercentTotal = continents.Values.Count;
        percent.PercentCurrent = 0;
        function = FunctionTimer.TimeFunction<int>("Generate From Continents", () =>
        {
            try
            {
                GenerateFromContinents(continents);
            }
            catch (Exception genError)
            {
                GD.PrintRaw($"\nGenerate From Continents Error:  {genError.Message}\n{genError.StackTrace}\n");
            }
            DrawContinentBorders(continents);
            return 0;
        }, percent);


    }

    private void AssignBiomes(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        foreach (var continent in continents)
        {
            Continent c = continent.Value;
            c.averageMoisture = BiomeAssigner.CalculateMoisture(c, rand, 0.5f);
            foreach (var cell in c.cells)
            {
                foreach (Point p in cell.Points)
                {
                    p.Biome = BiomeAssigner.AssignBiome(p.Height, c.averageMoisture);
                }
            }
        }
    }

    ///<summary>
    ///Goes through all the vertices and updates their heights based on the height of their containing continents
    ///</summary>
    ///<param name="Vertices">Dictionary of Index of Vertex to Point object </param>
    ///<param name="Continents">Dictionary of Index of starting Voronoi Cell in Continent to Continent </param>
    ///<returns>void</returns>
    public void UpdateVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            //GD.PrintRaw($"\nNumber of Continents Point {p} is in: {p.ContinentIndecies.Count}");
            if (p.ContinentIndecies.Count == 0)
            {
                GD.PrintRaw("This should never happen\n");
            }
            if (p.ContinentIndecies.Count == 1)
            {
                p.Height = Continents[p.ContinentIndecies.ElementAt(0)].averageHeight;
            }
            else if (p.ContinentIndecies.Count > 1)
            {
                float height = 0;
                foreach (int continentIndex in p.ContinentIndecies)
                {
                    height += Continents[continentIndex].averageHeight;
                }
                height /= p.ContinentIndecies.Count;
                p.Height = height;
            }
        }
    }

    ///<summary>
    /// 1st goes through every edge and calculates the stress on it depending on the type of edge it is.
    /// 2nd goes through every vertex and propogates out the stress from edges directly connected to it through neighbors up to a depth of 3
    /// </summary>
    public void CalculateBoundaryStress(Dictionary<Edge, HashSet<VoronoiCell>> edgeMap, HashSet<Point> points, Dictionary<int, Continent> continents)
    {

        //GD.PrintRaw($"Calculating Boundary Stress\n");
        // Calculate stress between neighboring continents
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            int continentIndex = continentPair.Key;
            Continent continent = continentPair.Value;
            Vector3 continentCenter = continent.averagedCenter;
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
                        float k = (1.0f - UnitNorm.X * borderCell.Center.X - UnitNorm.Y * borderCell.Center.Y - UnitNorm.Z * borderCell.Center.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                        Vector3 projectedCenterCell = new Vector3(borderCell.Center.X + k * UnitNorm.X, borderCell.Center.Y + k * UnitNorm.Y, borderCell.Center.Z + k * UnitNorm.Z);
                        Vector2 projectedCenterCell2D = new Vector2(uAxis.Dot(projectedCenterCell), vAxis.Dot(projectedCenterCell));

                        k = (1.0f - UnitNorm.X * neighborCell.Center.X - UnitNorm.Y * neighborCell.Center.Y - UnitNorm.Z * neighborCell.Center.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                        Vector3 projectedCenterNeighbor = new Vector3(neighborCell.Center.X + k * UnitNorm.X, neighborCell.Center.Y + k * UnitNorm.Y, neighborCell.Center.Z + k * UnitNorm.Z);
                        Vector2 projectedCenterNeighbor2D = new Vector2(uAxis.Dot(projectedCenterNeighbor), vAxis.Dot(projectedCenterNeighbor));

                        Vector2 relativePosition = projectedCenterCell2D - projectedCenterNeighbor2D;
                        float distance = relativePosition.LengthSquared();
                        Vector2 direction = relativePosition / distance;
                        Vector2 relativeMovement = (borderCell.MovementDirection * continent.velocity) - (neighborCell.MovementDirection * continents[neighborCell.ContinentIndex].velocity);                                                      //VoronoiCell[] neighbors = GetCellNeighbors(borderCell, false);

                        Vector3 directionPoint = uAxis * relativeMovement.X + vAxis * relativeMovement.Y;
                        directionPoint = directionPoint.Normalized();
                        float convergingVelocity = MathF.Acos(relativeMovement.Dot(direction) / (relativeMovement.Length() * direction.Length()));
                        if (convergingVelocity <= 3.0f * MathF.PI / 4.0f && convergingVelocity > MathF.PI / 4.0f)
                        {
                            e.CalculatedStress = relativeMovement.LengthSquared() / ((distance + 0.1f) * 5f) * ConvergentBoundaryStrength; // Adjust this factor as needed
                            e.Type = EdgeType.convergent;
                        }
                        else if (convergingVelocity >= 5.0f * MathF.PI / 4.0f && convergingVelocity < 7.0f * MathF.PI / 4.0f)
                        {
                            e.CalculatedStress = relativeMovement.LengthSquared() / ((distance + 0.1f) * 5f) * DivergentBoundaryStrength; // Adjust this factor as needed
                            e.Type = EdgeType.divergent;
                        }
                        else
                        {
                            e.CalculatedStress = relativeMovement.LengthSquared() / ((distance + 0.1f) * 5f) * TransformBoundaryStrength; // Adjust this factor as needed
                            e.Type = EdgeType.transform;
                        }
                    }
                    else
                    {
                        e.CalculatedStress = 0f;
                        e.Type = EdgeType.joined;
                    }
                }
                //foreach (VoronoiCell neighborCell in neighbors)
                //{
                //    float k = (1.0f - UnitNorm.X * borderCell.Center.X - UnitNorm.Y * borderCell.Center.Y - UnitNorm.Z * borderCell.Center.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                //    Vector3 projectedCenterCell = new Vector3(borderCell.Center.X + k * UnitNorm.X, borderCell.Center.Y + k * UnitNorm.Y, borderCell.Center.Z + k * UnitNorm.Z);
                //    Vector2 projectedCenterCell2D = new Vector2(uAxis.Dot(projectedCenterCell), vAxis.Dot(projectedCenterCell));

                //    k = (1.0f - UnitNorm.X * neighborCell.Center.X - UnitNorm.Y * neighborCell.Center.Y - UnitNorm.Z * neighborCell.Center.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                //    Vector3 projectedCenterNeighbor = new Vector3(neighborCell.Center.X + k * UnitNorm.X, neighborCell.Center.Y + k * UnitNorm.Y, neighborCell.Center.Z + k * UnitNorm.Z);
                //    Vector2 projectedCenterNeighbor2D = new Vector2(uAxis.Dot(projectedCenterNeighbor), vAxis.Dot(projectedCenterNeighbor));

                //    Vector2 relativePosition = projectedCenterCell2D - projectedCenterNeighbor2D;
                //    float distance = relativePosition.LengthSquared();
                //    Vector2 direction = relativePosition / distance;
                //    Vector2 relativeMovement = (borderCell.MovementDirection * continent.velocity) - (neighborCell.MovementDirection * continents[neighborCell.ContinentIndex].velocity);

                //    Vector3 directionPoint = uAxis * relativeMovement.X + vAxis * relativeMovement.Y;
                //    directionPoint = directionPoint.Normalized();
                //    //PolygonRendererSDL.DrawArrow(this, size + (continent.averageHeight / 100f), borderCell.Center, directionPoint, UnitNorm, 0.1f, Colors.Black);
                //    float convergingVelocity = relativeMovement.Dot(direction);
                //    //float strength = relativeMovement.LengthSquared() / ((distance + 0.1f) * 50.0f); // Adjust this factor as needed
                //    if (!continent.neighborStress.ContainsKey(neighborCell.ContinentIndex))
                //    {
                //        continent.neighborStress[neighborCell.ContinentIndex] = 0f;
                //    }
                //    if (convergingVelocity <= 0f)
                //    {
                //        continent.neighborStress[neighborCell.ContinentIndex] -= relativeMovement.LengthSquared();
                //    }
                //    else if (convergingVelocity > 0f && convergingVelocity < 1f)
                //    {
                //        continent.neighborStress[neighborCell.ContinentIndex] += relativeMovement.LengthSquared() / 4f;
                //    }
                //    else
                //    {
                //        continent.neighborStress[neighborCell.ContinentIndex] += relativeMovement.LengthSquared();
                //    }
                //}
            }
            //GD.PrintRaw($"Calculated Stress for Continent {continent.StartingIndex}\n");

            // Find neighboring continents
            //foreach (int neighborIndex in continent.neighborContinents)
            //{
            //    Continent neighbor = continents[neighborIndex];
            //    Vector3 neighborCenter = neighbor.averagedCenter;

            //    // Calculate relative movement vector
            //    Vector2 relativeMovement = neighbor.movementDirection - continent.movementDirection;
            //    float distance = (neighborCenter - continentCenter).LengthSquared();

            //    // Normalize distance to a reasonable scale (assuming sphere radius is 'size')
            //    //float normalizedDistance = distance / (2 * size);

            //    // Calculate stress based on relative velocity and distance
            //    // This is a simplified model - you might want to adjust the formula
            //    float stress = relativeMovement.LengthSquared() / (distance + 0.1f); // Add small value to avoid division by zero

            //    // Determine boundary type based on relative movement
            //    Continent.BOUNDARY_TYPE boundaryType;
            //    float dotProduct = continent.movementDirection.Dot(neighbor.movementDirection);

            //    if (dotProduct > 0.5f) // Moving in similar directions
            //    {
            //        boundaryType = Continent.BOUNDARY_TYPE.Transform;
            //    }
            //    else if (dotProduct < -0.5f) // Moving towards each other
            //    {
            //        boundaryType = Continent.BOUNDARY_TYPE.Divergent;
            //    }
            //    else // Moving apart or perpendicular
            //    {
            //        boundaryType = Continent.BOUNDARY_TYPE.Convergent;
            //    }

            //    // Update continent's stress and boundary type dictionaries
            //    continent.neighborStress[neighborIndex] = stress;
            //    continent.boundaryTypes[neighborIndex] = boundaryType;

            //    // Accumulate total stress
            //    continent.stressAccumulation += stress;
            //}

            // Save updated continent back to dictionary
            continents[continentIndex] = continent;
        }
        //GD.PrintRaw($"Calculated Boundary Stress\n");
        // Calculate propogated stress
        GD.PrintRaw($"Num of Edges: {edgeMap.Keys.Count}\n");
        foreach (Edge e in edgeMap.Keys)
        {
            Point origin = e.P;
            Point destination = e.Q;
            HashSet<Edge> visitedEdges = new HashSet<Edge>();
            try
            {
                if (e.CalculatedStress > 0.0f)
                {
                    CalculatePropogatedStress(e, e, origin, 0, visitedEdges);
                    CalculatePropogatedStress(e, e, destination, 0, visitedEdges);
                }
            }
            catch (Exception err)
            {
                GD.PrintRaw($"\nPropogated Stress Error:  {err.Message}\n{err.StackTrace}\n");
            }
        }
        GD.PrintRaw($"Calculated Propogated Stress\n");
    }

    private void CalculatePropogatedStress(Edge origin, Edge lastEdge, Point branchPoint, int depth, HashSet<Edge> visitedEdges)
    {
        if (depth > 3)
        {
            return;
        }
        Edge[] edgesFromPoint = GetEdgesFromPoint(branchPoint);
        Edge[] lastEdges = new Edge[] { lastEdge };
        edgesFromPoint.Except(lastEdges);
        foreach (Edge e in edgesFromPoint)
        {
            if (visitedEdges.Contains(e)) continue;
            visitedEdges.Add(e);
            if (e.Type == EdgeType.joined)
            {
                if (e.ContinentIndex == origin.ContinentIndex)
                {
                    e.PropogatedStress += origin.CalculatedStress * MathF.Pow(2.0f, -depth);
                }
            }
            else
            {
                float distance = lastEdge.Midpoint.DistanceTo(origin.Midpoint);
                e.PropogatedStress += origin.CalculatedStress / distance;
            }
            Point nextPoint = e.Q == branchPoint ? e.P : e.Q;
            CalculatePropogatedStress(origin, e, nextPoint, depth + 1, visitedEdges);
        }
    }

    public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        foreach (Point p in VoronoiCellVertices)
        {
            Edge[] edges = GetEdgesFromPoint(p);
            float alteredHeight = 0.0f;
            foreach (Edge e in edges)
            {
                //GD.PrintRaw($"Edge: {e} with stress: {e.TotalStress} from {e.CalculatedStress} and {e.PropogatedStress}, Edge Type: {e.Type}\n");
                switch (e.Type)
                {
                    case EdgeType.joined: alteredHeight += e.TotalStress; break;
                    case EdgeType.transform: alteredHeight += e.TotalStress; break;
                    case EdgeType.divergent: alteredHeight -= e.TotalStress; break;
                    case EdgeType.convergent: alteredHeight += e.TotalStress; break;
                }
            }
            p.Height += alteredHeight;
        }

        // First, apply stress directly to boundary cells
        //foreach (KeyValuePair<int, Continent> continentPair in continents)
        //{
        //    int continentIndex = continentPair.Key;
        //    Continent continent = continentPair.Value;

        //    // Process each boundary cell of this continent
        //    foreach (VoronoiCell boundaryCell in continent.boundaryCells)
        //    {
        //        VoronoiCell[] neighbors = GetCellNeighbors(boundaryCell, false);
        //        foreach (VoronoiCell neighborCell in neighbors)
        //        {
        //            Vector2 relativeMovement = boundaryCell.MovementDirection - neighborCell.MovementDirection;
        //            float distance = (boundaryCell.Center - neighborCell.Center).LengthSquared();
        //            float strength = relativeMovement.LengthSquared() / ((distance + 0.1f)); // Adjust this factor as needed
        //                                                                                     // Get the boundary type and stress for this specific neighbor
        //            Continent.BOUNDARY_TYPE boundaryType = continent.boundaryTypes[neighborCell.ContinentIndex];

        //            // Apply height modification based on boundary type and stress
        //            switch (boundaryType)
        //            {
        //                case Continent.BOUNDARY_TYPE.Convergent:
        //                    // Compressing - might create mountains or trenches
        //                    boundaryCell.Height += strength * ConvergentBoundaryStrength;
        //                    break;
        //                case Continent.BOUNDARY_TYPE.Divergent:
        //                    // Pulling apart - might create rifts
        //                    boundaryCell.Height -= strength * DivergentBoundaryStrength; // Example multiplier
        //                    break;
        //                case Continent.BOUNDARY_TYPE.Transform:
        //                    // Sliding past - might create fault lines
        //                    // Could add some noise or specific patterns
        //                    boundaryCell.Height += (float)rand.RandfRange(-0.1f, 0.1f) * strength * TransformBoundaryStrength;
        //                    break;
        //            }

        //            // Update the height of all points in this boundary cell
        //            foreach (Point p in boundaryCell.Points)
        //            {
        //                p.Height = boundaryCell.Height;
        //            }
        //        }
        //    }
        //}



        //// Then, propagate stress from boundary cells to interior cells
        //foreach (KeyValuePair<int, Continent> continentPair in continents)
        //{
        //    Continent continent = continentPair.Value;

        //    // Create a list of cells to process, starting with boundary cells
        //    //List<VoronoiCell> cellsToProcess = new List<VoronoiCell>(continent.boundaryCells);

        //    foreach (VoronoiCell cell in continent.boundaryCells)
        //    {
        //        HashSet<VoronoiCell> alreadyProcessed = new HashSet<VoronoiCell>();
        //        VoronoiCell[] neighborIndices = GetCellNeighbors(cell);
        //        foreach (VoronoiCell neighborCell in neighborIndices)
        //        {
        //            if (!alreadyProcessed.Contains(neighborCell) && neighborCell.Index != cell.Index)
        //            {
        //                float heightDifference = Mathf.Round(cell.Height) - Mathf.Round(neighborCell.Height);
        //                float propagationFactor = 1.0f;//Mathf.Sqrt(distance) / distance; // Adjust this factor as needed
        //                neighborCell.Height += heightDifference * propagationFactor;
        //                foreach (Point p in neighborCell.Points)
        //                {
        //                    p.Height = neighborCell.Height;
        //                }
        //                alreadyProcessed.Add(neighborCell);
        //            }
        //        }

        //        foreach (VoronoiCell continentCell in continent.cells)
        //        {
        //            if (!alreadyProcessed.Contains(continentCell) && continentCell.Index != cell.Index)
        //            {
        //                float distance = (cell.Center - continentCell.Center).LengthSquared() * 1000.0f;
        //                float heightDifference = Mathf.Round(cell.Height) - Mathf.Round(continentCell.Height);
        //                float propagationFactor = 1.0f;//Mathf.Sqrt(distance) / distance; // Adjust this factor as needed
        //                continentCell.Height += heightDifference * propagationFactor;
        //                foreach (Point p in continentCell.Points)
        //                {
        //                    p.Height = continentCell.Height;
        //                }
        //                alreadyProcessed.Add(continentCell);
        //            }
        //        }

        //    }


        //    /*
        //    // Process cells in layers, moving inward from the boundaries
        //    while (cellsToProcess.Count > 0 || processedCellsQueue.Count > 0)
        //    {
        //        //GD.Print($"Cells to Process: {cellsToProcess.Count} | Processed Cells Queue: {processedCellsQueue.Count}");
        //        VoronoiCell currentCell = null;
        //        if (cellsToProcess.Count > 0)
        //        {
        //            currentCell = cellsToProcess[0];
        //            cellsToProcess.RemoveAt(0);
        //        }
        //        else
        //        {
        //            currentCell = processedCellsQueue.Dequeue();
        //        }

        //        // Skip if already processed
        //        if (processedCells.Contains(currentCell))
        //            continue;

        //        processedCells.Add(currentCell);

        //        // Get neighboring cells within the same continent
        //        int[] neighborIndices = GetCellNeighbors(cells, currentCell.Index);
        //        foreach (int neighborIndex in neighborIndices)
        //        {
        //            VoronoiCell neighborCell = cells[neighborIndex];

        //            // Only process neighbors that belong to the same continent and haven't been processed
        //            if (neighborCell.ContinentIndex == currentCell.ContinentIndex &&
        //                                                                          !processedCells.Contains(neighborCell))
        //            {
        //                // Calculate distance between cell centers
        //                float distance = (currentCell.Center - neighborCell.Center).Length();

        //                // Propagate a fraction of the height difference based on distance
        //                // This creates a smoothing effect from boundaries inward
        //                float heightDifference = currentCell.Height - neighborCell.Height;
        //                float propagationFactor = 0.7f / (distance + 1.0f); // Adjust this factor as needed
        //                neighborCell.Height += heightDifference * propagationFactor;

        //                // Add this neighbor to the processing queue
        //                cellsToProcess.Add(neighborCell);

        //                // Update the height of all points in this neighbor cell
        //                foreach (Point p in neighborCell.Points)
        //                {
        //                    p.Height = neighborCell.Height;
        //                }
        //            }
        //        }
        //    }
        //    */
        //}
    }

    public VoronoiCell[] GetCellNeighbors(VoronoiCell origin, bool includeSameContinent = true)
    {
        HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
        foreach (Edge e in origin.Edges)
        {
            var neighboringCells = EdgeMap[e];
            foreach (VoronoiCell vc in neighboringCells)
            {
                if (includeSameContinent)
                {
                    neighbors.Add(vc);
                }
                else if (vc.ContinentIndex != origin.ContinentIndex)
                {
                    neighbors.Add(vc);
                }
            }
        }
        return neighbors.ToArray();
    }

    public HashSet<int> GenerateStartingCells(List<VoronoiCell> cells)
    {
        HashSet<int> startingCells = new HashSet<int>();
        while (startingCells.Count < NumContinents && startingCells.Count < cells.Count)
        {
            int position = rand.RandiRange(0, cells.Count - 1);
            startingCells.Add(position);
        }
        return startingCells;
    }

    public void DrawContinentBorders(Dictionary<int, Continent> continents)
    {
        int maxAttempts = continents.Count * 10;
        int currentAttempts = 0;
        foreach (var vc in continents)
        {
            var boundaries = vc.Value.boundaryCells;
            foreach (var b in boundaries)
            {
                if (b.IsBorderTile)
                {
                    for (int i = 0; i < b.OutsideEdges.Length; i++)
                    {
                        Edge e1 = b.OutsideEdges[i];
                        Point p1 = e1.P;
                        Point p2 = e1.Q;
                        Vector3 pos1 = p1.ToVector3().Normalized() * (size + p1.Height / 100f);
                        Vector3 pos2 = p2.ToVector3().Normalized() * (size + p2.Height / 100f);
                        Color lineColor = Colors.Black;
                        //Color lineColor = vc.Value.boundaryTypes[b.EdgeBoundaryMap[b.OutsideEdges[i]]] switch
                        //{
                        //    Continent.BOUNDARY_TYPE.Convergent => Colors.Aqua,
                        //    Continent.BOUNDARY_TYPE.Divergent => Colors.Red,
                        //    Continent.BOUNDARY_TYPE.Transform => Colors.Green,
                        //    _ => Colors.Black
                        //};
                        PolygonRendererSDL.DrawLine(this, 1.005f, pos1, pos2, lineColor);
                    }
                }
            }
            currentAttempts++;
            if (currentAttempts > maxAttempts)
            {
                break;
            }
        }
    }

    public Dictionary<int, Continent> FloodFillContinentGeneration(List<VoronoiCell> cells)
    {
        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        HashSet<int> startingCells = GenerateStartingCells(cells);
        var queue = startingCells.ToList();
        int[] neighborChart = new int[cells.Count];
        for (int i = 0; i < neighborChart.Length; i++)
        {
            neighborChart[i] = -1;
        }
        foreach (int i in startingCells)
        {
            Continent.CRUST_TYPE crustType = rand.RandiRange(0, 100) > 33 ? Continent.CRUST_TYPE.Oceanic : Continent.CRUST_TYPE.Continental;
            float averageHeight = crustType == Continent.CRUST_TYPE.Oceanic ? rand.RandfRange(-20.0f, -5.0f) : rand.RandfRange(5.0f, 30.0f);
            float rotation = Mathf.DegToRad(rand.RandiRange(-360, 360));
            float velocity = rand.RandfRange(0.3f, 1.7f);
            var continent = new Continent(i,
                    new List<VoronoiCell>(),//cells
                    new HashSet<VoronoiCell>(), //Boundary cells
                    new HashSet<Point>(), //points
                    new List<Point>(), //Convex Hull
                    new Vector3(0f, 0f, 0f), //averaged center
                    new Vector3(0f, 0f, 0f), //u axis
                    new Vector3(0f, 0f, 0f), //v axis
                    new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f)), velocity, rotation,//movement direction, velocity & rotation
                    crustType, averageHeight, rand.RandfRange(1.0f, 5.0f), //crust type, average height, average moisture
                    new HashSet<int>(), 0f,
                    new Dictionary<int, float>(),
                    new Dictionary<int, Continent.BOUNDARY_TYPE>());
            neighborChart[i] = i;
            continent.StartingIndex = i;
            continent.cells.Add(cells[i]);
            foreach (Point p in cells[i].Points)
            {
                continent.points.Add(p);
                //GD.PrintRaw($"Does VoronoiCellVertices contain {p}? {VoronoiCellVertices.Contains(p)}\n");
                p.ContinentIndecies.Add(continent.StartingIndex);
                //GD.PrintRaw($"Point {p} is in Continent {continent.StartingIndex}\n");
            }
            foreach (Edge e in cells[i].Edges)
            {
                e.ContinentIndex = i;
            }
            continents[i] = continent;

            var neighborIndices = GetCellNeighbors(cells[i]);
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb.Index] == -1)
                {
                    neighborChart[nb.Index] = neighborChart[i];
                    queue.Add(nb.Index);
                }
            }
        }
        testIndex = queue[0];
        for (int i = 0; i < queue.Count; i++)
        {
            var pos = rand.RandiRange(i, (queue.Count - 1));
            var currentVCell = queue[pos];
            queue[pos] = queue[i];
            var neighborIndices = GetCellNeighbors(cells[currentVCell]);
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb.Index] == -1)
                {
                    neighborChart[nb.Index] = neighborChart[currentVCell];
                    queue.Add(nb.Index);
                }
            }
        }

        for (int i = 0; i < neighborChart.Length; i++)
        {
            if (neighborChart[i] != -1)
            {
                var continent = continents[neighborChart[i]];
                continent.cells.Add(cells[i]);
                cells[i].Height = continent.averageHeight;
                cells[i].ContinentIndex = continent.StartingIndex;
                foreach (Edge e in cells[i].Edges)
                {
                    e.ContinentIndex = neighborChart[i];
                }
                foreach (Point p in cells[i].Points)
                {
                    p.Position = p.Position.Normalized();
                    p.Height = continent.averageHeight;
                    continent.points.Add(p);
                    p.ContinentIndecies.Add(continent.StartingIndex);
                    //GD.PrintRaw($"Point {p} is in Continent {continent.StartingIndex}\n");
                }
            }
        }

        foreach (var keyValuePair in continents)
        {
            var continent = keyValuePair.Value;
            foreach (Point p in continent.points)
            {
                continent.averagedCenter += p.Position;
            }
            foreach (VoronoiCell vc in continent.cells)
            {
                vc.ContinentIndex = continent.StartingIndex;
            }

            continent.averagedCenter /= continent.points.Count;
            continent.averagedCenter = continent.averagedCenter.Normalized();
            var v1 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            var v2 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            var UnitNorm = v1.Cross(v2);
            if (UnitNorm.Dot(continent.averagedCenter) < 0f)
            {
                UnitNorm = -UnitNorm;
            }
            var uAxis = v1;
            var vAxis = UnitNorm.Cross(uAxis);
            uAxis = uAxis.Normalized();
            vAxis = vAxis.Normalized();
            foreach (VoronoiCell vc in continent.cells)
            {
                //Calculate the average center of the cell
                Vector3 cellAverageCenter = Vector3.Zero;
                foreach (Point p in vc.Points)
                {
                    cellAverageCenter += p.Position;
                }
                cellAverageCenter /= vc.Points.Length;
                cellAverageCenter = cellAverageCenter.Normalized();

                //Project the cell's center onto the continent's 2D plane
                float k = (1.0f - UnitNorm.X * cellAverageCenter.X - UnitNorm.Y * cellAverageCenter.Y - UnitNorm.Z * cellAverageCenter.Z) / (UnitNorm.X * UnitNorm.X + UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z);
                Vector3 projectedCenter = new Vector3(cellAverageCenter.X + k * UnitNorm.X, cellAverageCenter.Y + k * UnitNorm.Y, cellAverageCenter.Z + k * UnitNorm.Z);
                Vector2 projectedCenter2D = new Vector2(uAxis.Dot(projectedCenter), vAxis.Dot(projectedCenter));

                float radius = (continent.averagedCenter - cellAverageCenter).Length();
                Vector3 positionFromCenter = continent.averagedCenter - projectedCenter;

                vc.MovementDirection = (continent.movementDirection * continent.velocity) + new Vector2(-continent.rotation * projectedCenter2D.Y, continent.rotation * projectedCenter2D.X);
                //Find Plane Equation
                float vcRadius = 0.0f;
                foreach (Point p in vc.Points)
                {
                    vcRadius += (cellAverageCenter - p.ToVector3().Normalized()).Length();
                }
                vcRadius /= vc.Points.Length;

                //var vcRadius = (average - vc.Points[0].ToVector3().Normalized()).Length() * .9f;
                var vcUnitNorm = v1.Cross(v2);
                var projectionRatio = (uAxis - UnitNorm).Length() / vcRadius;
                vcUnitNorm /= projectionRatio;
                vcUnitNorm = vcUnitNorm.Normalized();


                var d = UnitNorm.X * (vc.Points[0].X) + UnitNorm.Y * (vc.Points[0].Y) + UnitNorm.Z * (vc.Points[0].Z);
                var newZ = (d - (UnitNorm.X * vc.Points[0].X) - (UnitNorm.Y * vc.Points[0].Y)) / UnitNorm.Z;
                var newZ2 = (d / UnitNorm.Z);
                var directionPoint = uAxis * vc.MovementDirection.X + vAxis * vc.MovementDirection.Y;
                directionPoint *= vcRadius;
                directionPoint += cellAverageCenter;
                directionPoint = directionPoint.Normalized();
                if (ShouldDrawArrows)
                {
                    PolygonRendererSDL.DrawArrow(this, size + (continent.averageHeight / 100f), cellAverageCenter, directionPoint, UnitNorm, vcRadius, Colors.Black);
                }
            }
        }

        return continents;
    }

    public void RenderTriangleAndConnections(Triangle tri, bool dualMesh = false)
    {
        int i = 0;
        //var edgesFromTri = baseEdges.Where(e => tri.Points.Any(a => a == e.P || a == e.Q));
        List<Edge> edgesFromTri = new List<Edge>();
        if (!dualMesh)
        {
            foreach (Point p in tri.Points)
            {
                edgesFromTri.AddRange(HalfEdgesFrom[p]);
                edgesFromTri.AddRange(HalfEdgesTo[p]);
            }
        }
        else
        {
            foreach (Point p in tri.Points)
            {
                edgesFromTri.AddRange(worldHalfEdgeMapFrom[p].Values);
                edgesFromTri.AddRange(worldHalfEdgeMapTo[p].Values);
            }
        }
        if (!ProjectToSphere)
        {
            PolygonRendererSDL.DrawLine(this, size, tri.Points[0].ToVector3(), tri.Points[1].ToVector3());
            PolygonRendererSDL.DrawLine(this, size, tri.Points[1].ToVector3(), tri.Points[2].ToVector3());
            PolygonRendererSDL.DrawLine(this, size, tri.Points[2].ToVector3(), tri.Points[0].ToVector3());
            foreach (Point p in tri.Points)
            {
                switch (i)
                {
                    case 0:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Red);
                        break;
                    case 1:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Green);
                        break;
                    case 2:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3(), 0.05f, Colors.Blue);
                        break;
                }
                i++;
            }
        }
        else
        {
            PolygonRendererSDL.DrawLine(this, size, tri.Points[0].ToVector3().Normalized(), tri.Points[1].ToVector3().Normalized());
            PolygonRendererSDL.DrawLine(this, size, tri.Points[1].ToVector3().Normalized(), tri.Points[2].ToVector3().Normalized());
            PolygonRendererSDL.DrawLine(this, size, tri.Points[2].ToVector3().Normalized(), tri.Points[0].ToVector3().Normalized());
            foreach (Point p in tri.Points)
            {
                //GD.Print(p);
                //GD.Print(p.ToVector3());
                switch (i)
                {
                    case 0:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0 ? p.Radius : 0.05f, Colors.Red);
                        break;
                    case 1:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0 ? p.Radius : 0.05f, Colors.Green);
                        break;
                    case 2:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0 ? p.Radius : 0.05f, Colors.Blue);
                        break;
                }
                i++;
            }
        }
    }

    public Vector3 ConvertToSpherical(Vector3 pos)
    {
        Vector3 sphere = new Vector3(
            Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2) + Mathf.Pow(pos.Z, 2)),
            Mathf.Acos(pos.Z / Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2) + Mathf.Pow(pos.Z, 2))),
            Mathf.Sign(pos.Y) * Mathf.Acos(pos.X / Mathf.Sqrt(Mathf.Pow(pos.X, 2) + Mathf.Pow(pos.Y, 2)))
            );
        return sphere;
    }

    public Vector3 ConvertToCartesian(Vector3 sphere)
    {
        Vector3 cart = new Vector3(
            sphere.X * Mathf.Sin(sphere.Y) * Mathf.Cos(sphere.Z),
            sphere.X * Mathf.Sin(sphere.Y) * Mathf.Sin(sphere.Z),
            sphere.X * Mathf.Cos(sphere.Y)
            );
        return cart;
    }

    public void GenerateFromContinents(Dictionary<int, Continent> continents)
    {
        foreach (var keyValuePair in continents)
        {
            GenerateSurfaceMesh(keyValuePair.Value.cells);
            percent.PercentCurrent++;
        }
    }

    private Color GetVertexColor(float height)
    {
        // Single continuous formula without branching
        // Height range: -10 (deep water) to +10 (mountains)
        float normalizedHeight = (height + 10f) / 20f; // 0-1 range

        // Smooth color transition using mathematical functions
        // Blue(220) -> Cyan(180) -> Green(120) -> Yellow(50) -> Brown(30) -> Dark Brown(10)
        float hue = 220f - (210f * normalizedHeight);

        // Saturation curve: low for water, high for land
        float saturation = 0.3f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi);

        // Value curve: darker for deep water and high mountains, brighter for land
        float value = 0.4f + 0.5f * Mathf.Sin(normalizedHeight * Mathf.Pi * 0.8f + 0.2f);

        // Ensure values are within valid range
        hue = Mathf.Clamp(hue, 0f, 360f);
        saturation = Mathf.Clamp(saturation, 0.2f, 1f);
        value = Mathf.Clamp(value, 0.2f, 1f);

        return Color.FromHsv(hue / 360f, saturation, value);
    }

    private Color GetBiomeColor(BiomeType biome, float height)
    {
        switch (biome)
        {
            case BiomeType.Tundra:
                return new Color(0.85f, 0.85f, 0.8f); // Light gray-white
            case BiomeType.Icecap:
                return Colors.White;
            case BiomeType.Desert:
                return new Color(0.9f, 0.8f, 0.5f); // Sandy yellow
            case BiomeType.Grassland:
                return new Color(0.5f, 0.8f, 0.3f); // Green
            case BiomeType.Forest:
                return new Color(0.2f, 0.6f, 0.2f); // Dark green
            case BiomeType.Rainforest:
                return new Color(0.1f, 0.4f, 0.1f); // Very dark green
            case BiomeType.Taiga:
                return new Color(0.4f, 0.5f, 0.3f); // Dark green-brown
            case BiomeType.Ocean:
                return new Color(0.1f, 0.3f, 0.7f); // Deep blue
            case BiomeType.Coastal:
                return new Color(0.8f, 0.7f, 0.4f); // Light blue
            case BiomeType.Mountain:
                return new Color(0.6f, 0.5f, 0.4f); // Brown-gray
            default:
                return Colors.Gray;
        }
    }

    public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList)
    {
        RandomNumberGenerator randy = new RandomNumberGenerator();
        //float height = averageHeight;
        randy.Seed = Seed;
        var arrMesh = Mesh as ArrayMesh;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var material = new StandardMaterial3D();
        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.VertexColorUseAsAlbedo = true;
        //material.AlbedoColor = Colors.Pink;
        st.SetMaterial(material);
        foreach (VoronoiCell vor in VoronoiList)
        {
            Color biomeColor = Colors.Pink;
            for (int i = 0; i < vor.Points.Length / 3; i++)
            {
                var centroid = Vector3.Zero;
                centroid += vor.Points[3 * i].ToVector3();
                centroid += vor.Points[3 * i + 1].ToVector3();
                centroid += vor.Points[3 * i + 2].ToVector3();
                centroid /= 3.0f;

                var normal = (vor.Points[3 * i + 1].ToVector3() - vor.Points[3 * i].ToVector3()).Cross(vor.Points[3 * i + 2].ToVector3() - vor.Points[3 * i].ToVector3()).Normalized();
                var tangent = (vor.Points[3 * i].ToVector3() - centroid).Normalized();
                var bitangent = normal.Cross(tangent).Normalized();
                var min_u = Mathf.Inf;
                var min_v = Mathf.Inf;
                var max_u = -Mathf.Inf;
                var max_v = -Mathf.Inf;
                for (int j = 0; j < 3; j++)
                {
                    var rel_pos = vor.Points[3 * i + j].ToVector3() - centroid;
                    var u = rel_pos.Dot(tangent);
                    var v = rel_pos.Dot(bitangent);
                    min_u = Mathf.Min(min_u, u);
                    min_v = Mathf.Min(min_v, v);
                    max_u = Mathf.Max(max_u, u);
                    max_v = Mathf.Max(max_v, v);

                    var uv = new Vector2((u - min_u) / (max_u - min_u), (v - min_v) / (max_v - min_v));
                    st.SetUV(uv);
                    //st.SetNormal(tangent);
                    if (ProjectToSphere)
                    {
                        st.SetColor(ShouldDisplayBiomes ? GetBiomeColor(vor.Points[3 * i + j].Biome, vor.Points[3 * i + j].Height) : GetVertexColor(vor.Points[3 * i + j].Height));
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 100f));
                    }
                    else
                    {
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 10f));
                    }
                }
            }
        }
        st.GenerateNormals();
        st.CallDeferred("commit", arrMesh);
    }

    public Face[] Subdivide(Face face)
    {
        List<Face> subfaces = new List<Face>();
        var subVector1 = GetMiddle(face.v[0], face.v[1]);
        var subVector2 = GetMiddle(face.v[1], face.v[2]);
        var subVector3 = GetMiddle(face.v[2], face.v[0]);
        //VertexPoints.Add(subVector1);
        //VertexPoints.Add(subVector2);
        //VertexPoints.Add(subVector3);

        subfaces.Add(new Face(face.v[0], subVector1, subVector3));
        subfaces.Add(new Face(subVector1, face.v[1], subVector2));
        subfaces.Add(new Face(subVector2, face.v[2], subVector3));
        subfaces.Add(new Face(subVector3, subVector1, subVector2));

        return subfaces.ToArray();
    }

    public void AddJitter(Point original, Point jitter)
    {
        var tempVector = (jitter.ToVector3() + original.ToVector3()) / 2.0f;
        tempVector = tempVector.Normalized();
        original.Position = tempVector;
    }

    public Point GetMiddle(Point v1, Point v2)
    {
        //var tempVector = (v1 + v2) / 2.0f;
        var tempVector = (v2.ToVector3() - v1.ToVector3()) * 0.5f + v1.ToVector3();
        //tempVector.Normalized();

        if (VertexPoints.ContainsKey(Point.DetermineIndex(tempVector.X, tempVector.Y, tempVector.Z)))
        {
            var existingPoint = VertexPoints[Point.DetermineIndex(tempVector.X, tempVector.Y, tempVector.Z)];
            return existingPoint;
        }
        Point middlePoint = new Point(tempVector);

        VertexPoints.Add(middlePoint.Index, middlePoint);
        return middlePoint;
    }




}

