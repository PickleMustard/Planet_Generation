using System;
using Godot;
using UtilityLibrary;

namespace PlanetGeneration;
public enum SatelliteBodyType
{
    Asteroid, Comet, Moon, Planet, Satellite
}
public partial class SatelliteBody : Node3D
{
    Vector3 Velocity;
    float Mass;
    Vector3 TotalForce;
    SatelliteBodyType Type;
    CelestialBodyMesh Mesh;

    public SatelliteBody(String type, float mass, Vector3 velocity, CelestialBodyMesh mesh)
    {
        this.Type = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), type);
        this.Mass = mass;
        this.Velocity = velocity;
        this.Mesh = mesh;
        this.AddChild(mesh);

        switch (Type)
        {
            case SatelliteBodyType.Asteroid:
                //Add a omnidirectional light source
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
        var parent = GetParent() as CelestialBody;
        float distance = this.GlobalPosition.DistanceTo(parent.GlobalPosition);
        Vector3 direction = (parent.GlobalPosition - this.GlobalPosition);

        float force = OrbitalMath.GRAVITATIONAL_CONSTANT * parent.Mass / (distance * distance);
        TotalForce += direction.Normalized() * force;

        var deltaV = (TotalForce / Mass) * (float)delta;
        Velocity += deltaV;
        GlobalPosition += Velocity * (float)delta;
    }

    public void GenerateMesh()
    {
        //var meshParams = SystemGenTemplates.GetMeshParams(Type, Mesh.Seed);
        //this.Name = (String)meshParams["name"];
        //Mesh.ConfigureFrom(meshParams);
        //Mesh.GenerateMesh();
    }

}
