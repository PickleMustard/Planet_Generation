using Godot;
using System;
using UtilityLibrary;

namespace PlanetGeneration;
public enum CelestialBodyType
{
    Star, RockyPlanet, GasGiant, IceGiant, DwarfPlanet
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

    public CelestialBody(String type, float mass, Vector3 velocity, int size, CelestialBodyMesh mesh)
    {
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

    override public void _Ready()
    {
        AddToGroup("CelestialBody");
    }

    override public void _PhysicsProcess(double delta)
    {
        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        foreach (CelestialBody body in bodies)
        {
            if (body != this)
            {
                float distance = this.GlobalPosition.DistanceTo(body.GlobalPosition);
                Vector3 direction = (body.GlobalPosition - this.GlobalPosition);

                float force = OrbitalMath.GRAVITATIONAL_CONSTANT * Mass * body.Mass / (distance * distance);
                TotalForce += direction.Normalized() * force;
            }
        }

        var deltaV = (TotalForce / Mass) * (float)delta;
        Velocity += deltaV;
        GlobalPosition += Velocity * (float)delta;
    }

    public void GenerateMesh()
    {
        var meshParams = SystemGenTemplates.GetMeshParams(Type, Mesh.Seed);
        this.Name = (String)meshParams["name"];
        Mesh.ConfigureFrom(meshParams);
        Mesh.GenerateMesh();
    }
}
