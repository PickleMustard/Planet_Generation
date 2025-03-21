using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class Extension
{
    public static Vector3[] ToVectors3(this IEnumerable<Point> points) => points.Select(point => point.ToVector3()).ToArray();
    public static Vector3 ToVector3(this Point point) => new Vector3((float)point.X, (float)point.Y, (float)point.Z);
    public static Vector2 ToVector2(this Point point) => new Vector2((float)point.X, (float)point.Y);
    public static Point[] ToPoints(this IEnumerable<Vector3> vertices) => vertices.Select(vertex => vertex.ToPoint()).ToArray();
    public static Point ToPoint(this Vector3 vertex) => new Point(vertex);
    public static Edge ReverseEdge(this Edge e) { var t = e.Q; e.Q = e.P; e.P = t; return e; }
}

public partial class GenerateDocArrayMesh : MeshInstance3D
{
    public enum CrustType
    {
        Continental = 0,
        Oceanic = 1
    };
    public struct Face
    {
        public Point[] v;

        public Face(Point v0, Point v1, Point v2)
        {
            v = new Point[] { v0, v1, v2 };
        }
        public Face(params Point[] points)
        {
            v = points;
        }
        public Face(IEnumerable<Point> points)
        {
            v = points.ToArray();
        }
    }

    public struct Continent
    {
        public int StartingIndex;
        public List<VoronoiCell> cells;
        public HashSet<VoronoiCell> boundaryCells;
        public HashSet<Point> points;
        public List<Point> ConvexHull;
        public Vector3 averagedCenter;
        public Vector3 uAxis;
        public Vector3 vAxis;

        public Vector2 movementDirection;
        public float rotation;

        public CrustType elevation;
        public float averageHeight;
        public float averageMoisture;
    }
    public DelaunatorSharp.Delaunator dl;
    static float TAU = (1 + (float)Math.Sqrt(5)) / 2;
    Vector3 origin = new Vector3(0, 0, 0);
    public List<Point> VertexPoints;
    public List<Vector3> normals;
    public List<Vector2> uvs;
    public List<int> indices;
    public List<Face> faces;
    public int VertexIndex = 0;

    public List<Edge> baseEdges = new List<Edge>();
    public List<Triangle> baseTris = new List<Triangle>();
    public List<Face> dualFaces = new List<Face>();
    public List<Point> circumcenters = new List<Point>();
    public List<Edge> generatedEdges = new List<Edge>();
    public List<Triangle> generatedTris = new List<Triangle>();
    public List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();
    RandomNumberGenerator rand = new RandomNumberGenerator();

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

    public override void _Ready()
    {
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
        GD.Print(VertexPoints.Count);
        GD.Print(baseTris.Count);
        var OptimalArea = (4.0f * Mathf.Pi * size * size) / baseTris.Count;
        GD.Print($"Optimal Area of Triangle: {OptimalArea}");
        var OptimalSideLength = Mathf.Sqrt((OptimalArea * 4.0f) / Mathf.Sqrt(3.0f)) / 3f;
        GD.Print($"Optimal Side Length of Triangle: {OptimalSideLength}");
        var OptimalCentroidLength = Mathf.Cos(Mathf.DegToRad(30.0f)) * .5f * OptimalSideLength;
        GD.Print($"Optimal Length from Vertex of Triangle to Centroid: {OptimalCentroidLength}");
        int alteredIndex = 0;
        //var randomTri = baseTris[baseTris.Count - baseTris.Count / 2];
        for (int deforms = 0; deforms < NumDeformationCycles; deforms++)
        {
            GD.Print($"Deformation Cycle: {deforms} | Deform Amount: {(2f + deforms) / (deforms + 1)}");
            for (int i = 0; i < NumAbberations; i++)
            {
                var randomTri = baseTris[rand.RandiRange(0, baseTris.Count - 1)];
                var randomTriPoint = randomTri.Points[rand.RandiRange(0, 2)];
                var edgesWithPoint = baseEdges.Where(e => randomTri.Points.Contains(e.Q) && randomTri.Points.Contains(e.P));
                var edgesWithPointList = edgesWithPoint.ToList();
                List<Edge> edgesFromPoint = baseEdges.Where(e => e.P == randomTriPoint).ToList();
                List<Edge> edgesToPoint = baseEdges.Where(e => e.Q == randomTriPoint).ToList();
                List<Edge> allEdges = new List<Edge>(edgesFromPoint);
                allEdges.AddRange(edgesToPoint);
                bool shouldRedo = false;
                foreach (Edge e in edgesFromPoint)
                {
                    List<Edge> fromPoint = baseEdges.Where(ed => ed.P == e.Q).ToList();
                    List<Edge> ToPoint = baseEdges.Where(ed => ed.Q == e.Q).ToList();

                    //GD.Print($"Edges from Point: {fromPoint.Count + ToPoint.Count}");
                    if (fromPoint.Count + ToPoint.Count < 5) shouldRedo = true;

                }
                foreach (Edge e in edgesToPoint)
                {
                    List<Edge> fromPoint = baseEdges.Where(ed => ed.P == e.P).ToList();
                    List<Edge> ToPoint = baseEdges.Where(ed => ed.Q == e.P).ToList();

                    //GD.Print($"Edges from Point: {fromPoint.Count + ToPoint.Count}");
                    if (fromPoint.Count + ToPoint.Count < 5) shouldRedo = true;

                }
                //GD.Print($"Edges from Point: {edgesToPoint.Count + edgesFromPoint.Count}");
                bool EnoughEdges = edgesFromPoint.Count + edgesToPoint.Count > 5;


                if (EnoughEdges && !shouldRedo)
                //if (edgesFromPoint.Count + edgesToPoint.Count > 5 && edgesToPoint.Count + edgesFromPoint.Count < 7)
                {
                    if (edgesWithPointList.Count > 0)
                    {
                        var trisWithEdge = baseTris.Where(tri => tri.Points.Contains(edgesWithPointList[0].P) && tri.Points.Contains(edgesWithPointList[0].Q));
                        var tempTris1 = trisWithEdge.ElementAt(0);
                        var tempTris2 = trisWithEdge.ElementAt(1);
                        alteredIndex = tempTris1.Index;
                        var points1 = tempTris1.Points;
                        var points2 = tempTris2.Points;

                        var sharedEdge = edgesWithPointList[0];
                        var sharedPoint1 = sharedEdge.Q;
                        var sharedPoint2 = sharedEdge.P;
                        var t1UnsharedPoint = tempTris1.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                        var t2UnsharedPoint = tempTris2.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                        var sharedEdgeLength = (sharedEdge.P.ToVector3() - sharedEdge.Q.ToVector3()).Length();
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
                        sharedEdge.Q = t1UnsharedPoint;
                        sharedEdge.P = t2UnsharedPoint;

                        //var index = tempTris1.Index;
                        //tempTris1.Index = tempTris2.Index;
                        //tempTris2.Index = index;

                        var otherEdgesT1 = tempTris1.Edges.Where(e => e != sharedEdge).ToList();
                        otherEdgesT1[0].P = sharedPoint1;
                        otherEdgesT1[0].Q = t1UnsharedPoint;
                        otherEdgesT1[1].Q = sharedPoint1;
                        otherEdgesT1[1].P = t2UnsharedPoint;

                        var otherEdgesT2 = tempTris2.Edges.Where(e => e != sharedEdge).ToList();
                        otherEdgesT2[0].Q = sharedPoint2;
                        otherEdgesT2[0].P = t2UnsharedPoint;
                        otherEdgesT2[1].P = sharedPoint2;
                        otherEdgesT2[1].Q = t1UnsharedPoint;
                    }
                }
                else
                {
                    i--;
                    continue;
                }
            }


            GD.Print("Relaxing");
            for (int index = 0; index < 12; index++)
            {
                foreach (Point p in VertexPoints)
                {
                    var trianglesWithPoint = baseTris.Where(t => t.Points.Contains(p));
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
                    average /= trianglesWithPoint.ToList().Count;
                    p.Position = average;
                    var pointEdges = baseEdges.Where(e => e.P == p || e.Q == p);
                }
            }
        }

        GD.Print("Triangulating");
        foreach (Point p in VertexPoints)
        {
            //Find all triangles that contain the current point
            var triangleOfP = baseTris.Where(e => e.Points.Any(a => a == p));
            List<Triangle> trianglesWithPoint = triangleOfP.ToList();
            List<Point> triCircumcenters = new List<Point>();
            foreach (var tri in trianglesWithPoint)
            {
                var v3 = tri.Points.ToVectors3();
                var ac = v3[2] - v3[0];
                var ab = v3[1] - v3[0];
                var abXac = ab.Cross(ac);
                var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
                float circumsphereRadius = vToCircumsphereCenter.Length();
                var cc = v3[0] + vToCircumsphereCenter;
                if (triCircumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc))) continue;
                if (circumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc)))
                {
                    var usedCC = circumcenters.Where(cir => Mathf.Equals(cir.ToVector3(), cc));
                    triCircumcenters.Add(usedCC.ElementAt(0));
                }
                else
                {
                    circumcenters.Add(new Point(cc, circumcenters.Count));
                    triCircumcenters.Add(circumcenters[circumcenters.Count - 1]);
                }
            }
            Face dualFace = new Face(triCircumcenters.ToList());
            dualFaces.Add(dualFace);
            Vector3 center = new Vector3(0, 0, 0);
            for (int i = 0; i < triCircumcenters.Count; i++) center += triCircumcenters[i].ToVector3();
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
            VoronoiCell calculated = TriangulatePoints(UnitNorm, triCircumcenters, circumcenters, VoronoiCells.Count);
            calculated.IsBorderTile = false;
            if (calculated != null)
            {
                VoronoiCells.Add(calculated);
            }
        }

        var continents = FloodFillContinentGeneration(VoronoiCells);
        foreach (VoronoiCell vc in VoronoiCells)
        {
            var cellNeighbors = GetCellNeighbors(VoronoiCells, vc.Index);
            float averageHeight = vc.Height;
            foreach (int neighbor in cellNeighbors)
            {
                GD.Print($"VC: {vc.Index}, {vc.ContinentIndex} | Neighbor: {VoronoiCells[neighbor].Index}, {VoronoiCells[neighbor].ContinentIndex}");
                if (VoronoiCells[neighbor].ContinentIndex != vc.ContinentIndex)
                {
                    vc.IsBorderTile = true;
                    continents[vc.ContinentIndex].boundaryCells.Add(vc);
                    VoronoiCells[neighbor].IsBorderTile = true;
                }
                averageHeight += VoronoiCells[neighbor].Height;
            }
            vc.Height = averageHeight / (cellNeighbors.Length + 1);
            foreach (Point p in vc.Points)
            {
                p.Height = vc.Height;
            }
        }

        GD.Print($"Number of Cells in mesh: {VoronoiCells.Count}");
        GD.Print("Generating Mesh");
        GD.Print($"Cell Height: {circumcenters[0].Height}");
        //GenerateSurfaceMesh(VoronoiCells, circumcenters);
        foreach (Point p in circumcenters)
        {
            var trianglesWithPoint = generatedTris.Where(t => t.Points.Contains(p));
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
            var pointEdges = baseEdges.Where(e => e.P == p || e.Q == p);
        }
        CalculateBoundaryStress(continents, VoronoiCells, circumcenters);
        GenerateFromContinents(continents, circumcenters);

    }

    public void CalculateBoundaryStress(Dictionary<int, Continent> continents, List<VoronoiCell> cells, List<Point> points) {
      //

    }

    public int[] GetCellNeighbors(List<VoronoiCell> cells, int index)
    {
        var currentCell = cells[index];
        HashSet<VoronoiCell> neighbors = new HashSet<VoronoiCell>();
        foreach (Point p in currentCell.Points)
        {
            var neighboringCells = cells.Where(vc => vc.Points.Any(vcp => vcp == p));
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
            var continent = new Continent();
            continent.cells = new List<VoronoiCell>();
            continent.points = new HashSet<Point>();
            continent.averageHeight = rand.RandfRange(-10f, 10f);
            continent.averageMoisture = rand.RandfRange(1.0f, 5.0f);
            continent.movementDirection = new Vector2(rand.RandfRange(-1f, 1f), rand.RandfRange(-1f, 1f));
            continent.rotation = rand.RandiRange(-360, 360);
            continent.averagedCenter = new Vector3(0f, 0f, 0f);
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
            GD.Print($"StartingIndex: {continent.StartingIndex}");
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

                PolygonRendererSDL.DrawArrow(this, size + (continent.averageHeight / 100f), average, directionPoint, UnitNorm, vcRadius, Colors.Black);


                //}
            }
        }

        return continents;
    }

    public VoronoiCell TriangulatePoints(Vector3 unitNorm, List<Point> TriCircumcenters, List<Point> TrueCircumcenters, int index)
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
        var v = unitNorm.Cross(u);

        List<Point> projectedPoints = new List<Point>();
        var ccs = TriCircumcenters.ToVectors3();
        for (int i = 0; i < TriCircumcenters.Count; i++)
        {
            var projection = new Vector2((ccs[i] - ccs[0]).Dot(u), (ccs[i] - ccs[0]).Dot(v));
            projectedPoints.Add(new Point(new Vector3(projection.X, projection.Y, 0.0f), TriCircumcenters[i].Index));
        }

        //Order List of 2D points in clockwise order
        var orderedPoints = ReorderPoints(projectedPoints);
        var orderedPointsReversed = new List<Point>(orderedPoints);
        orderedPointsReversed.Reverse();

        List<Point> TriangulatedIndices = new List<Point>();
        List<Triangle> Triangles = new List<Triangle>();
        Edge[] triEdges;
        Point v1, v2, v3;
        Vector3 v1Tov2, v1Tov3, triangleCrossProduct;
        float angleTriangleFace;
        while (orderedPoints.Count > 3)
        {
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                var a = GetOrderedPoint(orderedPoints, i);
                var b = GetOrderedPoint(orderedPoints, i - 1);
                var c = GetOrderedPoint(orderedPoints, i + 1);

                Vector3 tab = b.ToVector3() - a.ToVector3();
                Vector3 tac = c.ToVector3() - a.ToVector3();
                Vector2 ab = new Vector2(tab.X, tab.Y);
                Vector2 ac = new Vector2(tac.X, tac.Y);

                if (ab.Cross(ac) < 0.0f)
                {
                    continue;
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
                        isEar = false;
                        break;
                    }
                }
                if (isEar)
                {
                    //Take the 3 points in 3D space and generate the normal
                    //If angle between triangle Normal and UnitNormal <90
                    v1 = TrueCircumcenters[c.Index];
                    v2 = TrueCircumcenters[a.Index];
                    v3 = TrueCircumcenters[b.Index];

                    v1Tov2 = v2.ToVector3() - v1.ToVector3();
                    v1Tov3 = v3.ToVector3() - v1.ToVector3();

                    triangleCrossProduct = v1Tov2.Cross(v1Tov3);
                    triangleCrossProduct = triangleCrossProduct.Normalized();
                    angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
                    if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
                    { //Inverse Winding
                        triEdges = new Edge[3];
                        triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[a.Index], TrueCircumcenters[b.Index]);
                        triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[c.Index], TrueCircumcenters[a.Index]);
                        triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[b.Index], TrueCircumcenters[c.Index]);
                        generatedEdges.AddRange(triEdges);
                        Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[c.Index], TrueCircumcenters[a.Index], TrueCircumcenters[b.Index] }, triEdges.ToList()));
                        TriangulatedIndices.Add(TrueCircumcenters[c.Index]);
                        TriangulatedIndices.Add(TrueCircumcenters[a.Index]);
                        TriangulatedIndices.Add(TrueCircumcenters[b.Index]);
                    }
                    else
                    {
                        triEdges = new Edge[3];
                        triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[b.Index], TrueCircumcenters[a.Index]);
                        triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[a.Index], TrueCircumcenters[c.Index]);
                        triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[c.Index], TrueCircumcenters[b.Index]);
                        generatedEdges.AddRange(triEdges);
                        Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[b.Index], TrueCircumcenters[a.Index], TrueCircumcenters[c.Index] }, triEdges.ToList()));
                        TriangulatedIndices.Add(TrueCircumcenters[b.Index]);
                        TriangulatedIndices.Add(TrueCircumcenters[a.Index]);
                        TriangulatedIndices.Add(TrueCircumcenters[c.Index]);
                    }

                    orderedPoints.RemoveAt(i);
                    break;
                }
            }
        }
        v1 = TrueCircumcenters[orderedPoints[2].Index];
        v2 = TrueCircumcenters[orderedPoints[1].Index];
        v3 = TrueCircumcenters[orderedPoints[0].Index];

        v1Tov2 = v2.ToVector3() - v1.ToVector3();
        v1Tov3 = v3.ToVector3() - v1.ToVector3();

        triangleCrossProduct = v1Tov2.Cross(v1Tov3);
        triangleCrossProduct = triangleCrossProduct.Normalized();
        angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
        if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
        {
            triEdges = new Edge[3];
            triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[0].Index]);
            triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[2].Index], TrueCircumcenters[orderedPoints[1].Index]);
            triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[2].Index]);
            generatedEdges.AddRange(triEdges);
            Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[orderedPoints[2].Index], TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[0].Index] }, triEdges.ToList()));
            TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[2].Index]);
            TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[1].Index]);
            TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[0].Index]);
        }
        else
        {
            triEdges = new Edge[3];
            triEdges[0] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[1].Index]);
            triEdges[1] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[2].Index]);
            triEdges[2] = new Edge(generatedEdges.Count, TrueCircumcenters[orderedPoints[2].Index], TrueCircumcenters[orderedPoints[0].Index]);
            generatedEdges.AddRange(triEdges);
            Triangles.Add(new Triangle(Triangles.Count, new List<Point>() { TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[2].Index] }, triEdges.ToList()));
            TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[0].Index]);
            TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[1].Index]);
            TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[2].Index]);
        }

        generatedTris.AddRange(Triangles);
        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray());
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

    public void RenderTriangleAndConnections(Triangle tri)
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
        var edgesFromTri = baseEdges.Where(e => tri.Points.Any(a => a == e.P || a == e.Q));
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
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), 0.05f, Colors.Red);
                        break;
                    case 1:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), 0.05f, Colors.Green);
                        break;
                    case 2:
                        PolygonRendererSDL.DrawPoint(this, size, p.ToVector3().Normalized(), 0.05f, Colors.Blue);
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
        foreach (Face f in faces)
        {
            //if(VertexPoints.Any(p => p.ToVector3() == tempVector)) {
            //  var existingPoint = VertexPoints.Where(a => a.ToVector3() == tempVector);
            //  return existingPoint.ElementAt(0);
            //}
            Edge[] triEdges = new Edge[3];
            for (int i = 0, j = f.v.Length - 1; i < f.v.Length; j = i++)
            {
                if (baseEdges.Any(e => e.P == f.v[j] && e.Q == f.v[i]))
                {
                    triEdges[i] = baseEdges.Where(a => a.P == f.v[j] && a.Q == f.v[i]).ElementAt(0);
                }
                else if (baseEdges.Any(e => e.P == f.v[i] && e.Q == f.v[j]))
                {
                    Edge e = baseEdges.Where(a => a.P == f.v[i] && a.Q == f.v[j]).ElementAt(0);
                    e = e.ReverseEdge();
                    triEdges[i] = e;
                }
                else
                {
                    triEdges[i] = new Edge(baseEdges.Count, f.v[j], f.v[i]);
                    baseEdges.Add(triEdges[i]);
                }
            }
            baseTris.Add(new Triangle(baseTris.Count, f.v.ToList(), triEdges.ToList()));
        }
    }

    public void GenerateNonDeformedFaces()
    {
        for (int i = 0; i < subdivide; i++)
        {
            var tempFaces = new List<Face>();
            foreach (Face face in faces)
            {
                tempFaces.AddRange(Subdivide(face));
            }
            faces = new List<Face>(tempFaces);
        }
    }

    public void GenerateFromContinents(Dictionary<int, Continent> continents, List<Point> circumcenters)
    {
        foreach (var keyValuePair in continents)
        {
            var continent = keyValuePair.Value;
            var HeightHue = -6f * continent.averageHeight + 180f;
            var MoistureCoefficient = 2f * (240f - HeightHue) / 240f;
            var Hue = ((17.5f * continent.averageMoisture * MoistureCoefficient) - (77.5f * MoistureCoefficient) + HeightHue) / 360f;
            var Saturation = (1f - Mathf.Log(.12f * (continent.averageHeight + Mathf.Abs(continent.averageHeight) + .01f) / 2)) / 10f;
            GD.Print($"Continent Color: Hue: {Hue} | Sat: {Saturation}");
            Color continentColor = Color.FromHsv(Hue, 1f, 1f, 1f);
            if (GenerateRealistic)
            {
                GenerateSurfaceMesh(keyValuePair.Value.cells, circumcenters, continent.averageHeight, continent.averageMoisture, continentColor);
            }
            else
            {
                GenerateSurfaceMesh(keyValuePair.Value.cells, circumcenters, continent.averageHeight, continent.averageMoisture, new Color(rand.Randf(), rand.Randf(), rand.Randf()));
            }
        }
    }

    public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList, List<Point> circumcenters, float averageHeight, float moisture, Color? color = null)
    {
        RandomNumberGenerator randy = new RandomNumberGenerator();
        float height = (averageHeight / 100.0f);
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
            //foreach (Triangle t in vor.Triangles)
            //{
            //    RenderTriangleAndConnections(t);
            //}
            //st.SetColor(new Color((float)vor.Index / (float)VoronoiList.Count,(float)vor.Index / (float)VoronoiList.Count ,(float)vor.Index / (float)VoronoiList.Count));
            if (vor.Height < 0f)
            {
                var depthRatio = Mathf.Min(1.0f, Mathf.Abs(vor.Height) / 10f);
                var Hue = (180 + (60 * depthRatio)) / 360f;
                var Saturation = .7f + (.3f * depthRatio);
                var Value = .6f - (.3f * depthRatio);
                color = Color.FromHsv(Hue, Saturation, Value, 1f);
            }
            else
            {
                var normalizedMoisture = (moisture - 1f) / 4f;
                var normalizedHeight = Mathf.Min(1f, vor.Height / 10f);
                var Hue = (60 + (60 * normalizedMoisture)) / 360f;
                var Saturation = .4f + (.5f * normalizedMoisture);
                var Value = .5f + (.3f * normalizedHeight) - (.1f * normalizedMoisture);
                color = Color.FromHsv(Hue, Saturation, Value, 1f);
            }
            st.SetColor(color ?? new Color(randy.Randf(), randy.Randf(), randy.Randf()));
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
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 100f));
                    else
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * (size + vor.Points[3 * i + j].Height / 10f));
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

        if (VertexPoints.Any(p => p.ToVector3() == tempVector))
        {
            var existingPoint = VertexPoints.Where(a => a.ToVector3() == tempVector);
            return existingPoint.ElementAt(0);
        }
        Point middlePoint = new Point(tempVector, VertexPoints.Count);

        VertexPoints.Add(middlePoint);
        return middlePoint;
    }

    public void PopulateArrays()
    {
        VertexPoints = new List<Point> {
                        new Point(new Vector3(0, 1, TAU), 0),
                        new Point( new Vector3(0, -1, TAU), 1),
                        new Point( new Vector3(0, -1, -TAU), 2),
                        new Point( new Vector3(0, 1, -TAU), 3),
                        new Point(new Vector3(1, TAU, 0), 4),
                        new Point( new Vector3(-1, TAU, 0), 5),
                        new Point( new Vector3(-1, -TAU, 0), 6),
                        new Point( new Vector3(1, -TAU, 0), 7),
                        new Point(new Vector3(TAU, 0, 1), 8),
                        new Point( new Vector3(TAU, 0, -1), 9),
                        new Point( new Vector3(-TAU, 0, -1), 10),
                        new Point( new Vector3(-TAU, 0, 1), 11)};
        VertexIndex = 12;
        // for(int i = 0; i < cartesionPoints.Count; i++) {
        //   cartesionPoints[i] = cartesionPoints[i].Normalized();
        // }
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        foreach (Point point in VertexPoints)
        {
            point.Position = point.Position.Normalized();
            normals.Add(point.ToVector3());
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
            faces.Add(new Face(VertexPoints[indices[i]], VertexPoints[indices[i + 1]], VertexPoints[indices[i + 2]]));
        }

    }

}

