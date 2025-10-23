using System;
using Godot;
using UtilityLibrary;

namespace PlanetGeneration;

public enum CelestialBodyType
{
    BlackHole,
    Star,
    RockyPlanet,
    GasGiant,
    IceGiant,
    DwarfPlanet,
}

///<class>CelestialBody</class>
///<summary>A CelestialBody is a single point in space that has a mass and velocity.
///It has mass and gravity that will be used to calculate the attrational force on other objects.
///Its position can be modified by the forces acting upon it</summary>
public partial class CelestialBody : Node3D
{
    public Vector3 Velocity;
    public float Mass;
    public Vector3 TotalForce;
    public CelestialBodyType Type;
    public CelestialBodyMesh Mesh;
    private Godot.Collections.Dictionary bodyDict;

    public CelestialBody(Godot.Collections.Dictionary bodyDict, CelestialBodyMesh mesh)
    {
        this.bodyDict = bodyDict;
        var baseTemplates = (Godot.Collections.Dictionary)bodyDict["Template"];
        var type = (String)bodyDict["Type"];
        var mass = (float)baseTemplates["mass"];
        var velocity = (Vector3)baseTemplates["velocity"];
        var size = (int)baseTemplates["size"];

        this.Type = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), type);
        this.Mass = mass;
        this.Velocity = velocity;
        this.Mesh = mesh;
        mesh.size = size;
        this.AddChild(mesh);

        switch (Type)
        {
            case CelestialBodyType.Star:
                //Add a omnidirectional light source
                OmniLight3D emision = new OmniLight3D();
                emision.OmniRange = 4096f;
                emision.OmniAttenuation = .14f;
                this.AddChild(emision);
                break;
        }
    }

    public override void _Ready()
    {
        AddToGroup("CelestialBody");
    }

    public override void _PhysicsProcess(double delta)
    {
        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        foreach (CelestialBody body in bodies)
        {
            if (body != this)
            {
                float distance = this.GlobalPosition.DistanceTo(body.GlobalPosition);
                Vector3 direction = (body.GlobalPosition - this.GlobalPosition);

                float force =
                    OrbitalMath.GRAVITATIONAL_CONSTANT * Mass * body.Mass / (distance * distance);
                TotalForce += direction.Normalized() * force;
            }
        }

        var deltaV = (TotalForce / Mass) * (float)delta;
        Velocity += deltaV;
        GlobalPosition += Velocity * (float)delta;
    }

    public void GenerateMesh()
    {
        Godot.Collections.Dictionary meshParams;
        // Check if custom mesh data is available in the body dictionary
        if (
            bodyDict != null
            && bodyDict.ContainsKey("Mesh")
            && bodyDict["Mesh"].Obj is Godot.Collections.Dictionary customMesh
        )
        {
            meshParams = ConvertCustomMeshToParams(customMesh);
        }
        else
        {
            meshParams = SystemGenTemplates.GetMeshParams(Type, Mesh.Seed);
        }
        this.Name = (String)meshParams["name"];
        Mesh.ConfigureFrom(meshParams);
        Mesh.GenerateMesh();
    }

    private Godot.Collections.Dictionary ConvertCustomMeshToParams(
        Godot.Collections.Dictionary customMesh
    )
    {
        var meshParams = new Godot.Collections.Dictionary();
        // Convert custom mesh data to the format expected by Mesh.ConfigureFrom
        if (
            customMesh.ContainsKey("base_mesh")
            && customMesh["base_mesh"].Obj is Godot.Collections.Dictionary baseMesh
        )
        {
            meshParams["subdivisions"] = baseMesh["subdivisions"];
            meshParams["num_abberations"] = baseMesh["num_abberations"];
            meshParams["num_deformation_cycles"] = baseMesh["num_deformation_cycles"];
            meshParams["vertices_per_edge"] = baseMesh["vertices_per_edge"];
        }
        if (
            customMesh.ContainsKey("tectonic")
            && customMesh["tectonic"].Obj is Godot.Collections.Dictionary tectonic
        )
        {
            meshParams["tectonic"] = tectonic;
        }
        // Add other sections if present (scaling, noise_settings, etc.)
        if (customMesh.ContainsKey("scaling"))
        {
            meshParams["scaling"] = customMesh["scaling"];
        }
        if (customMesh.ContainsKey("noise_settings"))
        {
            meshParams["noise_settings"] = customMesh["noise_settings"];
        }
        return meshParams;
    }
}
