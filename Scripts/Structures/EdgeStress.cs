using Godot;

namespace Structures;
public class EdgeStress
{
    public float CompressionStress { get; set; }
    public float ShearStress { get; set; }
    public Vector3 StressDirection { get; set; }
}
