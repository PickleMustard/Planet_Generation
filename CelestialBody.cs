using Godot;
using System;

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
    float Mass;
    Vector3 Velocity;
    Vector3 TotalForce;
    CelestialBodyType Type;

    override public void _Ready()
    {
        AddToGroup("CelestialBody");
        RandomNumberGenerator rng = new RandomNumberGenerator();
        Mass = rng.RandfRange(2.2f, 10.0f);
        Velocity = new Vector3(rng.RandfRange(-1.0f, 1.0f), rng.RandfRange(-1.0f, 1.0f), rng.RandfRange(-1.0f, 1.0f));
        TotalForce = new Vector3(0.0f, 0.0f, 0.0f);
    }

    override public void _PhysicsProcess(double delta)
    {
        var bodies = GetTree().GetNodesInGroup("CelestialBody");
        foreach (CelestialBody body in bodies)
        {
            if (body != this)
            {
                float distance = this.GlobalPosition.DistanceTo(body.GlobalPosition);
            }
        }
    }
}
