using Godot;
using Structures;

using static MeshGeneration.StructureDatabase;

namespace MeshGeneration
{
    public class LinearVertexGenerator : IVertexGenerator
    {
        public Point[] GenerateVertices(int count, Point start, Point end)
        {
            if (count <= 0) return new Point[0];
            Point[] vertices = new Point[count];
            float step = 1.0f / (count + 1);

            for (int i = 0; i < count; ++i)
            {
                float t = (i + 1) * step;
                Vector3 pos = start.ToVector3().Lerp(end.ToVector3(), t);
                vertices[i] = new Point(pos);
                if (VertexPoints.ContainsKey(vertices[i].Index))
                {
                    vertices[i] = VertexPoints[vertices[i].Index];
                }
                else
                {
                    VertexPoints.Add(vertices[i].Index, vertices[i]);
                }
            }

            return vertices;
        }

        public float GetParameterization(float index, float total)
        {
            return (index + 1.0f) / (total + 1.0f);
        }

        public bool ValidateConfiguration(int vertexCount)
        {
            return vertexCount >= 0;
        }
    }
}
