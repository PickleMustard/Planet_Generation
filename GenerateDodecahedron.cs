using Godot;
using System;
using System.Collections.Generic;

public partial class GenerateDodecahedron : Node3D
{
  static float TAU = (1 + (float)Math.Sqrt(5)) / 2;
  Vector3 origin = new Vector3(0, 0, 0);
  public List<Vector3> cartesionPoints;
  List<Vector3> normals;
  public List<int> indices;
  public Vector3[,] faces;

  public override void _Ready()
  {
    /*PopulateArrays();
    List<MeshInstance3D> meshInstances = new List<MeshInstance3D>();
    foreach (Vector3 point in cartesionPoints)
    {
      meshInstances.Add(DrawPoint(point, 0.05f, new Color(Math.Abs(point.X), Math.Abs(point.Y), Math.Abs(point.Z))));
    }

    for (int i = 0; i < faces.GetLength(0); i++)
    {
      GD.Print(faces[i, 0], " | ", faces[i, 1], " | ", faces[i, 2]);
      meshInstances.Add(DrawTriangle(faces[i, 0], faces[i, 1], faces[i, 2]));
    }
*/
  }

  public void GenerateSurfaceMesh()
  {
    //var arrMesh = Mesh as ArrayMesh;
    //var st = new SurfaceTool();
    //Godot.Collections.Array surfaceArray = new Godot.Collections.Array();
    //surfaceArray.Resize((int)Mesh.ArrayType.Max);

    //surfaceArray[(int)Mesh.ArrayType.Vertex] = cartesionPoints.ToArray();
    //surfaceArray[(int)Mesh.ArrayType.Normal] = normals.ToArray();
    //surfaceArray[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
    //surfaceArray[(int)Mesh.ArrayType.Index] = indices.ToArray();

    //var arrMesh = Mesh as ArrayMesh;
    //if (arrMesh != null)
    //{
      //arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray);
    //}

    //arrMesh.RegenNormalMaps();
    //arrMesh.LightmapUnwrap();

    /*MeshDataTool mdt = new MeshDataTool();
    mdt.CreateFromSurface(arrMesh, 0);
    for(int i = 0; i < mdt.GetFaceCount(); i++) {
      var a = mdt.GetFaceVertex(i, 0);
      var b = mdt.GetFaceVertex(i, 1);
      var c = mdt.GetFaceVertex(i, 2);

      var ap = mdt.GetVertex(a);
      var bp = mdt.GetVertex(b);
      var cp = mdt.GetVertex(c);

      var n = (bp - cp).Cross(ap-bp).Normalized();

      mdt.SetVertexNormal(a, n + mdt.GetVertexNormal(a));
      mdt.SetVertexNormal(b, n + mdt.GetVertexNormal(b));
      mdt.SetVertexNormal(c, n + mdt.GetVertexNormal(c));
      //List<Vector2> uvs = CalculateUV(ap, bp, cp);
      //mdt.SetVertexUV(a, uvs[0]);
      //mdt.SetVertexUV(b, uvs[1]);
      //mdt.SetVertexUV(c, uvs[2]);
    }

    for (int i = 0;i < mdt.GetVertexCount(); i++) {
      var v = mdt.GetVertexNormal(i).Normalized();
      mdt.SetVertexNormal(i, v);
      mdt.SetVertexColor(i, new Color(100, v.Y, v.Z));
    }

    arrMesh.ClearSurfaces();
    mdt.CommitToSurface(arrMesh);*/
  }

  public void PopulateArrays()
  {
    cartesionPoints = new List<Vector3>{
                               new Vector3(0, 1, TAU), new Vector3(0, -1, TAU), new Vector3(0, -1, -TAU), new Vector3(0, 1, -TAU),
                               new Vector3(1, TAU, 0), new Vector3(-1, TAU, 0), new Vector3(-1, -TAU, 0), new Vector3(1, -TAU, 0),
                               new Vector3(TAU, 0, 1), new Vector3(TAU, 0, -1), new Vector3(-TAU, 0, -1), new Vector3(-TAU, 0, 1)};
    normals = new List<Vector3>();
    foreach (Vector3 point in cartesionPoints)
    {
      normals.Add(point.Normalized());
    }
    indices = new List<int> { 0, 5, 4, 0, 11, 5 };
    faces = new Vector3[,] {
      { cartesionPoints[0], cartesionPoints[5], cartesionPoints[4] },
      { cartesionPoints[0], cartesionPoints[11], cartesionPoints[5]},
      { cartesionPoints[0], cartesionPoints[4], cartesionPoints[8]},
      { cartesionPoints[0], cartesionPoints[8], cartesionPoints[1]},
      { cartesionPoints[0], cartesionPoints[1], cartesionPoints[11]},
      { cartesionPoints[3], cartesionPoints[4], cartesionPoints[5] },
      { cartesionPoints[3], cartesionPoints[5], cartesionPoints[10]},
      { cartesionPoints[3], cartesionPoints[9], cartesionPoints[4]},
      { cartesionPoints[3], cartesionPoints[10], cartesionPoints[2]},
      { cartesionPoints[3], cartesionPoints[2], cartesionPoints[9]},
      { cartesionPoints[10], cartesionPoints[5], cartesionPoints[11]},
      { cartesionPoints[10], cartesionPoints[11], cartesionPoints[6]},
      { cartesionPoints[8], cartesionPoints[4], cartesionPoints[9]},
      { cartesionPoints[8], cartesionPoints[9], cartesionPoints[7]},
      { cartesionPoints[1], cartesionPoints[7], cartesionPoints[6]},
      { cartesionPoints[1], cartesionPoints[6], cartesionPoints[11]},
      { cartesionPoints[1], cartesionPoints[8], cartesionPoints[7],},
      { cartesionPoints[2], cartesionPoints[10], cartesionPoints[6]},
      { cartesionPoints[2], cartesionPoints[7], cartesionPoints[9]},
      { cartesionPoints[2], cartesionPoints[6], cartesionPoints[7]}, };

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
    meshInstance.Position = pos;

    sphereMesh.Radius = radius;
    sphereMesh.Height = radius * 2f;
    sphereMesh.Material = material;

    material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
    material.AlbedoColor = color ?? Colors.WhiteSmoke;

    this.AddChild(meshInstance);

    return meshInstance;
  }
}

