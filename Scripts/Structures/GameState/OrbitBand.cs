using Godot;

namespace Structures.GameState;

public partial class OrbitBand : Resource
{
    [Export]
    public int Index { get; set; }

    [Export]
    public float AltitudeMultiplier { get; set; }

    [Export]
    public float Radius { get; set; }

    [Export]
    public int Capacity { get; set; }

    /// <summary>Angular speed in rad/s, physics-derived: sqrt(G*M/r^3).</summary>
    [Export]
    public float AngularSpeed { get; set; }

    /// <summary>Linear orbital speed = Radius * AngularSpeed.</summary>
    [Export]
    public float LinearSpeed { get; set; }

    public OrbitBand(int index, float altitudeMultiplier, float radius, int capacity,
        float angularSpeed, float linearSpeed)
    {
        Index = index;
        AltitudeMultiplier = altitudeMultiplier;
        Radius = radius;
        Capacity = capacity;
        AngularSpeed = angularSpeed;
        LinearSpeed = linearSpeed;
    }

    public OrbitBand()
    {
        Index = 0;
        AltitudeMultiplier = 1.0f;
        Radius = 0.0f;
        Capacity = 0;
        AngularSpeed = 0.0f;
        LinearSpeed = 0.0f;
    }

    public override string ToString()
    {
        return $"OrbitBand: Index={Index}, AltitudeMultiplier={AltitudeMultiplier}, Radius={Radius}, Capacity={Capacity}, AngularSpeed={AngularSpeed:F4}, LinearSpeed={LinearSpeed:F2}";
    }
}
