using Godot;
using MeshGeneration;
using Structures;
using System;
using System.Collections.Generic;
using System.Linq;


public partial class GenerateDocArrayMesh : MeshInstance3D
{
    public DelaunatorSharp.Delaunator dl;
    static float TAU = (1 + (float)Math.Sqrt(5)) / 2;
    Vector3 origin = new Vector3(0, 0, 0);
    //public static List<Point> VertexPoints = new List<Point>();
    public List<Vector3> normals;
    public List<Vector2> uvs;
    public List<int> indices;
    public List<Face> faces;
    public int VertexIndex = 0;
    public int testIndex = 0;
    public static float maxHeight = 0;

    public static Dictionary<int, Point> VertexPoints = new Dictionary<int, Point>();
    public static Dictionary<int, Edge> Edges = new Dictionary<int, Edge>();
    public static Dictionary<Point, HashSet<Edge>> HalfEdgesFrom = new Dictionary<Point, HashSet<Edge>>();
    public static Dictionary<Point, HashSet<Edge>> HalfEdgesTo = new Dictionary<Point, HashSet<Edge>>();
    public static Dictionary<(Point, Point, Point), Triangle> BaseTris = new Dictionary<(Point, Point, Point), Triangle>();
    public static Dictionary<Edge, List<Triangle>> EdgeTriangles = new Dictionary<Edge, List<Triangle>>();

    //public static Dictionary<(float, float, float), Point> triCircumcenters = new Dictionary<(float, float, float), Point>();
    public static Dictionary<int, Point> circumcenters = new Dictionary<int, Point>();

    public static Dictionary<Point, HashSet<VoronoiCell>> CellMap = new Dictionary<Point, HashSet<VoronoiCell>>();
    public static Dictionary<Edge, HashSet<VoronoiCell>> EdgeMap = new Dictionary<Edge, HashSet<VoronoiCell>>();
    public static Dictionary<Point, HashSet<Triangle>> VoronoiTriMap = new Dictionary<Point, HashSet<Triangle>>();
    public static Dictionary<Edge, HashSet<Triangle>> VoronoiEdgeTriMap = new Dictionary<Edge, HashSet<Triangle>>();

    //public List<Edge> baseEdges = new List<Edge>();
    public List<Triangle> baseTris = new List<Triangle>();
    public List<Face> dualFaces = new List<Face>();
    //public List<Point> circumcenters = new List<Point>();
    public List<Edge> generatedEdges = new List<Edge>();
    public Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapFrom = new Dictionary<Point, Dictionary<Point, Edge>>();
    public Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapTo = new Dictionary<Point, Dictionary<Point, Edge>>();
    public List<Triangle> generatedTris = new List<Triangle>();
    public List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();
    RandomNumberGenerator rand = new RandomNumberGenerator();

    private ConfigurableSubdivider _subdivider = new ConfigurableSubdivider();
    public static GenerateDocArrayMesh instance;

    [Export]
    public int subdivide = 1;

    [Export]
    public int size = 5;

    [Export]
    public bool ProjectToSphere = true;

    [Export]
    public bool GenerateRealistic = true;

    [Export]
    public ulong Seed = 5001;

    [Export]
    public int NumAbberations = 3;

    [Export]
    public int NumDeformationCycles = 3;

    [Export]
    public int NumContinents = 5;

    [Export]
    public float ConvergentBoundaryStrength = 0.5f;

    [Export]
    public float DivergentBoundaryStrength = 0.3f;

    [Export]
    public float TransformBoundaryStrength = 0.1f;

    [Export]
    public bool ShouldDisplayBiomes = true;

    [Export]
    public int[] VerticesPerEdge = new int[] { 1, 2, 4 };


    [Export]
    public bool AllTriangles = false;

    private Point testPoint;
    private int testContinent;

    public override void _Ready()
    {
        //instance = this;
        rand.Seed = Seed;
        PolygonRendererSDL.DrawPoint(this, size, new Vector3(0, 0, 0), 0.1f, Colors.White);
        //Generate the starting dodecahedron
        PopulateArrays();
        //Split the faces n times
        GenerateNonDeformedFaces();
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();


        //Construct Adjacency Matrices for all triangles and edges given all points
        GenerateTriangleList();

        //RenderTriangleAndConnections(tris[0]);

        //var p = VertexPoints[300];
        //List<VoronoiCell> VoronoiPoints = new List<VoronoiCell>();
        //GD.Print(VertexPoints.Count);
        //GD.Print(baseTris.Count);
        var OptimalArea = (4.0f * Mathf.Pi * size * size) / baseTris.Count;
        //GD.Print($"Optimal Area of Triangle: {OptimalArea}");
        var OptimalSideLength = Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;
        //GD.Print($"Optimal Side Length of Triangle: {OptimalSideLength}");
        var OptimalCentroidLength = Mathf.Cos(Mathf.DegToRad(30.0f)) * .5f * OptimalSideLength;
        //GD.Print($"Optimal Length from Vertex of Triangle to Centroid: {OptimalCentroidLength}");
        int alteredIndex = 0;
        //var randomTri = baseTris[baseTris.Count - baseTris.Count / 2];
        for (int deforms = 0; deforms < NumDeformationCycles; deforms++)
        {
            //GD.Print($"Deformation Cycle: {deforms} | Deform Amount: {(2f + deforms) / (deforms + 1)}");
            for (int i = 0; i < NumAbberations; i++)
            {
                var randomTri = baseTris[rand.RandiRange(0, baseTris.Count - 1)];
                var randomTriPoint = randomTri.Points[rand.RandiRange(0, 2)];
                testPoint = randomTriPoint;
                //var edgesWithPoint = baseEdges.Where(e => randomTri.Points.Contains(e.Q) && randomTri.Points.Contains(e.P));
                HashSet<Edge> edgesWithPointFrom = HalfEdgesFrom[randomTriPoint];
                HashSet<Edge> edgesWithPointTo = HalfEdgesTo[randomTriPoint];
                HashSet<Edge> allEdgesWithPoint = new HashSet<Edge>(edgesWithPointFrom);
                foreach (Edge e in edgesWithPointTo)
                {
                    allEdgesWithPoint.Add(e);
                }
                foreach (Edge e in edgesWithPointFrom)
                {
                    allEdgesWithPoint.Add(e);
                }
                //List<Edge> edgesFromPoint = baseEdges.Where(e => e.P == randomTriPoint).ToList();
                //List<Edge> edgesToPoint = baseEdges.Where(e => e.Q == randomTriPoint).ToList();
                //List<Edge> allEdges = new List<Edge>(edgesFromPoint);
                //allEdges.AddRange(edgesToPoint);
                List<Edge> allEdges = allEdgesWithPoint.ToList();
                bool shouldRedo = false;
                //foreach (Edge e in edgesFromPoint)
                //{
                //    List<Edge> fromPoint = baseEdges.Where(ed => ed.P == e.Q).ToList();
                //    List<Edge> ToPoint = baseEdges.Where(ed => ed.Q == e.Q).ToList();

                //    //GD.Print($"Edges from Point: {fromPoint.Count + ToPoint.Count}");
                //    if (fromPoint.Count + ToPoint.Count < 5) shouldRedo = true;

                //}
                //foreach (Edge e in edgesToPoint)
                //{
                //    List<Edge> fromPoint = baseEdges.Where(ed => ed.P == e.P).ToList();
                //    List<Edge> ToPoint = baseEdges.Where(ed => ed.Q == e.P).ToList();

                //    //GD.Print($"Edges from Point: {fromPoint.Count + ToPoint.Count}");
                //    if (fromPoint.Count + ToPoint.Count < 5) shouldRedo = true;

                //}
                //GD.Print($"Edges from Point: {edgesToPoint.Count + edgesFromPoint.Count}");
                bool EnoughEdges = allEdgesWithPoint.Count > 5;

                if (EnoughEdges && !shouldRedo)
                //if (edgesFromPoint.Count + edgesToPoint.Count > 5 && edgesToPoint.Count + edgesFromPoint.Count < 7)
                {
                    if (allEdgesWithPoint.Count > 0)
                    {
                        foreach (Edge e in allEdgesWithPoint)
                        {
                            List<Triangle> trisWithEdge = EdgeTriangles[e];
                            if (trisWithEdge.Count < 2) continue;
                            //var trisWithEdge = baseTris.Where(tri => tri.Points.Contains(edgesWithPointList[0].P) && tri.Points.Contains(edgesWithPointList[0].Q));
                            Triangle tempTris1 = trisWithEdge.ElementAt(0);
                            Triangle tempTris2 = trisWithEdge.ElementAt(1);
                            alteredIndex = tempTris1.Index;
                            var points1 = tempTris1.Points;
                            var points2 = tempTris2.Points;

                            var sharedPoint1 = e.Q;
                            var sharedPoint2 = e.P;
                            var t1UnsharedPoint = tempTris1.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                            var t2UnsharedPoint = tempTris2.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                            var sharedEdgeLength = (e.P.ToVector3() - e.Q.ToVector3()).Length();
                            var newEdgeLength = (t1UnsharedPoint.ToVector3() - t2UnsharedPoint.ToVector3()).Length();
                            if (Mathf.Abs(sharedEdgeLength - newEdgeLength) > OptimalSideLength / .5f)
                            {
                                i--;
                                continue;
                            }
                            points1[0] = sharedPoint1;
                            points1[2] = t1UnsharedPoint;
                            points1[1] = t2UnsharedPoint;

                            points2[0] = sharedPoint2;
                            points2[1] = t1UnsharedPoint;
                            points2[2] = t2UnsharedPoint;
                            e.Q = t1UnsharedPoint;
                            e.P = t2UnsharedPoint;

                            //var index = tempTris1.Index;
                            //tempTris1.Index = tempTris2.Index;
                            //tempTris2.Index = index;

                            var otherEdgesT1 = tempTris1.Edges.Where(edge => edge != e).ToList();
                            otherEdgesT1[0].P = sharedPoint1;
                            otherEdgesT1[0].Q = t1UnsharedPoint;
                            otherEdgesT1[1].Q = sharedPoint1;
                            otherEdgesT1[1].P = t2UnsharedPoint;

                            var otherEdgesT2 = tempTris2.Edges.Where(edge => edge != e).ToList();
                            otherEdgesT2[0].Q = sharedPoint2;
                            otherEdgesT2[0].P = t2UnsharedPoint;
                            otherEdgesT2[1].P = sharedPoint2;
                            otherEdgesT2[1].Q = t1UnsharedPoint;
                        }
                    }
                }
                else
                {
                    i--;
                    continue;
                }
            }


            //GD.Print("Relaxing");
            for (int index = 0; index < 12; index++)
            {
                foreach (Point p in VertexPoints.Values)
                {
                    //var trianglesWithPoint = baseTris.Where(t => t.Points.Contains(p));
                    HashSet<Edge> edgesWithPoint = HalfEdgesFrom[p];
                    HashSet<Edge> edgesWithPointTo = HalfEdgesTo[p];
                    HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
                    foreach (Edge e in edgesWithPoint)
                    {
                        foreach (Triangle t in EdgeTriangles[e])
                        {
                            trianglesWithPoint.Add(t);
                        }
                    }
                    foreach (Edge e in edgesWithPointTo)
                    {
                        foreach (Triangle t in EdgeTriangles[e])
                        {
                            trianglesWithPoint.Add(t);
                        }
                    }
                    Vector3 average = new Vector3(0, 0, 0);
                    foreach (Triangle t in trianglesWithPoint)
                    {
                        Vector3 triCenter = new Vector3(0, 0, 0);
                        triCenter = t.Points[0].ToVector3();
                        triCenter += t.Points[1].ToVector3();
                        triCenter += t.Points[2].ToVector3();
                        triCenter /= 3f;
                        average += triCenter;
                    }
                    average /= trianglesWithPoint.Count;
                    p.Position = average;
                }
            }
        }

        if (AllTriangles)
        {
            foreach (var triangle in baseTris)
            {
                RenderTriangleAndConnections(triangle);
            }
        }

        //GD.Print("Triangulating");
        foreach (Point p in VertexPoints.Values)
        {
            //GD.Print($"Triangulating Point: {p}\r");
            //Find all triangles that contain the current point
            HashSet<Edge> edgesWithPoint = HalfEdgesFrom[p];
            HashSet<Edge> edgesWithPointTo = HalfEdgesTo[p];
            HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
            foreach (Edge e in edgesWithPoint)
            {
                foreach (Triangle t in EdgeTriangles[e])
                {
                    trianglesWithPoint.Add(t);
                }
            }
            foreach (Edge e in edgesWithPointTo)
            {
                foreach (Triangle t in EdgeTriangles[e])
                {
                    trianglesWithPoint.Add(t);
                }
            }
            var triangleOfP = baseTris.Where(e => e.Points.Any(a => a == p));
            List<Triangle> trianglesWithPointOld = triangleOfP.ToList();
            List<Point> triCircumcenters = new List<Point>();
            //GD.Print($"Number of Triangles: {trianglesWithPoint.Count}");
            //GD.Print($"Old Method Triangles: {trianglesWithPointOld.Count}");
            foreach (var tri in trianglesWithPoint)
            {
                if (testPoint == p)
                {
                    RenderTriangleAndConnections(tri, false);
                }
                var v3 = Point.ToVectors3(tri.Points);
                var ac = v3[2] - v3[0];
                var ab = v3[1] - v3[0];
                var abXac = ab.Cross(ac);
                var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
                float circumsphereRadius = vToCircumsphereCenter.Length();
                Point cc = new Point(v3[0] + vToCircumsphereCenter);
                //if (triCircumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc))) continue;
                if (triCircumcenters.Contains(cc)){
                    //GD.Print($"Triangulation Point: {cc} already in list");
                    continue;
                }
                                 //if (circumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc)))
                if (circumcenters.ContainsKey(cc.Index))
                {
                    //var usedCC = circumcenters.Where(cir => Mathf.Equals(cir.ToVector3(), cc));
                    Point usedCC = circumcenters[cc.Index];
                    triCircumcenters.Add(usedCC);
                }
                else
                {
                    circumcenters.Add(cc.Index, cc);
                    triCircumcenters.Add(cc);
                    //circumcenters.Add(new Point(cc, circumcenters.Count));
                    //triCircumcenters.Add(circumcenters[circumcenters.Count - 1]);
                }
            }
            Face dualFace = new Face(triCircumcenters.ToList());
            dualFaces.Add(dualFace);
            Vector3 center = new Vector3(0, 0, 0);
            //for (int i = 0; i < triCircumcenters.Count; i++) center += triCircumcenters[i].ToVector3();
            foreach (Point triCenter in triCircumcenters)
            {
                center += triCenter.ToVector3();
            }
            //GD.Print($"Number of Triangulated Points: {triCircumcenters.Count}");
            center /= triCircumcenters.Count;
            center = center.Normalized();

            var centroid = new Vector3(0, 0, 0);
            var v1 = triCircumcenters[1].ToVector3() - triCircumcenters[0].ToVector3();
            var v2 = triCircumcenters[2].ToVector3() - triCircumcenters[0].ToVector3();
            var UnitNorm = v1.Cross(v2);
            UnitNorm = UnitNorm.Normalized();
            if (UnitNorm.Dot(triCircumcenters[0].ToVector3()) < 0f)
            {
                UnitNorm = -UnitNorm;
            }
            //GD.Print($"Triagulating Point, {triCircumcenters.Count}");
            VoronoiCell calculated = TriangulatePoints(UnitNorm, triCircumcenters, VoronoiCells.Count);
            //GD.Print("Finished Triagulating Point");
            calculated.IsBorderTile = false;
            if (calculated != null)
            {
                VoronoiCells.Add(calculated);
            }
        }
        //foreach (VoronoiCell vc in VoronoiCells)
        //{
        //    List<Vector3> Points = new List<Vector3>();
        //    foreach (Point p in vc.Points)
        //    {
        //        Points.Add(p.ToVector3().Normalized());
        //    }
        //    PolygonRendererSDL.DrawFace(this, 1.0005f * (float)size, Colors.White, Points.ToArray());
        //}

        /*Edge testEdge = generatedEdges[0];
        GD.Print($"Number of Edges: {generatedEdges.Count}, Number of World Edges: {worldEdgeMap.Count}");
        foreach (var edges in generatedEdges)
        {
            if ((edges.P == testEdge.P && edges.Q == testEdge.Q) || (edges.P == testEdge.Q && edges.Q == testEdge.P))
            {
                GD.Print($"Original Edge {testEdge} has duplicate edge {edges}");
            }
        }*/
        var continents = FloodFillContinentGeneration(VoronoiCells);
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
                            if (e.Equals(e2) || e.ReverseEdge().Equals(e2))
                            {
                                OutsideEdges.Add(e);
                            }
                        }
                    }

                }
                //averageHeight += VoronoiCells[neighbor].Height;
            }
            vc.BoundingContinentIndex = BoundingContinentIndex.ToArray();
            vc.OutsideEdges = OutsideEdges.ToArray();
            //vc.Height = averageHeight / (cellNeighbors.Length + 1);
            //foreach (Point p in vc.Points)
            //{
            //    p.Height = vc.Height;
            //}
        }

        //GD.Print($"Number of Cells in mesh: {VoronoiCells.Count}");
        //GD.Print("Generating Mesh");
        //GD.Print($"Cell Height: {circumcenters[0].Height}");
        //GenerateSurfaceMesh(VoronoiCells, circumcenters);
        foreach (Point p in circumcenters.Values)
        {
            //var trianglesWithPoint = generatedTris.Where(t => t.Points.Contains(p));
            var EdgesFrom = worldHalfEdgeMapFrom[p].Values.ToList();
            var EdgesTo = worldHalfEdgeMapTo[p].Values.ToList();
            HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
            foreach (Edge e in EdgesFrom)
            {
                foreach (Triangle t in VoronoiEdgeTriMap[e])
                {
                    trianglesWithPoint.Add(t);
                }
            }
            foreach (Edge e in EdgesTo)
            {
                foreach (Triangle t in VoronoiEdgeTriMap[e])
                {
                    trianglesWithPoint.Add(t);
                }
            }

            float height = p.Height;
            int counter = 1;
            foreach (Triangle t in trianglesWithPoint)
            {
                foreach (Point p2 in t.Points)
                {
                    height += p2.Height;
                    counter++;
                }
            }
            height /= counter;
            p.Height = height;
            //var pointEdges = baseEdges.Where(e => e.P == p || e.Q == p);
        }
        CalculateBoundaryStress(continents, VoronoiCells, circumcenters);
        ApplyStressToTerrain(continents, VoronoiCells);
        maxHeight = circumcenters.Values.Max(p => p.Height);
        AssignBiomes(continents, VoronoiCells);
        GenerateFromContinents(continents, circumcenters);
        DrawContinentBorders(continents);
        //foreach (var continent in continents)
        //{
        //    GD.Print(continent.Value.ToString());
        //}
        //var randomTriTest = baseTris[baseTris.Count - baseTris.Count / 2];
        //var randomTriPointTest = randomTriTest.Points[rand.RandiRange(0, 2)];
        //var otherPointInTri = randomTriTest.Points.Where(p => p != randomTriPointTest).ElementAt(rand.RandiRange(0, randomTriTest.Points.Count - 1));
        //Point testP1 = new Point(randomTriPointTest.X, randomTriPointTest.Y, randomTriPointTest.Z);
        //Point testP2 = new Point(otherPointInTri.X, otherPointInTri.Y, otherPointInTri.Z);
        //Edge testEdge = new Edge(testP1, testP2);
        //Edge inverseTestEdge = new Edge(testP2, testP1);
        //GD.Print($"Test Edge: {testEdge}");
        //GD.Print($"Inverse Test Edge: {inverseTestEdge}");

        GD.Print($"Number of Vertices: {circumcenters.Values.Count}");


    }

    private void AssignBiomes(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        foreach (var continent in continents)
        {
            Continent c = continent.Value;
            c.averageMoisture = BiomeAssigner.CalculateMoisture(c, rand, 0.5f);
            foreach (var cell in c.cells)
            {
                cell.Biome = BiomeAssigner.AssignBiome(cell.Height, c.averageMoisture);
            }
        }
    }

    public void CalculateBoundaryStress(Dictionary<int, Continent> continents, List<VoronoiCell> cells, Dictionary<int, Point> points)
    {
        // Initialize stress fields for each continent
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            int continentIndex = continentPair.Key;
            Continent continent = continentPair.Value;

            // Reset stress accumulation for this continent
            continent.stressAccumulation = 0f;
            continent.neighborStress = new Dictionary<int, float>();
            continent.boundaryTypes = new Dictionary<int, Continent.BOUNDARY_TYPE>();

            // Save updated continent back to dictionary
            continents[continentIndex] = continent;
        }

        // Calculate stress between neighboring continents
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            int continentIndex = continentPair.Key;
            Continent continent = continentPair.Value;
            Vector3 continentCenter = continent.averagedCenter;

            // Find neighboring continents
            foreach (int neighborIndex in continent.neighborContinents)
            {
                Continent neighbor = continents[neighborIndex];
                Vector3 neighborCenter = neighbor.averagedCenter;

                // Calculate relative movement vector
                Vector2 relativeMovement = neighbor.movementDirection - continent.movementDirection;
                float distance = (neighborCenter - continentCenter).LengthSquared();

                // Normalize distance to a reasonable scale (assuming sphere radius is 'size')
                //float normalizedDistance = distance / (2 * size);

                // Calculate stress based on relative velocity and distance
                // This is a simplified model - you might want to adjust the formula
                float stress = relativeMovement.LengthSquared() / (distance + 0.1f); // Add small value to avoid division by zero

                // Determine boundary type based on relative movement
                Continent.BOUNDARY_TYPE boundaryType;
                float dotProduct = continent.movementDirection.Dot(neighbor.movementDirection);

                if (dotProduct > 0.5f) // Moving in similar directions
                {
                    boundaryType = Continent.BOUNDARY_TYPE.Transform;
                }
                else if (dotProduct < -0.5f) // Moving towards each other
                {
                    boundaryType = Continent.BOUNDARY_TYPE.Divergent;
                }
                else // Moving apart or perpendicular
                {
                    boundaryType = Continent.BOUNDARY_TYPE.Convergent;
                }

                // Update continent's stress and boundary type dictionaries
                continent.neighborStress[neighborIndex] = stress;
                continent.boundaryTypes[neighborIndex] = boundaryType;

                // Accumulate total stress
                continent.stressAccumulation += stress;
            }

            // Save updated continent back to dictionary
            continents[continentIndex] = continent;
        }
    }

    public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
    {
        // First, apply stress directly to boundary cells
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            int continentIndex = continentPair.Key;
            Continent continent = continentPair.Value;

            // Process each boundary cell of this continent
            foreach (VoronoiCell boundaryCell in continent.boundaryCells)
            {
                // For each neighboring continent that this boundary cell touches
                foreach (int neighborIndex in boundaryCell.BoundingContinentIndex)
                {
                    // Check if this neighbor is actually a neighbor of the current continent
                    if (continent.neighborContinents.Contains(neighborIndex))
                    {
                        // Get the boundary type and stress for this specific neighbor
                        if (continent.boundaryTypes.ContainsKey(neighborIndex) &&
                            continent.neighborStress.ContainsKey(neighborIndex))
                        {
                            Continent.BOUNDARY_TYPE boundaryType = continent.boundaryTypes[neighborIndex];
                            float neighborStress = continent.neighborStress[neighborIndex];

                            // Apply height modification based on boundary type and stress
                            switch (boundaryType)
                            {
                                case Continent.BOUNDARY_TYPE.Convergent:
                                    // Compressing - might create mountains or trenches
                                    boundaryCell.Height += neighborStress * ConvergentBoundaryStrength;
                                    break;
                                case Continent.BOUNDARY_TYPE.Divergent:
                                    // Pulling apart - might create rifts
                                    boundaryCell.Height -= neighborStress * DivergentBoundaryStrength; // Example multiplier
                                    break;
                                case Continent.BOUNDARY_TYPE.Transform:
                                    // Sliding past - might create fault lines
                                    // Could add some noise or specific patterns
                                    boundaryCell.Height += (float)rand.RandfRange(-0.1f, 0.1f) * neighborStress * TransformBoundaryStrength;
                                    break;
                            }

                            // Update the height of all points in this boundary cell
                            foreach (Point p in boundaryCell.Points)
                            {
                                p.Height = boundaryCell.Height;
                            }
                        }
                    }
                }
            }
        }

        // Then, propagate stress from boundary cells to interior cells
        foreach (KeyValuePair<int, Continent> continentPair in continents)
        {
            Continent continent = continentPair.Value;

            // Create a list of cells to process, starting with boundary cells
            List<VoronoiCell> cellsToProcess = new List<VoronoiCell>(continent.boundaryCells);
            HashSet<VoronoiCell> processedCells = new HashSet<VoronoiCell>();
            Queue<VoronoiCell> processedCellsQueue = new Queue<VoronoiCell>();


            // Process cells in layers, moving inward from the boundaries
            while (cellsToProcess.Count > 0 || processedCellsQueue.Count > 0)
            {
                //GD.Print($"Cells to Process: {cellsToProcess.Count} | Processed Cells Queue: {processedCellsQueue.Count}");
                VoronoiCell currentCell = null;
                if (cellsToProcess.Count > 0)
                {
                    currentCell = cellsToProcess[0];
                    cellsToProcess.RemoveAt(0);
                }
                else
                {
                    currentCell = processedCellsQueue.Dequeue();
                }

                // Skip if already processed
                if (processedCells.Contains(currentCell))
                    continue;

                processedCells.Add(currentCell);

                // Get neighboring cells within the same continent
                int[] neighborIndices = GetCellNeighbors(cells, currentCell.Index);
                foreach (int neighborIndex in neighborIndices)
                {
                    VoronoiCell neighborCell = cells[neighborIndex];

                    // Only process neighbors that belong to the same continent and haven't been processed
                    if (neighborCell.ContinentIndex == currentCell.ContinentIndex &&
                                                                                  !processedCells.Contains(neighborCell))
                    {
                        // Calculate distance between cell centers
                        float distance = (currentCell.Center - neighborCell.Center).Length();

                        // Propagate a fraction of the height difference based on distance
                        // This creates a smoothing effect from boundaries inward
                        float heightDifference = currentCell.Height - neighborCell.Height;
                        float propagationFactor = 0.7f / (distance + 1.0f); // Adjust this factor as needed
                        neighborCell.Height += heightDifference * propagationFactor;

                        // Add this neighbor to the processing queue
                        cellsToProcess.Add(neighborCell);

                        // Update the height of all points in this neighbor cell
                        foreach (Point p in neighborCell.Points)
                        {
                            p.Height = neighborCell.Height;
                        }
                    }
                }
            }
        }
    }

    public int[] GetCellNeighbors(List<VoronoiCell> cells, int index)
    {
        var currentCell = cells[index];
        HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
        foreach (Point p in currentCell.Points)
        {
            //var neighboringCells = cells.Where(vc => vc.Points.Any(vcp => vcp == p));
            var neighboringCells = CellMap[p];
            foreach (VoronoiCell vc in neighboringCells)
            {
                neighbors.Add(vc);
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
        Color[] colors = new Color[] {
            Colors.Red,
            Colors.Green,
            Colors.Blue,
            Colors.Yellow,
            Colors.Pink,
            Colors.Aqua,
            Colors.White,
            Colors.Gray,
            Colors.Black,
            Colors.DarkGray,
            Colors.LightGray,
            Colors.Purple,
            Colors.Gold,
            Colors.Orange,
            Colors.Brown,
            Colors.Maroon,
            Colors.DeepPink,
            Colors.RoyalBlue,
            Colors.SteelBlue,
            Colors.CornflowerBlue,
            Colors.SkyBlue,
            Colors.LightSteelBlue,
            Colors.LightBlue,
            Colors.PowderBlue,
            Colors.CadetBlue,
            Colors.MidnightBlue,
            Colors.DarkBlue,
            Colors.MediumBlue,
            Colors.BlueViolet,
            Colors.Indigo,
            Colors.DarkOliveGreen,
            Colors.CadetBlue,
            Colors.SteelBlue,
            Colors.CornflowerBlue,
        };
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
                        PolygonRendererSDL.DrawLine(this, 1.005f, pos1, pos2, Colors.Black);
                    }
                }
            }
        }
    }

    public Dictionary<int, Continent> FloodFillContinentGeneration(List<VoronoiCell> cells)
    {
        Dictionary<int, Continent> continents = new Dictionary<int, Continent>();
        HashSet<int> startingCells = GenerateStartingCells(cells);
        var queue = startingCells.ToList();
        testContinent = queue[1];
        int[] neighborChart = new int[cells.Count];
        for (int i = 0; i < neighborChart.Length; i++)
        {
            neighborChart[i] = -1;
        }
        foreach (int i in startingCells)
        {
            Continent.CRUST_TYPE crustType = rand.RandiRange(0, 100) > 33 ? Continent.CRUST_TYPE.Oceanic : Continent.CRUST_TYPE.Continental;
            float averageHeight = crustType == Continent.CRUST_TYPE.Oceanic ? rand.RandfRange(-20.0f, -5.0f) : rand.RandfRange(-2.0f, 30.0f);
            var continent = new Continent(i,
                    new List<VoronoiCell>(),//cells
                    new HashSet<VoronoiCell>(),
                    new HashSet<Point>(),
                    new List<Point>(),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f)), rand.RandiRange(-360, 360),
                    crustType, averageHeight, rand.RandfRange(1.0f, 5.0f),
                    new HashSet<int>(), 0f,
                    new Dictionary<int, float>(),
                    new Dictionary<int, Continent.BOUNDARY_TYPE>());
            neighborChart[i] = i;
            continent.StartingIndex = i;
            continent.cells.Add(cells[i]);
            foreach (Point p in cells[i].Points)
            {
                continent.points.Add(p);
            }
            continents[i] = continent;

            var neighborIndices = GetCellNeighbors(cells, i);
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb] == -1)
                {
                    neighborChart[nb] = neighborChart[i];
                    queue.Add(nb);
                }
            }
        }
        testIndex = queue[0];
        for (int i = 0; i < queue.Count; i++)
        {
            var pos = rand.RandiRange(i, (queue.Count - 1));
            var currentVCell = queue[pos];
            queue[pos] = queue[i];
            var neighborIndices = GetCellNeighbors(cells, currentVCell);
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb] == -1)
                {
                    neighborChart[nb] = neighborChart[currentVCell];
                    queue.Add(nb);
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
                foreach (Point p in cells[i].Points)
                {
                    p.Position = p.Position.Normalized();
                    p.Height = continent.averageHeight;
                    continent.points.Add(p);
                }
            }
        }

        foreach (var keyValuePair in continents)
        {
            var continent = keyValuePair.Value;
            //GD.Print($"StartingIndex: {continent.StartingIndex}");
            foreach (Point p in continent.points)
            {
                continent.averagedCenter += p.Position;
            }

            continent.averagedCenter /= continent.points.Count;
            continent.averagedCenter = continent.averagedCenter.Normalized();
            var v1 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            var v2 = (continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized() - continent.points.ElementAt(rand.RandiRange(0, continent.points.Count - 1)).ToVector3().Normalized());
            var UnitNorm = v1.Cross(v2);
            //UnitNorm = UnitNorm / size;
            if (UnitNorm.Dot(continent.averagedCenter) < 0f)
            {
                UnitNorm = -UnitNorm;
            }
            //UnitNorm *= (float)size;
            var uAxis = v1;// * (float)size;
            var vAxis = UnitNorm.Cross(uAxis);// * (float)size;
            uAxis = uAxis.Normalized();
            vAxis = vAxis.Normalized();
            foreach (VoronoiCell vc in continent.cells)
            {
                Vector3 average = Vector3.Zero;
                foreach (Point p in vc.Points)
                {
                    average += p.Position;
                }
                average /= vc.Points.Length;
                average = average.Normalized();
                float radius = (continent.averagedCenter - average).Length();
                vc.MovementDirection = continent.movementDirection + new Vector2(radius * Mathf.Cos(continent.rotation), radius * Mathf.Sin(continent.rotation));
                //Find Plane Equation
                var vcRadius = (average - vc.Points[0].ToVector3().Normalized()).Length() * .9f;
                var vcUnitNorm = v1.Cross(v2);
                var projectionRatio = (uAxis - UnitNorm).Length() / vcRadius;
                vcUnitNorm /= projectionRatio;
                vcUnitNorm = vcUnitNorm.Normalized();


                var d = UnitNorm.X * (vc.Points[0].X) + UnitNorm.Y * (vc.Points[0].Y) + UnitNorm.Z * (vc.Points[0].Z);
                var newZ = (d - (UnitNorm.X * vc.Points[0].X) - (UnitNorm.Y * vc.Points[0].Y)) / UnitNorm.Z;
                var newZ2 = (d / UnitNorm.Z);
                //GD.Print($"Movement Direction: {vc.MovementDirection}");
                var directionPoint = uAxis * vc.MovementDirection.X + vAxis * vc.MovementDirection.Y;
                directionPoint *= vcRadius;
                directionPoint += average;
                directionPoint = directionPoint.Normalized();


                //GD.Print($"Plane Equation for {vc} is {d} = {UnitNorm.X}a + {UnitNorm.Y}b + {UnitNorm.Z}c");

                //PolygonRendererSDL.DrawArrow(this, size + (continent.averageHeight / 100f), average, directionPoint, UnitNorm, vcRadius, Colors.Black);


                //}
            }
        }

        return continents;
    }

    private bool TryVariation(List<Point> orderedPoints, int i)
    {
        var a = GetOrderedPoint(orderedPoints, i);
        var b = GetOrderedPoint(orderedPoints, i + 1);
        var c = GetOrderedPoint(orderedPoints, i - 1);
        Vector3 tab = b.ToVector3() - a.ToVector3();
        Vector3 tac = c.ToVector3() - a.ToVector3();
        Vector2 ab = new Vector2(tab.X, tab.Y);
        Vector2 ac = new Vector2(tac.X, tac.Y);
        //GD.Print($"Variation Points: {a}, {b}, {c}");
        //GD.Print($"Variation: {ab.Cross(ac)}");
        if (ab.Cross(ac) > 0.0f) return true;
        return false;

    }


    public VoronoiCell TriangulatePoints(Vector3 unitNorm, List<Point> TriCircumcenters, int index)
    {
        var u = new Vector3(0, 0, 0);
        if (!Mathf.Equals(unitNorm.X, 0.0f))
        {
            u = new Vector3(-unitNorm.Y, unitNorm.X, 0.0f);
        }
        else if (!Mathf.Equals(unitNorm.Y, 0.0f))
        {
            u = new Vector3(-unitNorm.Z, 0, unitNorm.Y);
        }
        else
        {
            u = new Vector3(1, 0, 0);
        }
        u = u.Normalized();
        //GD.Print($"Unit Normal: {unitNorm}");
        //GD.Print($"Unit Vector: {u}");
        var v = unitNorm.Cross(u);

        List<Point> projectedPoints = new List<Point>();
        //foreach(Point p in TriCircumcenters) {
        //    GD.Print($"Point: {p}");
        //}
        var ccs = Point.ToVectors3(TriCircumcenters);
        //foreach(Vector3 vec in ccs) {
        //    GD.Print($"Vector: {vec}");
        //}
        for (int i = 0; i < TriCircumcenters.Count; i++)
        {
            var projection = new Vector2((ccs[i] - ccs[0]).Dot(u), (ccs[i] - ccs[0]).Dot(v));
            //GD.Print($"{(ccs[i]-ccs[0]).Dot(u)}");
            projectedPoints.Add(new Point(new Vector3(projection.X, projection.Y, 0.0f), TriCircumcenters[i].Index));
        }
        //foreach(Point p in projectedPoints) {
        //    GD.Print($"Point Projected: {p}");
        //}

        //Order List of 2D points in clockwise order
        var orderedPoints = ReorderPoints(projectedPoints);
        //foreach(Point p in orderedPoints) {
        //    GD.Print($"Point Reordered: {p}");
        //}
        var orderedPointsReversed = new List<Point>(orderedPoints);
        orderedPointsReversed.Reverse();

        List<Point> TriangulatedIndices = new List<Point>();
        List<Triangle> Triangles = new List<Triangle>();
        HashSet<Edge> CellEdges = new HashSet<Edge>();
        Edge[] triEdges;
        Point v1, v2, v3;
        Vector3 v1Tov2, v1Tov3, triangleCrossProduct;
        float angleTriangleFace;
        int end = 0;
        while (orderedPoints.Count > 3)
        {
            //GD.Print($"Number of Points: {orderedPoints.Count}");
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                var a = GetOrderedPoint(orderedPoints, i);
                var b = GetOrderedPoint(orderedPoints, i - 1);
                var c = GetOrderedPoint(orderedPoints, i + 1);
                //GD.Print($"Points: {a}, {b}, {c}");

                Vector3 tab = b.ToVector3() - a.ToVector3();
                Vector3 tac = c.ToVector3() - a.ToVector3();
                Vector2 ab = new Vector2(tab.X, tab.Y);
                Vector2 ac = new Vector2(tac.X, tac.Y);

                bool variationResult = TryVariation(orderedPoints, i);
                if (ab.Cross(ac) < 0.0f)// && !variationResult)
                {
                    continue;
                }
                else if (variationResult)
                {
                    a = GetOrderedPoint(orderedPoints, i);
                    b = GetOrderedPoint(orderedPoints, i + 1);
                    c = GetOrderedPoint(orderedPoints, i - 1);
                    tab = b.ToVector3() - a.ToVector3();
                    tac = c.ToVector3() - a.ToVector3();
                    ab = new Vector2(tab.X, tab.Y);
                    ac = new Vector2(tac.X, tac.Y);
                }


                bool isEar = true;
                for (int j = 0; j < orderedPoints.Count; j++)
                {
                    if (orderedPoints[j].Index == a.Index || orderedPoints[j].Index == b.Index || orderedPoints[j].Index == c.Index)
                    {
                        continue;
                    }
                    Vector2 p = new Vector2(orderedPoints[j].X, orderedPoints[j].Y);
                    if (IsPointInTriangle(p, new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), new Vector2(c.X, c.Y), false))
                    {
                        //GD.Print($"Point not in triangle: {p}");
                        isEar = false;
                        break;
                    }
                }
                if (isEar)
                {
                    //Take the 3 points in 3D space and generate the normal
                    //If angle between triangle Normal and UnitNormal <90
                    v1 = circumcenters[c.Index];
                    v2 = circumcenters[a.Index];
                    v3 = circumcenters[b.Index];

                    v1Tov2 = v2.ToVector3() - v1.ToVector3();
                    v1Tov3 = v3.ToVector3() - v1.ToVector3();

                    triangleCrossProduct = v1Tov2.Cross(v1Tov3);
                    triangleCrossProduct = triangleCrossProduct.Normalized();
                    angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
                    if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
                    { //Inverse Winding
                        triEdges = new Edge[3];
                        triEdges[0] = UpdateWorldEdgeMap(circumcenters[a.Index], circumcenters[b.Index]);
                        triEdges[1] = UpdateWorldEdgeMap(circumcenters[c.Index], circumcenters[a.Index]);
                        triEdges[2] = UpdateWorldEdgeMap(circumcenters[b.Index], circumcenters[c.Index]);
                        generatedEdges.Add(triEdges[0]);
                        generatedEdges.Add(triEdges[1]);
                        generatedEdges.Add(triEdges[2]);
                        Triangle newTri = new Triangle(Triangles.Count,
                                new List<Point>() { circumcenters[c.Index], circumcenters[a.Index], circumcenters[b.Index] },
                                triEdges.ToList());
                        foreach (Point p in newTri.Points)
                        {
                            if (!VoronoiTriMap.ContainsKey(p))
                            {
                                VoronoiTriMap.Add(p, new HashSet<Triangle>());
                            }
                            VoronoiTriMap[p].Add(newTri);
                        }
                        foreach (Edge e in triEdges)
                        {
                            if (!VoronoiEdgeTriMap.ContainsKey(e))
                            {
                                VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
                            }
                            VoronoiEdgeTriMap[e].Add(newTri);
                        }
                        //RenderTriangleAndConnections(newTri);
                        Triangles.Add(newTri);
                        TriangulatedIndices.Add(circumcenters[c.Index]);
                        TriangulatedIndices.Add(circumcenters[a.Index]);
                        TriangulatedIndices.Add(circumcenters[b.Index]);
                        CellEdges.Add(triEdges[0]);
                        CellEdges.Add(triEdges[1]);
                        CellEdges.Add(triEdges[2]);
                    }
                    else
                    {
                        triEdges = new Edge[3];
                        triEdges[0] = UpdateWorldEdgeMap(circumcenters[b.Index], circumcenters[a.Index]);
                        triEdges[1] = UpdateWorldEdgeMap(circumcenters[a.Index], circumcenters[c.Index]);
                        triEdges[2] = UpdateWorldEdgeMap(circumcenters[c.Index], circumcenters[b.Index]);
                        generatedEdges.Add(triEdges[0]);
                        generatedEdges.Add(triEdges[1]);
                        generatedEdges.Add(triEdges[2]);
                        Triangle newTri = new Triangle(Triangles.Count,
                                new List<Point>() { circumcenters[b.Index], circumcenters[a.Index], circumcenters[c.Index] },
                                triEdges.ToList());
                        foreach (Point p in newTri.Points)
                        {
                            if (!VoronoiTriMap.ContainsKey(p))
                            {
                                VoronoiTriMap.Add(p, new HashSet<Triangle>());
                            }
                            VoronoiTriMap[p].Add(newTri);
                        }
                        foreach (Edge e in triEdges)
                        {
                            if (!VoronoiEdgeTriMap.ContainsKey(e))
                            {
                                VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
                            }
                            VoronoiEdgeTriMap[e].Add(newTri);
                        }
                        //RenderTriangleAndConnections(newTri);
                        Triangles.Add(newTri);
                        TriangulatedIndices.Add(circumcenters[b.Index]);
                        TriangulatedIndices.Add(circumcenters[a.Index]);
                        TriangulatedIndices.Add(circumcenters[c.Index]);
                        CellEdges.Add(triEdges[0]);
                        CellEdges.Add(triEdges[1]);
                        CellEdges.Add(triEdges[2]);
                    }

                    orderedPoints.RemoveAt(i);
                    break;
                }
            }
            end++;
        }
        v1 = circumcenters[orderedPoints[2].Index];
        v2 = circumcenters[orderedPoints[1].Index];
        v3 = circumcenters[orderedPoints[0].Index];

        v1Tov2 = v2.ToVector3() - v1.ToVector3();
        v1Tov3 = v3.ToVector3() - v1.ToVector3();

        triangleCrossProduct = v1Tov2.Cross(v1Tov3);
        triangleCrossProduct = triangleCrossProduct.Normalized();
        angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
        if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
        {
            triEdges = new Edge[3];
            triEdges[0] = UpdateWorldEdgeMap(circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[0].Index]);
            triEdges[1] = UpdateWorldEdgeMap(circumcenters[orderedPoints[2].Index], circumcenters[orderedPoints[1].Index]);
            triEdges[2] = UpdateWorldEdgeMap(circumcenters[orderedPoints[0].Index], circumcenters[orderedPoints[2].Index]);
            generatedEdges.Add(triEdges[0]);
            generatedEdges.Add(triEdges[1]);
            generatedEdges.Add(triEdges[2]);
            Triangle newTri = new Triangle(Triangles.Count, new List<Point>() { circumcenters[orderedPoints[2].Index], circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[0].Index] }, triEdges.ToList());
            foreach (Point p in newTri.Points)
            {
                if (!VoronoiTriMap.ContainsKey(p))
                {
                    VoronoiTriMap.Add(p, new HashSet<Triangle>());
                }
                VoronoiTriMap[p].Add(newTri);
            }
            foreach (Edge e in triEdges)
            {
                if (!VoronoiEdgeTriMap.ContainsKey(e))
                {
                    VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
                }
                VoronoiEdgeTriMap[e].Add(newTri);
            }
            //RenderTriangleAndConnections(newTri);
            Triangles.Add(newTri);
            TriangulatedIndices.Add(circumcenters[orderedPoints[2].Index]);
            TriangulatedIndices.Add(circumcenters[orderedPoints[1].Index]);
            TriangulatedIndices.Add(circumcenters[orderedPoints[0].Index]);
            CellEdges.Add(triEdges[0]);
            CellEdges.Add(triEdges[1]);
            CellEdges.Add(triEdges[2]);
        }
        else
        {
            triEdges = new Edge[3];
            triEdges[0] = UpdateWorldEdgeMap(circumcenters[orderedPoints[0].Index], circumcenters[orderedPoints[1].Index]);
            triEdges[1] = UpdateWorldEdgeMap(circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[2].Index]);
            triEdges[2] = UpdateWorldEdgeMap(circumcenters[orderedPoints[2].Index], circumcenters[orderedPoints[0].Index]);
            generatedEdges.Add(triEdges[0]);
            generatedEdges.Add(triEdges[1]);
            generatedEdges.Add(triEdges[2]);

            Triangle newTri = new Triangle(Triangles.Count, new List<Point>() { circumcenters[orderedPoints[0].Index], circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[2].Index] }, triEdges.ToList());
            foreach (Point p in newTri.Points)
            {
                if (!VoronoiTriMap.ContainsKey(p))
                {
                    VoronoiTriMap.Add(p, new HashSet<Triangle>());
                }
                VoronoiTriMap[p].Add(newTri);
            }
            foreach (Edge e in triEdges)
            {
                if (!VoronoiEdgeTriMap.ContainsKey(e))
                {
                    VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
                }
                VoronoiEdgeTriMap[e].Add(newTri);
            }
            //RenderTriangleAndConnections(newTri, true);
            Triangles.Add(newTri);
            TriangulatedIndices.Add(circumcenters[orderedPoints[0].Index]);
            TriangulatedIndices.Add(circumcenters[orderedPoints[1].Index]);
            TriangulatedIndices.Add(circumcenters[orderedPoints[2].Index]);
            CellEdges.Add(triEdges[0]);
            CellEdges.Add(triEdges[1]);
            CellEdges.Add(triEdges[2]);
        }

        generatedTris.AddRange(Triangles);
        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray(), CellEdges.ToArray());
        foreach (Point p in TriangulatedIndices)
        {
            if (!CellMap.ContainsKey(p))
            {
                CellMap.Add(p, new HashSet<VoronoiCell>());
                CellMap[p].Add(GeneratedCell);
            }
            else
            {
                CellMap[p].Add(GeneratedCell);
            }
        }
        foreach (Edge e in CellEdges)
        {
            if (!EdgeMap.ContainsKey(e))
            {
                EdgeMap.Add(e, new HashSet<VoronoiCell>());
                EdgeMap[e].Add(GeneratedCell);
            }
            else
            {
                EdgeMap[e].Add(GeneratedCell);
            }
        }
        return GeneratedCell;
    }

    public bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, bool reversed)
    {
        var ab = b - a;
        var bc = c - b;
        var ca = a - c;

        var ap = p - a;
        var bp = p - b;
        var cp = p - c;

        if (reversed)
        {

            if (ab.Cross(ap) < 0f || bc.Cross(bp) < 0f || ca.Cross(cp) < 0f)
            {
                return false;
            }
        }
        else
        {
            if (ab.Cross(ap) > 0f || bc.Cross(bp) > 0f || ca.Cross(cp) > 0f)
            {
                return false;
            }
        }
        return true;
    }

    public Point GetOrderedPoint(List<Point> points, int index)
    {
        if (index >= points.Count)
        {
            return points[index % points.Count];
        }
        else if (index < 0)
        {
            return points[index % points.Count + points.Count];
        }
        else
        {
            return points[index];
        }
    }

    public List<Point> ReorderPoints(List<Point> points)
    {
        var average = new Vector3(0, 0, 0);
        foreach (Point p in points)
        {
            average += p.ToVector3();
        }
        average /= points.Count;
        var center = new Vector2(average.X, average.Y);
        //if (center.X <= 0f) { shouldInvertX = true; }
        List<Point> orderedPoints = new List<Point>();
        for (int i = 0; i < points.Count; i++)
        {
            orderedPoints.Add(new Point(new Vector3(points[i].X, points[i].Y, less(center, new Vector2(points[i].X, points[i].Y))), points[i].Index));
        }
        orderedPoints = orderedPoints.OrderBy(p => p.Z).ToList();
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            points[i] = new Point(new Vector3(orderedPoints[i].X, orderedPoints[i].Y, 0.0f), orderedPoints[i].Index);
        }
        return points;
    }
    public float less(Vector2 center, Vector2 a)
    {
        float a1 = (Mathf.RadToDeg(Mathf.Atan2(a.X - center.X, a.Y - center.Y)) + 360) % 360;
        return a1;
    }

    public void RenderTriangleAndConnections(Triangle tri, bool dualMesh = false)
    {
        int i = 0;
        foreach (var p in tri.Points)
        {
            //GD.Print($"{p.ToVector3()}");
        }
        foreach (Edge e in tri.Edges)
        {
            //GD.Print(e);
        }
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
                //GD.Print(p);
                //GD.Print(p.ToVector3());
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
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0? p.Radius: 0.05f, Colors.Red);
                        break;
                    case 1:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0? p.Radius: 0.05f, Colors.Green);
                        break;
                    case 2:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), p.Radius > 0? p.Radius: 0.05f, Colors.Blue);
                        break;
                }
                i++;
            }
        }
        foreach (var edge in edgesFromTri)
        {
            //GD.Print($"Edge: {edge.Index} from {edge.P.ToVector3()} to {edge.Q.ToVector3()}");
        }


        //foreach(Triangle tri in tris) {

        //}
        /*foreach (Edge edge in edges)
        {
            foreach (var p in tri.Points)
            {
                if (edge.P.Index == p.Index || edge.Q.Index == p.Index)
                {
                    if (ProjectToSphere)
                        DrawLine(edge.P.ToVector3().Normalized(), edge.Q.ToVector3().Normalized());
                    else
                        DrawLine(edge.P.ToVector3(), edge.Q.ToVector3());
                }
            }
            //DrawLine(edge.P.ToVector3().Normalized(), edge.Q.ToVector3().Normalized());

        }*/
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

    public void GenerateTriangleList()
    {
        //GD.Print($"Number of Faces: {faces.Count}");
        foreach (Face f in faces)
        {
            //if(VertexPoints.Any(p => p.ToVector3() == tempVector)) {
            //  var existingPoint = VertexPoints.Where(a => a.ToVector3() == tempVector);
            //  return existingPoint.ElementAt(0);
            //}
            //Edge[] triEdges = new Edge[3];
            //for (int i = 0, j = f.v.Length - 1; i < f.v.Length; j = i++)
            //{
            //    if (baseEdges.Any(e => e.P == f.v[j] && e.Q == f.v[i]))
            //    {
            //        triEdges[i] = baseEdges.Where(a => a.P == f.v[j] && a.Q == f.v[i]).ElementAt(0);
            //    }
            //    else if (baseEdges.Any(e => e.P == f.v[i] && e.Q == f.v[j]))
            //    {
            //        Edge e = baseEdges.Where(a => a.P == f.v[i] && a.Q == f.v[j]).ElementAt(0);
            //        e = e.ReverseEdge();
            //        triEdges[i] = e;
            //    }
            //    else
            //    {
            //        triEdges[i] = new Edge(baseEdges.Count, f.v[j], f.v[i]);
            //        baseEdges.Add(triEdges[i]);
            //    }
            //}
            Triangle newTri = new Triangle(baseTris.Count, f.v.ToList(), f.e.ToList());
            //baseEdges.AddRange(f.e);
            baseTris.Add(newTri);
            BaseTris.Add((f.v[0], f.v[1], f.v[2]), newTri);
            foreach (Edge edge in f.e)
            {
                if (!EdgeTriangles.ContainsKey(edge) || EdgeTriangles[edge] == null) EdgeTriangles[edge] = new List<Triangle>();
                EdgeTriangles[edge].Add(newTri);
                if (!HalfEdgesFrom.ContainsKey(edge.P))
                {
                    HalfEdgesFrom.Add(edge.P, new HashSet<Edge>());
                    HalfEdgesFrom[edge.P].Add(edge);
                }
                else
                {
                    HalfEdgesFrom[edge.P].Add(edge);
                }
                if (!HalfEdgesTo.ContainsKey(edge.Q))
                {
                    HalfEdgesTo.Add(edge.Q, new HashSet<Edge>());
                    HalfEdgesTo[edge.Q].Add(edge);
                }
                else
                {
                    HalfEdgesTo[edge.Q].Add(edge);
                }
            }
        }
    }

    public void GenerateNonDeformedFaces()
    {
        List<Face> tempFaces = new List<Face>();
        for (int level = 0; level < subdivide; level++)
        {
            var verticesToGenerate = level < VerticesPerEdge.Length ? VerticesPerEdge[level] : VerticesPerEdge[VerticesPerEdge.Length - 1];
            //GD.Print($"Vertices to Generate: {verticesToGenerate}");
            foreach (Face face in faces)
            {
                tempFaces.AddRange(_subdivider.SubdivideFace(face, verticesToGenerate));
            }
            faces.Clear();
            Edges.Clear();
            faces = new List<Face>(tempFaces);
            tempFaces.Clear();
        }
    }

    public void GenerateFromContinents(Dictionary<int, Continent> continents, Dictionary<int, Point> circumcenters)
    {
        foreach (var keyValuePair in continents)
        {
            GenerateSurfaceMesh(keyValuePair.Value.cells, circumcenters);
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

    private Color GetBiomeColor(VoronoiCell.BiomeType biome, float height)
    {
        switch (biome)
        {
            case VoronoiCell.BiomeType.Tundra:
                return new Color(0.85f, 0.85f, 0.8f); // Light gray-white
            case VoronoiCell.BiomeType.Icecap:
                return Colors.White;
            case VoronoiCell.BiomeType.Desert:
                return new Color(0.9f, 0.8f, 0.5f); // Sandy yellow
            case VoronoiCell.BiomeType.Grassland:
                return new Color(0.5f, 0.8f, 0.3f); // Green
            case VoronoiCell.BiomeType.Forest:
                return new Color(0.2f, 0.6f, 0.2f); // Dark green
            case VoronoiCell.BiomeType.Rainforest:
                return new Color(0.1f, 0.4f, 0.1f); // Very dark green
            case VoronoiCell.BiomeType.Taiga:
                return new Color(0.4f, 0.5f, 0.3f); // Dark green-brown
            case VoronoiCell.BiomeType.Ocean:
                return new Color(0.1f, 0.3f, 0.7f); // Deep blue
            case VoronoiCell.BiomeType.Coastal:
                return new Color(0.8f, 0.7f, 0.4f); // Light blue
            case VoronoiCell.BiomeType.Mountain:
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
            Color biomeColor = Colors.Pink;
            if (ShouldDisplayBiomes)
            {
                biomeColor = GetBiomeColor(vor.Biome, vor.Height);
            }
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
                        st.SetColor(biomeColor == Colors.Pink ? GetVertexColor(vor.Points[3 * i + j].Height) : biomeColor);
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
        st.Commit(arrMesh);
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

    public void PopulateArrays()
    {
        List<Point> cartesionPoints = new List<Point> {
                        new Point(new Vector3(0, 1, TAU)),
                        new Point( new Vector3(0, -1, TAU)),
                        new Point( new Vector3(0, -1, -TAU)),
                        new Point( new Vector3(0, 1, -TAU)),
                        new Point(new Vector3(1, TAU, 0)),
                        new Point( new Vector3(-1, TAU, 0)),
                        new Point( new Vector3(-1, -TAU, 0)),
                        new Point( new Vector3(1, -TAU, 0)),
                        new Point(new Vector3(TAU, 0, 1)),
                        new Point( new Vector3(TAU, 0, -1)),
                        new Point( new Vector3(-TAU, 0, -1)),
                        new Point( new Vector3(-TAU, 0, 1))};
        VertexIndex = 12;
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        foreach (Point p in cartesionPoints)
        {
            p.Position = p.Position.Normalized();
            normals.Add(p.ToVector3());
            //GD.Print($"Point: {p}");
            VertexPoints.Add(p.Index, p);
        }
        faces = new List<Face>();
        indices = new List<int> {
      0, 5, 4,
      0, 11, 5,
      0, 4, 8,
      0, 8, 1,
      0, 1, 11,
      3, 4, 5,
      3, 5, 10,
      3, 9, 4,
      3, 10, 2,
      3, 2, 9,
      10, 5, 11,
      10, 11, 6,
      8, 4, 9,
      8, 9, 7,
      1, 7, 6,
      1, 6, 11,
      1, 8, 7,
      2, 10, 6,
      2, 7, 9,
      2, 6, 7,
    };
        for (int i = 0; i < indices.Count; i += 3)
        {
            faces.Add(new Face(cartesionPoints[indices[i]], cartesionPoints[indices[i + 1]], cartesionPoints[indices[i + 2]], new Edge(cartesionPoints[indices[i]], cartesionPoints[indices[i + 1]]), new Edge(cartesionPoints[indices[i + 1]], cartesionPoints[indices[i + 2]]), new Edge(cartesionPoints[indices[i + 2]], cartesionPoints[indices[i]])));
        }

    }


    private Edge UpdateWorldEdgeMap(Point p1, Point p2)
    {
        Edge generatedEdge = new Edge(p1, p2);
        Edge toUpdate = null;
        if (!MapContains(worldHalfEdgeMapTo, p1))
        {
            worldHalfEdgeMapTo.Add(p1, new Dictionary<Point, Edge>());
        }
        if (!MapContains(worldHalfEdgeMapFrom, p2))
        {
            worldHalfEdgeMapFrom.Add(p2, new Dictionary<Point, Edge>());
        }
        if (MapContains(worldHalfEdgeMapTo, p1, p2))
        {
            toUpdate = worldHalfEdgeMapTo[p1][p2];
        }
        else
        {
            worldHalfEdgeMapTo[p1].Add(p2, generatedEdge);
            toUpdate = generatedEdge;
        }
        if (MapContains(worldHalfEdgeMapFrom, p2, p1))
        {
            toUpdate = worldHalfEdgeMapFrom[p2][p1];
        }
        else
        {
            worldHalfEdgeMapFrom[p2].Add(p1, generatedEdge);
            toUpdate = generatedEdge;
        }
        return toUpdate;
    }

    private bool MapContains(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1)
    {
        return map.ContainsKey(p1);
    }
    private bool MapContains(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1, Point p2)
    {
        if (map.ContainsKey(p1))
        {
            return map[p1].ContainsKey(p2);
        }
        return false;
    }

    private bool MapIndexCreated(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1)
    {
        return map[p1] != null;
    }
}

