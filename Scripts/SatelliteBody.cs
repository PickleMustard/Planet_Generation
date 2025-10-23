using System;
using Godot;

namespace PlanetGeneration;

public enum SatelliteGroupTypes
{
    AsteroidBelt,
    IceBelt,
    Comet,
}

public enum GroupingCategories
{
    Balanced,
    Clustered,
    DualGrouping,
}

public enum SatelliteBodyType
{
    Asteroid,
    Comet,
    Moon,
    Planet,
    Satellite,
    Rings,
}

public partial class SatelliteBody : Node3D
{
    Vector3 Velocity;
    float Mass;
    Vector3 TotalForce;
    bool isSatelliteGroup = false;
    SatelliteBodyType SatelliteType;
    SatelliteGroupTypes GroupType;
    CelestialBodyMesh Mesh;

    public SatelliteBody(
        CelestialBodyType parentType,
        String type,
        float mass,
        Vector3 velocity,
        CelestialBodyMesh mesh
    )
    {
        this.SatelliteType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), type);
        this.Mass = mass;
        this.Velocity = velocity;
        this.Mesh = mesh;
        this.AddChild(mesh);

        switch (SatelliteType)
        {
            case SatelliteBodyType.Asteroid:
                break;
        }
    }

    public override void _Ready()
    {
        // Satellites do not affect other bodies' gravity
    }

    public override void _PhysicsProcess(double delta)
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
