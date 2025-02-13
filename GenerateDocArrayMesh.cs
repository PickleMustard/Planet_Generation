using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class Extension
{
    public static Vector3[] ToVectors3(this IEnumerable<IPoint> points) => points.Select(point => point.ToVector3()).ToArray();
    public static Vector3 ToVector3(this IPoint point) => new Vector3((float)point.X, (float)point.Y, (float)point.Z);
    public static Vector2 ToVector2(this IPoint point) => new Vector2((float)point.X, (float)point.Y);
    public static IPoint[] ToPoints(this IEnumerable<Vector3> vertices) => vertices.Select(vertex => vertex.ToPoint()).ToArray();
    public static IPoint ToPoint(this Vector3 vertex) => new Point(vertex);
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
    public DelaunatorSharp.Delaunator dl;
    static float TAU = (1 + (float)Math.Sqrt(5)) / 2;
    Vector3 origin = new Vector3(0, 0, 0);
    public List<Point> VertexPoints;
    public List<Vector3> normals;
    public List<Vector2> uvs;
    public List<int> indices;
    public List<Face> faces;
    public int VertexIndex = 0;

    public List<Edge> edges = new List<Edge>();
    public List<Triangle> tris = new List<Triangle>();
    public List<Face> dualFaces = new List<Face>();
    public List<Point> circumcenters = new List<Point>();

    [Export]
    public int subdivide = 1;

    [Export]
    public int size = 5;

    [Export]
    public bool ProjectToSphere = true;

    [Export]
    public ulong Seed = 5001;

    int ahh = 0;

    int triangleIndex = 0;
    bool animationFlag = false;
    float animationWeight = 0;
    public List<MeshInstance3D> animatedPoints = new List<MeshInstance3D>();


    public void PreviousTriangle()
    {
        GD.Print(triangleIndex);
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }
        var tri = tris[triangleIndex];
        var triv3 = tri.Points.ToVectors3();
        if (ProjectToSphere)
        {
            DrawLine(triv3[0].Normalized(), triv3[1].Normalized());
            DrawLine(triv3[1].Normalized(), triv3[2].Normalized());
            DrawLine(triv3[2].Normalized(), triv3[0].Normalized());
        }
        else
        {
            DrawLine(triv3[0], triv3[1]);
            DrawLine(triv3[1], triv3[2]);
            DrawLine(triv3[2], triv3[0]);
        }
        foreach (Point p in tri.Points)
        {
            GD.Print($"{p.Index} at {p.ToVector3()}");
            if (ProjectToSphere)
                DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Black);
            else
                DrawPoint(p.ToVector3(), 0.05f, Colors.Black);
        }
        triangleIndex--;
        if (triangleIndex < 0) triangleIndex = tris.Count - 1;
    }
    public void NextTriangle()
    {
        GD.Print(triangleIndex);
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }
        var tri = tris[triangleIndex];
        var triv3 = tri.Points.ToVectors3();
        if (ProjectToSphere)
        {
            DrawLine(triv3[0].Normalized(), triv3[1].Normalized());
            DrawLine(triv3[1].Normalized(), triv3[2].Normalized());
            DrawLine(triv3[2].Normalized(), triv3[0].Normalized());
        }
        else
        {
            DrawLine(triv3[0], triv3[1]);
            DrawLine(triv3[1], triv3[2]);
            DrawLine(triv3[2], triv3[0]);
        }
        foreach (Point p in tri.Points)
        {
            //GD.Print($"{p.Index} at {p.ToVector3()}");
            if (ProjectToSphere)
                DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Black);
            else
                DrawPoint(p.ToVector3(), 0.05f, Colors.Black);
        }
        triangleIndex = (triangleIndex + 1) % tris.Count;

    }


    public void GenerateAnimatedPoints()
    {
        for (int i = 0; i < circumcenters.Count; i++)
        {
            var v1 = circumcenters[1].ToVector3() - circumcenters[0].ToVector3();
            var v2 = circumcenters[circumcenters.Count - 1].ToVector3() - circumcenters[0].ToVector3();
            var UnitNorm = v1.Cross(v2);
            UnitNorm = UnitNorm.Normalized();
            UnitNorm.X = Mathf.Round(UnitNorm.X);
            UnitNorm.Y = Mathf.Round(UnitNorm.Y);
            UnitNorm.Z = Mathf.Round(UnitNorm.Z);
            var alpha = MathF.Atan2(UnitNorm.X, UnitNorm.Y);
            var ccs = circumcenters.Cast<IPoint>().ToVectors3();
            animatedPoints.Add(DrawPoint(ccs[i].Normalized(), 0.05f, Colors.Snow));
        }
    }
    public void animateMovement()
    {
        for (int i = 0; i < circumcenters.Count; i++)
        {
            var v1 = circumcenters[1].ToVector3() - circumcenters[0].ToVector3();
            var v2 = circumcenters[circumcenters.Count - 1].ToVector3() - circumcenters[0].ToVector3();
            var UnitNorm = v1.Cross(v2);
            UnitNorm = UnitNorm.Normalized();
            //UnitNorm.X = Mathf.Round(UnitNorm.X);
            //UnitNorm.Y = Mathf.Round(UnitNorm.Y);
            //UnitNorm.Z = Mathf.Round(UnitNorm.Z);
            var alpha = MathF.Atan2(UnitNorm.X, UnitNorm.Y);
            var ccs = circumcenters.Cast<IPoint>().ToVectors3();

            var inbetween = new Vector3(
            //                ccs[i].X * Mathf.Cos(alpha) + ccs[i].Y * -Mathf.Sin(alpha),
            //                 ccs[i].X * Mathf.Sin(alpha) + ccs[i].Y * Mathf.Cos(alpha),
            //                ccs[i].Z
            ccs[i].X * (UnitNorm.Y * UnitNorm.Y) / (1 + UnitNorm.Z) + ccs[i].X * UnitNorm.Z + ccs[i].Y * (-UnitNorm.X * UnitNorm.Y) / (1 + UnitNorm.Z) + ccs[i].Z * UnitNorm.X,
            ccs[i].X * (-UnitNorm.X * UnitNorm.Y) / (1 + UnitNorm.Z) + ccs[i].Y * (UnitNorm.X * UnitNorm.X) / (1 + UnitNorm.Z) + ccs[i].Y * UnitNorm.Z + ccs[i].Z * UnitNorm.Y,
            ccs[i].X * -UnitNorm.X + ccs[i].Y * UnitNorm.Y + ccs[i].Z * UnitNorm.Z
                );

            animatedPoints[i].Position = ccs[i].Normalized().Lerp(inbetween.Normalized(), Mathf.Clamp(animationWeight, 0.0f, 1.0f));
            //DrawPoint(ccs[i], 0.05f, Colors.Snow);
        }
    }
    public override void _Process(double delta)
    {
        if (animationFlag)
        {
            animateMovement();
            animationWeight += .01f;
        }
    }
    public override void _Ready()
    {
        RandomNumberGenerator rand = new RandomNumberGenerator();
        DrawPoint(new Vector3(0, 0, 0), 0.1f, Colors.White);
        //Generate the starting dodecahedron
        PopulateArrays();
        //Split the faces n times
        GenerateNonDeformedFaces();
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        foreach (Point point in VertexPoints)
        {
            meshInstances.Add(DrawPoint(point.ToVector3(), 0.05f, new Color(Math.Abs(point.ToVector3().X), Math.Abs(point.ToVector3().Y), Math.Abs(point.ToVector3().Z))));
        }


        //Construct Adjacency Matrices for all triangles and edges given all points
        GenerateTriangleList();
        //RenderTriangleAndConnections(tris[0]);

        //var p = VertexPoints[300];
        List<VoronoiCell> VoronoiPoints = new List<VoronoiCell>();
        GD.Print(VertexPoints.Count);
        var randomTri = tris[rand.RandiRange(0, tris.Count-1)];
        var randomTriPoint = randomTri.Points.ToArray()[rand.RandiRange(0, 2)];
        //var edgesFromTri = edges.Where(e => tri.Points.Any(a => a.Index == e.P.Index || a.Index == e.Q.Index));
        var edgesWithPoint = edges.Where(e => e.Q.Index == randomTri.Points.ToArray()[0].Index && e.P.Index == randomTri.Points.ToArray()[1].Index ||
            e.Q.Index == randomTri.Points.ToArray()[1].Index && e.P.Index == randomTri.Points.ToArray()[0].Index);
        var edgesWithPointList = edgesWithPoint.ToList();
        var trisWithEdge = tris.Where(tri => tri.Points.ToArray()[0].Index == edgesWithPointList[0].P.Index && tri.Points.ToArray()[1].Index == edgesWithPointList[0].Q.Index ||
            tri.Points.ToArray()[0].Index == edgesWithPointList[0].P.Index && tri.Points.ToArray()[2].Index == edgesWithPointList[0].Q.Index ||
            tri.Points.ToArray()[1].Index == edgesWithPointList[0].P.Index && tri.Points.ToArray()[2].Index == edgesWithPointList[0].Q.Index ||
            tri.Points.ToArray()[0].Index == edgesWithPointList[0].Q.Index && tri.Points.ToArray()[1].Index == edgesWithPointList[0].P.Index ||
            tri.Points.ToArray()[0].Index == edgesWithPointList[0].Q.Index && tri.Points.ToArray()[2].Index == edgesWithPointList[0].P.Index ||
            tri.Points.ToArray()[1].Index == edgesWithPointList[0].Q.Index && tri.Points.ToArray()[2].Index == edgesWithPointList[0].P.Index );
        GD.Print(trisWithEdge.ToList().Count);
        RenderTriangleAndConnections(trisWithEdge.ToArray()[0]);
        RenderTriangleAndConnections(trisWithEdge.ToArray()[1]);

        foreach(var edge in edgesWithPoint) {
          GD.Print($"Edge: {edge.Index} from {edge.P.ToVector3()} to {edge.Q.ToVector3()}");
        }
        foreach (Point p in VertexPoints)
        {
            GD.Print($"Index: {p.Index}");
            //Find all triangles that contain the current point
            var triangleOfP = tris.Where(e => e.Points.Any(a => a.Index == p.Index));
            List<Point> triCircumcenters = new List<Point>();
            GD.Print($"Triangles: {triangleOfP.ToList().Count}");
            //Go through all the triangles containing current point p
            //Find the circumcenter of the triangle and add it to the list
            foreach (var tri in triangleOfP)
            {
                var v3 = tri.Points.ToVectors3();
                for (int i = 0; i < tri.Points.ToArray().Length; i++)
                {
                    if (i < 2)
                    {
                        //DrawPoint(v3[i].Normalized(), 0.05f, Colors.Gold);
                    }
                    else
                    {
                        //DrawPoint(v3[i].Normalized(), 0.05f, Colors.Lime);
                    }
                }
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
                    //GD.Print(usedCC.ToList().Count);
                    circumcenters.Add(new Point(cc, circumcenters.Count));
                    triCircumcenters.Add(circumcenters[circumcenters.Count - 1]);
                }
                else
                {
                    circumcenters.Add(new Point(cc, circumcenters.Count));
                    triCircumcenters.Add(circumcenters[circumcenters.Count - 1]);
                }
                //GD.Print(triCircumcenters.Count);
            }
            Face dualFace = new Face(triCircumcenters.ToList());
            dualFaces.Add(dualFace);
            Vector3 center = new Vector3(0, 0, 0);
            for (int i = 0; i < triCircumcenters.Count; i++) center += triCircumcenters[i].ToVector3();
            center /= triCircumcenters.Count;
            center = center.Normalized();

            var centroid = new Vector3(0, 0, 0);
            foreach (Point cc in triCircumcenters) { centroid += cc.ToVector3(); }
            centroid /= triCircumcenters.Count;
            //Translate points by vector (Origin, Circumcenter)
            for (int i = 0; i < triCircumcenters.Count; i++) { triCircumcenters[i] = new Point(triCircumcenters[i].ToVector3() - centroid, triCircumcenters[i].Index); }
            var v1 = triCircumcenters[1].ToVector3() - triCircumcenters[0].ToVector3();
            var v2 = triCircumcenters[triCircumcenters.Count - 1].ToVector3() - triCircumcenters[0].ToVector3();
            var UnitNorm = v1.Cross(v2);
            UnitNorm = UnitNorm.Normalized();

            var alpha = MathF.Atan2(UnitNorm.X, UnitNorm.Y);
            var beta = Mathf.Atan(UnitNorm.Y / Mathf.Sqrt(UnitNorm.X * UnitNorm.X + UnitNorm.Z * UnitNorm.Z));
            var ccs = triCircumcenters.Cast<IPoint>().ToVectors3();
            //GenerateAnimatedPoints();
            VoronoiPoints.Add(TriangulatePoints(UnitNorm, triCircumcenters, circumcenters, VoronoiPoints.Count));
            rand.Seed = Seed;
            //for (int i = 0; i < VoronoiPoints.Count / 3; i++)
            //{
            //    DrawTriangle(circumcenters[VoronoiPoints[3 * i]].ToVector3().Normalized(),
            //        circumcenters[VoronoiPoints[3 * i + 1]].ToVector3().Normalized(),
            //        circumcenters[VoronoiPoints[3 * i + 2]].ToVector3().Normalized(), new Color(rand.RandfRange(0.0f, 100.0f) / 100f, rand.RandfRange(0.0f, 100.0f) / 100f, rand.RandfRange(0.0f, 100.0f) / 100f));
            //}

            //Attempt to construct a matrix to translate and rotate the points onto the World Plane XY
            //Would be able to orient them around the camera that way
            {
                /*for (int i = 0; i < circumcenters.Count; i++)
                {
                    //DrawPoint(ccs[i], 0.05f, Colors.Gold);
                    var inbetween = new Vector3(
                        ccs[i].X * Mathf.Cos(alpha) + ccs[i].Y * -Mathf.Sin(alpha),
                        ccs[i].X * Mathf.Sin(alpha) + ccs[i].Y * Mathf.Cos(alpha),
                        ccs[i].Z
                        );
                    var inbetween2 = new Vector3(
                        inbetween.X * Mathf.Cos(beta) + inbetween.Y * Mathf.Sin(beta),
                        inbetween.X * -Mathf.Sin(beta) + inbetween.Y * Mathf.Cos(beta),
                        inbetween.Z
                        );
                    //DrawPoint(inbetween, 0.05f, Colors.Teal);
                    //DrawPoint(inbetween, 0.05f, Colors.Brown);
                    var tempVec = new Vector3(
                        UnitNorm.X,
                        UnitNorm.Y * Mathf.Cos(alpha) + UnitNorm.Z * -Mathf.Sin(alpha),
                        UnitNorm.Y * Mathf.Sin(alpha) + UnitNorm.Z * Mathf.Cos(alpha)
                        );
                    //DrawPoint(tempVec, 0.05f, Colors.DarkBlue);
                    var tempVec2 = new Vector3(
                        tempVec.X * Mathf.Cos(beta) + tempVec.Y * Mathf.Sin(beta),
                        tempVec.X * -Mathf.Sin(beta) + tempVec.Y * Mathf.Cos(beta),
                        tempVec.Z
                        );
                    //ccs[i].X = ccs[i].X * Mathf.Cos(beta) * Mathf.Cos(alpha) + ccs[i].Y * Mathf.Cos(beta) * Mathf.Sin(alpha) + ccs[i].Z * Mathf.Sin(beta);
                    //ccs[i].Y = ccs[i].X * -Mathf.Sin(alpha) + ccs[i].Y * Mathf.Cos(alpha);
                    //ccs[i].Z = ccs[i].X * -Mathf.Sin(beta) * Mathf.Cos(alpha) + ccs[i].Z * Mathf.Cos(beta);
                    ccs[i].X = ccs[i].X * (UnitNorm.Y * UnitNorm.Y) / (1 + UnitNorm.Z) + ccs[i].X * UnitNorm.Z + ccs[i].Y * (-UnitNorm.X * UnitNorm.Y) / (1 + UnitNorm.Z) + ccs[i].Z * UnitNorm.X;
                    ccs[i].Y = ccs[i].X * (-UnitNorm.X * UnitNorm.Y) / (1 + UnitNorm.Z) + ccs[i].Y * (UnitNorm.X * UnitNorm.X) / (1 + UnitNorm.Z) + ccs[i].Y * UnitNorm.Z + ccs[i].Z * UnitNorm.Y;
                    ccs[i].Z = ccs[i].X * -UnitNorm.X + ccs[i].Y * UnitNorm.Y + ccs[i].Z * UnitNorm.Z;
                    //DrawPoint(ccs[i], 0.05f, Colors.DarkSalmon);
                }*/



                /* Attempt using the rotation matrices for the Perspective Transform
                var rho = Mathf.Sqrt(center.X * center.X + center.Y * center.Y + center.Z * center.Z);
                var theta = Mathf.Atan2(center.Y, center.X);
                var phi = Mathf.Acos(center.Z / rho);
                var ccs = circumcenters.Cast<IPoint>().ToVectors3();
                for(int i = 0; i < circumcenters.Count; i++) {
                  ccs[i].X = ccs[i].X * -Mathf.Sin(theta) + ccs[i].Y * Mathf.Cos(theta);
                  ccs[i].Y = ccs[i].X * Mathf.Cos(phi) * Mathf.Cos(theta) + ccs[i].Y * -Mathf.Cos(phi) * Mathf.Sin(theta) + ccs[i].Z * Mathf.Sin(phi);
                  ccs[i].Z = Mathf.Round(ccs[i].X * -Mathf.Sin(phi) * Mathf.Cos(theta) + ccs[i].Y * -Mathf.Sin(phi) * Mathf.Sin(theta) + ccs[i].Z * Mathf.Cos(phi) + 1.0f * -rho);
                 // ccs[i].X = ccs[i].X * -Mathf.Cos(theta) + ccs[i].Y * -Mathf.Cos(phi) * Mathf.Sin(theta) + ccs[i].Z * Mathf.Sin(phi) * Mathf.Sin(theta);
                 // ccs[i].Y = ccs[i].X * Mathf.Sin(theta) + ccs[i].Y * -Mathf.Cos(phi) * Mathf.Cos(theta) + ccs[i].Z * Mathf.Sin(phi) * Mathf.Cos(theta);
                 // ccs[i].Z = ccs[i].Y * Mathf.Sin(phi) + ccs[i].Z * Mathf.Cos(phi);
                  DrawPoint(ccs[i], 0.05f, Colors.DarkSalmon);
                }
                //
                //DrawFace(Colors.Peru, circumcenters.Cast<IPoint>().ToVectors3());
                //*/

            }
            GD.Print("Done with Point");
        }

        //GenerateSurfaceMesh(VoronoiPoints, circumcenters);

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
        var ccs = TriCircumcenters.Cast<IPoint>().ToVectors3();
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
        while (orderedPoints.Count > 3)
        {
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                //Console.WriteLine($"Triangulating Polygon {i}");
                var a = GetOrderedPoint(orderedPoints, i);
                var b = GetOrderedPoint(orderedPoints, i - 1);
                var c = GetOrderedPoint(orderedPoints, i + 1);

                Vector3 tab = b.ToVector3() - a.ToVector3();
                Vector3 tac = c.ToVector3() - a.ToVector3();
                Vector2 ab = new Vector2(tab.X, tab.Y);
                Vector2 ac = new Vector2(tac.X, tac.Y);

                if (ab.Cross(ac) < 0.0f)
                {
                    //Console.WriteLine("Continue");
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
                    Triangles.Add(new Triangle(Triangles.Count, new List<IPoint>() { TrueCircumcenters[b.Index], TrueCircumcenters[a.Index], TrueCircumcenters[c.Index] }));
                    TriangulatedIndices.Add(TrueCircumcenters[b.Index]);
                    TriangulatedIndices.Add(TrueCircumcenters[a.Index]);
                    TriangulatedIndices.Add(TrueCircumcenters[c.Index]);

                    orderedPoints.RemoveAt(i);
                    break;
                }
            }
        }
        Triangles.Add(new Triangle(Triangles.Count, new List<IPoint>() { TrueCircumcenters[orderedPoints[0].Index], TrueCircumcenters[orderedPoints[1].Index], TrueCircumcenters[orderedPoints[2].Index] }));
        TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[0].Index]);
        TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[1].Index]);
        TriangulatedIndices.Add(TrueCircumcenters[orderedPoints[2].Index]);


        while (orderedPointsReversed.Count > 3)
        {
            for (int i = 0; i < orderedPointsReversed.Count; i++)
            {
                //Console.WriteLine($"Triangulating Polygon {i}");
                var a = GetOrderedPoint(orderedPointsReversed, i);
                var b = GetOrderedPoint(orderedPointsReversed, i - 1);
                var c = GetOrderedPoint(orderedPointsReversed, i + 1);

                Vector3 tab = b.ToVector3() - a.ToVector3();
                Vector3 tac = c.ToVector3() - a.ToVector3();
                Vector2 ab = new Vector2(tab.X, tab.Y);
                Vector2 ac = new Vector2(tac.X, tac.Y);

                if (ab.Cross(ac) > 0.0f)
                {
                    continue;
                }

                bool isEar = true;
                for (int j = 0; j < orderedPointsReversed.Count; j++)
                {
                    if (orderedPointsReversed[j].Index == a.Index || orderedPointsReversed[j].Index == b.Index || orderedPointsReversed[j].Index == c.Index)
                    {
                        continue;
                    }
                    Vector2 p = new Vector2(orderedPointsReversed[j].X, orderedPointsReversed[j].Y);
                    if (IsPointInTriangle(p, new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), new Vector2(c.X, c.Y), true))
                    {
                        isEar = false;
                        break;
                    }
                }
                if (isEar)
                {
                    Triangles.Add(new Triangle(Triangles.Count, new List<IPoint>() {TrueCircumcenters[b.Index],TrueCircumcenters[a.Index],TrueCircumcenters[c.Index]    }));
                    TriangulatedIndices.Add(TrueCircumcenters[b.Index]);
                    TriangulatedIndices.Add(TrueCircumcenters[a.Index]);
                    TriangulatedIndices.Add(TrueCircumcenters[c.Index]);

                    orderedPointsReversed.RemoveAt(i);
                    break;
                }
            }

        }
        Triangles.Add(new Triangle(Triangles.Count, new List<IPoint>() { TrueCircumcenters[orderedPointsReversed[0].Index], TrueCircumcenters[orderedPointsReversed[1].Index], TrueCircumcenters[orderedPointsReversed[2].Index] }));
        TriangulatedIndices.Add(TrueCircumcenters[orderedPointsReversed[0].Index]);
        TriangulatedIndices.Add(TrueCircumcenters[orderedPointsReversed[1].Index]);
        TriangulatedIndices.Add(TrueCircumcenters[orderedPointsReversed[2].Index]);

        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.Cast<IPoint>().ToArray(), Triangles.Cast<ITriangle>().ToArray());
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
        foreach (var p in tri.Points)
        {
            //GD.Print($"{p.ToVector3()}");
        }
        var edgesFromTri = edges.Where(e => tri.Points.Any(a => a.Index == e.P.Index || a.Index == e.Q.Index));
        DrawLine(tri.Points.ToArray()[0].ToVector3(), tri.Points.ToArray()[1].ToVector3());
        DrawLine(tri.Points.ToArray()[1].ToVector3(), tri.Points.ToArray()[2].ToVector3());
        DrawLine(tri.Points.ToArray()[2].ToVector3(), tri.Points.ToArray()[0].ToVector3());
        foreach (Point p in tri.Points)
        {
            GD.Print(p.ToVector3());
            if (ProjectToSphere)
                DrawPoint(p.ToVector3().Normalized(), 0.1f, Colors.Black);
            else
                DrawPoint(p.ToVector3(), 0.1f, Colors.Black);
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

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey)
        {
            if (eventKey.Pressed && eventKey.Keycode == Key.R)
            {
                NextTriangle();
            }
            else if (eventKey.Pressed && eventKey.Keycode == Key.T)
            {
                PreviousTriangle();
            }
            else if (eventKey.Pressed && eventKey.Keycode == Key.P)
            {
                animationFlag = true;
                animationWeight = 0.0f;
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

    public void GenerateTriangleList()
    {
        foreach (Face f in faces)
        {
            Edge[] triEdges = new Edge[3];
            for (int i = 0, j = f.v.Length - 1; i < f.v.Length; j = i++)
            {
                triEdges[i] = new Edge(edges.Count, f.v[j], f.v[i]);
                edges.Add(triEdges[i]);
            }
            tris.Add(new Triangle(tris.Count, f.v.Cast<IPoint>().ToList(), triEdges.Cast<IEdge>().ToList()));
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

    public void GenerateSurfaceMesh(List<VoronoiCell> VoronoiList, List<Point> circumcenters)
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
            //st.SetColor(new Color((float)vor.Index / (float)VoronoiList.Count,(float)vor.Index / (float)VoronoiList.Count ,(float)vor.Index / (float)VoronoiList.Count));
            st.SetColor(new Color(randy.Randf(), randy.Randf(), randy.Randf()));
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
                    st.SetNormal(tangent);
                    if (ProjectToSphere)
                        st.AddVertex(vor.Points[3 * i + j].ToVector3().Normalized() * size);
                    else
                        st.AddVertex(vor.Points[3 * i + j].ToVector3() * size);
                }
            }
        }
        st.Commit(arrMesh);
    }

    public Face[] Subdivide(Face face)
    {
        List<Face> subfaces = new List<Face>();
        var subVector1 = GetMiddle(face.v[0], face.v[1]);
        var subVector2 = GetMiddle(face.v[1], face.v[2]);
        var subVector3 = GetMiddle(face.v[2], face.v[0]);
        VertexPoints.Add(subVector1);
        VertexPoints.Add(subVector2);
        VertexPoints.Add(subVector3);

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

        Point middlePoint = new Point(tempVector, VertexIndex);
        foreach (Point p in VertexPoints)
        {
            if (Mathf.Equals(p.ToVector3(), tempVector))
            {
                return p;
            }
        }

        VertexIndex++;

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

