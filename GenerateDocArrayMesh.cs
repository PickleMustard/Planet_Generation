using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using DelaunatorSharp;

public static class Extension
{
  public static Vector3[] ToVectors3(this IEnumerable<IPoint> points) => points.Select(point => point.ToVector3()).ToArray();
  public static Vector3 ToVector3(this IPoint point) => new Vector3((float)point.X, (float)point.Y, 0.0f);
}

public partial class GenerateDocArrayMesh : MeshInstance3D
{


  public struct Face
  {
    public Vector3[] v;

    public Face(Vector3 v0, Vector3 v1, Vector3 v2)
    {
      v = new Vector3[] { v0, v1, v2 };
    }
  }
  public DelaunatorSharp.Delaunator dl;
  static float TAU = (1 + (float)Math.Sqrt(5)) / 2;
  Vector3 origin = new Vector3(0, 0, 0);
  public List<Vector3> cartesionPoints;
  public List<Vector3> normals;
  public List<Vector2> uvs;
  public List<int> indices;
  public List<Face> faces;

  [Export]
  public int subdivide = 1;

  [Export]
  public int size = 5;

  [Export]
  public bool ProjectToSphere = true;

  public override void _Ready()
  {
    PopulateArrays();
    GenerateNonDeformedFaces();
    //Project3DTo2D();
    List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
    foreach (Vector3 point in cartesionPoints)
    {
      meshInstances.Add(DrawPoint(point, 0.05f, new Color(Math.Abs(point.X), Math.Abs(point.Y), Math.Abs(point.Z))));
    }

    //DrawVoronoi();
    GenerateSurfaceMesh();

  }

  public void DrawVoronoi()
  {
    var arrMesh = Mesh as ArrayMesh;
    //var st = new SurfaceTool();
    //st.Begin(Mesh.PrimitiveType.Triangles);
    //Godot.Collections.Array surfaceArray = new Godot.Collections.Array();
    var newPoints = new List<Vector3>();
    //surfaceArray.Resize((int)Mesh.ArrayType.Max);
    var relaxedPoints = dl.GetRellaxedPoints();
    GD.Print(relaxedPoints.Length / 3.0f);
    for (int i = 0; i < relaxedPoints.Length - 3; i += 3)
    {
      newPoints.Add(relaxedPoints[i + 2].ToVector3());
      newPoints.Add(relaxedPoints[i + 1].ToVector3());
      newPoints.Add(relaxedPoints[i].ToVector3());
    }
    var newnewPoints = new List<Vector3>();
    foreach (var point in dl.Points)
    {
      var tempVector = new Vector3((2.0f * (float)point.X) / (1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)),
            (2.0f * (float)point.Y) / (1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)),
            (-1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)) / 1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f));

      //tempVector = tempVector.Normalized();
      //tempVector = tempVector.Inverse();
      //st.AddVertex(new Vector3((2.0f * (float)point.X) / (1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)),
      //(2.0f * (float)point.Y) / (1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)),
      //(-1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)) / 1.0f + Mathf.Pow((float)point.X, 2.0f) + Mathf.Pow((float)point.Y, 2.0f)).Normalized());
      //newPoints.Add(point.ToVector3().Normalized());
      //newnewPoints.Add(tempVector);
      //st.AddVertex(tempVector);
    }
    var tempFaces = new List<Face>();
    for (int i = 0; i < dl.Triangles.Length / 3; i++)
    {
      var tempVectorV1 = new Vector3((2.0f * (float)dl.Points[dl.Triangles[3 * i]].X) / (1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].Y, 2.0f)),
            (2.0f * (float)dl.Points[dl.Triangles[3 * i]].Y) / (1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].Y, 2.0f)),
            (-1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].Y, 2.0f)) /
            1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i]].Y, 2.0f));
      var tempVectorV2 = new Vector3((2.0f * (float)dl.Points[dl.Triangles[3 * i + 1]].X) / (1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].Y, 2.0f)),
            (2.0f * (float)dl.Points[dl.Triangles[3 * i + 1]].Y) / (1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].Y, 2.0f)),
            (-1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].Y, 2.0f)) /
            1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 1]].Y, 2.0f));
      var tempVectorV3 = new Vector3((2.0f * (float)dl.Points[dl.Triangles[3 * i + 2]].X) / (1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].Y, 2.0f)),
            (2.0f * (float)dl.Points[dl.Triangles[3 * i + 2]].Y) / (1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].Y, 2.0f)),
            (-1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].Y, 2.0f)) /
            1.0f + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].X, 2.0f) + Mathf.Pow((float)dl.Points[dl.Triangles[3 * i + 2]].Y, 2.0f));

      // var mu1 = 2.0f * Mathf.Atan(1.0f / (float)dl.Points[dl.Triangles[3 * i + 2]].X);
      // float theta1 = (float)dl.Points[dl.Triangles[3 * i + 2]].Y;
      // var tempVectorV1 = new Vector3(Mathf.Sin(theta1) * Mathf.Cos(mu1), Mathf.Sin(theta1) * Mathf.Sin(mu1), Mathf.Cos(theta1));

      // var mu2 = 2.0f * Mathf.Atan(1.0f / (float)dl.Points[dl.Triangles[3 * i + 1]].X);
      // float theta2 = (float)dl.Points[dl.Triangles[3 * i + 1]].Y;
      // var tempVectorV2 = new Vector3(Mathf.Sin(theta2) * Mathf.Cos(mu2), Mathf.Sin(theta2) * Mathf.Sin(mu2), Mathf.Cos(theta2));

      // var mu3 = 2.0f * Mathf.Atan(1.0f / (float)dl.Points[dl.Triangles[3 * i]].X);
      // float theta3 = (float)dl.Points[dl.Triangles[3 * i]].Y;
      // var tempVectorV3 = new Vector3(Mathf.Sin(theta3) * Mathf.Cos(mu3), Mathf.Sin(theta3) * Mathf.Sin(mu3), Mathf.Cos(theta3));
      tempFaces.Add(new Face(tempVectorV3, tempVectorV2, tempVectorV1));
    }
    faces = new List<Face>(tempFaces);
    //surfaceArray[(int)Mesh.ArrayType.Vertex] = newnewPoints.ToArray();
    //surfaceArray[(int)Mesh.ArrayType.Index] = dl.Triangles.ToArray();
    //if (arrMesh != null) {
    //arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
    //}
    dl.ForEachVoronoiEdge(edge =>
        {
          DrawLine(edge.P.ToVector3(), edge.Q.ToVector3(), Colors.Gold);
          //st.AddVertex(edge.P.ToVector3());
          //st.AddVertex(edge.Q.ToVector3());
        });
    dl.ForEachVoronoiCellBasedOnCentroids(cell =>
        {
          var edges = dl.GetEdgesOfTriangle(Mathf.FloorToInt(cell.Index / 3));
          foreach (var edge in edges)
          {
            DrawLine(edge.P.ToVector3(), edge.Q.ToVector3(), Colors.Plum);
          }
          //        var seen = new HashSet<int>();
          //        for(int i = 0; i < dl.Triangles.Length; i++) {
          //        var p = dl.Triangles[Delaunator.NextHalfedge(i)];
          //        if(!seen.Contains(p)) {
          //        seen.Add(p);
          //        var edges = dl.EdgesAroundPoint(i);
          //        var triangles = edges.Select(edge => Delaunator.EdgesOfTriangle(edge)).ToArray();
          //        var vertices = triangles.Select(t => )
          //        }
          //        }
        });

    //st.Commit();
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

  public void Project3DTo2D()
  {
    List<IPoint> points = new List<IPoint>();
    foreach (Face face in faces)
    {
      foreach (Vector3 vertex in face.v)
      {
        var vertexNormal = vertex.Normalized();
        //var theta = Mathf.Acos(vertex.Z / Mathf.Sqrt(vertex.X * vertex.X + vertex.Y * vertex.Y + vertex.Z * vertex.Z));
        //var mu = Mathf.Sign(vertex.Y) * Mathf.Acos(vertex.X / Mathf.Sqrt(vertex.X * vertex.X + vertex.Y * vertex.Y));
        DelaunatorSharp.Point point = new DelaunatorSharp.Point(vertexNormal.X / (1.0f - vertexNormal.Z), vertexNormal.Y / (1.0f - vertexNormal.Z));

        //Point point = new Point(Mathf.Sin(mu) / (1 - Mathf.Cos(mu)), theta);
        if (!Double.IsNaN(point.X) && !Double.IsNaN(point.Y))
          points.Add(point);
      }
    }
    GD.Print(points.Count);
    IPoint[] pointArray = new IPoint[points.Count];
    points.CopyTo(pointArray);
    GD.Print(pointArray.Length);
    dl = new Delaunator(pointArray);
    GD.Print(dl.Triangles);
    for (int i = 0; i < dl.Triangles.Length; i++)
    {
      if (i > dl.Halfedges[i])
      {
        var p = points[dl.Triangles[i]].ToVector3();
        var q = points[dl.Triangles[Delaunator.NextHalfedge(i)]].ToVector3();
        //DrawLine(p, q, Colors.Lime);
      }
    }
  }

  public void GenerateSurfaceMesh()
  {
    var arrMesh = Mesh as ArrayMesh;
    var st = new SurfaceTool();
    st.Begin(Mesh.PrimitiveType.Triangles);
    foreach (Face face in faces)
    {
      var centroid = Vector3.Zero;
      centroid += face.v[0];
      centroid += face.v[1];
      centroid += face.v[2];
      centroid /= 3.0f;

      var normal = (face.v[1] - face.v[0]).Cross(face.v[2] - face.v[0]).Normalized();
      var tangent = (face.v[0] - centroid).Normalized();
      var bitangent = normal.Cross(tangent).Normalized();
      var min_u = Mathf.Inf;
      var min_v = Mathf.Inf;
      var max_u = -Mathf.Inf;
      var max_v = -Mathf.Inf;
      for (int j = 0; j < 3; j++)
      {
        var rel_pos = face.v[j] - centroid;
        var u = rel_pos.Dot(tangent);
        var v = rel_pos.Dot(bitangent);
        min_u = Mathf.Min(min_u, u);
        min_v = Mathf.Min(min_v, v);
        max_u = Mathf.Max(max_u, u);
        max_v = Mathf.Max(max_v, v);

        var uv = new Vector2((u - min_u) / (max_u - min_u), (v - min_v) / (max_v - min_v));
        st.SetUV(uv);
        if (ProjectToSphere)
          st.AddVertex(face.v[j].Normalized() * size);
        else
          st.AddVertex(face.v[j] * size);
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

    subfaces.Add(new Face(face.v[0], subVector1, subVector3));
    subfaces.Add(new Face(subVector1, face.v[1], subVector2));
    subfaces.Add(new Face(subVector2, face.v[2], subVector3));
    subfaces.Add(new Face(subVector3, subVector1, subVector2));

    return subfaces.ToArray();
  }

  public Vector3 GetMiddle(Vector3 v1, Vector3 v2)
  {
    //var tempVector = (v1 + v2) / 2.0f;
    var tempVector = (v2 - v1) * 0.5f + v1;
    tempVector.Normalized();

    return tempVector;

  }

  public void PopulateArrays()
  {
    cartesionPoints = new List<Vector3>{
                               new Vector3(0, 1, TAU), new Vector3(0, -1, TAU), new Vector3(0, -1, -TAU), new Vector3(0, 1, -TAU),
                               new Vector3(1, TAU, 0), new Vector3(-1, TAU, 0), new Vector3(-1, -TAU, 0), new Vector3(1, -TAU, 0),
                               new Vector3(TAU, 0, 1), new Vector3(TAU, 0, -1), new Vector3(-TAU, 0, -1), new Vector3(-TAU, 0, 1)};
    // for(int i = 0; i < cartesionPoints.Count; i++) {
    //   cartesionPoints[i] = cartesionPoints[i].Normalized();
    // }
    normals = new List<Vector3>();
    uvs = new List<Vector2>();
    foreach (Vector3 point in cartesionPoints)
    {
      normals.Add(point);
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
      faces.Add(new Face(cartesionPoints[indices[i]], cartesionPoints[indices[i + 1]], cartesionPoints[indices[i + 2]]));
    }

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
    immediateMesh.SurfaceAddVertex(pos1);
    immediateMesh.SurfaceAddVertex(pos2);
    immediateMesh.SurfaceAddVertex(pos3);
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
    immediateMesh.SurfaceAddVertex(pos1);
    immediateMesh.SurfaceAddVertex(pos2);
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

