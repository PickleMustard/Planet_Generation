using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class Extension
{
    public static Vector3[] ToVectors3(this IEnumerable<IPoint> points) => points.Select(point => point.ToVector3()).ToArray();
    public static Vector3 ToVector3(this IPoint point) => new Vector3((float)point.X, (float)point.Y, (float)point.Z);
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

    [Export]
    public int subdivide = 1;

    [Export]
    public int size = 5;

    [Export]
    public bool ProjectToSphere = true;

    int triangleIndex = 0;


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
            GD.Print($"{p.Index} at {p.ToVector3()}");
            if (ProjectToSphere)
                DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Black);
            else
                DrawPoint(p.ToVector3(), 0.05f, Colors.Black);
        }
        triangleIndex = (triangleIndex + 1) % tris.Count;

    }
    public override void _Ready()
    {
        DrawPoint(new Vector3(0, 0, 0), 0.1f, Colors.White);
        PopulateArrays();
        GD.Print(faces.Count);
        GenerateNonDeformedFaces();
        GD.Print(faces.Count);
        List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
        foreach (Point point in VertexPoints)
        {
            meshInstances.Add(DrawPoint(point.ToVector3(), 0.05f, new Color(Math.Abs(point.ToVector3().X), Math.Abs(point.ToVector3().Y), Math.Abs(point.ToVector3().Z))));
        }


        GenerateTriangleList();

        var p = VertexPoints[4];
        //foreach (Point p in VertexPoints)
        //{
        var triangleOfP = tris.Where(e => e.Points.Any(a => a.Index == p.Index));
        List<Point> circumcenters = new List<Point>();
        foreach (var tri in triangleOfP)
        {
            var v3 = tri.Points.ToVectors3();
            /*for(int i = 0; i < tri.Points.ToArray().Length; i++)
            {
              GD.Print(i);
              if(i < 2){
                DrawPoint(v3[i].Normalized(), 0.05f, Colors.Gold);
              } else {
                DrawPoint(v3[i].Normalized(), 0.05f, Colors.Lime);
              }
            }*/
            var ac = v3[2] - v3[0];
            var ab = v3[1] - v3[0];
            var abXac = ab.Cross(ac);
            var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
            float circumsphereRadius = vToCircumsphereCenter.Length();
            var cc = v3[0] + vToCircumsphereCenter;
            if (circumcenters.Any(cir => Mathf.Equals(cir.ToVector3(), cc))) continue;
            circumcenters.Add(new Point(cc, circumcenters.Count));
        }
        Face dualFace = new Face(circumcenters.ToList());
        dualFaces.Add(dualFace);
        Vector3 center = new Vector3(0, 0, 0);
        DrawLine(center, new Vector3(1,0,0), Colors.Red);
        DrawLine(center, new Vector3(0,1,0), Colors.Green);
        DrawLine(center, new Vector3(0,0,1), Colors.Blue);
        for (int i = 0; i < circumcenters.Count; i++)
        {
            center += circumcenters[i].ToVector3();
            if (i < 2 || i == circumcenters.Count -1)
            {
                DrawPoint(circumcenters[i].ToVector3().Normalized(), 0.05f, Colors.Gold);
            }
            else
            {
                DrawPoint(circumcenters[i].ToVector3().Normalized(), 0.05f, Colors.Lime);
            }
        }
        center /= circumcenters.Count;
        center = center.Normalized();
        DrawPoint(center, 0.05f, Colors.MediumSlateBlue);

        //Attempt using Rodrigues' Rotation Formula
        //Calculate Centroid
        var centroid = new Vector3(0, 0, 0);
        foreach (Point cc in circumcenters) { centroid += cc.ToVector3(); }
        centroid /= circumcenters.Count;
        //Translate points by vector (Origin, Circumcenter)
        for (int i = 0; i < circumcenters.Count; i++) { circumcenters[i] = new Point(circumcenters[i].ToVector3() - centroid, circumcenters[i].Index); }
        var v1 = circumcenters[1].ToVector3() - circumcenters[0].ToVector3();
        var v2 = circumcenters[circumcenters.Count-1].ToVector3() - circumcenters[0].ToVector3();
        var UnitNorm = v1.Cross(v2);
        UnitNorm = UnitNorm.Normalized();
        UnitNorm.X = Mathf.Round(UnitNorm.X);
        UnitNorm.Y = Mathf.Round(UnitNorm.Y);
        UnitNorm.Z = Mathf.Round(UnitNorm.Z);

        DrawPoint(new Vector3(UnitNorm.X, 0,0), 0.05f, Colors.Red);
        DrawPoint(new Vector3(0, UnitNorm.Y, 0), 0.05f, Colors.Green);
        DrawPoint(new Vector3(0,0, UnitNorm.Z), 0.05f, Colors.Blue);
        DrawLine(new Vector3(0,0,0), UnitNorm);
        var alpha = MathF.Atan(UnitNorm.Y / UnitNorm.Z);
        GD.Print(alpha);
        //var alpha = Mathf.Acos(UnitNorm.Dot(Vector3.Right) / (UnitNorm.Length() * Vector3.Right.Length()));
        //var beta = Mathf.Acos(UnitNorm.Dot(Vector3.Back) / (UnitNorm.Length() * Vector3.Back.Length()));
        var beta = Mathf.Atan(-UnitNorm.X / MathF.Sqrt(UnitNorm.Y * UnitNorm.Y + UnitNorm.Z * UnitNorm.Z));
        var ccs = circumcenters.Cast<IPoint>().ToVectors3();
        for (int i = 0; i < circumcenters.Count; i++)
        {
            DrawPoint(ccs[i], 0.05f, Colors.Gold);
            var inbetween = new Vector3(
                ccs[i].X,
                ccs[i].Y * Mathf.Cos(alpha) + ccs[i].Z * -Mathf.Sin(alpha),
                ccs[i].Y * Mathf.Sin(alpha) + ccs[i].Z * Mathf.Cos(alpha)
                );
            DrawPoint(inbetween, 0.05f, Colors.Teal);
            var tempVec = new Vector3(
                UnitNorm.X,
                UnitNorm.Y * Mathf.Cos(alpha) + UnitNorm.Z * -Mathf.Sin(alpha),
                UnitNorm.Y * Mathf.Sin(alpha) + UnitNorm.Z * Mathf.Cos(alpha)
                );
            DrawPoint(tempVec, 0.05f, Colors.DarkBlue);
            var tempVec2 = new Vector3(
                tempVec.X * Mathf.Cos(beta) + tempVec.Z * Mathf.Sin(beta),
                tempVec.Y,
                tempVec.X * -Mathf.Sin(beta) + tempVec.Z * Mathf.Cos(beta)
                );
            DrawPoint(tempVec2, 0.05f, Colors.DarkGreen);
            DrawLine(new Vector3(0,0,0), tempVec);
            DrawLine(new Vector3(0,0,0), tempVec2);
            ccs[i].X = ccs[i].X * Mathf.Cos(beta) * Mathf.Cos(alpha) + ccs[i].Y * Mathf.Cos(beta) * Mathf.Sin(alpha) + ccs[i].Z * Mathf.Sin(beta);
            ccs[i].Y = ccs[i].X * -Mathf.Sin(alpha) + ccs[i].Y * Mathf.Cos(alpha);
            ccs[i].Z = ccs[i].X * -Mathf.Sin(beta) * Mathf.Cos(alpha) + ccs[i].Z * Mathf.Cos(beta);
            DrawPoint(ccs[i], 0.05f, Colors.DarkSalmon);
        }



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
        */
        //}

        GenerateSurfaceMesh();

    }

    public void RenderTriangleAndConnections()
    {
        var tri = tris[0];
        foreach (var p in tri.Points)
        {
            //GD.Print($"{p.ToVector3()}");
        }
        var edgesFromTri = edges.Where(e => tri.Points.Any(a => a.Index == e.P.Index || a.Index == e.Q.Index));
        foreach (Point p in tri.Points)
        {
            if (ProjectToSphere)
                DrawPoint(p.ToVector3().Normalized(), 0.05f, Colors.Black);
            else
                DrawPoint(p.ToVector3(), 0.05f, Colors.Black);
        }
        foreach (var edge in edgesFromTri)
        {
            //GD.Print($"Edge: {edge.Index} from {edge.P.ToVector3()} to {edge.Q.ToVector3()}");
            //DrawLine(edge.P.ToVector3().Normalized(), edge.Q.ToVector3().Normalized());
        }


        //foreach(Triangle tri in tris) {

        //}
        foreach (Edge edge in edges)
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

        }
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
            tris.Add(new Triangle(tris.Count, f.v.Cast<IPoint>()));
            for (int i = 0, j = f.v.Length - 1; i < f.v.Length; j = i++)
            {
                //GD.Print($"{i} | {j}");
                edges.Add(new Edge(edges.Count, f.v[j], f.v[i]));
                // if (!finalPoints.Contains(f.v[i].ToVector3()))
                // {
                //     bool shouldAdd = true;
                //     foreach (var v in finalPoints)
                //     {
                //         if (Mathf.Abs((v - f.v[i].ToVector3()).Length()) <= Mathf.Pow(2, -52)) shouldAdd = false;
                //     }
                //     var ver = f.v[i].ToVector3();
                //     ver = ConvertToSpherical(ver.Normalized());

                //     if (shouldAdd) finalPoints.Add(ver);
                // }
            }
        }
        /*
        Vector3 normal = (f.v[1].ToVector3() - f.v[0].ToVector3()).Cross(f.v[2].ToVector3() - f.v[0].ToVector3()).Normalized();
        //DrawPoint(normal, 0.05f, Colors.Plum);
        if (!circumcenters.Contains(normal))
        {
            bool shouldAdd = true;
            foreach (var cc in circumcenters)
            {
                GD.Print((cc - normal).Length());
                GD.Print(Mathf.Abs((cc - normal).Length()) <= Mathf.Pow(2, -52));
                if (Mathf.Abs((cc - normal).Length()) <= Mathf.Pow(2, -52)) shouldAdd = false;
            }
            if (shouldAdd)
            {
                circumcenters.Add(normal);
                Vector3 sphericalPosition = ConvertToSpherical(normal);
                sphericalPoints.Add(sphericalPosition);
            }
        }
    }
    sphericalPoints.Sort(delegate (Vector3 x, Vector3 y)
    {
        if (x.Z > y.Z) return 1;
        else if (x.Z == y.Z) return 0;
        else return -1;
    });
    foreach (Vector3 v in sphericalPoints)
    {
        DrawPoint(ConvertToCartesian(v), 0.05f, Colors.Plum);
    }
    //DrawLine(ConvertToCartesian(sphericalPoints[0]), ConvertToCartesian(sphericalPoints[1]), Colors.Red);

    /*Delaunator3D dl = new Delaunator3D(sphericalPoints.ToPoints());
    dl.ForEachTriangle(tri =>
        {
            Vector3[] ps = tri.Points.ToVectors3();
            DrawLine(ps[0], ps[1], Colors.Red);
            DrawLine(ps[0], ps[2], Colors.Red);
            DrawLine(ps[2], ps[1], Colors.Red);
        });
    dl.ForEachVoronoiEdge(edge =>
            {
                DrawPoint(edge.P.ToVector3(), 0.03f, Colors.SeaGreen);
                DrawLine(edge.P.ToVector3(), edge.Q.ToVector3(), Colors.Gold);
            });*/

    }

    public void ComputerVoronoiCells()
    {
        foreach (Face f in faces)
        {

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

    public void GenerateSurfaceMesh()
    {
        var arrMesh = Mesh as ArrayMesh;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        GD.Print(faces.Count);
        foreach (Face face in faces)
        {
            var centroid = Vector3.Zero;
            centroid += face.v[0].ToVector3();
            centroid += face.v[1].ToVector3();
            centroid += face.v[2].ToVector3();
            centroid /= 3.0f;

            var normal = (face.v[1].ToVector3() - face.v[0].ToVector3()).Cross(face.v[2].ToVector3() - face.v[0].ToVector3()).Normalized();
            var tangent = (face.v[0].ToVector3() - centroid).Normalized();
            var bitangent = normal.Cross(tangent).Normalized();
            var min_u = Mathf.Inf;
            var min_v = Mathf.Inf;
            var max_u = -Mathf.Inf;
            var max_v = -Mathf.Inf;
            for (int j = 0; j < 3; j++)
            {
                var rel_pos = face.v[j].ToVector3() - centroid;
                var u = rel_pos.Dot(tangent);
                var v = rel_pos.Dot(bitangent);
                min_u = Mathf.Min(min_u, u);
                min_v = Mathf.Min(min_v, v);
                max_u = Mathf.Max(max_u, u);
                max_v = Mathf.Max(max_v, v);

                var uv = new Vector2((u - min_u) / (max_u - min_u), (v - min_v) / (max_v - min_v));
                st.SetUV(uv);
                if (ProjectToSphere)
                    st.AddVertex(face.v[j].ToVector3().Normalized() * size);
                else
                    st.AddVertex(face.v[j].ToVector3() * size);
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
        var material = ResourceLoader.Load("res://face_shader.tres") as ShaderMaterial;
        material.Set("base_color", new Color(Math.Abs(pos1.X), Math.Abs(pos2.Y), Math.Abs(pos3.Z)));
        material.Set("border_thickness", 0.01);

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

