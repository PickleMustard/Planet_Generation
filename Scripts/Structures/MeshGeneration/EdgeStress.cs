using Godot;

namespace Structures.MeshGeneration;

public partial class EdgeStress : Resource
{
    public float CompressionStress { get; set; }
    public float ShearStress { get; set; }
    public Vector3 StressDirection { get; set; }
}
