using Godot;
using System;
using System.Collections.Generic;
using DelaunatorSharp;

public partial class DelauneyTriangulator2D : MeshInstance3D
{

    Random rand;
    public List<Point> points = new List<Point>();
    [Export]
    public int NumPoints = 50;
    [Export]
    public int Seed = 5001;
    [Export]
    public bool DisplayTriangles = false;
    public override void _Ready()
    {
        rand = new Random(Seed);
        GeneratePoints();
        foreach (Point p in points)
        {
            DrawPoint(p.ToVector3(), 0.03f, Colors.Pink);
        }
        Delaunator3D dl = new Delaunator3D(points.ToArray());
        //Delaunator dl = new Delaunator(points.ToArray());
        if (DisplayTriangles)
        {
            dl.ForEachTriangle(tri =>
            {
                Vector3[] ps = tri.Points.ToVectors3();
                DrawLine(ps[0], ps[1], Colors.Red);
                DrawLine(ps[0], ps[2], Colors.Red);
                DrawLine(ps[2], ps[1], Colors.Red);
            });
        }
        dl.ForEachVoronoiEdge(edge =>
        {
            DrawPoint(edge.P.ToVector3(), 0.03f, Colors.SeaGreen);
            DrawLine(edge.P.ToVector3(), edge.Q.ToVector3(), Colors.Gold);
        });
    }


    public void GeneratePoints()
    {
        for (int i = 0; i < NumPoints; i++)
        {
            Point newPoint = new Point(rand.NextSingle() * 10, rand.NextSingle() * 10, 0.0f);
            points.Add(newPoint);
        }
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
