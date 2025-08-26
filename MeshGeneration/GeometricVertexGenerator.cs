
using System;
using Godot;
using Structures;

namespace MeshGeneration
{
    public class GeometricVertexGenerator : IVertexGenerator
    {
        public float Exponent { get; set; } = 2.0f;
        public Point[] GenerateVertices(int count, Point start, Point end)
        {
            if (count <= 0) return new Point[0];
            Point[] vertices = new Point[count];
            Vector3 startVector = start.Position;
            Vector3 endVector = end.Position;
            float step = 1.0f / (count - 1);

            for (int i = 0; i < count; ++i)
            {
                float t = GetParameterization(i, count);
                Vector3 pos = startVector.Lerp(endVector, t);
                vertices[i] = new Point(pos);
            }

            return vertices;
        }

        public float GetParameterization(float index, float total)
        {
            float normalized = (index) / (total + 1.0f);
            return (float)Math.Pow(normalized, 1.0f / Exponent);
        }

        public bool ValidateConfiguration(int vertexCount)
        {
            return vertexCount >= 0;
        }
    }
}
