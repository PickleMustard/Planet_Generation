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

    public OrbitBand(int index, float altitudeMultiplier, float radius, int capacity)
    {
        Index = index;
        AltitudeMultiplier = altitudeMultiplier;
        Radius = radius;
        Capacity = capacity;
    }

    public OrbitBand()
    {
        Index = 0;
        AltitudeMultiplier = 1.0f;
        Radius = 0.0f;
        Capacity = 0;
    }

    public override string ToString()
    {
        return $"OrbitBand: Index={Index}, AltitudeMultiplier={AltitudeMultiplier}, Radius={Radius}, Capacity={Capacity}";
    }
}
