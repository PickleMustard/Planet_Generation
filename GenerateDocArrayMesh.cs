using Godot;
using MeshGeneration;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UtilityLibrary;
//using static Structures.Point;
using static MeshGeneration.StructureDatabase;
using static Structures.Biome;


public partial class GenerateDocArrayMesh : MeshInstance3D
{
    /* TODO:
     * Check through the base mesh generation and Voronoi Cell generation for any errors in Vertex CRUD
     * If that doesn't fix the errors in generating faces, then look into the Delaunay Triangulation
     *
     */
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
    public float StressScale = 4.0f;
    [Export]
    public float ShearScale = 1.2f;
    [Export]
    public float MaxPropagationDistance = 0.1f;
    [Export]
    public float PropagationFalloff = 1.5f;
    [Export]
    public float InactiveStressThreshold = 1.0f;
    [Export]
    public float GeneralHeightScale = 1.0f;
    [Export]
    public float GeneralShearScale = 1.0f;
    [Export]
    public float GeneralCompressionScale = 1.0f;
    [Export]
    public float GeneralTransformScale = 1.0f;

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
    [Export]
    public Logger.Mode LogMode = Logger.Mode.PROD;

    public static bool ShouldDrawArrows = false;

    public override void _Ready()
    {
        instance = this;
        Logger.logMode = LogMode;
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
                try
                {
                    baseMesh.PopulateArrays();
                    baseMesh.GenerateNonDeformedFaces();
                    GD.PrintRaw($"\nNon-Deformed Face {Edges.Count}\n");
                    baseMesh.GenerateTriangleList();
                }
                catch (Exception e)
                {
                    GD.PrintRaw($"Error: {e.Message}\n{e.StackTrace}\n");
                }
                return 0;
            }, emptyPercent);

        GD.PrintRaw($"Base Triangles Generated, {BaseTris.Count}\nNum HalfEdgesFrom {HalfEdgesFrom.Count}\n");

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
        GD.PrintRaw($"Voronoi Cells Generated, {VoronoiCells.Count}\n");

        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        function = FunctionTimer.TimeFunction<int>("Flood Filling", () =>
        {
            try
            {
                continents = FloodFillContinentGeneration(VoronoiCells);
            }
            catch (Exception e)
            {
                GD.PrintRaw($"Error: {e.Message}\n{e.StackTrace}\n");
            }
            return 0;
        }, emptyPercent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>("Calculate Voronoi Cells", () =>
        {
            try
            {
                foreach (VoronoiCell vc in VoronoiCells)
                {
                    var cellNeighbors = GetCellNeighbors(VoronoiCells, vc.Index);
                    float averageHeight = vc.Height;
                    List<Edge> OutsideEdges = new List<Edge>();
                    List<int> BoundingContinentIndex = new List<int>();
                    foreach (int neighbor in cellNeighbors)
                    {
                        //GD.Print($"VC: {vc.Index}, {vc.ContinentIndex} | Neighbor: {VoronoiCells[neighbor].Index}, {VoronoiCells[neighbor].ContinentIndex}");
                        //GD.Print($"Continent: {continents[vc.ContinentIndex]}, Boundary Cells: {continents[vc.ContinentIndex].boundaryCells}");
                        if (VoronoiCells[neighbor].ContinentIndex != vc.ContinentIndex)
                        {
                            vc.IsBorderTile = true;
                            continents[vc.ContinentIndex].boundaryCells.Add(vc);
                            continents[vc.ContinentIndex].neighborContinents.Add(VoronoiCells[neighbor].ContinentIndex);
                            continents[VoronoiCells[neighbor].ContinentIndex].neighborContinents.Add(vc.ContinentIndex);
                            VoronoiCells[neighbor].IsBorderTile = true;
                            BoundingContinentIndex.Add(VoronoiCells[neighbor].ContinentIndex);
                            foreach (Point p in vc.Points)
                            {
                                foreach (Point p2 in VoronoiCells[neighbor].Points)
                                {
                                    //GD.Print($"Comparing: {p} to {p2}");
                                    if (p.Equals(p2))
                                    {
                                        p.continentBorder = true;
                                    }
                                }
                            }
                            foreach (Edge e in vc.Edges)
                            {
                                foreach (Edge e2 in VoronoiCells[neighbor].Edges)
                                {
                                    if (e.Equals(e2) || e.Sym().Equals(e2))
                                    {
                                        if (!vc.EdgeBoundaryMap.ContainsKey(e))
                                        {
                                            vc.EdgeBoundaryMap.Add(e, VoronoiCells[neighbor].ContinentIndex);
                                        }
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
                        p.Height = vc.Height;
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintRaw($"Error: {e.Message}\n{e.StackTrace}\n");
            }
            return 0;
        }, percent);
        emptyPercent.Reset();
        function = FunctionTimer.TimeFunction<int>("Average out Heights", () =>
        {
            UpdateVertexHeights(VoronoiCellVertices, continents);
            return 0;
        }, emptyPercent);
        percent.PercentTotal = continents.Count;
        GD.PrintRaw("Are we making it here?");
        function = FunctionTimer.TimeFunction<int>("Calculate Boundary Stress", () =>
        {
            CalculateBoundaryStress(EdgeMap, VoronoiCellVertices, continents, percent);
            return 0;
        }, percent);
        percent.Reset();
        function = FunctionTimer.TimeFunction<int>("Apply Stress to Terrain", () =>
        {
            try
            {
                ApplyStressToTerrain(continents, VoronoiCells);
                for (int i = 0; i < 4; i++)
                {
                    FinalizeVertexHeights(VoronoiCellVertices, continents);
                }
            }
            catch (Exception e)
            {
                GD.PrintRaw($"Error: {e.Message}\n{e.StackTrace}\n");
            }
            return 0;
        }, percent);
        percent.Reset();
        maxHeight = circumcenters.Values.Max(p => p.Height);
        function = FunctionTimer.TimeFunction<int>("Assign Biomes", () =>
        {
            AssignBiomes(continents, VoronoiCells);
            return 0;
        }, percent);
        percent.Reset();
        percent.PercentTotal = continents.Values.Count;
        percent.PercentCurrent = 0;
        function = FunctionTimer.TimeFunction<int>("Generate From Continents", () =>
        {
            GenerateFromContinents(continents, circumcenters);
            //DrawContinentBorders(continents);
            return 0;
        }, percent);


    }

    public void FinalizeVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            Edge[] edges = GetEdgesFromPoint(p);
            float averagedHeight = p.Height;
            int num = 1;
            foreach (Edge e in edges)
            {
                averagedHeight = averagedHeight + (e.GetDestination().Height - averagedHeight) / num;
                num++;
            }
            p.Height = averagedHeight;
        }
    }

    public void UpdateVertexHeights(HashSet<Point> Vertices, Dictionary<int, Continent> Continents)
    {
        foreach (Point p in Vertices)
        {
            if (p.ContinentIndices.Count == 0)
            {
                //GD.PrintRaw("This should never happen");
            }
            if (p.ContinentIndices.Count == 1)
            {
                if (p.ContinentIndices.ElementAt(0) == -1)
                {
                    var cells = CellMap[p];
                    string str = "";
                    foreach (VoronoiCell vc in cells)
                    {
                        str += $"{vc}, ";
                    }
                    GD.PrintRaw($"Encountered a point with no continent: {p}, part of {str}\n");
                }
                p.Height = Continents[p.ContinentIndices.ElementAt(0)].averageHeight;
            }
            else if (p.ContinentIndices.Count > 1)
            {
                float height = 0;
                foreach (int continentIndex in p.ContinentIndices)
                {
                    height += Continents[continentIndex].averageHeight;
                }
                height /= p.ContinentIndices.Count;
                p.Height = height;
            }
        }
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

    public void CalculateBoundaryStress(Dictionary<Edge, HashSet<VoronoiCell>> edgeMap, HashSet<Point> points, Dictionary<int, Continent> continents, GenericPercent percent)
    {
        GD.PrintRaw($"Calculating Boundary Stress\n{continents.Count}\n");
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
                        Vector3 projectedBorderCellMovement = uAxis * (borderCell.MovementDirection.X * continent.velocity) + vAxis * (borderCell.MovementDirection.Y * continent.velocity);
                        Vector3 projectedNeighborCellMovement = uAxis * (neighborCell.MovementDirection.X * continents[neighborCell.ContinentIndex].velocity) + vAxis * (neighborCell.MovementDirection.Y * continents[neighborCell.ContinentIndex].velocity);

                        Vector3 EdgeVector = (e.P.Position - e.Q.Position).Normalized();
                        Vector3 EdgeNormal = EdgeVector.Cross(e.Q.Position.Normalized());

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
                        toVisit.EnqueueRange(GetEdgesFromPoint(e.Q).ToArray(), 0.0f);
                        toVisit.EnqueueRange(GetEdgesFromPoint(e.P).ToArray(), 0.0f);
                        while (toVisit.Count > 0)
                        {
                            Edge current;
                            float distance;
                            bool success = toVisit.TryDequeue(out current, out distance);
                            if (!success) break;
                            if (visited.Contains(current) || distance > MaxPropagationDistance) continue;
                            visited.Add(current);
                            //float magnitude = CalculateStressAtDistance(e.Stress, distance, current, e);
                            float magnitude = 1f;
                            current.StressMagnitude += magnitude;
                            toVisit.EnqueueRange(GetEdgesFromPoint(current.Q).ToArray(), (current.Midpoint - e.Midpoint).Length());
                            toVisit.EnqueueRange(GetEdgesFromPoint(current.P).ToArray(), (current.Midpoint - e.Midpoint).Length());
                        }

                    }
                }
            }
            continents[continentIndex] = continent;
            percent.PercentCurrent++;
        }
    }

    private EdgeType ClassifyBoundaryType(EdgeStress es)
    {
        float normalizedCompression = Mathf.Abs(es.CompressionStress);
        float normalizedShear = Mathf.Abs(es.ShearStress);
        float totalStress = normalizedCompression + normalizedShear;
        //GD.PrintRaw($"Total Stress: {totalStress}\n");

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

    public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        foreach (Point p in VoronoiCellVertices)
        {
            //GD.PrintRaw($"Applying Stress to Point\n");
            Edge[] edges = GetEdgesFromPoint(p);
            //GD.PrintRaw($"Edges: {edges.Length}\n");
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

    public int[] GetCellNeighbors(List<VoronoiCell> cells, int index, bool includeSameContinent = true)
    {
        var currentCell = cells[index];
        HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
        foreach (Point p in currentCell.Points)
        {
            var neighboringCells = CellMap[p];
            foreach (VoronoiCell vc in neighboringCells)
            {
                if (vc.ContinentIndex == currentCell.ContinentIndex && includeSameContinent)
                {
                    neighbors.Add(vc);
                }
                else if (vc.ContinentIndex != currentCell.ContinentIndex)
                {
                    neighbors.Add(vc);
                }
            }
        }
        List<int> neighborIndices = new List<int>();
        foreach (VoronoiCell vc in neighbors)
        {
            neighborIndices.Add(vc.Index);
        }
        return neighborIndices.ToArray();
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
        foreach (var vc in continents.Values)
        {
            var boundaries = vc.boundaryCells;
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
                        Color lineColor = e1.Type switch
                        {
                            EdgeType.convergent => Colors.Aqua,
                            EdgeType.divergent => Colors.Red,
                            EdgeType.transform => Colors.Green,
                            _ => Colors.Black
                        };
                        PolygonRendererSDL.DrawLine(this, 1.005f, pos1, pos2, lineColor);
                    }
                }
            }
        }
    }

    public Dictionary<int, Continent> FloodFillContinentGeneration(List<VoronoiCell> cells)
    {
        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        HashSet<int> startingCells = GenerateStartingCells(cells);
        String startingCellsStr = $"\nStarting Cells: ";
        foreach (int startingCellIndex in startingCells)
        {
            startingCellsStr += $"{startingCellIndex}, ";
        }
        GD.PrintRaw($"{startingCellsStr}\n");
        //List of cells that can be popped to pull neighbors from
        List<int> poppableCells = startingCells.ToList();
        //List containing all Voronoi Cells. Value at the index is the Starting Cell Index of the continent it belongs to
        // Or -1 if it is not yet part of a continent
        int[] neighborChart = new int[cells.Count];
        for (int i = 0; i < neighborChart.Length; i++)
        {
            neighborChart[i] = -1;
        }
        foreach (int startingCellIndex in startingCells)
        {
            Continent.CRUST_TYPE crustType = rand.RandiRange(0, 100) > 33 ? Continent.CRUST_TYPE.Oceanic : Continent.CRUST_TYPE.Continental;
            float averageHeight = crustType == Continent.CRUST_TYPE.Oceanic ? rand.RandfRange(-5.0f, 0.0f) : rand.RandfRange(1.2f, 7.0f);
            float rotation = Mathf.DegToRad(rand.RandiRange(-360, 360));
            float velocity = rand.RandfRange(0.3f, 1.7f);
            var continent = new Continent(startingCellIndex,
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
            neighborChart[startingCellIndex] = startingCellIndex;
            continent.StartingIndex = startingCellIndex;
            continent.cells.Add(cells[startingCellIndex]);
            foreach (Point p in cells[startingCellIndex].Points)
            {
                continent.points.Add(p);
                p.ContinentIndices.Add(continent.StartingIndex);
            }
            foreach (Edge e in cells[startingCellIndex].Edges)
            {
                e.ContinentIndex = startingCellIndex;
            }
            continents[startingCellIndex] = continent;

            var neighborIndices = GetCellNeighbors(cells, startingCellIndex);
            String neighborsStr = $"Neighbors: ";
            foreach (var nb in neighborIndices)
            {
                neighborsStr += $"{cells[nb]}, ";
                if (neighborChart[nb] == -1)
                {
                    neighborChart[nb] = neighborChart[startingCellIndex];
                    poppableCells.Add(nb);
                }
            }
            //GD.PrintRaw($"{neighborsStr}\n");
        }
        //testIndex = poppableCells[0];
        while (poppableCells.Count > 0)
        {
            int poppedIndex = rand.RandiRange(0, (poppableCells.Count - 1));
            int currentVCellIndex = poppableCells[poppedIndex];
            poppableCells.RemoveAt(poppedIndex);
            if (currentVCellIndex == 0 || currentVCellIndex == 102438)
            {
                GD.PrintRaw($"Current Cell: {currentVCellIndex}\n");
            }
            var neighborIndices = GetCellNeighbors(cells, currentVCellIndex);
            String neighborsStr = $"Neighbors: ";
            foreach (var nb in neighborIndices)
            {
                neighborsStr += $"{nb}, ";
                if (neighborChart[nb] == -1)
                {
                    neighborChart[nb] = neighborChart[currentVCellIndex];
                    poppableCells.Add(nb);
                }
            }
            //GD.PrintRaw($"{neighborsStr}\n");
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
                //GD.PrintRaw($"Cell {i} is in Continent {neighborChart[i]}, adding #Points: {cells[i].Points.Length}\n");
                foreach (Point p in cells[i].Points)
                {
                    p.Position = p.Position.Normalized();
                    p.Height = continent.averageHeight;
                    continent.points.Add(p);
                    p.ContinentIndices.Add(continent.StartingIndex);
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
            //GD.PrintRaw($"\nContinent Points Size: {continent.points.Count}");
            if (continent.points.Count < 1)
            {
                continents.Remove(continent.StartingIndex);
                continue;
            }
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
                Vector3 projectedCenter2D = new Vector3(uAxis.Dot(projectedCenter), vAxis.Dot(projectedCenter), 0f);

                float radius = (continent.averagedCenter - cellAverageCenter).Length();
                Vector3 positionFromCenter = continent.averagedCenter - projectedCenter;

                vc.MovementDirection = new Vector2(continent.movementDirection.X * continent.velocity, continent.movementDirection.Y * continent.velocity) + new Vector2(-continent.rotation * projectedCenter2D.Y, continent.rotation * projectedCenter2D.X);
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


                var d = UnitNorm.X * (vc.Points[0].Position.X) + UnitNorm.Y * (vc.Points[0].Position.Y) + UnitNorm.Z * (vc.Points[0].Position.Z);
                var newZ = (d - (UnitNorm.X * vc.Points[0].Position.X) - (UnitNorm.Y * vc.Points[0].Position.Y)) / UnitNorm.Z;
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

    public void GenerateFromContinents(Dictionary<int, Continent> continents, Dictionary<int, Point> circumcenters)
    {
        foreach (var keyValuePair in continents)
        {
            try
            {
                GenerateSurfaceMesh(keyValuePair.Value.cells, circumcenters);
            }
            catch (Exception e)
            {
                GD.PrintRaw($"Error: {e.Message}\n{e.StackTrace}\n");
            }
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

    public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList, Dictionary<int, Point> circumcenters)
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
            Dictionary<int, Queue<int>> VertexMap = new Dictionary<int, Queue<int>>();
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
                    if (VertexMap.ContainsKey(vor.Points[3 * i + j].Index))
                    {
                        VertexMap[vor.Points[3 * i + j].Index].Enqueue(3 * i + j);
                    }
                    else
                    {
                        VertexMap.Add(vor.Points[3 * i + j].Index, new Queue<int>());
                        VertexMap[vor.Points[3 * i + j].Index].Enqueue(3 * i + j);
                    }
                    min_u = Mathf.Min(min_u, u);
                    min_v = Mathf.Min(min_v, v);
                    max_u = Mathf.Max(max_u, u);
                    max_v = Mathf.Max(max_v, v);

                    var uv = new Vector2((u - min_u) / (max_u - min_u), (v - min_v) / (max_v - min_v));
                    st.SetUV(uv);
                    //st.SetNormal(tangent);
                    st.SetColor(ShouldDisplayBiomes ? GetBiomeColor(vor.Points[3 * i + j].Biome, vor.Points[3 * i + j].Height) : GetVertexColor(vor.Points[3 * i + j].Height));
                    st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 100f));
                }
            }
            //foreach (Triangle triangle in vor.Triangles)
            //{
            //    st.AddIndex(VertexMap[triangle.Points[0].Index]);
            //    st.AddIndex(VertexMap[triangle.Points[2].Index]);
            //    st.AddIndex(VertexMap[triangle.Points[1].Index]);
            //}
        }
        st.GenerateNormals();
        //st.GenerateTangents();
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

