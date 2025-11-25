namespace Structures.MeshGeneration;

public interface IPoint
{
    int Index { get; set; }
    int VectorSpace { get; }
    float Stress { get; set; }
    float[] Components { get; set; }
}
