using Godot;
using System;
public static class PolygonRendererSDL
{
    public static MeshInstance3D DrawFace(Node parent, float size, Color? color = null, params Vector3[] vertices)
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
        //parent.AddChild(meshInstance);
        parent.CallDeferredThreadGroup("add_child", meshInstance);

        return meshInstance;
    }

    public static MeshInstance3D DrawTriangle(Node parent, float size, Vector3 pos1, Vector3 pos2, Vector3 pos3, Color? color = null)
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
        //parent.AddChild(meshInstance);
        parent.CallDeferredThreadGroup("add_child", meshInstance);

        return meshInstance;
    }

    public static MeshInstance3D DrawArrow(Node parent, float size, Vector3 arrowBase, Vector3 arrowTip, Vector3 normal, float height, Color? color = null)
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();
        var material = new StandardMaterial3D();

        meshInstance.Mesh = immediateMesh;
        meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;


        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        immediateMesh.SurfaceAddVertex(arrowBase * size);
        immediateMesh.SurfaceAddVertex(arrowBase.Lerp(arrowTip, .75f) * size);
        immediateMesh.SurfaceEnd();


        material.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
        material.AlbedoColor = color ?? Colors.Aqua;
        //parent.AddChild(meshInstance);
        parent.CallDeferredThreadGroup("add_child", meshInstance);

        var coneInstance = new MeshInstance3D();
        CylinderMesh pointer = new CylinderMesh();
        pointer.BottomRadius = 0.0f;
        pointer.TopRadius = height;
        pointer.Height = height;

        coneInstance.Mesh = pointer;
        //parent.AddChild(coneInstance);
        parent.CallDeferredThreadGroup("add_child", coneInstance);
        coneInstance.LookAtFromPosition(arrowBase.Lerp(arrowTip, .75f) * size, arrowTip * size, normal);
        coneInstance.RotateObjectLocal(Vector3.Right, 90);

        return meshInstance;

    }

    public static MeshInstance3D DrawLine(Node parent, float size, Vector3 pos1, Vector3 pos2, Color? color = null)
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
        //parent.AddChild(meshInstance);
        parent.CallDeferredThreadGroup("add_child", meshInstance);

        return meshInstance;
    }

    public static MeshInstance3D DrawPoint(Node parent, float size, Vector3 pos, float radius = 0.05f, Color? color = null)
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

        //parent.AddChild(meshInstance);
        parent.CallDeferredThreadGroup("add_child", meshInstance);

        return meshInstance;
    }
}
