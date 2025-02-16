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

        public Vector2 movementDirection;
        public float rotation;

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
        DrawPoint(new Vector3(0, 0, 0), 0.1f, Colors.White);
        //Generate the starting dodecahedron
        PopulateArrays();
        //Split the faces n times
        GenerateNonDeformedFaces();
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        foreach (Point point in VertexPoints)
        {
            //meshInstances.Add(DrawPoint(point.ToVector3(), 0.05f, new Color(Math.Abs(point.ToVector3().X), Math.Abs(point.ToVector3().Y), Math.Abs(point.ToVector3().Z))));
        }


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
                        //GD.Print(tempTris1);
                        //GD.Print(tempTris2);
                        alteredIndex = tempTris1.Index;
                        var points1 = tempTris1.Points;
                        var points2 = tempTris2.Points;

                        var sharedEdge = edgesWithPointList[0];
                        //GD.Print(sharedEdge);
                        var sharedPoint1 = sharedEdge.Q;
                        var sharedPoint2 = sharedEdge.P;
                        var t1UnsharedPoint = tempTris1.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                        var t2UnsharedPoint = tempTris2.Points.Where(p => p != sharedPoint1 && p != sharedPoint2).ElementAt(0);
                        var sharedEdgeLength = (sharedEdge.P.ToVector3() - sharedEdge.Q.ToVector3()).Length();
                        var newEdgeLength = (t1UnsharedPoint.ToVector3() - t2UnsharedPoint.ToVector3()).Length();
                        //GD.Print($"Difference in Edge Lengths: {Mathf.Abs(sharedEdgeLength - newEdgeLength)}");
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
            if (calculated != null)
            {
                VoronoiCells.Add(calculated);
            }
        }

        var continents = FloodFillContinentGeneration(VoronoiCells);

        GD.Print($"Number of Cells in mesh: {VoronoiCells.Count}");
        GD.Print("Generating Mesh");
        //GenerateSurfaceMesh(VoronoiCells, circumcenters);
        GenerateFromContinents(continents, circumcenters);

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
            continent.averageHeight = rand.RandfRange(-10f, 10f);
            continent.averageMoisture = rand.RandfRange(1.0f, 5.0f);
            neighborChart[i] = i;
            continent.StartingIndex = i;
            continent.cells.Add(cells[i]);
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
            var currentRegion = queue[pos];
            queue[pos] = queue[i];
            var neighborIndices = GetCellNeighbors(cells, currentRegion);
            foreach (var nb in neighborIndices)
            {
                if (neighborChart[nb] == -1)
                {
                    neighborChart[nb] = neighborChart[currentRegion];
                    queue.Add(nb);
                }
            }
        }

        for (int i = 0; i < neighborChart.Length; i++)
        {
            if (neighborChart[i] != -1)
            {
                continents[neighborChart[i]].cells.Add(cells[i]);
            }
        }

        foreach (var keyValuePair in continents)
        {
            GD.Print("Meow");
            GD.Print(keyValuePair.Value.StartingIndex);
            GD.Print(keyValuePair.Value.cells.Count);
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
        DrawLine(tri.Points[0].ToVector3().Normalized(), tri.Points[1].ToVector3().Normalized());
        DrawLine(tri.Points[1].ToVector3().Normalized(), tri.Points[2].ToVector3().Normalized());
        DrawLine(tri.Points[2].ToVector3().Normalized(), tri.Points[0].ToVector3().Normalized());
        foreach (Point p in tri.Points)
        {
            //GD.Print(p);
            //GD.Print(p.ToVector3());
            if (ProjectToSphere)
            {
                switch (i)
                {
                    case 0:
                        DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Red);
                        break;
                    case 1:
                        DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Green);
                        break;
                    case 2:
                        DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Blue);
                        break;
                }
            }
            else
                DrawPoint(p.ToVector3(), 0.1f, Colors.Black);
            i++;
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
            Color continentColor = new Color(rand.RandfRange(.3f, 1f), rand.RandfRange(.3f, 1f), rand.RandfRange(0f, .5f));
            if (keyValuePair.Value.averageHeight < 0f)
            {
                continentColor = new Color(0.0f, rand.RandfRange(0f, .4f), rand.RandfRange(.7f, 1f));
            }
            GenerateSurfaceMesh(keyValuePair.Value.cells, circumcenters, continentColor);
        }
    }

    public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList, List<Point> circumcenters, Color? color = null)
    {
        RandomNumberGenerator randy = new RandomNumberGenerator();
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
                        st.AddVertex(vor.Points[3 * i + j].ToVector3().Normalized() * size);
                    else
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * size);
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
        tempVector.Normalized();

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

    public MeshInstance3D DrawFace(Color? color = null, params Vector3[] vertices)
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();
        var material = ResourceLoader.Load("res://face_shader.tres") as ShaderMaterial;
        material.Set("base_color", new Color(Math.Abs(50), Math.Abs(70), Math.Abs(50)));
        material.Set("border_thickness", 0.01);

        meshInstance.Mesh = immediateMesh;
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;


        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        foreach (Vector3 vertex in vertices)
        {
            immediateMesh.SurfaceAddVertex(vertex * size);
        }
        immediateMesh.SurfaceEnd();

        //material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        //material.AlbedoColor = color ?? Colors.WhiteSmoke;
        this.AddChild(meshInstance);

        return meshInstance;
    }

    public MeshInstance3D DrawTriangle(Vector3 pos1, Vector3 pos2, Vector3 pos3, Color? color = null)
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();
        //var material = ResourceLoader.Load("res://face_shader.tres") as ShaderMaterial;
        var material = new StandardMaterial3D();
        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = color ?? Colors.Pink;


        meshInstance.Mesh = immediateMesh;
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;


        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        immediateMesh.SurfaceAddVertex(pos1 * size);
        immediateMesh.SurfaceAddVertex(pos2 * size);
        immediateMesh.SurfaceAddVertex(pos3 * size);
        immediateMesh.SurfaceEnd();

        //material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        //material.AlbedoColor = color ?? Colors.WhiteSmoke;
        this.AddChild(meshInstance);

        return meshInstance;
    }

    public MeshInstance3D DrawLine(Vector3 pos1, Vector3 pos2, Color? color = null)
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();
        var material = new StandardMaterial3D();

        meshInstance.Mesh = immediateMesh;
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;


        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        immediateMesh.SurfaceAddVertex(pos1 * size);
        immediateMesh.SurfaceAddVertex(pos2 * size);
        immediateMesh.SurfaceEnd();

        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = color ?? Colors.Aqua;
        this.AddChild(meshInstance);

        return meshInstance;
    }

    public MeshInstance3D DrawPoint(Vector3 pos, float radius = 0.05f, Color? color = null)
    {
        var meshInstance = new MeshInstance3D();
        var sphereMesh = new SphereMesh();
        var material = new StandardMaterial3D();

        meshInstance.Mesh = sphereMesh;
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        meshInstance.Position = pos * size;

        sphereMesh.Radius = radius;
        sphereMesh.Height = radius * 2f;
        sphereMesh.Material = material;

        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = color ?? Colors.WhiteSmoke;

        this.AddChild(meshInstance);

        return meshInstance;
    }
}

