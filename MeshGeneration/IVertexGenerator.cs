using Structures;

namespace MeshGeneration
{
    public interface IVertexGenerator
    {
        public Point[] GenerateVertices(int count, Point start, Point end);
        public float GetParameterization(float index, float total);
        public bool ValidateConfiguration(int vertexCount);
    }

    public enum VertexDistribution { Linear, Geometric, Custom };
}
